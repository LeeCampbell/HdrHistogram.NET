/*
 * Written by Matt Warren, and released to the public domain,
 * as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 *
 * This is a .NET port of the original Java version, which was written by
 * Gil Tene as described in
 * https://github.com/HdrHistogram/HdrHistogram
 */

namespace HdrHistogram.NET.Iteration
{
    /// <summary>
    /// Used for iterating through histogram values in logarithmically increasing levels. The iteration is
    /// performed in steps that start at<i>valueUnitsInFirstBucket</i> and increase exponentially according to
    /// <i>logBase</i>, terminating when all recorded histogram values are exhausted. Note that each iteration "bucket"
    /// includes values up to and including the next bucket boundary value.
    /// </summary>
    public sealed class LogarithmicIterator : AbstractHistogramIterator
    {
        private readonly double _logBase;
        private long _nextValueReportingLevel;
        private long _nextValueReportingLevelLowestEquivalent;

        /// <summary>
        /// The constructor for the <see cref="LogarithmicIterator"/>
        /// </summary>
        /// <param name="histogram">The histogram this iterator will operate on</param>
        /// <param name="valueUnitsInFirstBucket">the size (in value units) of the first value bucket step</param>
        /// <param name="logBase">the multiplier by which the bucket size is expanded in each iteration step.</param>
        public LogarithmicIterator(AbstractHistogram histogram, int valueUnitsInFirstBucket, double logBase)
        {
            _logBase = logBase;

            ResetIterator(histogram);
            _nextValueReportingLevel = valueUnitsInFirstBucket;
            _nextValueReportingLevelLowestEquivalent = histogram.LowestEquivalentValue(_nextValueReportingLevel);
        }

        public override bool HasNext()
        {
            if (base.HasNext())
            {
                return true;
            }
            // If next iterate does not move to the next sub bucket index (which is empty if
            // if we reached this point), then we are not done iterating... Otherwise we're done.
            return (_nextValueReportingLevelLowestEquivalent < NextValueAtIndex);
        }

        protected override void IncrementIterationLevel()
        {
            _nextValueReportingLevel *= (long)_logBase;
            _nextValueReportingLevelLowestEquivalent = SourceHistogram.LowestEquivalentValue(_nextValueReportingLevel);
        }

        protected override long GetValueIteratedTo()
        {
            return _nextValueReportingLevel;
        }

        protected override bool ReachedIterationLevel()
        {
            return (CurrentValueAtIndex >= _nextValueReportingLevelLowestEquivalent);
        }
    }
}
