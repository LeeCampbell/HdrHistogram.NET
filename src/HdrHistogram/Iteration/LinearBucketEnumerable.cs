/*
 * Written by Matt Warren, and released to the public domain,
 * as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 *
 * This is a .NET port of the original Java version, which was written by
 * Gil Tene as described in
 * https://github.com/HdrHistogram/HdrHistogram
 */
using System.Collections;
using System.Collections.Generic;

namespace HdrHistogram.Iteration
{
    /// <summary>
    /// An enumerator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="LinearEnumerator"/>
    /// </summary>
    internal sealed class LinearBucketEnumerable : IEnumerable<HistogramIterationValue>
    {
        private readonly HistogramBase _histogram;
        private readonly int _valueUnitsPerBucket;

        public LinearBucketEnumerable(HistogramBase histogram, int valueUnitsPerBucket)
        {
            this._histogram = histogram;
            this._valueUnitsPerBucket = valueUnitsPerBucket;
        }

        public IEnumerator<HistogramIterationValue> GetEnumerator()
        {
            return new LinearEnumerator(_histogram, _valueUnitsPerBucket);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}