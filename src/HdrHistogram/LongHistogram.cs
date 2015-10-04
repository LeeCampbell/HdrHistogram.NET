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
using HdrHistogram.Utilities;
using LongBuffer = HdrHistogram.Utilities.WrappedBuffer<long>;

namespace HdrHistogram
{
    /// <summary>
    /// A High Dynamic Range (HDR) Histogram
    /// </summary>
    /// <remarks>
    /// Histogram supports the recording and analyzing sampled data value counts across a configurable integer value
    /// range with configurable value precision within the range.
    /// Value precision is expressed as the number of significant digits in the value recording, and provides control 
    /// over value quantization behavior across the value range and the subsequent value resolution at any given level.
    /// <para>
    /// For example, a Histogram could be configured to track the counts of observed integer values between 0 and
    /// 3,600,000,000 while maintaining a value precision of 3 significant digits across that range.
    /// Value quantization within the range will thus be no larger than 1/1,000th (or 0.1%) of any value.
    /// This example Histogram could be used to track and analyze the counts of observed response times ranging between
    ///  1 microsecond and 1 hour in magnitude, while maintaining a value resolution of 1 microsecond up to 1 millisecond, 
    /// a resolution of 1 millisecond(or better) up to one second, and a resolution of 1 second (or better) up to 1,000 seconds.
    /// At it's maximum tracked value(1 hour), it would still maintain a resolution of 3.6 seconds (or better).
    /// </para>
    /// Histogram tracks value counts in <c>long</c> fields.
    /// Smaller field types are available in the <see cref="IntHistogram"/> and <see cref="ShortHistogram"/> implementations of <see cref="HistogramBase"/>.
    /// </remarks>
    public class LongHistogram : HistogramBase
    {
        private readonly long[] _counts;

        // We try to cache the LongBuffer used in output cases, as repeated output from the same histogram using the same buffer is likely.
        private LongBuffer _cachedDstLongBuffer;
        private ByteBuffer _cachedDstByteBuffer;
        private int _cachedDstByteBufferPosition;

        /// <summary>
        /// Construct a Histogram given the Highest value to be tracked and a number of significant decimal digits. 
        /// The histogram will be constructed to implicitly track(distinguish from 0) values as low as 1.
        /// </summary>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= 2.</param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation.
        /// Must be a non-negative integer between 0 and 5.
        /// </param>
        public LongHistogram(long highestTrackableValue, int numberOfSignificantValueDigits)
            : this(1, highestTrackableValue, numberOfSignificantValueDigits)
        {
        }

        /// <summary>
        /// Construct a Histogram given the Lowest and Highest values to be tracked and a number of significant decimal digits.
        /// Providing a lowestTrackableValue is useful is situations where the units used for the histogram's values are much smaller that the minimal accuracy required. 
        /// E.g. when tracking time values stated in nanosecond units, where the minimal accuracy required is a microsecond, the proper value for lowestTrackableValue would be 1000.
        /// </summary>
        /// <param name="lowestTrackableValue">The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1. May be internally rounded down to nearest power of 2.</param>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= (2 * lowestTrackableValue).</param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation.
        /// Must be a non-negative integer between 0 and 5.
        /// </param>
        public LongHistogram(long lowestTrackableValue, long highestTrackableValue,
                         int numberOfSignificantValueDigits)
            : base(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits)
        {
            _counts = new long[CountsArrayLength];
        }


        public override long TotalCount { get; protected set; }

        protected override int WordSizeInBytes => 8;

        public override HistogramBase Copy()
        {
            var copy = new LongHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
            copy.Add(this);
            return copy;
        }

        public override HistogramBase CopyCorrectedForCoordinatedOmission(long expectedIntervalBetweenValueSamples)
        {
            var toHistogram = new LongHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
            toHistogram.AddWhileCorrectingForCoordinatedOmission(this, expectedIntervalBetweenValueSamples);
            return toHistogram;
        }

        /// <summary>
        /// Construct a new histogram by decoding it from a ByteBuffer.
        /// </summary>
        /// <param name="buffer">The buffer to decode from</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <returns>The newly constructed histogram</returns>
        public static LongHistogram DecodeFromByteBuffer(ByteBuffer buffer, long minBarForHighestTrackableValue)
        {
            return DecodeFromByteBuffer<LongHistogram>(buffer, minBarForHighestTrackableValue);
        }

        /// <summary>
        /// Construct a new histogram by decoding it from a compressed form in a ByteBuffer.
        /// </summary>
        /// <param name="buffer">The buffer to encode into</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <returns>The newly constructed histogram</returns>
        public static LongHistogram DecodeFromCompressedByteBuffer(ByteBuffer buffer, long minBarForHighestTrackableValue)
        {
            return (LongHistogram)DecodeFromCompressedByteBuffer(buffer, typeof(LongHistogram), minBarForHighestTrackableValue);
        }


        protected override long GetCountAtIndex(int index)
        {
            return _counts[index];
        }

        protected override void IncrementCountAtIndex(int index)
        {
            _counts[index]++;
        }

        protected override void AddToCountAtIndex(int index, long value)
        {
            _counts[index] += value;
        }

        protected override void ClearCounts()
        {
            Array.Clear(_counts, 0, _counts.Length);
            TotalCount = 0;
        }

        protected override void IncrementTotalCount()
        {
            TotalCount++;
        }

        protected override void AddToTotalCount(long value)
        {
            TotalCount += value;
        }

        protected override void FillCountsArrayFromBuffer(ByteBuffer buffer, int length)
        {
            lock (UpdateLock)
            {
                buffer.asLongBuffer().get(_counts, 0, length);
            }
        }

        protected override void FillBufferFromCountsArray(ByteBuffer buffer, int length)
        {
            lock (UpdateLock)
            {
                if ((_cachedDstLongBuffer == null) ||
                    (buffer != _cachedDstByteBuffer) ||
                    (buffer.position() != _cachedDstByteBufferPosition))
                {
                    _cachedDstByteBuffer = buffer;
                    _cachedDstByteBufferPosition = buffer.position();
                    _cachedDstLongBuffer = buffer.asLongBuffer();
                }
                _cachedDstLongBuffer.rewind();
                _cachedDstLongBuffer.put(_counts, 0, length);
            }
        }
    }
}
