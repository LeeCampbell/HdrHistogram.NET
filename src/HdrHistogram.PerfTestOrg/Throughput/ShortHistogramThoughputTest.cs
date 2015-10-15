using HdrHistogram.NET;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.Throughput
{
    [TestFixture]
    public sealed class ShortHistogramThoughputTest : HistogramThoughputTestBase
    {
        protected override string Label => "ShortHistogram";

        protected override AbstractHistogram CreateHistogram()
        {
            return new ShortHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }
    }
}