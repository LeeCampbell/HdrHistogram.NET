using System;
using HdrHistogram.NET;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.Throughput
{
    public abstract class HistogramThoughputTestBase
    {
        protected static long HighestTrackableValue => TimeSpan.TicksPerHour;
        protected static int NumberOfSignificantValueDigits => 3;
        private const long TestValueLevel = 12340;
        private const long WarmupLoopLength = 50 * 1000;

        protected abstract string Label { get; }
        protected abstract AbstractHistogram CreateHistogram();

        [TestFixtureSetUp]
        public void WarmUp()
        {
            var histogram = CreateHistogram();
            ThroughputTestResult.Capture(
                Label,
                WarmupLoopLength,
                () => RecordLoop(histogram, WarmupLoopLength));
        }
        
        [TestCase(10000000)]
        [TestCase(1000000000)]
        public void TestRawRecordingSpeed(int messages)
        {
            var result = MeasureRawRecordingSpeed(messages);
            Console.WriteLine($"Msg:{result.Messages}, Time:{result.Elapsed}, GC-Gen0s:{result.GarbageCollections.Gen0}, GC-Gen1s:{result.GarbageCollections.Gen1}, GC-Gen2s:{result.GarbageCollections.Gen2}, GC-AllocationDelta:{result.GarbageCollections.TotalBytesAllocated}B");
        }

        public ThroughputTestResult MeasureRawRecordingSpeed(int messages)
        {
            var histogram = CreateHistogram();
            var result = ThroughputTestResult.Capture(
                Label,
                messages,
                () => RecordLoop(histogram, messages));

            return result;
        }

        private static void RecordLoop(AbstractHistogram histogram, long loopCount)
        {
            for (long i = 0; i < loopCount; i++)
                histogram.recordValue(TestValueLevel + (i & 0x8000));
        }
    }
}