using HdrHistogram.NET;
using NUnit.Framework;

namespace HdrHistogram.PerfTests.Throughput
{
    [TestFixture]
    public sealed class SynchronizedHistogramThoughputTest : HistogramThoughputTestBase
    {
        protected override string Label => "SynchronizedHistogram";

        protected override AbstractHistogram CreateHistogram()
        {
            return new SynchronizedHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
        }
    }
}