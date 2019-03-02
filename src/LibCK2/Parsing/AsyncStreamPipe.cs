//from: Pipelines.Sockets.Unofficial
using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;

namespace LibCK2.Parsing
{
    internal sealed class AsyncStreamPipe
    {
        private readonly Pipe _readPipe;
        private readonly Stream _inner;

        public AsyncStreamPipe(Stream stream, PipeOptions receivePipeOptions = null)
        {
            receivePipeOptions = receivePipeOptions ?? PipeOptions.Default;
            _inner = stream ?? throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead) throw new InvalidOperationException("Cannot create a read pipe over a non-readable stream");
            _readPipe = new Pipe(receivePipeOptions);
            receivePipeOptions.ReaderScheduler.Schedule(obj => ((AsyncStreamPipe)obj).CopyFromStreamToReadPipe().PipelinesFireAndForget(), this);
        }

        public PipeReader Input => _readPipe?.Reader ?? throw new InvalidOperationException("Cannot read from this pipe");

        private async Task CopyFromStreamToReadPipe()
        {
            Exception err = null;
            var writer = _readPipe.Writer;
            try
            {
                while (true)
                {
                    var memory = writer.GetMemory(1);
                    var arr = memory.GetArray();
                    int read = await _inner.ReadAsync(arr.Array, arr.Offset, arr.Count).ConfigureAwait(false);
                    if (read <= 0) break;
                    writer.Advance(read);
                    //Interlocked.Add(ref _totalBytesSent, read);

                    // need to flush regularly, a: to respect backoffs, and b: to awaken the reader
                    var flush = await writer.FlushAsync().ConfigureAwait(false);
                    if (flush.IsCompleted || flush.IsCanceled) break;
                }
            }
            catch (Exception ex)
            {
                err = ex;
            }
            writer.Complete(err);
        }
    }
}
