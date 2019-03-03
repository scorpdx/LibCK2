//adapted from https://devblogs.microsoft.com/dotnet/system-io-pipelines-high-performance-io-in-net/

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Text;

namespace LibCK2.Parsing
{
    public sealed class Scanner
    {
        private readonly PipeReader _reader;
        private readonly Encoding _encoding;

        public Scanner(PipeReader reader, Encoding encoding)
        {
            _reader = reader;
            _encoding = encoding;
        }

        private string GetEncodedString(ReadOnlySequence<byte> buffer)
        {
            if (buffer.IsSingleSegment)
            {
                return _encoding.GetString(buffer.First.Span);
            }

            return string.Create((int)buffer.Length, buffer, (span, sequence) =>
            {
                foreach (var segment in sequence)
                {
                    _encoding.GetChars(segment.Span, span);

                    span = span.Slice(segment.Length);
                }
            });
        }

        public async IAsyncEnumerable<(string token, byte stoppedBy)> ReadTokensAsync(ReadOnlyMemory<byte> stopBytes)
        {
            while (true)
            {
                var result = await _reader.ReadAsync();
                var buf = result.Buffer;
                do
                {
                    var foundPos = buf.PositionOfAny(stopBytes.Span);
                    if (!foundPos.HasValue) break;

                    var pos = foundPos.Value;
                    var tokenBuffer = buf.Slice(0, pos);
                    byte stoppedBy = buf.Slice(pos, 1).First.Span[0];
                    if (!tokenBuffer.IsEmpty || stoppedBy > 32)
                    {
                        yield return (GetEncodedString(tokenBuffer), stoppedBy);
                    }

                    buf = buf.Slice(buf.GetPosition(1, pos));
                } while (true);

                _reader.AdvanceTo(buf.Start, buf.End);
                if (result.IsCompleted)
                {
                    yield return (GetEncodedString(buf.Slice(0)), 0);
                    break;
                }
            }

            _reader.Complete();
        }
    }
}
