using NUnit.Framework;

namespace HdrHistogram.PerfTests.TimerThroughput
{
    [TestFixture]
    public sealed class HistogramActionStopwatchIncrementerThoughputTest : HistogramTimerThoughputTestBase
    {
        protected override string Label => "LongHistogramAutoStopwatchIncrementer";

        protected override HistogramBase CreateHistogram()
        {
            return new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }

        protected override void RecordLoop(HistogramBase histogram, long loopCount)
        {
            for (long i = 0; i<loopCount; i++)
            {
                histogram.Record(IncrementNumber);
            }
        }
    }
}