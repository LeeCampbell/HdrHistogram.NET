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
    /**
     * Used for iterating through histogram values using the finest granularity steps supported by the underlying
     * representation. The iteration steps through all possible unit value levels, regardless of whether or not
     * there were recorded values for that value level, and terminates when all recorded histogram values are exhausted.
     */

    public class AllValuesIterator : AbstractHistogramIterator
    {
        int visitedSubBucketIndex;
        int visitedBucketIndex;

        /**
         * Reset iterator for re-use in a fresh iteration over the same histogram data set.
         */
        public void reset() {
            this.reset(this.SourceHistogram);
        }

        private void reset(AbstractHistogram histogram) {
            base.ResetIterator(histogram);
            this.visitedSubBucketIndex = -1;
            this.visitedBucketIndex = -1;
        }

        /**
         * @param histogram The histogram this iterator will operate on
         */
        public AllValuesIterator(AbstractHistogram histogram) {
            this.reset(histogram);
        }

        protected override void IncrementIterationLevel() {
            this.visitedSubBucketIndex = this.CurrentSubBucketIndex;
            this.visitedBucketIndex = this.CurrentBucketIndex;
        }

        protected override bool ReachedIterationLevel() {
            return (this.visitedSubBucketIndex != this.CurrentSubBucketIndex) || (this.visitedBucketIndex != this.CurrentBucketIndex);
        }
    }
}
