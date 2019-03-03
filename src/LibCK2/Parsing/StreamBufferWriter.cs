//adapted from the example in https://devblogs.microsoft.com/dotnet/announcing-net-core-3-preview-2/
using System;
using System.Buffers;
using System.IO;

namespace LibCK2.Parsing
{
    internal class StreamBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private readonly Stream _stream;

        private byte[] _buffer;

        public StreamBufferWriter(Stream stream, int initialCapacity = 0x1000)
        {
            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writable", nameof(stream));

            _stream = stream;
            _buffer = ArrayPool<byte>.Shared.Rent(initialCapacity);
        }

        public void Advance(int count)
        {
            _stream.Write(_buffer, 0, count);
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsMemory();
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            EnsureCapacity(sizeHint);
            return _buffer.AsSpan();
        }

        private void EnsureCapacity(int length)
        {
            if (length > _buffer.Length)
            {
                Array.Resize(ref _buffer, length);
            }
        }

        public void Dispose()
        {
            ArrayPool<byte>.Shared.Return(_buffer);
        }
    }
}
