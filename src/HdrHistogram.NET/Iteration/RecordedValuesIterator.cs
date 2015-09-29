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
    public sealed class RecordedValuesIterator : AbstractHistogramIterator
    {
        private int _visitedSubBucketIndex;
        private int _visitedBucketIndex;

        /// <summary>
        /// The constructor for <see cref="RecordedValuesIterator"/>
        /// </summary>
        /// <param name="histogram">The histogram this iterator will operate on</param>
        public RecordedValuesIterator(AbstractHistogram histogram)
        {
            ResetIterator(histogram);
        }

        protected override void ResetIterator(AbstractHistogram histogram)
        {
            base.ResetIterator(histogram);
            _visitedSubBucketIndex = -1;
            _visitedBucketIndex = -1;
        }

        protected override void IncrementIterationLevel()
        {
            _visitedSubBucketIndex = CurrentSubBucketIndex;
            _visitedBucketIndex = CurrentBucketIndex;
        }

        protected override bool ReachedIterationLevel()
        {
            long currentIJCount = SourceHistogram.GetCountAt(CurrentBucketIndex, CurrentSubBucketIndex);
            return (currentIJCount != 0) &&
                    ((_visitedSubBucketIndex != CurrentSubBucketIndex) || (_visitedBucketIndex != CurrentBucketIndex));
        }
    }
}
