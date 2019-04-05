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
            if (!MemoryMarshal.TryGetArray(buffer, out var segment))
                throw new InvalidOperationException("MemoryMarshal.TryGetArray<byte> could not provide an array");

            return segment;
        }

        internal static void PipelinesFireAndForget(this Task task)
            => task?.ContinueWith(t => GC.KeepAlive(t.Exception), TaskContinuationOptions.OnlyOnFaulted);
    }
}
