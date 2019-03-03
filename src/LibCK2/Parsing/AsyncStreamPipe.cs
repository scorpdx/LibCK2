//Modified from: Pipelines.Sockets.Unofficial
//Original license reproduced below
//  https://github.com/mgravell/Pipelines.Sockets.Unofficial/blob/master/LICENSE @ 2019-03-02
/*
    The MIT License (MIT)

    Copyright (c) 2018 Marc Gravell

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files (the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions:

    The above copyright notice and this permission notice shall be included in all
    copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
    SOFTWARE.
*/

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
