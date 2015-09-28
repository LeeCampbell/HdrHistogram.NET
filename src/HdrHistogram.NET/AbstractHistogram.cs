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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HdrHistogram.NET.Iteration;
using HdrHistogram.NET.Utilities;
using System.IO.Compression;
using System.Reflection;

namespace HdrHistogram.NET
{
    /// <summary>
    /// Base class for High Dynamic Range (HDR) Histograms
    /// </summary>
    /// <remarks>
    /// <see cref="AbstractHistogram"/> supports the recording and analyzing sampled data value counts across a configurable
    /// integer value range with configurable value precision within the range.
    /// Value precision is expressed as the number of significant digits in the value recording, and provides control over 
    /// value quantization behavior across the value range and the subsequent value resolution at any given level.
    /// <para>
    /// For example, a Histogram could be configured to track the counts of observed integer values between 0 and
    /// 3,600,000,000 while maintaining a value precision of 3 significant digits across that range.
    /// Value quantization within the range will thus be no larger than 1/1,000th(or 0.1%) of any value.
    /// This example Histogram could be used to track and analyze the counts of observed response times ranging between 
    /// 1 microsecond and 1 hour in magnitude, while maintaining a value resolution of 1 microsecond up to 1 millisecond, 
    /// a resolution of 1 millisecond(or better) up to one second, and a resolution of 1 second(or better) up to 1,000 
    /// seconds.
    /// At it's maximum tracked value(1 hour), it would still maintain a resolution of 3.6 seconds(or better).
    /// </para>
    /// </remarks>
    public abstract class AbstractHistogram : AbstractHistogramBase //, ISerializable 
    {
        // "Hot" accessed fields (used in the the value recording code path) are bunched here, such
        // that they will have a good chance of ending up in the same cache line as the totalCounts and
        // counts array reference fields that subclass implementations will typically add.
        private int subBucketHalfCountMagnitude;
        private int unitMagnitude;
        private long subBucketMask;

        private long startTimeStampMsec;
        private long endTimeStampMsec;

        internal int subBucketHalfCount;


        // Sub-classes will typically add a totalCount field and a counts array field.

        //
        //
        //
        // Abstract, counts-type dependent methods to be provided by subclass implementations:
        //
        //
        //

        protected abstract long getCountAtIndex(int index);

        protected abstract void incrementCountAtIndex(int index);

        protected abstract void addToCountAtIndex(int index, long value);

        protected abstract void setTotalCount(long totalCount);

        protected abstract void incrementTotalCount();

        protected abstract void addToTotalCount(long value);

        protected abstract void clearCounts();

        protected abstract int _getEstimatedFootprintInBytes();

        public abstract long getTotalCount();

        //
        //
        //
        // Construction:
        //
        //
        //

        /// <summary>
        /// Construct a histogram given the Lowest and Highest values to be tracked and a number of significant decimal digits.
        /// </summary>
        /// <param name="lowestTrackableValue">The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1.
        /// May be internally rounded down to nearest power of 2.
        /// </param>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram.
        /// Must be a positive integer that is &gt;= (2 * lowestTrackableValue).
        /// </param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation. 
        /// Must be a non-negative integer between 0 and 5.
        /// </param>
        /// <remarks>
        /// Providing a lowestTrackableValue is useful is situations where the units used for the histogram's values are much 
        /// smaller that the minimal accuracy required. E.g. when tracking time values stated in nanosecond units, where the 
        /// minimal accuracy required is a microsecond, the proper value for lowestTrackableValue would be 1000.
        /// </remarks>
        protected AbstractHistogram(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            // Verify argument validity
            if (lowestTrackableValue < 1)
            {
                throw new ArgumentException("lowestTrackableValue must be >= 1");
            }
            if (highestTrackableValue < 2 * lowestTrackableValue)
            {
                throw new ArgumentException("highestTrackableValue must be >= 2 * lowestTrackableValue");
            }
            if ((numberOfSignificantValueDigits < 0) || (numberOfSignificantValueDigits > 5))
            {
                throw new ArgumentException("numberOfSignificantValueDigits must be between 0 and 6");
            }
            identity = GetNextIdentity();

            init(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits, 0);
        }

        private void init(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits, long totalCount)
        {
            this.highestTrackableValue = highestTrackableValue;
            this.numberOfSignificantValueDigits = numberOfSignificantValueDigits;
            this.lowestTrackableValue = lowestTrackableValue;

            /*final*/
            long largestValueWithSingleUnitResolution = 2 * (long)Math.Pow(10, numberOfSignificantValueDigits);

            unitMagnitude = (int)Math.Floor(Math.Log(lowestTrackableValue) / Math.Log(2));

            // We need to maintain power-of-two subBucketCount (for clean direct indexing) that is large enough to
            // provide unit resolution to at least largestValueWithSingleUnitResolution. So figure out
            // largestValueWithSingleUnitResolution's nearest power-of-two (rounded up), and use that:
            int subBucketCountMagnitude = (int)Math.Ceiling(Math.Log(largestValueWithSingleUnitResolution) / Math.Log(2));
            subBucketHalfCountMagnitude = ((subBucketCountMagnitude > 1) ? subBucketCountMagnitude : 1) - 1;
            subBucketCount = (int)Math.Pow(2, (subBucketHalfCountMagnitude + 1));
            subBucketHalfCount = subBucketCount / 2;
            subBucketMask = (subBucketCount - 1) << unitMagnitude;

            // determine exponent range needed to support the trackable value with no overflow:

            this.bucketCount = getBucketsNeededToCoverValue(highestTrackableValue);

            countsArrayLength = getLengthForNumberOfBuckets(bucketCount);

            setTotalCount(totalCount);

            percentileIterator = new PercentileIterator(this, 1);
            recordedValuesIterator = new RecordedValuesIterator(this);
        }

        //
        //
        //
        // Value recording support:
        //
        //
        //

        /// <summary>
        /// Records a value in the histogram
        /// </summary>
        /// <param name="value">The value to be recorded</param>
        /// <exception cref="System.IndexOutOfRangeException">if value is exceeds highestTrackableValue</exception>
        public void recordValue(long value) //throws ArrayIndexOutOfBoundsException 
        {
            recordSingleValue(value);
        }

        /// <summary>
        /// Record a value in the histogram (adding to the value's current count)
        /// </summary>
        /// <param name="value">The value to be recorded</param>
        /// <param name="count">The number of occurrences of this value to record</param>
        /// <exception cref="System.IndexOutOfRangeException">if value is exceeds highestTrackableValue</exception>
        public void recordValueWithCount(long value, long count) //throws ArrayIndexOutOfBoundsException 
        {
            recordCountAtValue(count, value);
        }

        /// <summary>
        /// Record a value in the histogram.
        /// </summary>
        /// <param name="value">The value to record</param>
        /// <param name="expectedIntervalBetweenValueSamples">If <param name="expectedIntervalBetweenValueSamples"/> is larger than 0, add auto-generated value records as appropriate if <param name="value"/> is larger than <param name="expectedIntervalBetweenValueSamples"/></param>
        /// <remarks></remarks>
        /// <exception cref="System.IndexOutOfRangeException">if value is exceeds highestTrackableValue</exception>
        /// <remarks>
        /// To compensate for the loss of sampled values when a recorded value is larger than the expected interval between value samples, 
        /// Histogram will auto-generate an additional series of decreasingly-smaller (down to the expectedIntervalBetweenValueSamples) value records.
        /// <para>
        /// Note: This is a at-recording correction method, as opposed to the post-recording correction method provided by <see cref="copyCorrectedForCoordinatedOmission"/>.
        /// The two methods are mutually exclusive, and only one of the two should be be used on a given data set to correct for the same coordinated omission issue.
        /// </para>
        /// See notes in the description of the Histogram calls for an illustration of why this corrective behavior is important.
        /// </remarks>
        public void recordValueWithExpectedInterval(long value, long expectedIntervalBetweenValueSamples) //throws ArrayIndexOutOfBoundsException 
        {
            recordValueWithCountAndExpectedInterval(value, 1, expectedIntervalBetweenValueSamples);
        }


        private void recordCountAtValue(long count, long value) //throws ArrayIndexOutOfBoundsException 
        {
            // Dissect the value into bucket and sub-bucket parts, and derive index into counts array:
            int bucketIndex = getBucketIndex(value);
            int subBucketIndex = getSubBucketIndex(value, bucketIndex);
            int countsIndex = countsArrayIndex(bucketIndex, subBucketIndex);
            addToCountAtIndex(countsIndex, count);
            addToTotalCount(count);
        }

        private void recordSingleValue(long value) //throws ArrayIndexOutOfBoundsException 
        {
            // Dissect the value into bucket and sub-bucket parts, and derive index into counts array:
            int bucketIndex = getBucketIndex(value);
            int subBucketIndex = getSubBucketIndex(value, bucketIndex);
            int countsIndex = countsArrayIndex(bucketIndex, subBucketIndex);
            incrementCountAtIndex(countsIndex);
            incrementTotalCount();
        }

        private void recordValueWithCountAndExpectedInterval(long value, long count,
                                                             long expectedIntervalBetweenValueSamples) //throws ArrayIndexOutOfBoundsException 
        {
            recordCountAtValue(count, value);
            if (expectedIntervalBetweenValueSamples <= 0)
                return;
            for (long missingValue = value - expectedIntervalBetweenValueSamples;
                 missingValue >= expectedIntervalBetweenValueSamples;
                 missingValue -= expectedIntervalBetweenValueSamples)
            {
                recordCountAtValue(count, missingValue);
            }
        }


        //
        //
        //
        // Clearing support:
        //
        //
        //

        /// <summary>
        /// Reset the contents and stats of this histogram
        /// </summary>
        public void reset()
        {
            clearCounts();
        }

        //
        //
        //
        // Copy support:
        //
        //
        //

        /// <summary>
        /// Create a copy of this histogram, complete with data and everything.
        /// </summary>
        /// <returns>A distinct copy of this histogram.</returns>
        public abstract AbstractHistogram copy();

        /// <summary>
        /// Get a copy of this histogram, corrected for coordinated omission.
        /// </summary>
        /// <param name="expectedIntervalBetweenValueSamples">If <param name="expectedIntervalBetweenValueSamples"/> is larger than 0, add auto-generated value records as appropriate if value is larger than <param name="expectedIntervalBetweenValueSamples"/></param>
        /// <returns>a copy of this histogram, corrected for coordinated omission.</returns>
        /// <remarks>
        /// To compensate for the loss of sampled values when a recorded value is larger than the expected interval between value samples, 
        /// the new histogram will include an auto-generated additional series of decreasingly-smaller(down to the <param name="expectedIntervalBetweenValueSamples"/>) 
        /// value records for each count found in the current histogram that is larger than the expectedIntervalBetweenValueSamples.
        /// <para>
        /// Note: This is a post-correction method, as opposed to the at-recording correction method provided by <seealso cref="recordValueWithExpectedInterval(long, long)"/>. 
        /// The two methods are mutually exclusive, and only one of the two should be be used on a given data set to correct for the same coordinated omission issue.
        /// </para>
        /// See notes in the description of the Histogram calls for an illustration of why this corrective behavior is important.
        /// </remarks>
        public abstract AbstractHistogram copyCorrectedForCoordinatedOmission(long expectedIntervalBetweenValueSamples);

        /// <summary>
        /// Copy this histogram into the target histogram, overwriting it's contents.
        /// </summary>
        /// <param name="targetHistogram">the histogram to copy into</param>
        public void copyInto(AbstractHistogram targetHistogram)
        {
            targetHistogram.reset();
            targetHistogram.add(this);
            targetHistogram.StartTimeStamp = this.StartTimeStamp;
            targetHistogram.EndTimeStamp = this.EndTimeStamp;
        }

        /// <summary>
        /// Copy this histogram, corrected for coordinated omission, into the target histogram, overwriting it's contents.
        /// </summary>
        /// <param name="targetHistogram">the histogram to copy into</param>
        /// <param name="expectedIntervalBetweenValueSamples">If <param name="expectedIntervalBetweenValueSamples"/> is larger than 0, add auto-generated value records as appropriate if value is larger than <param name="expectedIntervalBetweenValueSamples"/></param>
        /// <remarks>
        /// See <see cref="copyCorrectedForCoordinatedOmission"/> for more detailed explanation about how correction is applied
        /// </remarks>
        public void copyIntoCorrectedForCoordinatedOmission(AbstractHistogram targetHistogram, long expectedIntervalBetweenValueSamples)
        {
            targetHistogram.reset();
            targetHistogram.addWhileCorrectingForCoordinatedOmission(this, expectedIntervalBetweenValueSamples);
            targetHistogram.StartTimeStamp = this.StartTimeStamp;
            targetHistogram.EndTimeStamp = this.EndTimeStamp;
        }

        //
        //
        //
        // Add support:
        //
        //
        //

        /// <summary>
        /// Add the contents of another histogram to this one.
        /// </summary>
        /// <param name="fromHistogram">The other histogram.</param>
        /// <exception cref="System.IndexOutOfRangeException">if values in fromHistogram's are higher than highestTrackableValue.</exception>
        public void add(AbstractHistogram fromHistogram) //throws ArrayIndexOutOfBoundsException 
        {
            if (this.highestTrackableValue < fromHistogram.highestTrackableValue)
            {
                throw new ArgumentOutOfRangeException("The other histogram covers a wider range than this one.");
            }
            if ((bucketCount == fromHistogram.bucketCount) &&
                    (subBucketCount == fromHistogram.subBucketCount) &&
                    (unitMagnitude == fromHistogram.unitMagnitude))
            {
                // Counts arrays are of the same length and meaning, so we can just iterate and add directly:
                for (int i = 0; i < fromHistogram.countsArrayLength; i++)
                {
                    addToCountAtIndex(i, fromHistogram.getCountAtIndex(i));
                }
                setTotalCount(getTotalCount() + fromHistogram.getTotalCount());
            }
            else
            {
                // Arrays are not a direct match, so we can't just stream through and add them.
                // Instead, go through the array and add each non-zero value found at it's proper value:
                for (int i = 0; i < fromHistogram.countsArrayLength; i++)
                {
                    long count = fromHistogram.getCountAtIndex(i);
                    recordValueWithCount(fromHistogram.valueFromIndex(i), count);
                }
            }
        }

        /// <summary>
        /// Add the contents of another histogram to this one, while correcting the incoming data for coordinated omission.
        /// </summary>
        /// <param name="fromHistogram">The other histogram. highestTrackableValue and largestValueWithSingleUnitResolution must match.</param>
        /// <param name="expectedIntervalBetweenValueSamples">If <param name="expectedIntervalBetweenValueSamples"/> is larger than 0, add auto-generated value records as appropriate if value is larger than <param name="expectedIntervalBetweenValueSamples"/></param>
        /// <remarks>
        /// To compensate for the loss of sampled values when a recorded value is larger than the expected interval between value samples, the values added will include an auto-generated additional series of decreasingly-smaller(down to the expectedIntervalBetweenValueSamples) value records for each count found in the current histogram that is larger than the expectedIntervalBetweenValueSamples.
        /// 
        /// Note: This is a post-recording correction method, as opposed to the at-recording correction method provided by {@link #recordValueWithExpectedInterval(long, long) recordValueWithExpectedInterval}. 
        /// The two methods are mutually exclusive, and only one of the two should be be used on a given data set to correct for the same coordinated omission issue.
        /// See notes in the description of the Histogram calls for an illustration of why this corrective behavior is important.
        /// </remarks>
        /// <exception cref="System.IndexOutOfRangeException">if values exceed highestTrackableValue.</exception>
        public void addWhileCorrectingForCoordinatedOmission(AbstractHistogram fromHistogram, long expectedIntervalBetweenValueSamples)
        {
            /*final*/
            AbstractHistogram toHistogram = this;

            //for (HistogramIterationValue v : fromHistogram.recordedValues()) 
            foreach (HistogramIterationValue v in fromHistogram.recordedValues())
            {
                toHistogram.recordValueWithCountAndExpectedInterval(v.getValueIteratedTo(),
                        v.getCountAtValueIteratedTo(), expectedIntervalBetweenValueSamples);
            }
        }

        //
        //
        //
        // Comparison support:
        //
        //
        //


        //TODO: Implement IEquatable? -LC
        /// <summary>
        /// Determine if this histogram is equivalent to another.
        /// </summary>
        /// <param name="other">the other histogram to compare to</param>
        /// <returns><c>true</c> if this histogram are equivalent with the other.</returns>
        public override bool Equals(object other)
        {
            if (this == other)
            {
                return true;
            }
            if (!(other is AbstractHistogram))
            {
                return false;
            }
            AbstractHistogram that = (AbstractHistogram)other;
            if ((lowestTrackableValue != that.lowestTrackableValue) ||
                (highestTrackableValue != that.highestTrackableValue) ||
                (numberOfSignificantValueDigits != that.numberOfSignificantValueDigits))
            {
                return false;
            }
            if (countsArrayLength != that.countsArrayLength)
            {
                return false;
            }
            if (getTotalCount() != that.getTotalCount())
            {
                return false;
            }

            ////if (this is SynchronizedHistogram)
            //{
            //    var builder = new StringBuilder();
            //    for (int i = 0; i < countsArrayLength; i++)
            //    {
            //        if (i % 100 == 0)
            //            builder.AppendLine();
            //        builder.AppendFormat("{0}, ", getCountAtIndex(i));
            //    }
            //    Console.WriteLine("this{0}", builder.ToString());

            //    builder.Clear();
            //    for (int i = 0; i < countsArrayLength; i++)
            //    {
            //        if (i % 100 == 0)
            //            builder.AppendLine();
            //        builder.AppendFormat("{0}, ", that.getCountAtIndex(i));
            //    }
            //    Console.WriteLine("that{0}\n", builder.ToString());
            //}

            for (int i = 0; i < countsArrayLength; i++)
            {
                if (getCountAtIndex(i) != that.getCountAtIndex(i))
                {
                    Debug.WriteLine("Error at position {0}, this[{0}] = {1}, that[{0}] = {2}", i, getCountAtIndex(i), that.getCountAtIndex(i));
                    return false;
                }
            }
            return true;
        }

        public override int GetHashCode()
        {
            // From http://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethashcode/263416#263416
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                // Suitable nullity checks etc, of course :)
                hash = hash * 23 + highestTrackableValue.GetHashCode();
                hash = hash * 23 + numberOfSignificantValueDigits.GetHashCode();
                hash = hash * 23 + countsArrayLength.GetHashCode();
                hash = hash * 23 + getTotalCount().GetHashCode();

                for (int i = 0; i < countsArrayLength; i++)
                {
                    hash = hash * 23 + getCountAtIndex(i).GetHashCode();
                }

                return hash;
            }
        }

        //
        //
        //
        // Histogram structure querying support:
        //
        //
        //

        //TODO: Make properties. -LC

        /// <summary>
        /// Get the configured lowestTrackableValue
        /// </summary>
        /// <returns>lowestTrackableValue</returns>
        public long getLowestTrackableValue()
        {
            return lowestTrackableValue;
        }

        /// <summary>
        /// Get the configured highestTrackableValue
        /// </summary>
        /// <returns>highestTrackableValue</returns>
        public long getHighestTrackableValue()
        {
            return highestTrackableValue;
        }

        /// <summary>
        /// Get the configured numberOfSignificantValueDigits
        /// </summary>
        /// <returns>numberOfSignificantValueDigits</returns>
        public int getNumberOfSignificantValueDigits()
        {
            return numberOfSignificantValueDigits;
        }

        /// <summary>
        /// Get the size (in value units) of the range of values that are equivalent to the given value within the histogram's resolution. 
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value">The given value</param>
        /// <returns>The lowest value that is equivalent to the given value within the histogram's resolution.</returns>
        public long sizeOfEquivalentValueRange(long value)
        {
            int bucketIndex = getBucketIndex(value);
            int subBucketIndex = getSubBucketIndex(value, bucketIndex);
            long distanceToNextValue =
                    (1 << (unitMagnitude + ((subBucketIndex >= subBucketCount) ? (bucketIndex + 1) : bucketIndex)));
            return distanceToNextValue;
        }

        /// <summary>
        /// Get the lowest value that is equivalent to the given value within the histogram's resolution.
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value">The given value</param>
        /// <returns>The lowest value that is equivalent to the given value within the histogram's resolution.</returns>
        public long lowestEquivalentValue(long value)
        {
            int bucketIndex = getBucketIndex(value);
            int subBucketIndex = getSubBucketIndex(value, bucketIndex);
            long thisValueBaseLevel = valueFromIndex(bucketIndex, subBucketIndex);
            return thisValueBaseLevel;
        }

        /// <summary>
        /// Get the highest value that is equivalent to the given value within the histogram's resolution.
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value">The given value</param>
        /// <returns>The highest value that is equivalent to the given value within the histogram's resolution.</returns>
        public long highestEquivalentValue(long value)
        {
            return nextNonEquivalentValue(value) - 1;
        }

        /// <summary>
        /// Get a value that lies in the middle (rounded up) of the range of values equivalent the given value.
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value">The given value</param>
        /// <returns>The value lies in the middle (rounded up) of the range of values equivalent the given value.</returns>
        public long medianEquivalentValue(long value)
        {
            return (lowestEquivalentValue(value) + (sizeOfEquivalentValueRange(value) >> 1));
        }

        /// <summary>
        /// Get the next value that is not equivalent to the given value within the histogram's resolution.
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value">The given value</param>
        /// <returns>The next value that is not equivalent to the given value within the histogram's resolution.</returns>
        public long nextNonEquivalentValue(long value)
        {
            return lowestEquivalentValue(value) + sizeOfEquivalentValueRange(value);
        }

        /// <summary>
        /// Determine if two values are equivalent with the histogram's resolution.
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value1">first value to compare</param>
        /// <param name="value2">second value to compare</param>
        /// <returns><c>true</c> if values are equivalent with the histogram's resolution.</returns>
        public bool valuesAreEquivalent(long value1, long value2)
        {
            return (lowestEquivalentValue(value1) == lowestEquivalentValue(value2));
        }

        /// <summary>
        /// Provide a (conservatively high) estimate of the Histogram's total footprint in bytes
        /// </summary>
        /// <returns>a (conservatively high) estimate of the Histogram's total footprint in bytes</returns>
        public int getEstimatedFootprintInBytes()
        {
            return _getEstimatedFootprintInBytes();
        }

        //
        //
        //
        // Timestamp support:
        //
        //
        //

        /// <summary>
        /// Gets or Sets the start time stamp value associated with this histogram to a given value.
        /// By convention in msec since the epoch.
        /// </summary>
        public long StartTimeStamp { get { return startTimeStampMsec; } set { startTimeStampMsec = value; } }

        /// <summary>
        /// Gets or Sets the end time stamp value associated with this histogram to a given value.
        /// By convention in msec since the epoch.
        /// </summary>
        public long EndTimeStamp { get { return endTimeStampMsec; } set { endTimeStampMsec = value; } }

        //
        //
        //
        // Histogram Data access support:
        //
        //
        //

        /// <summary>
        /// Get the lowest recorded value level in the histogram
        /// </summary>
        /// <returns>the Min value recorded in the histogram</returns>
        public long getMinValue()
        {
            recordedValuesIterator.reset();
            long min = 0;
            if (recordedValuesIterator.hasNext())
            {
                HistogramIterationValue iterationValue = recordedValuesIterator.next();
                min = iterationValue.getValueIteratedTo();
            }
            return lowestEquivalentValue(min);
        }

        /// <summary>
        /// Get the highest recorded value level in the histogram
        /// </summary>
        /// <returns>the Max value recorded in the histogram</returns>
        public long getMaxValue()
        {
            recordedValuesIterator.reset();
            long max = 0;
            while (recordedValuesIterator.hasNext())
            {
                HistogramIterationValue iterationValue = recordedValuesIterator.next();
                max = iterationValue.getValueIteratedTo();
            }
            return lowestEquivalentValue(max);
        }

        /// <summary>
        /// Get the computed mean value of all recorded values in the histogram
        /// </summary>
        /// <returns>the mean value (in value units) of the histogram data</returns>
        public double getMean()
        {
            recordedValuesIterator.reset();
            long totalValue = 0;
            while (recordedValuesIterator.hasNext())
            {
                HistogramIterationValue iterationValue = recordedValuesIterator.next();
                totalValue = iterationValue.getTotalValueToThisValue();
            }
            return (totalValue * 1.0) / getTotalCount();
        }

        /// <summary>
        /// Get the computed standard deviation of all recorded values in the histogram
        /// </summary>
        /// <returns>the standard deviation (in value units) of the histogram data</returns>
        public double getStdDeviation()
        {
            double mean = getMean();
            double geometric_deviation_total = 0.0;
            recordedValuesIterator.reset();
            while (recordedValuesIterator.hasNext())
            {
                HistogramIterationValue iterationValue = recordedValuesIterator.next();
                Double deviation = (medianEquivalentValue(iterationValue.getValueIteratedTo()) * 1.0) - mean;
                geometric_deviation_total += (deviation * deviation) * iterationValue.getCountAddedInThisIterationStep();
            }
            double std_deviation = Math.Sqrt(geometric_deviation_total / getTotalCount());
            return std_deviation;
        }

        /// <summary>
        /// Get the value at a given percentile
        /// </summary>
        /// <param name="percentile">The percentile to get the value for</param>
        /// <returns>The value a given percentage of all recorded value entries in the histogram fall below.</returns>
        public long getValueAtPercentile(double percentile)
        {
            double requestedPercentile = Math.Min(percentile, 100.0); // Truncate down to 100%
            long countAtPercentile = (long)(((requestedPercentile / 100.0) * getTotalCount()) + 0.5); // round to nearest
            countAtPercentile = Math.Max(countAtPercentile, 1); // Make sure we at least reach the first recorded entry
            long totalToCurrentIJ = 0;
            for (int i = 0; i < bucketCount; i++)
            {
                int j = (i == 0) ? 0 : (subBucketCount / 2);
                for (; j < subBucketCount; j++)
                {
                    totalToCurrentIJ += getCountAt(i, j);
                    if (totalToCurrentIJ >= countAtPercentile)
                    {
                        long valueAtIndex = valueFromIndex(i, j);
                        return highestEquivalentValue(valueAtIndex);
                    }
                }
            }
            throw new ArgumentOutOfRangeException("percentile value not found in range"); // should not reach here.
        }

        /// <summary>
        /// Get the percentile at a given value
        /// </summary>
        /// <param name="value">The value to get the associated percentile for</param>
        /// <returns>The percentile of values recorded at or below the given value in the histogram.</returns>
        public double getPercentileAtOrBelowValue(long value)
        {
            long totalToCurrentIJ = 0;

            int targetBucketIndex = getBucketIndex(value);
            int targetSubBucketIndex = getSubBucketIndex(value, targetBucketIndex);

            if (targetBucketIndex >= bucketCount)
                return 100.0;

            for (int i = 0; i <= targetBucketIndex; i++)
            {
                int j = (i == 0) ? 0 : (subBucketCount / 2);
                int subBucketCap = (i == targetBucketIndex) ? (targetSubBucketIndex + 1) : subBucketCount;
                for (; j < subBucketCap; j++)
                {
                    totalToCurrentIJ += getCountAt(i, j);
                }
            }

            return (100.0 * totalToCurrentIJ) / getTotalCount();
        }

        /// <summary>
        /// Get the count of recorded values within a range of value levels. (inclusive to within the histogram's resolution)
        /// </summary>
        /// <param name="lowValue">The lower value bound on the range for which to provide the recorded count. Will be rounded down with <see cref="lowestEquivalentValue"/>.</param>
        /// <param name="highValue">The higher value bound on the range for which to provide the recorded count. Will be rounded up with <see cref="highestEquivalentValue"/>.</param>
        /// <returns>the total count of values recorded in the histogram within the value range that is &gt;= <param name="lowValue"/> &lt;= <param name="highValue"></param></returns>
        /// <exception cref="IndexOutOfRangeException">on parameters that are outside the tracked value range</exception>
        public long getCountBetweenValues(long lowValue, long highValue)
        {
            long count = 0;

            // Compute the sub-bucket-rounded values for low and high:
            int lowBucketIndex = getBucketIndex(lowValue);
            int lowSubBucketIndex = getSubBucketIndex(lowValue, lowBucketIndex);
            long valueAtlowValue = valueFromIndex(lowBucketIndex, lowSubBucketIndex);
            int highBucketIndex = getBucketIndex(highValue);
            int highSubBucketIndex = getSubBucketIndex(highValue, highBucketIndex);
            long valueAtHighValue = valueFromIndex(highBucketIndex, highSubBucketIndex);

            if ((lowBucketIndex >= bucketCount) || (highBucketIndex >= bucketCount))
                throw new ArgumentOutOfRangeException();

            for (int i = lowBucketIndex; i <= highBucketIndex; i++)
            {
                int j = (i == 0) ? 0 : (subBucketCount / 2);
                for (; j < subBucketCount; j++)
                {
                    long valueAtIndex = valueFromIndex(i, j);
                    if (valueAtIndex > valueAtHighValue)
                        return count;
                    if (valueAtIndex >= valueAtlowValue)
                        count += getCountAt(i, j);
                }
            }
            return count;
        }

        /// <summary>
        /// Get the count of recorded values at a specific value
        /// </summary>
        /// <param name="value">The value for which to provide the recorded count</param>
        /// <returns>The total count of values recorded in the histogram at the given value (to within the histogram resolution at the value level).</returns>
        /// <exception cref="IndexOutOfRangeException">On parameters that are outside the tracked value range</exception>
        public long getCountAtValue(long value) //throws ArrayIndexOutOfBoundsException 
        {
            int bucketIndex = getBucketIndex(value);
            int subBucketIndex = getSubBucketIndex(value, bucketIndex);
            // May throw ArrayIndexOutOfBoundsException:
            return getCountAt(bucketIndex, subBucketIndex);
        }

        /// <summary>
        /// Provide a means of iterating through histogram values according to percentile levels. 
        /// The iteration is performed in steps that start at 0% and reduce their distance to 100% according to the <i>percentileTicksPerHalfDistance</i> parameter, ultimately reaching 100% when all recorded histogram values are exhausted.
        /// </summary>
        /// <param name="percentileTicksPerHalfDistance">The number of iteration steps per half-distance to 100%.</param>
        /// <returns>An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="PercentileIterator"/></returns>
        public Percentiles percentiles(int percentileTicksPerHalfDistance)
        {
            return new Percentiles(this, percentileTicksPerHalfDistance);
        }

        /// <summary>
        /// Provide a means of iterating through histogram values using linear steps. The iteration is performed in steps of <i>valueUnitsPerBucket</i> in size, terminating when all recorded histogram values are exhausted.
        /// </summary>
        /// <param name="valueUnitsPerBucket">The size (in value units) of the linear buckets to use</param>
        /// <returns>An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="LinearIterator"/></returns>
        public LinearBucketValues linearBucketValues(int valueUnitsPerBucket)
        {
            return new LinearBucketValues(this, valueUnitsPerBucket);
        }

        /// <summary>
        /// Provide a means of iterating through histogram values at logarithmically increasing levels. 
        /// The iteration is performed in steps that start at<i>valueUnitsInFirstBucket</i> and increase exponentially according to <i>logBase</i>, terminating when all recorded histogram values are exhausted.
        /// </summary>
        /// <param name="valueUnitsInFirstBucket">The size (in value units) of the first bucket in the iteration</param>
        /// <param name="logBase">The multiplier by which bucket sizes will grow in each iteration step</param>
        /// <returns>An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="LogarithmicIterator"/></returns>
        public LogarithmicBucketValues logarithmicBucketValues(int valueUnitsInFirstBucket, double logBase)
        {
            return new LogarithmicBucketValues(this, valueUnitsInFirstBucket, logBase);
        }

        /// <summary>
        /// Provide a means of iterating through all recorded histogram values using the finest granularity steps supported by the underlying representation.
        /// The iteration steps through all non-zero recorded value counts, and terminates when all recorded histogram values are exhausted.
        /// </summary>
        /// <returns>An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="RecordedValuesIterator"/></returns>
        public RecordedValues recordedValues()
        {
            return new RecordedValues(this);
        }

        /// <summary>
        /// Provide a means of iterating through all histogram values using the finest granularity steps supported by the underlying representation.
        /// The iteration steps through all possible unit value levels, regardless of whether or not there were recorded values for that value level, and terminates when all recorded histogram values are exhausted.
        /// </summary>
        /// <returns>An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="RecordedValuesIterator"/></returns>
        public AllValues allValues()
        {
            return new AllValues(this);
        }


        // Percentile iterator support:


        /// <summary>
        /// An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="PercentileIterator"/>
        /// </summary>
        public class Percentiles : IEnumerable<HistogramIterationValue>
        {
            readonly AbstractHistogram histogram;
            readonly int percentileTicksPerHalfDistance;

            public Percentiles(AbstractHistogram histogram, int percentileTicksPerHalfDistance)
            {
                this.histogram = histogram;
                this.percentileTicksPerHalfDistance = percentileTicksPerHalfDistance;
            }

            public IEnumerator<HistogramIterationValue> GetEnumerator()
            {
                return new PercentileIterator(histogram, percentileTicksPerHalfDistance);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        // Linear iterator support:

        /// <summary>
        /// An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="LinearIterator"/>
        /// </summary>
        public class LinearBucketValues : IEnumerable<HistogramIterationValue>
        {
            private readonly AbstractHistogram histogram;
            private readonly int valueUnitsPerBucket;

            public LinearBucketValues(AbstractHistogram histogram, int valueUnitsPerBucket)
            {
                this.histogram = histogram;
                this.valueUnitsPerBucket = valueUnitsPerBucket;
            }

            public IEnumerator<HistogramIterationValue> GetEnumerator()
            {
                return new LinearIterator(histogram, valueUnitsPerBucket);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        // Logarithmic iterator support:

        /// <summary>
        /// An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="LogarithmicIterator"/>
        /// </summary>
        public class LogarithmicBucketValues : IEnumerable<HistogramIterationValue>
        {
            readonly AbstractHistogram histogram;
            readonly int valueUnitsInFirstBucket;
            readonly double logBase;

            public LogarithmicBucketValues(AbstractHistogram histogram, int valueUnitsInFirstBucket, double logBase)
            {
                this.histogram = histogram;
                this.valueUnitsInFirstBucket = valueUnitsInFirstBucket;
                this.logBase = logBase;
            }

            public IEnumerator<HistogramIterationValue> GetEnumerator()
            {
                return new LogarithmicIterator(histogram, valueUnitsInFirstBucket, logBase);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        // Recorded value iterator support:

        /// <summary>
        /// An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="RecordedValuesIterator"/>
        /// </summary>
        public class RecordedValues : IEnumerable<HistogramIterationValue>
        {
            readonly AbstractHistogram histogram;

            public RecordedValues(AbstractHistogram histogram)
            {
                this.histogram = histogram;
            }

            public IEnumerator<HistogramIterationValue> GetEnumerator()
            {
                return new RecordedValuesIterator(histogram);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        // AllValues iterator support:

        /// <summary>
        /// An iterator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="AllValuesIterator"/>
        /// </summary>
        public class AllValues : IEnumerable<HistogramIterationValue>
        {
            private readonly AbstractHistogram histogram;

            public AllValues(AbstractHistogram histogram)
            {
                this.histogram = histogram;
            }

            public IEnumerator<HistogramIterationValue> GetEnumerator()
            {
                return new AllValuesIterator(histogram);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return this.GetEnumerator();
            }
        }

        //
        //
        //
        // Textual percentile output support:
        //
        //
        //

        /// <summary>
        /// Produce textual representation of the value distribution of histogram data by percentile. 
        /// The distribution is output with exponentially increasing resolution, with each exponentially decreasing half-distance containing <i>dumpTicksPerHalf</i> percentile reporting tick points.
        /// </summary>
        /// <param name="printStream">Stream into which the distribution will be output</param>
        /// <param name="percentileTicksPerHalfDistance">The number of reporting points per exponentially decreasing half-distance</param>
        /// <param name="outputValueUnitScalingRatio">The scaling factor by which to divide histogram recorded values units in output</param>
        /// <param name="useCsvFormat">Output in CSV format if <c>true</c>, else use plain text form.</param>
        public void outputPercentileDistribution(TextWriter /*PrintStream*/ printStream,
                                                 int percentileTicksPerHalfDistance = 5,
                                                 Double outputValueUnitScalingRatio = 1000.0,
                                                 bool useCsvFormat = false)
        {
            if (useCsvFormat)
            {
                printStream.Write("\"Value\",\"Percentile\",\"TotalCount\",\"1/(1-Percentile)\"\n");
            }
            else
            {
                printStream.Write("{0,12} {1,14} {2,10} {3,14}\n\n", "Value", "Percentile", "TotalCount", "1/(1-Percentile)");
            }

            PercentileIterator iterator = percentileIterator;
            iterator.reset(percentileTicksPerHalfDistance);

            String percentileFormatString;
            String lastLinePercentileFormatString;
            if (useCsvFormat)
            {
                percentileFormatString = "{0:F" + numberOfSignificantValueDigits + "},{1:F12},{2},{3:F2}\n";
                lastLinePercentileFormatString = "{0:F" + numberOfSignificantValueDigits + "},{1:F12},{2},Infinity\n";
            }
            else
            {
                percentileFormatString = "{0,12:F" + numberOfSignificantValueDigits + "}" + " {1,2:F12} {2,10} {3,14:F2}\n";
                lastLinePercentileFormatString = "{0,12:F" + numberOfSignificantValueDigits + "} {1,2:F12} {2,10}\n";
            }

            try
            {
                while (iterator.hasNext())
                {
                    HistogramIterationValue iterationValue = iterator.next();
                    if (iterationValue.getPercentileLevelIteratedTo() != 100.0D)
                    {
                        printStream.Write(percentileFormatString,
                                iterationValue.getValueIteratedTo() / outputValueUnitScalingRatio,
                                iterationValue.getPercentileLevelIteratedTo() / 100.0D,
                                iterationValue.getTotalCountToThisValue(),
                                1 / (1.0D - (iterationValue.getPercentileLevelIteratedTo() / 100.0D)));
                    }
                    else
                    {
                        printStream.Write(lastLinePercentileFormatString,
                                iterationValue.getValueIteratedTo() / outputValueUnitScalingRatio,
                                iterationValue.getPercentileLevelIteratedTo() / 100.0D,
                                iterationValue.getTotalCountToThisValue());
                    }
                }

                if (!useCsvFormat)
                {
                    // Calculate and output mean and std. deviation.
                    // Note: mean/std. deviation numbers are very often completely irrelevant when
                    // data is extremely non-normal in distribution (e.g. in cases of strong multi-modal
                    // response time distribution associated with GC pauses). However, reporting these numbers
                    // can be very useful for contrasting with the detailed percentile distribution
                    // reported by outputPercentileDistribution(). It is not at all surprising to find
                    // percentile distributions where results fall many tens or even hundreds of standard
                    // deviations away from the mean - such results simply indicate that the data sampled
                    // exhibits a very non-normal distribution, highlighting situations for which the std.
                    // deviation metric is a useless indicator.

                    double mean = getMean() / outputValueUnitScalingRatio;
                    double std_deviation = getStdDeviation() / outputValueUnitScalingRatio;
                    printStream.Write("#[Mean    = {0,12:F" + numberOfSignificantValueDigits + "}, " +
                                       "StdDeviation   = {1,12:F" + numberOfSignificantValueDigits + "}]\n", mean, std_deviation);
                    printStream.Write("#[Max     = {0,12:F" + numberOfSignificantValueDigits + "}, Total count    = {1,12}]\n",
                                        getMaxValue() / outputValueUnitScalingRatio, getTotalCount());
                    printStream.Write("#[Buckets = {0,12}, SubBuckets     = {1,12}]\n",
                                        bucketCount, subBucketCount);
                }
            }
            catch (ArgumentOutOfRangeException e)
            {
                // Overflow conditions on histograms can lead to ArrayIndexOutOfBoundsException on iterations:
                if (hasOverflowed())
                {
                    //printStream.format(Locale.US, "# Histogram counts indicate OVERFLOW values");
                    printStream.Write("# Histogram counts indicate OVERFLOW values");
                }
                else
                {
                    // Re-throw if reason is not a known overflow:
                    throw e;
                }
            }
        }

        //
        //
        //
        // Serialization support:
        //
        //
        //

        //
        //
        //
        // Encoding/Decoding support:
        //
        //
        //

        /// <summary>
        /// Get the capacity needed to encode this histogram into a <see cref="ByteBuffer"/>
        /// </summary>
        /// <returns>the capacity needed to encode this histogram into a <see cref="ByteBuffer"/></returns>
        public int getNeededByteBufferCapacity()
        {
            return getNeededByteBufferCapacity(countsArrayLength);
        }

        private int getNeededByteBufferCapacity(int relevantLength)
        {
            return (relevantLength * wordSizeInBytes) + 32;
        }

        protected abstract void fillCountsArrayFromBuffer(ByteBuffer buffer, int length);

        protected abstract void fillBufferFromCountsArray(ByteBuffer buffer, int length);

        private static int encodingCookieBase = 0x1c849308;
        private static int compressedEncodingCookieBase = 0x1c849309;

        private int getEncodingCookie()
        {
            return encodingCookieBase + (wordSizeInBytes << 4);
        }

        private int getCompressedEncodingCookie()
        {
            return compressedEncodingCookieBase + (wordSizeInBytes << 4);
        }

        private static int getCookieBase(int cookie)
        {
            return (cookie & ~0xf0);
        }

        private static int getWordSizeInBytesFromCookie(int cookie)
        {
            return (cookie & 0xf0) >> 4;
        }

        /// <summary>
        /// Encode this histogram into a <see cref="ByteBuffer"/>
        /// </summary>
        /// <param name="buffer">The buffer to encode into</param>
        /// <returns>The number of bytes written to the buffer</returns>
        public int encodeIntoByteBuffer(ByteBuffer buffer)
        {
            lock (updateLock)
            {
                long maxValue = getMaxValue();
                int relevantLength = getLengthForNumberOfBuckets(getBucketsNeededToCoverValue(maxValue));
                Console.WriteLine($"buffer.capacity() < getNeededByteBufferCapacity(relevantLength))");
                Console.WriteLine($"  buffer.capacity() = {buffer.capacity()}");
                Console.WriteLine($"  relevantLength = {relevantLength}");
                Console.WriteLine($"  getNeededByteBufferCapacity(relevantLength) = {getNeededByteBufferCapacity(relevantLength)}");
                if (buffer.capacity() < getNeededByteBufferCapacity(relevantLength))
                {
                    throw new ArgumentOutOfRangeException("buffer does not have capacity for" + getNeededByteBufferCapacity(relevantLength) + " bytes");
                }
                buffer.putInt(getEncodingCookie());
                buffer.putInt(numberOfSignificantValueDigits);
                buffer.putLong(lowestTrackableValue);
                buffer.putLong(highestTrackableValue);
                buffer.putLong(getTotalCount()); // Needed because overflow situations may lead this to differ from counts totals

                Debug.WriteLine("MaxValue = {0}, Buckets needed = {1}, relevantLength = {2}", maxValue, getBucketsNeededToCoverValue(maxValue), relevantLength);
                Debug.WriteLine("MaxValue = {0}, Buckets needed = {1}, relevantLength = {2}", maxValue, getBucketsNeededToCoverValue(maxValue), relevantLength);

                Console.WriteLine($"fillBufferFromCountsArray({buffer}, {relevantLength} * {wordSizeInBytes});");
                fillBufferFromCountsArray(buffer, relevantLength * wordSizeInBytes);

                return getNeededByteBufferCapacity(relevantLength);
            }
        }

        /// <summary>
        /// Encode this histogram in compressed form into a byte array
        /// </summary>
        /// <param name="targetBuffer">The buffer to encode into</param>
        /// <param name="compressionLevel">Compression level.</param>
        /// <returns>The number of bytes written to the buffer</returns>
        public long encodeIntoCompressedByteBuffer(ByteBuffer targetBuffer, CompressionLevel /*int*/ compressionLevel)
        {
            lock (updateLock)
            {
                if (intermediateUncompressedByteBuffer == null)
                {
                    intermediateUncompressedByteBuffer = ByteBuffer.allocate(getNeededByteBufferCapacity(countsArrayLength));
                }
                intermediateUncompressedByteBuffer.clear();
                int uncompressedLength = encodeIntoByteBuffer(intermediateUncompressedByteBuffer);

                targetBuffer.putInt(getCompressedEncodingCookie());
                targetBuffer.putInt(0); // Placeholder for compressed contents length
                byte[] targetArray = targetBuffer.array();
                long compressedDataLength = 0;
                using (var outputStream = new CountingMemoryStream(targetArray, 8, targetArray.Length - 8))
                {
                    using (var compressor = new DeflateStream(outputStream, compressionLevel))
                    {
                        compressor.Write(intermediateUncompressedByteBuffer.array(), 0, uncompressedLength);
                        compressor.Flush();
                    }
                    compressedDataLength = outputStream.BytesWritten;
                }

                targetBuffer.putInt(4, (int)compressedDataLength); // Record the compressed length

                Debug.WriteLine("COMPRESSING - Wrote {0} bytes (header = 8), original size {1}", compressedDataLength + 8, uncompressedLength);

                return compressedDataLength + 8;
            }
        }

        /// <summary>
        /// Encode this histogram in compressed form into a byte array
        /// </summary>
        /// <param name="targetBuffer">The buffer to encode into</param>
        /// <returns>The number of bytes written to the array</returns>
        public long encodeIntoCompressedByteBuffer(ByteBuffer targetBuffer)
        {
            return encodeIntoCompressedByteBuffer(targetBuffer, /*Deflater.DEFAULT_COMPRESSION*/ CompressionLevel.Optimal);
        }

        private static readonly Type[] constructorArgsTypes = { typeof(long), typeof(long), typeof(int) };

        static AbstractHistogram constructHistogramFromBufferHeader(ByteBuffer buffer,
                                                                    Type histogramClass,
                                                                    long minBarForHighestTrackableValue)
        {
            int cookie = buffer.getInt();
            if (getCookieBase(cookie) != encodingCookieBase)
            {
                throw new ArgumentException("The buffer does not contain a Histogram");
            }

            int numberOfSignificantValueDigits = buffer.getInt();
            long lowestTrackableValue = buffer.getLong();
            long highestTrackableValue = buffer.getLong();
            long totalCount = buffer.getLong();

            highestTrackableValue = Math.Max(highestTrackableValue, minBarForHighestTrackableValue);

            try
            {
                //@SuppressWarnings("unchecked")
                ConstructorInfo constructor = histogramClass.GetConstructor(constructorArgsTypes);
                AbstractHistogram histogram =
                        (AbstractHistogram)constructor.Invoke(new object[] { lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits });
                histogram.setTotalCount(totalCount); // Restore totalCount
                if (cookie != histogram.getEncodingCookie())
                {
                    throw new ArgumentException(
                            "The buffer's encoded value byte size (" +
                                    getWordSizeInBytesFromCookie(cookie) +
                                    ") does not match the Histogram's (" +
                                    histogram.wordSizeInBytes + ")");
                }
                return histogram;
            }
            // TODO fix this, find out what Exceptions can actually be thrown!!!!
            catch (Exception ex)
            {
                throw new ArgumentException("Unable to create histogram of Type " + histogramClass.Name + ": " + ex.Message, ex);
            }
            //} catch (IllegalAccessException ex) {
            //    throw new ArgumentException(ex);
            //} catch (NoSuchMethodException ex) {
            //    throw new ArgumentException(ex);
            //} catch (InstantiationException ex) {
            //    throw new ArgumentException(ex);
            //} catch (InvocationTargetException ex) {
            //    throw new ArgumentException(ex);
            //}
        }

        protected static AbstractHistogram decodeFromByteBuffer(ByteBuffer buffer, Type histogramClass,
                                                      long minBarForHighestTrackableValue)
        {
            AbstractHistogram histogram = constructHistogramFromBufferHeader(buffer, histogramClass,
                    minBarForHighestTrackableValue);

            int expectedCapacity = histogram.getNeededByteBufferCapacity(histogram.countsArrayLength);
            if (expectedCapacity > buffer.capacity())
            {
                throw new ArgumentException("The buffer does not contain the full Histogram");
            }

            Debug.WriteLine("DECODING: Writing {0} items (int/short/long, NOT bytes)", histogram.countsArrayLength);

            // TODO to optimise this we'd have to store "relevantLength" in the buffer itself and pull it out here
            // See https://github.com/HdrHistogram/HdrHistogram/issues/18 for full discussion

            histogram.fillCountsArrayFromBuffer(buffer, histogram.countsArrayLength * histogram.wordSizeInBytes);

            return histogram;
        }

        protected static AbstractHistogram decodeFromCompressedByteBuffer(ByteBuffer buffer, Type histogramClass,
                                                                long minBarForHighestTrackableValue) //throws DataFormatException 
        {
            int cookie = buffer.getInt();
            if (getCookieBase(cookie) != compressedEncodingCookieBase)
            {
                throw new ArgumentException("The buffer does not contain a compressed Histogram");
            }
            int lengthOfCompressedContents = buffer.getInt();
            AbstractHistogram histogram;
            ByteBuffer countsBuffer;
            int numOfBytesDecompressed = 0;
            using (var inputStream = new MemoryStream(buffer.array(), 8, lengthOfCompressedContents))
            using (var decompressor = new DeflateStream(inputStream, CompressionMode.Decompress))
            {
                ByteBuffer headerBuffer = ByteBuffer.allocate(32);
                decompressor.Read(headerBuffer.array(), 0, 32);
                histogram = constructHistogramFromBufferHeader(headerBuffer, histogramClass, minBarForHighestTrackableValue);
                countsBuffer = ByteBuffer.allocate(histogram.getNeededByteBufferCapacity(histogram.countsArrayLength) - 32);
                numOfBytesDecompressed = decompressor.Read(countsBuffer.array(), 0, countsBuffer.array().Length);
            }

            Debug.WriteLine("DECOMPRESSING: Writing {0} bytes (plus 32 for header) into array size {1}, started with {2} bytes of compressed data  ({3} + 8 for the header)",
                numOfBytesDecompressed, countsBuffer.array().Length, lengthOfCompressedContents + 8, lengthOfCompressedContents);

            // TODO Sigh, have to fix this for AtomicHistogram, it's needs a count of ITEMS, not BYTES)
            //histogram.fillCountsArrayFromBuffer(countsBuffer, histogram.countsArrayLength * histogram.wordSizeInBytes);
            histogram.fillCountsArrayFromBuffer(countsBuffer, numOfBytesDecompressed);

            return histogram;
        }

        //
        //
        //
        // Support for overflow detection and re-establishing a proper totalCount:
        //
        //
        //

        /// <summary>
        /// Determine if this histogram had any of it's value counts overflow.
        /// </summary>
        /// <returns><c>true</c> if this histogram has had a count value overflow, else <c>false</c>.</returns>
        /// <remarks>
        /// Since counts are kept in fixed integer form with potentially limited range (e.g. int and short), a specific value range count could potentially overflow, leading to an inaccurate and misleading histogram representation.
        /// This method accurately determines whether or not an overflow condition has happened in an IntHistogram or ShortHistogram.
        /// </remarks>
        public bool hasOverflowed()
        {
            // On overflow, the totalCount accumulated counter will (always) not match the total of counts
            long totalCounted = 0;
            for (int i = 0; i < countsArrayLength; i++)
            {
                totalCounted += getCountAtIndex(i);
            }
            return (totalCounted != getTotalCount());
        }

        /// <summary>
        /// Reestablish the internal notion of totalCount by recalculating it from recorded values.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Implementations of AbstractHistogram may maintain a separately tracked notion of totalCount, which is useful for concurrent modification tracking, overflow detection, and speed of execution in iteration.
        /// This separately tracked totalCount can get into a state that is inconsistent with the currently recorded value counts under various concurrent modification and overflow conditions.
        /// </para>
        /// <para>
        /// Applying this method will override internal indications of potential overflows and concurrent modification, and will reestablish a self-consistent representation of the histogram data based purely on the current internal representation of recorded counts.
        /// </para>
        /// <para>
        /// In cases of concurrent modifications such as during copying, or due to racy multi-threaded updates on non-atomic or non-synchronized variants, which can result in potential loss of counts and an inconsistent (indicating potential overflow) internal state, calling this method on a histogram will reestablish a consistent internal state based on the potentially lossy counts representations.
        /// </para>
        /// <para>
        /// Note that this method is not synchronized against concurrent modification in any way, and will only reliably reestablish consistent internal state when no concurrent modification of the histogram is performed while it executes.
        /// </para>
        /// Note that in the cases of actual overflow conditions (which can result in negative counts) this self consistent view may be very wrong, and not just slightly lossy.
        /// </remarks>
        public void reestablishTotalCount()
        {
            // On overflow, the totalCount accumulated counter will (always) not match the total of counts
            long totalCounted = 0;
            for (int i = 0; i < countsArrayLength; i++)
            {
                totalCounted += getCountAtIndex(i);
            }
            setTotalCount(totalCounted);
        }

        //
        //
        //
        // Internal helper methods:
        //
        //
        //

        private int getBucketsNeededToCoverValue(long value)
        {
            long trackableValue = (subBucketCount - 1) << unitMagnitude;
            int bucketsNeeded = 1;
            while (trackableValue < value)
            {
                trackableValue <<= 1;
                bucketsNeeded++;
            }
            return bucketsNeeded;
        }

        private int getLengthForNumberOfBuckets(int numberOfBuckets)
        {
            int lengthNeeded = (numberOfBuckets + 1) * (subBucketCount / 2);
            return lengthNeeded;
        }

        private int countsArrayIndex(int bucketIndex, int subBucketIndex)
        {
            Debug.Assert(subBucketIndex < subBucketCount);
            Debug.Assert(bucketIndex == 0 || (subBucketIndex >= subBucketHalfCount));
            // Calculate the index for the first entry in the bucket:
            // (The following is the equivalent of ((bucketIndex + 1) * subBucketHalfCount) ):
            int bucketBaseIndex = (bucketIndex + 1) << subBucketHalfCountMagnitude;
            // Calculate the offset in the bucket:
            int offsetInBucket = subBucketIndex - subBucketHalfCount;
            // The following is the equivalent of ((subBucketIndex  - subBucketHalfCount) + bucketBaseIndex;
            return bucketBaseIndex + offsetInBucket;
        }

        internal long getCountAt(int bucketIndex, int subBucketIndex)
        {
            return getCountAtIndex(countsArrayIndex(bucketIndex, subBucketIndex));
        }

        private int getBucketIndex(long value)
        {
            int pow2ceiling = 64 - MiscUtilities.numberOfLeadingZeros(value | subBucketMask); // smallest power of 2 containing value
            return pow2ceiling - unitMagnitude - (subBucketHalfCountMagnitude + 1);
        }

        private int getSubBucketIndex(long value, int bucketIndex)
        {
            return (int)(value >> (bucketIndex + unitMagnitude));
        }

        /*final*/
        internal long valueFromIndex(int bucketIndex, int subBucketIndex)
        {
            return ((long)subBucketIndex) << (bucketIndex + unitMagnitude);
        }

        /*final*/

        private long valueFromIndex(int index)
        {
            int bucketIndex = (index >> subBucketHalfCountMagnitude) - 1;
            int subBucketIndex = (index & (subBucketHalfCount - 1)) + subBucketHalfCount;
            if (bucketIndex < 0)
            {
                subBucketIndex -= subBucketHalfCount;
                bucketIndex = 0;
            }
            return valueFromIndex(bucketIndex, subBucketIndex);
        }
    }
}
