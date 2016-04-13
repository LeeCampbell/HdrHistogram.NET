using System.Diagnostics;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.TimerThroughput
{
    [TestFixture]
    public sealed class HistogramManualStopwatchMd5ThoughputTest : HistogramTimerThoughputTestBase
    {
        protected override string Label => "LongHistogramManualStopwatchMd5";

        protected override HistogramBase CreateHistogram()
        {
            return new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }

        protected override void RecordLoop(HistogramBase histogram, long loopCount)
        {
            for (long i = 0; i<loopCount; i++)
            {
                long startTimestamp = Stopwatch.GetTimestamp();
                var result = base.Md5HashIncrementingNumber();
                long ticks = Stopwatch.GetTimestamp() - startTimestamp;
                histogram.RecordValue(ticks);
            }
        }
    }
}