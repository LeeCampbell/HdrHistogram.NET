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
    /// An enumerator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="LogarithmicEnumerator"/>
    /// </summary>
    internal sealed class LogarithmicBucketEnumerable : IEnumerable<HistogramIterationValue>
    {
        private readonly HistogramBase _histogram;
        private readonly int _valueUnitsInFirstBucket;
        private readonly double _logBase;

        public LogarithmicBucketEnumerable(HistogramBase histogram, int valueUnitsInFirstBucket, double logBase)
        {
            _histogram = histogram;
            _valueUnitsInFirstBucket = valueUnitsInFirstBucket;
            _logBase = logBase;
        }

        public IEnumerator<HistogramIterationValue> GetEnumerator()
        {
            return new LogarithmicEnumerator(_histogram, _valueUnitsInFirstBucket, _logBase);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}