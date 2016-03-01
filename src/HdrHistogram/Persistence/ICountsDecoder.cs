using System;
using HdrHistogram.Utilities;

namespace HdrHistogram.Persistence
{
    public interface ICountsDecoder
    {
        int WordSize { get; }

        int ReadCounts(ByteBuffer sourceBuffer, int lengthInBytes, int maxIndex, Action<int, long> setCount);
    }
}