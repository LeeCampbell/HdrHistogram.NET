using System.Diagnostics;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.TimerThroughput
{
    [TestFixture]
    public sealed class HistogramManualStopwatchIncrementerThoughputTest : HistogramTimerThoughputTestBase
    {
        protected override string Label => "LongHistogramManualStopwatchIncrementer";

        protected override HistogramBase CreateHistogram()
        {
            return new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }

        protected override void RecordLoop(HistogramBase histogram, long loopCount)
        {
            for (long i = 0; i<loopCount; i++)
            {
                long startTimestamp = Stopwatch.GetTimestamp();
                base.IncrementNumber();
                long ticks = Stopwatch.GetTimestamp() - startTimestamp;
                histogram.RecordValue(ticks);
            }
        }
    }
}