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
     * Used for iterating through histogram values in linear steps. The iteration is
     * performed in steps of <i>valueUnitsPerBucket</i> in size, terminating when all recorded histogram
     * values are exhausted. Note that each iteration "bucket" includes values up to and including
     * the next bucket boundary value.
     */
    public class LinearIterator : AbstractHistogramIterator
    {
        long valueUnitsPerBucket;
        long nextValueReportingLevel;
        long nextValueReportingLevelLowestEquivalent;

        /**
         * Reset iterator for re-use in a fresh iteration over the same histogram data set.
         * @param valueUnitsPerBucket The size (in value units) of each bucket iteration.
         */
        public void reset(int valueUnitsPerBucket) {
            this.reset(this.SourceHistogram, valueUnitsPerBucket);
        }

        private void reset(AbstractHistogram histogram, long valueUnitsPerBucket) {
            base.ResetIterator(histogram);
            this.valueUnitsPerBucket = valueUnitsPerBucket;
            this.nextValueReportingLevel = valueUnitsPerBucket;
            this.nextValueReportingLevelLowestEquivalent = histogram.LowestEquivalentValue(this.nextValueReportingLevel);
        }

        /**
         * @param histogram The histogram this iterator will operate on
         * @param valueUnitsPerBucket The size (in value units) of each bucket iteration.
         */
        public LinearIterator(AbstractHistogram histogram, int valueUnitsPerBucket) {
            this.reset(histogram, valueUnitsPerBucket);
        }

        public override bool HasNext() 
        {
            if (base.HasNext()) 
            {
                return true;
            }
            // If next iterate does not move to the next sub bucket index (which is empty if
            // if we reached this point), then we are not done iterating... Otherwise we're done.
            return (this.nextValueReportingLevelLowestEquivalent < this.NextValueAtIndex);
        }

        protected override void IncrementIterationLevel() 
        {
            this.nextValueReportingLevel += this.valueUnitsPerBucket;
            this.nextValueReportingLevelLowestEquivalent = this.SourceHistogram.LowestEquivalentValue(this.nextValueReportingLevel);
        }

        protected override long GetValueIteratedTo() 
        {
            return this.nextValueReportingLevel;
        }

        protected override bool ReachedIterationLevel() 
        {
            return (this.CurrentValueAtIndex >= this.nextValueReportingLevelLowestEquivalent);
        }
    }
}
