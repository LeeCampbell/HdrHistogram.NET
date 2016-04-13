using NUnit.Framework;

namespace HdrHistogram.PerfTests.TimerThroughput
{
    [TestFixture]
    public sealed class HistogramActionStopwatchMd5ThoughputTest : HistogramTimerThoughputTestBase
    {
        protected override string Label => "LongHistogramAutoStopwatchMd5";

        protected override HistogramBase CreateHistogram()
        {
            return new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }

        protected override void RecordLoop(HistogramBase histogram, long loopCount)
        {
            for (long i = 0; i<loopCount; i++)
            {
                histogram.Record(() => base.Md5HashIncrementingNumber());
            }
        }
    }
}