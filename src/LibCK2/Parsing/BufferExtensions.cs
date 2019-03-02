using System;
using System.Buffers;
using System.Runtime.CompilerServices;

namespace LibCK2.Parsing
{
    internal static class BufferExtensions
    {
        /// <summary>
        /// Returns position of first occurrence of item in the <see cref="ReadOnlySequence{T}"/>
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SequencePosition? PositionOfAny<T>(in this ReadOnlySequence<T> source, ReadOnlySpan<T> values) where T : IEquatable<T>
        {
            if (source.IsSingleSegment)
            {
                int index = source.First.Span.IndexOfAny(values);
                if (index != -1)
                {
                    return source.GetPosition(index);
                }

                return null;
            }
            else
            {
                return PositionOfAnyMultiSegment(source, values);
            }
        }

        private static SequencePosition? PositionOfAnyMultiSegment<T>(in ReadOnlySequence<T> source, ReadOnlySpan<T> values) where T : IEquatable<T>
        {
            SequencePosition position = source.Start;
            SequencePosition result = position;
            while (source.TryGet(ref position, out ReadOnlyMemory<T> memory))
            {
                int index = memory.Span.IndexOfAny(values);
                if (index != -1)
                {
                    return source.GetPosition(index, result);
                }
                else if (position.GetObject() == null)
                {
                    break;
                }

                result = position;
            }

            return null;
        }
    }
}
