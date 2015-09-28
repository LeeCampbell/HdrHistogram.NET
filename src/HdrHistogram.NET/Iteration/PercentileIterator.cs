/*
 * Written by Matt Warren, and released to the public domain,
 * as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 *
 * This is a .NET port of the original Java version, which was written by
 * Gil Tene as described in
 * https://github.com/HdrHistogram/HdrHistogram
 */

using System;

namespace HdrHistogram.NET.Iteration
{
    /**
     * Used for iterating through histogram values according to percentile levels. The iteration is
     * performed in steps that start at 0% and reduce their distance to 100% according to the
     * <i>percentileTicksPerHalfDistance</i> parameter, ultimately reaching 100% when all recorded histogram
     * values are exhausted.
    */
    public class PercentileIterator : AbstractHistogramIterator
    {
        int percentileTicksPerHalfDistance;
        double percentileLevelToIterateTo;
        double percentileLevelToIterateFrom;
        bool reachedLastRecordedValue;

        /**
         * Reset iterator for re-use in a fresh iteration over the same histogram data set.
         *
         * @param percentileTicksPerHalfDistance The number of iteration steps per half-distance to 100%.
         */
        public void reset(int percentileTicksPerHalfDistance) 
        {
            this.reset(this.SourceHistogram, percentileTicksPerHalfDistance);
        }

        private void reset(AbstractHistogram histogram, int percentileTicksPerHalfDistance) 
        {
            base.ResetIterator(histogram);
            this.percentileTicksPerHalfDistance = percentileTicksPerHalfDistance;
            this.percentileLevelToIterateTo = 0.0;
            this.percentileLevelToIterateFrom = 0.0;
            this.reachedLastRecordedValue = false;
        }

        /**
         * @param histogram The histogram this iterator will operate on
         * @param percentileTicksPerHalfDistance The number of iteration steps per half-distance to 100%.
         */
        public PercentileIterator(AbstractHistogram histogram, int percentileTicksPerHalfDistance) 
        {
            this.reset(histogram, percentileTicksPerHalfDistance);
        }

        public override bool HasNext() 
        {
            if (base.HasNext())
                return true;
            // We want one additional last step to 100%
            if (!this.reachedLastRecordedValue && (this.ArrayTotalCount > 0)) {
                this.percentileLevelToIterateTo = 100.0;
                this.reachedLastRecordedValue = true;
                return true;
            }
            return false;
        }

        protected override void IncrementIterationLevel() 
        {
            this.percentileLevelToIterateFrom = this.percentileLevelToIterateTo;
            long percentileReportingTicks =
                    this.percentileTicksPerHalfDistance *
                            (long) Math.Pow(2,
                                    (long) (Math.Log(100.0 / (100.0 - (this.percentileLevelToIterateTo))) / Math.Log(2)) + 1);
            this.percentileLevelToIterateTo += 100.0 / percentileReportingTicks;
        }

        protected override bool ReachedIterationLevel() 
        {
            if (this.CountAtThisValue == 0)
                return false;
            double currentPercentile = (100.0 * (double) this.TotalCountToCurrentIndex) / this.ArrayTotalCount;
            return (currentPercentile >= this.percentileLevelToIterateTo);
        }

        protected override double GetPercentileIteratedTo() 
        {
            return this.percentileLevelToIterateTo;
        }
    }
}
