using System;
using System.Security.Cryptography;
using HdrHistogram.PerfTests.Throughput;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.TimerThroughput
{
    public abstract class HistogramTimerThoughputTestBase
    {
        private const long WarmupLoopLength = 50 * 1000;

        private readonly MD5 _md5 = MD5.Create();

        protected static long HighestTrackableValue => TimeSpan.TicksPerHour;
        protected static int NumberOfSignificantValueDigits => 3;
        protected abstract string Label { get; }
        protected abstract HistogramBase CreateHistogram();


        [OneTimeSetUp]
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

        private long _idx = 0;

        protected byte[] Md5HashIncrementingNumber()
        {
            var bytes = BitConverter.GetBytes(_idx++);
            return _md5.ComputeHash(bytes);
        }
        protected void IncrementNumber()
        {
            _idx++;
        }

        protected abstract void RecordLoop(HistogramBase histogram, long loopCount);
    }
}