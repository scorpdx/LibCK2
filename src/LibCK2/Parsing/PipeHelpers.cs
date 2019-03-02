//from: Pipelines.Sockets.Unofficial
using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LibCK2.Parsing
{
    internal static class PipeHelpers
    {
        internal static ArraySegment<byte> GetArray(this Memory<byte> buffer)
            => GetArray((ReadOnlyMemory<byte>)buffer);

        internal static ArraySegment<byte> GetArray(this ReadOnlyMemory<byte> buffer)
        {
            if (!MemoryMarshal.TryGetArray<byte>(buffer, out var segment))
                throw new InvalidOperationException("MemoryMarshal.TryGetArray<byte> could not provide an array");

            return segment;
        }

        internal static void PipelinesFireAndForget(this Task task)
            => task?.ContinueWith(t => GC.KeepAlive(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
    }
}
