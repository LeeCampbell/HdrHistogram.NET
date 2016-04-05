using System.Collections;
using System.Collections.Generic;

namespace HdrHistogram.Iteration
{
    /// <summary>
    /// An enumerator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="RecordedValuesEnumerator"/>
    /// </summary>
    internal sealed class RecordedValuesEnumerable : IEnumerable<HistogramIterationValue>
    {
        private readonly HistogramBase _histogram;

        public RecordedValuesEnumerable(HistogramBase histogram)
        {
            _histogram = histogram;
        }

        public IEnumerator<HistogramIterationValue> GetEnumerator()
        {
            return new RecordedValuesEnumerator(_histogram);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}