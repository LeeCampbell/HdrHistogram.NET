using NUnit.Framework;

namespace HdrHistogram.PerfTests.Throughput
{
    [TestFixture]
    public sealed class LongHistogramThoughputTest : HistogramThoughputTestBase
    {
        protected override string Label => "LongHistogram";

        protected override HistogramBase CreateHistogram()
        {
            return new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }
    }
}