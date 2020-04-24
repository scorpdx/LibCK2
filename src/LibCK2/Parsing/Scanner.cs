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
        public enum TokenTypes
        {
            None,

            Value,
            Equal,
            Comment,
            Open,
            Close
        }

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

        public async IAsyncEnumerable<(string token, TokenTypes stoppedBy)> ReadTokensAsync()
        {
            var stopBytes = SaveGame.SaveGameEncoding.GetBytes("\r\n\t\" {}=#");
            while (true)
            {
                var result = await _reader.ReadAsync();
                var buf = result.Buffer;
                do
                {
                    var foundPos = buf.PositionOfAny(stopBytes);
                    if (!foundPos.HasValue) break;

                    var pos = foundPos.Value;
                    byte stoppedBy = buf.Slice(pos, 1).First.Span[0];

                    if (stoppedBy == (byte)'"')
                    {
                        var quotePos = buf.Slice(1).PositionOf((byte)'"');
                        if (!quotePos.HasValue) break;
                        foundPos = buf.Slice(buf.GetPosition(1, quotePos.Value)).PositionOfAny(stopBytes);
                        if (!foundPos.HasValue) break;

                        pos = foundPos.Value;
                        stoppedBy = buf.Slice(pos, 1).First.Span[0];
                    }

                    var tokenBuffer = buf.Slice(0, pos);
                    if (!tokenBuffer.IsEmpty || stoppedBy > 32)
                    {
                        TokenTypes type;
                        switch(stoppedBy)
                        {
                            case (byte)'{': type = TokenTypes.Open; break;
                            case (byte)'}': type = TokenTypes.Close; break;
                            case (byte)'=': type = TokenTypes.Equal; break;
                            case (byte)'#': type = TokenTypes.Comment; break;
                            case (byte)'\r':
                            case (byte)'\n':
                            case (byte)'\t':
                            case (byte)' ':
                                type = TokenTypes.Value;
                                break;
                            default:
                                throw new InvalidOperationException("Stopped by unexpected token");
                        }
                        yield return (GetEncodedString(tokenBuffer), type);
                    }

                    buf = buf.Slice(buf.GetPosition(1, pos));
                } while (true);

                _reader.AdvanceTo(buf.Start, buf.End);
                if (result.IsCompleted)
                {
                    yield return (GetEncodedString(buf.Slice(0)), TokenTypes.Value);
                    break;
                }
            }

            _reader.Complete();
        }
    }
}
