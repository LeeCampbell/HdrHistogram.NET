using System;

namespace HdrHistogram.PerfTests.Throughput
{
    public sealed class GcInfo
    {
        public static GcInfo Current()
        {
            return new GcInfo(GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), GC.GetTotalMemory(forceFullCollection: false));
        }

        public int Gen0 { get; }
        public int Gen1 { get; }
        public int Gen2 { get; }
        public long TotalBytesAllocated { get; }

        public GcInfo(int gen0, int gen1, int gen2, long totalBytesAllocated)
        {
            Gen0 = gen0;
            Gen1 = gen1;
            Gen2 = gen2;
            TotalBytesAllocated = totalBytesAllocated;
        }

        public GcInfo Delta(GcInfo previous)
        {
            return new GcInfo(Gen0-previous.Gen0, Gen1-previous.Gen1, Gen2-previous.Gen2, TotalBytesAllocated - previous.TotalBytesAllocated);
        }
    }
}