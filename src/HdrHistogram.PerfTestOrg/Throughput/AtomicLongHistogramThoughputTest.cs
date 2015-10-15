using HdrHistogram.NET;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.Throughput
{
    [TestFixture]
    public sealed class AtomicLongHistogramThoughputTest : HistogramThoughputTestBase
    {
        protected override string Label => "AtomicLongHistogram";

        protected override AbstractHistogram CreateHistogram()
        {
            return new AtomicHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }
    }
}