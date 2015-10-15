using HdrHistogram.NET;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.Throughput
{
    [TestFixture]
    public sealed class IntHistogramThoughputTest : HistogramThoughputTestBase
    {
        protected override string Label => "IntHistogram";

        protected override AbstractHistogram CreateHistogram()
        {
            return new IntHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }
    }
}