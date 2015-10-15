using HdrHistogram.NET;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.Throughput
{
    [TestFixture]
    public sealed class HistogramThoughputTest : HistogramThoughputTestBase
    {
        protected override string Label => "LongHistogram";

        protected override AbstractHistogram CreateHistogram()
        {
            return new Histogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }
    }
}