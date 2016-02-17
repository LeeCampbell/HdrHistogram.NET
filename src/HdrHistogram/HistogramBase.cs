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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Threading;
using HdrHistogram.Iteration;
using HdrHistogram.Utilities;

namespace HdrHistogram
{
    /// <summary>
    /// Base class for High Dynamic Range (HDR) Histograms
    /// </summary>
    /// <remarks>
    /// <see cref="HistogramBase"/> supports the recording and analyzing sampled data value counts across a configurable
    /// integer value range with configurable value precision within the range.
    /// Value precision is expressed as the number of significant digits in the value recording, and provides control over 
    /// value quantization behavior across the value range and the subsequent value resolution at any given level.
    /// <para>
    /// For example, a Histogram could be configured to track the counts of observed integer values between 0 and
    /// 36,000,000,000 while maintaining a value precision of 3 significant digits across that range.
    /// Value quantization within the range will thus be no larger than 1/1,000th (or 0.1%) of any value.
    /// This example Histogram could be used to track and analyze the counts of observed response times ranging between
    /// 1 tick (100 nanoseconds) and 1 hour in magnitude, while maintaining a value resolution of 100 nanosecond up to 
    /// 100 microseconds, a resolution of 1 millisecond(or better) up to one second, and a resolution of 1 second 
    /// (or better) up to 1,000 seconds.
    /// At it's maximum tracked value(1 hour), it would still maintain a resolution of 3.6 seconds (or better).
    /// </para>
    /// </remarks>
    public abstract class HistogramBase
    {

        private static long _nextIdentity = -1L;

        /// <summary>
        /// The object to used to lock on when performing atomic actions.
        /// </summary>
        protected readonly object UpdateLock = new object();

        private readonly int _subBucketHalfCountMagnitude;
        private readonly int _unitMagnitude;
        private readonly long _subBucketMask;
        private readonly int _bucketIndexOffset;
        private long _maxValue;
        private long _minNonZeroValue;

        /// <summary>
        /// Get the configured highestTrackableValue
        /// </summary>
        /// <returns>highestTrackableValue</returns>
        public long HighestTrackableValue { get; }

        /// <summary>
        /// Get the configured lowestTrackableValue
        /// </summary>
        /// <returns>lowestTrackableValue</returns>
        public long LowestTrackableValue { get; }

        /// <summary>
        /// Get the configured numberOfSignificantValueDigits
        /// </summary>
        /// <returns>numberOfSignificantValueDigits</returns>
        public int NumberOfSignificantValueDigits { get; }

        /// <summary>
        /// A unique id number for this instance.
        /// </summary>
        public long Identity { get; private set; }

        /// <summary>
        /// Gets or Sets the start time stamp value associated with this histogram to a given value.
        /// By convention in msec since the epoch.
        /// </summary>
        public long StartTimeStamp { get; set; }

        /// <summary>
        /// Gets or Sets the end time stamp value associated with this histogram to a given value.
        /// By convention in msec since the epoch.
        /// </summary>
        public long EndTimeStamp { get; set; }

        /// <summary>
        /// Gets the total number of recorded values.
        /// </summary>
        public abstract long TotalCount { get; internal set; }


        internal int BucketCount { get; }       //TODO: Candidate for private read-only field. -LC
        internal int SubBucketCount { get; }    //TODO: Candidate for private read-only field. -LC
        internal int SubBucketHalfCount { get; }//TODO: Candidate for private read-only field. -LC

        /// <summary>
        /// The length of the internal array that stores the counts.
        /// </summary>
        internal int CountsArrayLength { get; }

        /// <summary>
        /// Returns the word size of this implementation
        /// </summary>
        internal abstract int WordSizeInBytes { get; }
        protected abstract long MaxAllowableCount { get; }


        /// <summary>
        /// Construct a histogram given the lowest and highest values to be tracked and a number of significant decimal digits.
        /// </summary>
        /// <param name="lowestTrackableValue">The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1.
        /// May be internally rounded down to nearest power of 2.
        /// </param>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram.
        /// Must be a positive integer that is &gt;= (2 * lowestTrackableValue).
        /// </param>
        /// <param name="numberOfSignificantValueDigits">
        /// The number of significant decimal digits to which the histogram will maintain value resolution and separation. 
        /// Must be a non-negative integer between 0 and 5.
        /// </param>
        /// <remarks>
        /// Providing a lowestTrackableValue is useful in situations where the units used for the histogram's values are much 
        /// smaller that the minimal accuracy required.
        /// For example when tracking time values stated in ticks (100 nanoseconds), where the minimal accuracy required is a
        /// microsecond, the proper value for lowestTrackableValue would be 10.
        /// </remarks>
        protected HistogramBase(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
        {
            if (lowestTrackableValue < 1) throw new ArgumentException("lowestTrackableValue must be >= 1");
            if (highestTrackableValue < 2 * lowestTrackableValue) throw new ArgumentException("highestTrackableValue must be >= 2 * lowestTrackableValue");
            if ((numberOfSignificantValueDigits < 0) || (numberOfSignificantValueDigits > 5)) throw new ArgumentException("numberOfSignificantValueDigits must be between 0 and 6");

            Identity = Interlocked.Increment(ref _nextIdentity);
            LowestTrackableValue = lowestTrackableValue;
            HighestTrackableValue = highestTrackableValue;
            NumberOfSignificantValueDigits = numberOfSignificantValueDigits;

            _unitMagnitude = (int)Math.Floor(Math.Log(LowestTrackableValue) / Math.Log(2));

            // We need to maintain power-of-two subBucketCount (for clean direct indexing) that is large enough to
            // provide unit resolution to at least largestValueWithSingleUnitResolution. So figure out
            // largestValueWithSingleUnitResolution's nearest power-of-two (rounded up), and use that:
            var largestValueWithSingleUnitResolution = 2 * (long)Math.Pow(10, NumberOfSignificantValueDigits);
            var subBucketCountMagnitude = (int)Math.Ceiling(Math.Log(largestValueWithSingleUnitResolution) / Math.Log(2));

            _subBucketHalfCountMagnitude = ((subBucketCountMagnitude > 1) ? subBucketCountMagnitude : 1) - 1;
            SubBucketCount = (int)Math.Pow(2, (_subBucketHalfCountMagnitude + 1));
            SubBucketHalfCount = SubBucketCount / 2;
            _subBucketMask = (SubBucketCount - 1) << _unitMagnitude;


            _bucketIndexOffset = 64 - _unitMagnitude - (_subBucketHalfCountMagnitude + 1);


            // determine exponent range needed to support the trackable value with no overflow:
            BucketCount = GetBucketsNeededToCoverValue(HighestTrackableValue);

            CountsArrayLength = GetLengthForNumberOfBuckets(BucketCount);
        }

        /// <summary>
        /// Records a value in the histogram
        /// </summary>
        /// <param name="value">The value to be recorded</param>
        /// <exception cref="System.IndexOutOfRangeException">if value is exceeds highestTrackableValue</exception>
        public void RecordValue(long value)
        {
            RecordSingleValue(value);
        }

        /// <summary>
        /// Record a value in the histogram (adding to the value's current count)
        /// </summary>
        /// <param name="value">The value to be recorded</param>
        /// <param name="count">The number of occurrences of this value to record</param>
        /// <exception cref="System.IndexOutOfRangeException">if value is exceeds highestTrackableValue</exception>
        public void RecordValueWithCount(long value, long count)
        {
            // Dissect the value into bucket and sub-bucket parts, and derive index into counts array:
            var bucketIndex = GetBucketIndex(value);
            var subBucketIndex = GetSubBucketIndex(value, bucketIndex);
            var countsIndex = CountsArrayIndex(bucketIndex, subBucketIndex);
            AddToCountAtIndex(countsIndex, count);
        }

        /// <summary>
        /// Record a value in the histogram.
        /// </summary>
        /// <param name="value">The value to record</param>
        /// <param name="expectedIntervalBetweenValueSamples">If <paramref name="expectedIntervalBetweenValueSamples"/> is larger than 0, add auto-generated value records as appropriate if <paramref name="value"/> is larger than <paramref name="expectedIntervalBetweenValueSamples"/></param>
        /// <exception cref="System.IndexOutOfRangeException">if value is exceeds highestTrackableValue</exception>
        /// <remarks>
        /// To compensate for the loss of sampled values when a recorded value is larger than the expected interval between value samples, 
        /// Histogram will auto-generate an additional series of decreasingly-smaller (down to the expectedIntervalBetweenValueSamples) value records.
        /// <para>
        /// Note: This is a at-recording correction method, as opposed to the post-recording correction method provided by <see cref="CopyCorrectedForCoordinatedOmission"/>.
        /// The two methods are mutually exclusive, and only one of the two should be be used on a given data set to correct for the same coordinated omission issue.
        /// </para>
        /// See notes in the description of the Histogram calls for an illustration of why this corrective behavior is important.
        /// </remarks>
        public void RecordValueWithExpectedInterval(long value, long expectedIntervalBetweenValueSamples)
        {
            RecordValueWithCountAndExpectedInterval(value, 1, expectedIntervalBetweenValueSamples);
        }

        /// <summary>
        /// Reset the contents and stats of this histogram
        /// </summary>
        public void Reset()
        {
            ClearCounts();
        }

        /// <summary>
        /// Copy this histogram, corrected for coordinated omission, into the target histogram, overwriting it's contents.
        /// </summary>
        /// <param name="targetHistogram">the histogram to copy into</param>
        /// <param name="expectedIntervalBetweenValueSamples">If <paramref name="expectedIntervalBetweenValueSamples"/> is larger than 0, add auto-generated value records as appropriate if value is larger than <paramref name="expectedIntervalBetweenValueSamples"/></param>
        /// <remarks>
        /// See <see cref="CopyCorrectedForCoordinatedOmission"/> for more detailed explanation about how correction is applied
        /// </remarks>
        public void CopyIntoCorrectedForCoordinatedOmission(HistogramBase targetHistogram, long expectedIntervalBetweenValueSamples)
        {
            targetHistogram.Reset();
            targetHistogram.AddWhileCorrectingForCoordinatedOmission(this, expectedIntervalBetweenValueSamples);
            targetHistogram.StartTimeStamp = StartTimeStamp;
            targetHistogram.EndTimeStamp = EndTimeStamp;
        }

        /// <summary>
        /// Add the contents of another histogram to this one.
        /// </summary>
        /// <param name="fromHistogram">The other histogram.</param>
        /// <exception cref="System.IndexOutOfRangeException">if values in fromHistogram's are higher than highestTrackableValue.</exception>
        public virtual void Add(HistogramBase fromHistogram)
        {
            if (HighestTrackableValue < fromHistogram.HighestTrackableValue)
            {
                throw new ArgumentOutOfRangeException(nameof(fromHistogram), $"The other histogram covers a wider range ({fromHistogram.HighestTrackableValue} than this one ({HighestTrackableValue}).");
            }
            if ((BucketCount == fromHistogram.BucketCount) &&
                    (SubBucketCount == fromHistogram.SubBucketCount) &&
                    (_unitMagnitude == fromHistogram._unitMagnitude))
            {
                // Counts arrays are of the same length and meaning, so we can just iterate and add directly:
                for (var i = 0; i < fromHistogram.CountsArrayLength; i++)
                {
                    AddToCountAtIndex(i, fromHistogram.GetCountAtIndex(i));
                }
            }
            else
            {
                // Arrays are not a direct match, so we can't just stream through and add them.
                // Instead, go through the array and add each non-zero value found at it's proper value:
                for (var i = 0; i < fromHistogram.CountsArrayLength; i++)
                {
                    var count = fromHistogram.GetCountAtIndex(i);
                    RecordValueWithCount(fromHistogram.ValueFromIndex(i), count);
                }
            }
        }

        /// <summary>
        /// Add the contents of another histogram to this one, while correcting the incoming data for coordinated omission.
        /// </summary>
        /// <param name="fromHistogram">The other histogram. highestTrackableValue and largestValueWithSingleUnitResolution must match.</param>
        /// <param name="expectedIntervalBetweenValueSamples">If <paramref name="expectedIntervalBetweenValueSamples"/> is larger than 0, add auto-generated value records as appropriate if value is larger than <paramref name="expectedIntervalBetweenValueSamples"/></param>
        /// <remarks>
        /// To compensate for the loss of sampled values when a recorded value is larger than the expected interval between value samples, the values added will include an auto-generated additional series of decreasingly-smaller(down to the expectedIntervalBetweenValueSamples) value records for each count found in the current histogram that is larger than the expectedIntervalBetweenValueSamples.
        /// 
        /// Note: This is a post-recording correction method, as opposed to the at-recording correction method provided by {@link #recordValueWithExpectedInterval(long, long) recordValueWithExpectedInterval}. 
        /// The two methods are mutually exclusive, and only one of the two should be be used on a given data set to correct for the same coordinated omission issue.
        /// See notes in the description of the Histogram calls for an illustration of why this corrective behavior is important.
        /// </remarks>
        /// <exception cref="System.IndexOutOfRangeException">if values exceed highestTrackableValue.</exception>
        public void AddWhileCorrectingForCoordinatedOmission(HistogramBase fromHistogram, long expectedIntervalBetweenValueSamples)
        {
            foreach (var v in fromHistogram.RecordedValues())
            {
                RecordValueWithCountAndExpectedInterval(
                    v.ValueIteratedTo,
                    v.CountAtValueIteratedTo,
                    expectedIntervalBetweenValueSamples);
            }
        }

        /// <summary>
        /// Get the size (in value units) of the range of values that are equivalent to the given value within the histogram's resolution. 
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value">The given value</param>
        /// <returns>The lowest value that is equivalent to the given value within the histogram's resolution.</returns>
        public long SizeOfEquivalentValueRange(long value)
        {
            var bucketIndex = GetBucketIndex(value);
            var subBucketIndex = GetSubBucketIndex(value, bucketIndex);
            if (subBucketIndex >= SubBucketCount)
                bucketIndex++;
            var distanceToNextValue = 1 << (_unitMagnitude + bucketIndex);
            return distanceToNextValue;
        }

        /// <summary>
        /// Get the lowest value that is equivalent to the given value within the histogram's resolution.
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value">The given value</param>
        /// <returns>The lowest value that is equivalent to the given value within the histogram's resolution.</returns>
        public long LowestEquivalentValue(long value)
        {
            var bucketIndex = GetBucketIndex(value);
            var subBucketIndex = GetSubBucketIndex(value, bucketIndex);
            return ValueFromIndex(bucketIndex, subBucketIndex);
        }

        /// <summary>
        /// Get a value that lies in the middle (rounded up) of the range of values equivalent the given value.
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value">The given value</param>
        /// <returns>The value lies in the middle (rounded up) of the range of values equivalent the given value.</returns>
        public long MedianEquivalentValue(long value)
        {
            return LowestEquivalentValue(value)
                + (SizeOfEquivalentValueRange(value) >> 1);
        }

        /// <summary>
        /// Get the next value that is not equivalent to the given value within the histogram's resolution.
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value">The given value</param>
        /// <returns>The next value that is not equivalent to the given value within the histogram's resolution.</returns>
        public long NextNonEquivalentValue(long value)
        {
            return LowestEquivalentValue(value) + SizeOfEquivalentValueRange(value);
        }

        /// <summary>
        /// Determine if two values are equivalent with the histogram's resolution.
        /// Where "equivalent" means that value samples recorded for any two equivalent values are counted in a common total count.
        /// </summary>
        /// <param name="value1">first value to compare</param>
        /// <param name="value2">second value to compare</param>
        /// <returns><c>true</c> if values are equivalent with the histogram's resolution.</returns>
        public bool ValuesAreEquivalent(long value1, long value2)
        {
            return LowestEquivalentValue(value1) == LowestEquivalentValue(value2);
        }

        /// <summary>
        /// Get the value at a given percentile
        /// </summary>
        /// <param name="percentile">The percentile to get the value for</param>
        /// <returns>The value a given percentage of all recorded value entries in the histogram fall below.</returns>
        public long GetValueAtPercentile(double percentile)
        {
            var requestedPercentile = Math.Min(percentile, 100.0); // Truncate down to 100%
            var countAtPercentile = (long)(((requestedPercentile / 100.0) * TotalCount) + 0.5); // round to nearest
            countAtPercentile = Math.Max(countAtPercentile, 1); // Make sure we at least reach the first recorded entry
            long runningCount = 0;
            for (var i = 0; i < BucketCount; i++)
            {
                var j = (i == 0) ? 0 : (SubBucketCount / 2);
                for (; j < SubBucketCount; j++)
                {
                    runningCount += GetCountAt(i, j);
                    if (runningCount >= countAtPercentile)
                    {
                        var valueAtIndex = ValueFromIndex(i, j);
                        return this.HighestEquivalentValue(valueAtIndex);
                    }
                }
            }
            throw new ArgumentOutOfRangeException(nameof(percentile), "percentile value not found in range"); // should not reach here.
        }

        /// <summary>
        /// Get the percentile at a given value
        /// </summary>
        /// <param name="value">The value to get the associated percentile for</param>
        /// <returns>The percentile of values recorded at or below the given value in the histogram.</returns>
        public double GetPercentileAtOrBelowValue(long value)
        {
            var targetBucketIndex = GetBucketIndex(value);
            var targetSubBucketIndex = GetSubBucketIndex(value, targetBucketIndex);

            if (targetBucketIndex >= BucketCount)
                return 100.0;

            var runningCount = 0L;
            for (var i = 0; i <= targetBucketIndex; i++)
            {
                var j = (i == 0) ? 0 : (SubBucketCount / 2);
                var subBucketCap = (i == targetBucketIndex) ? (targetSubBucketIndex + 1) : SubBucketCount;
                for (; j < subBucketCap; j++)
                {
                    runningCount += GetCountAt(i, j);
                }
            }

            return (100.0 * runningCount) / TotalCount;
        }

        /// <summary>
        /// Get the count of recorded values within a range of value levels. (inclusive to within the histogram's resolution)
        /// </summary>
        /// <param name="lowValue">The lower value bound on the range for which to provide the recorded count. Will be rounded down with <see cref="LowestEquivalentValue"/>.</param>
        /// <param name="highValue">The higher value bound on the range for which to provide the recorded count. Will be rounded up with <see cref="HistogramExtensions.HighestEquivalentValue"/>.</param>
        /// <returns>the total count of values recorded in the histogram within the value range that is &gt;= <paramref name="lowValue"/> &lt;= <paramref name="highValue"/></returns>
        /// <exception cref="IndexOutOfRangeException">on parameters that are outside the tracked value range</exception>
        public long GetCountBetweenValues(long lowValue, long highValue)
        {
            // Compute the sub-bucket-rounded values for low and high:
            var lowBucketIndex = GetBucketIndex(lowValue);
            var lowSubBucketIndex = GetSubBucketIndex(lowValue, lowBucketIndex);
            var valueAtlowValue = ValueFromIndex(lowBucketIndex, lowSubBucketIndex);
            var highBucketIndex = GetBucketIndex(highValue);
            var highSubBucketIndex = GetSubBucketIndex(highValue, highBucketIndex);
            var valueAtHighValue = ValueFromIndex(highBucketIndex, highSubBucketIndex);

            if ((lowBucketIndex >= BucketCount) || (highBucketIndex >= BucketCount))
                throw new ArgumentOutOfRangeException();

            var runningCount = 0L;
            for (var i = lowBucketIndex; i <= highBucketIndex; i++)
            {
                var j = (i == 0) ? 0 : (SubBucketCount / 2);
                for (; j < SubBucketCount; j++)
                {
                    var valueAtIndex = ValueFromIndex(i, j);
                    if (valueAtIndex > valueAtHighValue)
                        return runningCount;
                    if (valueAtIndex >= valueAtlowValue)
                        runningCount += GetCountAt(i, j);
                }
            }
            return runningCount;
        }

        /// <summary>
        /// Get the count of recorded values at a specific value
        /// </summary>
        /// <param name="value">The value for which to provide the recorded count</param>
        /// <returns>The total count of values recorded in the histogram at the given value (to within the histogram resolution at the value level).</returns>
        /// <exception cref="IndexOutOfRangeException">On parameters that are outside the tracked value range</exception>
        public long GetCountAtValue(long value)
        {
            var bucketIndex = GetBucketIndex(value);
            var subBucketIndex = GetSubBucketIndex(value, bucketIndex);
            // May throw ArrayIndexOutOfBoundsException:
            return GetCountAt(bucketIndex, subBucketIndex);
        }

        /// <summary>
        /// Provide a means of iterating through all recorded histogram values using the finest granularity steps supported by the underlying representation.
        /// The iteration steps through all non-zero recorded value counts, and terminates when all recorded histogram values are exhausted.
        /// </summary>
        /// <returns>An enumerator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="RecordedValuesEnumerator"/></returns>
        public IEnumerable<HistogramIterationValue> RecordedValues()
        {
            return new RecordedValuesEnumerable(this);
        }

        /// <summary>
        /// Provide a means of iterating through all histogram values using the finest granularity steps supported by the underlying representation.
        /// The iteration steps through all possible unit value levels, regardless of whether or not there were recorded values for that value level, and terminates when all recorded histogram values are exhausted.
        /// </summary>
        /// <returns>An enumerator of <see cref="HistogramIterationValue"/> through the histogram using a <see cref="RecordedValuesEnumerator"/></returns>
        public IEnumerable<HistogramIterationValue> AllValues()
        {
            return new AllValueEnumerable(this);
        }

        /// <summary>
        /// Get the capacity needed to encode this histogram into a <see cref="ByteBuffer"/>
        /// </summary>
        /// <returns>the capacity needed to encode this histogram into a <see cref="ByteBuffer"/></returns>
        public int GetNeededByteBufferCapacity()
        {
            return GetNeededByteBufferCapacity(CountsArrayLength);
        }

        //TODO Push Encoding and Decoding out into version specific types

        /// <summary>
        /// Encode this histogram into a <see cref="ByteBuffer"/>
        /// </summary>
        /// <param name="buffer">The buffer to encode into</param>
        /// <returns>The number of bytes written to the buffer</returns>
        public int EncodeIntoByteBuffer(ByteBuffer buffer)
        {
            lock (UpdateLock)
            {
                var maxValue = this.GetMaxValue();
                var relevantLength = GetLengthForNumberOfBuckets(GetBucketsNeededToCoverValue(maxValue));
                Debug.WriteLine("buffer.capacity() < getNeededByteBufferCapacity(relevantLength))");
                Debug.WriteLine($"  buffer.capacity() = {buffer.Capacity()}");
                Debug.WriteLine($"  relevantLength = {relevantLength}");
                Debug.WriteLine($"  getNeededByteBufferCapacity(relevantLength) = {GetNeededByteBufferCapacity(relevantLength)}");
                if (buffer.Capacity() < GetNeededByteBufferCapacity(relevantLength))
                {
                    throw new ArgumentOutOfRangeException("buffer does not have capacity for" + GetNeededByteBufferCapacity(relevantLength) + " bytes");
                }
                buffer.PutInt(Histogram.GetEncodingCookie(this));
                buffer.PutInt(NumberOfSignificantValueDigits);
                buffer.PutLong(LowestTrackableValue);
                buffer.PutLong(HighestTrackableValue);
                buffer.PutLong(TotalCount); // Needed because overflow situations may lead this to differ from counts totals

                Debug.WriteLine("MaxValue = {0}, Buckets needed = {1}, relevantLength = {2}", maxValue, GetBucketsNeededToCoverValue(maxValue), relevantLength);
                Debug.WriteLine("MaxValue = {0}, Buckets needed = {1}, relevantLength = {2}", maxValue, GetBucketsNeededToCoverValue(maxValue), relevantLength);

                Debug.WriteLine($"fillBufferFromCountsArray({buffer}, {relevantLength} * {WordSizeInBytes});");
                FillBufferFromCountsArray(buffer, relevantLength * WordSizeInBytes);

                return GetNeededByteBufferCapacity(relevantLength);
            }
        }

        /// <summary>
        /// Encode this histogram in compressed form into a byte array
        /// </summary>
        /// <param name="targetBuffer">The buffer to encode into</param>
        /// <returns>The number of bytes written to the buffer</returns>
        public long EncodeIntoCompressedByteBuffer(ByteBuffer targetBuffer)
        {
            var temp = ByteBuffer.Allocate(GetNeededByteBufferCapacity(CountsArrayLength));
            lock (UpdateLock)
            {
                var uncompressedLength = EncodeIntoByteBuffer(temp);

                targetBuffer.PutInt(Histogram.GetCompressedEncodingCookie(this));
                var contentLengthIdx = targetBuffer.Position;
                targetBuffer.PutInt(0); // Placeholder for compressed contents length *
                var headerSize = targetBuffer.Position;
                int compressedDataLength;
                using (var outputStream = targetBuffer.GetWriter())
                {
                    //using (var compressor = new DeflateStream(outputStream, compressionLevel))    //Make usable by Mono -LC
                    using (var compressor = new DeflateStream(outputStream, CompressionMode.Compress))
                    {
                        temp.WriteTo(compressor, 0, uncompressedLength);
                    }
                    compressedDataLength = outputStream.BytesWritten;
                }

                // *Go back and write the now known compressed data length
                targetBuffer.PutInt(contentLengthIdx, compressedDataLength); // Record the compressed length

                Debug.WriteLine($"COMPRESSING - Wrote {compressedDataLength + headerSize} bytes (header = {headerSize}), original size {uncompressedLength}");

                return compressedDataLength + headerSize;
            }
        }

        /// <summary>
        /// Determine if this histogram had any of it's value counts overflow.
        /// </summary>
        /// <returns><c>true</c> if this histogram has had a count value overflow, else <c>false</c>.</returns>
        /// <remarks>
        /// Since counts are kept in fixed integer form with potentially limited range (e.g. int and short), a specific value range count could potentially overflow, leading to an inaccurate and misleading histogram representation.
        /// This method accurately determines whether or not an overflow condition has happened in an <see cref="IntHistogram"/> or <see cref="ShortHistogram"/>.
        /// </remarks>
        public bool HasOverflowed()
        {
            // On overflow, the totalCount accumulated counter will (always) not match the total of counts
            var totalCounted = 0L;
            for (var i = 0; i < CountsArrayLength; i++)
            {
                totalCounted += GetCountAtIndex(i);
            }
            return (totalCounted != TotalCount);
        }

        /// <summary>
        /// Reestablish the internal notion of totalCount by recalculating it from recorded values.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Implementations of HistogramBase may maintain a separately tracked notion of totalCount, which is useful for concurrent modification tracking, overflow detection, and speed of execution in iteration.
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
        public void ReestablishTotalCount()
        {
            // On overflow, the totalCount accumulated counter will (always) not match the total of counts
            var totalCounted = 0L;
            for (var i = 0; i < CountsArrayLength; i++)
            {
                totalCounted += GetCountAtIndex(i);
            }
            TotalCount = totalCounted;
        }

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
            var that = other as HistogramBase;
            if (LowestTrackableValue != that?.LowestTrackableValue ||
                (HighestTrackableValue != that.HighestTrackableValue) ||
                (NumberOfSignificantValueDigits != that.NumberOfSignificantValueDigits))
            {
                return false;
            }
            if (CountsArrayLength != that.CountsArrayLength)
            {
                return false;
            }
            if (TotalCount != that.TotalCount)
            {
                return false;
            }

            for (var i = 0; i < CountsArrayLength; i++)
            {
                if (GetCountAtIndex(i) != that.GetCountAtIndex(i))
                {
                    Debug.WriteLine("Error at position {0}, this[{0}] = {1}, that[{0}] = {2}", i, GetCountAtIndex(i), that.GetCountAtIndex(i));
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Serves as the default hash function. 
        /// </summary>
        /// <returns>
        /// A hash code for the current object.
        /// </returns>
        public override int GetHashCode()
        {
            // From http://stackoverflow.com/questions/263400/what-is-the-best-algorithm-for-an-overridden-system-object-gethashcode/263416#263416
            unchecked // Overflow is fine, just wrap
            {
                var hash = 17;
                // Suitable nullity checks etc, of course :)
                hash = hash * 23 + HighestTrackableValue.GetHashCode();
                hash = hash * 23 + NumberOfSignificantValueDigits.GetHashCode();
                hash = hash * 23 + CountsArrayLength.GetHashCode();
                hash = hash * 23 + TotalCount.GetHashCode();

                for (var i = 0; i < CountsArrayLength; i++)
                {
                    hash = hash * 23 + GetCountAtIndex(i).GetHashCode();
                }

                return hash;
            }
        }

        /// <summary>
        /// Create a copy of this histogram, complete with data and everything.
        /// </summary>
        /// <returns>A distinct copy of this histogram.</returns>
        public abstract HistogramBase Copy();

        /// <summary>
        /// Get a copy of this histogram, corrected for coordinated omission.
        /// </summary>
        /// <param name="expectedIntervalBetweenValueSamples">If <paramref name="expectedIntervalBetweenValueSamples"/> is larger than 0, add auto-generated value records as appropriate if value is larger than <paramref name="expectedIntervalBetweenValueSamples"/></param>
        /// <returns>a copy of this histogram, corrected for coordinated omission.</returns>
        /// <remarks>
        /// To compensate for the loss of sampled values when a recorded value is larger than the expected interval between value samples, 
        /// the new histogram will include an auto-generated additional series of decreasingly-smaller(down to the <paramref name="expectedIntervalBetweenValueSamples"/>) 
        /// value records for each count found in the current histogram that is larger than the expectedIntervalBetweenValueSamples.
        /// <para>
        /// Note: This is a post-correction method, as opposed to the at-recording correction method provided by <seealso cref="RecordValueWithExpectedInterval"/>. 
        /// The two methods are mutually exclusive, and only one of the two should be be used on a given data set to correct for the same coordinated omission issue.
        /// </para>
        /// See notes in the description of the Histogram calls for an illustration of why this corrective behavior is important.
        /// </remarks>
        public abstract HistogramBase CopyCorrectedForCoordinatedOmission(long expectedIntervalBetweenValueSamples);

        /// <summary>
        /// Provide a (conservatively high) estimate of the Histogram's total footprint in bytes
        /// </summary>
        /// <returns>a (conservatively high) estimate of the Histogram's total footprint in bytes</returns>
        public virtual int GetEstimatedFootprintInBytes()
        {
            return (512 + (WordSizeInBytes * CountsArrayLength));
        }


        internal long GetCountAt(int bucketIndex, int subBucketIndex)
        {
            return GetCountAtIndex(CountsArrayIndex(bucketIndex, subBucketIndex));
        }

        internal long ValueFromIndex(int bucketIndex, int subBucketIndex)
        {
            return ((long)subBucketIndex) << (bucketIndex + _unitMagnitude);
        }

        

        /// <summary>
        /// Copies data from the provided buffer into the internal counts array.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <param name="length">The length of the buffer to read.</param>
        /// <param name="wordSizeInBytes">The word size in bytes.</param>
        internal int FillCountsFromBuffer(ByteBuffer buffer, int length, int wordSizeInBytes)
        {
            //var maxAllowableCountInHistigram =
            //    this.WordSizeInBytes == 2
            //        ? short.MaxValue
            //        : this.WordSizeInBytes == 4
            //            ? int.MaxValue 
            //            : long.MaxValue;

            var countsDecoder = Persistence.CountsDecoder.GetDecoderForWordSize(wordSizeInBytes);

            return countsDecoder.ReadCounts(buffer, length, (idx, count) =>
                                                     {
                                                         if (count > MaxAllowableCount)
                                                         {
                                                             throw new ArgumentException($"An encoded count ({count}) does not fit in the Histogram's ({WordSizeInBytes} bytes) was encountered in the source");
                                                         }
                                                         SetCountAtIndex(idx, count);
                                                     });
        }

        internal void EstablishInternalTackingValues(int lengthToCover)
        {

            ResetMaxValue(0);
            ResetMinNonZeroValue(long.MaxValue);
            int maxIndex = -1;
            int minNonZeroIndex = -1;
            long observedTotalCount = 0;
            for (int index = 0; index < lengthToCover; index++)
            {
                long countAtIndex;
                if ((countAtIndex = GetCountAtIndex(index)) > 0)
                {
                    observedTotalCount += countAtIndex;
                    maxIndex = index;
                    if ((minNonZeroIndex == -1) && (index != 0))
                    {
                        minNonZeroIndex = index;
                    }
                }
            }
            if (maxIndex >= 0)
            {
                UpdatedMaxValue(this.HighestEquivalentValue(ValueFromIndex(maxIndex)));
            }
            if (minNonZeroIndex >= 0)
            {
                UpdateMinNonZeroValue(ValueFromIndex(minNonZeroIndex));
            }
            TotalCount = observedTotalCount;
        }

        protected abstract long ReadWord(ByteBuffer buffer);
        //{
        //    return ((wordSizeInBytes == 2) ? sourceBuffer.getShort() :
        //                            ((wordSizeInBytes == 4) ? sourceBuffer.getInt() :
        //                                    sourceBuffer.getLong()
        //                            )
        //                    );
        //}
        /// <summary>
        /// Writes the data from the internal counts array into the buffer.
        /// </summary>
        /// <param name="buffer">The buffer to write to</param>
        /// <param name="length">The length to write.</param>
        protected abstract void FillBufferFromCountsArray(ByteBuffer buffer, int length);

        /// <summary>
        /// Gets the number of recorded values at a given index.
        /// </summary>
        /// <param name="index">The index to get the count for</param>
        /// <returns>The number of recorded values at the given index.</returns>
        protected abstract long GetCountAtIndex(int index);

        protected abstract void SetCountAtIndex(int index, long value);

        /// <summary>
        /// Increments the count at the given index. Will also increment the <see cref="TotalCount"/>.
        /// </summary>
        /// <param name="index">The index to increment the count at.</param>
        protected abstract void IncrementCountAtIndex(int index);

        /// <summary>
        /// Adds the specified amount to the count of the provided index. Also increments the <see cref="TotalCount"/> by the same amount.
        /// </summary>
        /// <param name="index">The index to increment.</param>
        /// <param name="addend">The amount to increment by.</param>
        protected abstract void AddToCountAtIndex(int index, long addend);


        /// <summary>
        /// Clears the counts of this implementation.
        /// </summary>
        protected abstract void ClearCounts();

        /**
     * Set internally tracked _maxValue to new value if new value is greater than current one.
     * May be overridden by subclasses for synchronization or atomicity purposes.
     * @param value new _maxValue to set
     */
        void UpdatedMaxValue(long value)
        {
            while (value > _maxValue)
            {
                ////TODO: Perform atomic CAS operation here -LC
                //maxValueUpdater.compareAndSet(this, _maxValue, value);
                _maxValue = value;
            }
        }

        /// <summary>
        /// Set internally tracked _minNonZeroValue to new value if new value is smaller than current one.
        /// May be overridden by subclasses for synchronization or atomicity purposes.
        /// </summary>
        /// <param name="value">new _minNonZeroValue to set</param>
        void UpdateMinNonZeroValue(long value)
        {
            while (value < _minNonZeroValue)
            {
                //TODO: Perform atomic CAS operation here -LC
                //minNonZeroValueUpdater.compareAndSet(this, _minNonZeroValue, value);
                _minNonZeroValue = value;
            }
        }
        private void ResetMinNonZeroValue(long minNonZeroValue)
        {
            this._minNonZeroValue = minNonZeroValue;
        }

        private void ResetMaxValue(long maxValue)
        {
            this._maxValue = maxValue;
        }

        private void RecordSingleValue(long value)
        {
            // Dissect the value into bucket and sub-bucket parts, and derive index into counts array:
            var bucketIndex = GetBucketIndex(value);
            var subBucketIndex = GetSubBucketIndex(value, bucketIndex);
            var countsIndex = CountsArrayIndex(bucketIndex, subBucketIndex);
            IncrementCountAtIndex(countsIndex);
        }

        private void RecordValueWithCountAndExpectedInterval(long value, long count, long expectedIntervalBetweenValueSamples)
        {
            RecordValueWithCount(value, count);
            if (expectedIntervalBetweenValueSamples <= 0)
                return;
            for (var missingValue = value - expectedIntervalBetweenValueSamples;
                 missingValue >= expectedIntervalBetweenValueSamples;
                 missingValue -= expectedIntervalBetweenValueSamples)
            {
                RecordValueWithCount(missingValue, count);
            }
        }

        private int GetNeededByteBufferCapacity(int relevantLength)
        {
            return (relevantLength * WordSizeInBytes) + 32;
        }

        private int GetBucketsNeededToCoverValue(long value)
        {
            long trackableValue = (SubBucketCount - 1) << _unitMagnitude;
            var bucketsNeeded = 1;
            while (trackableValue < value)
            {
                trackableValue <<= 1;
                bucketsNeeded++;
            }
            return bucketsNeeded;
        }

        private int GetLengthForNumberOfBuckets(int numberOfBuckets)
        {
            var lengthNeeded = (numberOfBuckets + 1) * (SubBucketCount / 2);
            return lengthNeeded;
        }

        private int CountsArrayIndex(int bucketIndex, int subBucketIndex)
        {
            Debug.Assert(subBucketIndex < SubBucketCount);
            Debug.Assert(bucketIndex == 0 || (subBucketIndex >= SubBucketHalfCount));
            // Calculate the index for the first entry in the bucket:
            // (The following is the equivalent of ((bucketIndex + 1) * subBucketHalfCount) ):
            var bucketBaseIndex = (bucketIndex + 1) << _subBucketHalfCountMagnitude;
            // Calculate the offset in the bucket:
            var offsetInBucket = subBucketIndex - SubBucketHalfCount;
            // The following is the equivalent of ((subBucketIndex  - subBucketHalfCount) + bucketBaseIndex;
            return bucketBaseIndex + offsetInBucket;
        }

        //Optimization. This simple method should be in-lined by the JIT compiler, allowing hot path `GetBucketIndex(long, long, int)` to become static. -LC
        private int GetBucketIndex(long value)
        {
            return GetBucketIndex(value, _subBucketMask, _bucketIndexOffset);
        }
        private static int GetBucketIndex(long value, long subBucketMask, int bucketIndexOffset)
        {
            var leadingZeros = NumberOfLeadingZeros(value | subBucketMask); // smallest power of 2 containing value
            return bucketIndexOffset - leadingZeros;
        }

        private int GetSubBucketIndex(long value, int bucketIndex)
        {
            return (int)(value >> (bucketIndex + _unitMagnitude));
        }

        private long ValueFromIndex(int index)
        {
            var bucketIndex = (index >> _subBucketHalfCountMagnitude) - 1;
            var subBucketIndex = (index & (SubBucketHalfCount - 1)) + SubBucketHalfCount;
            if (bucketIndex < 0)
            {
                subBucketIndex -= SubBucketHalfCount;
                bucketIndex = 0;
            }
            return ValueFromIndex(bucketIndex, subBucketIndex);
        }



        private static int NumberOfLeadingZeros(long value)
        {
            // Code from http://stackoverflow.com/questions/9543410/i-dont-think-numberofleadingzeroslong-i-in-long-java-is-based-floorlog2x/9543537#9543537

            // HD, Figure 5-6
            if (value == 0)
                return 64;
            var n = 1;
            // >>> in Java is a "unsigned bit shift", to do the same in C# we use >> (but it HAS to be an unsigned int)
            var x = (uint)(value >> 32);
            if (x == 0) { n += 32; x = (uint)value; }
            if (x >> 16 == 0) { n += 16; x <<= 16; }
            if (x >> 24 == 0) { n += 8; x <<= 8; }
            if (x >> 28 == 0) { n += 4; x <<= 4; }
            if (x >> 30 == 0) { n += 2; x <<= 2; }
            n -= (int)(x >> 31);
            return n;
        }


    }
}
