using System;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.TimerThroughput
{
    [TestFixture]
    public sealed class HistogramActionStopwatchCachedIncrementerDelegateThoughputTest : HistogramTimerThoughputTestBase
    {
        protected override string Label => "LongHistogramAutoStopwatchCachedIncrementer";

        protected override HistogramBase CreateHistogram()
        {
            return new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }

        protected override void RecordLoop(HistogramBase histogram, long loopCount)
        {
            Action incrementNumber = IncrementNumber;
            for (long i = 0; i<loopCount; i++)
            {
                histogram.Record(incrementNumber);
            }
        }
    }
}
