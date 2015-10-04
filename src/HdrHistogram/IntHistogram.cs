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
using IntBuffer = HdrHistogram.Utilities.WrappedBuffer<int>;

namespace HdrHistogram
{
    /// <summary>
    /// A High Dynamic Range (HDR) Histogram using an <c>int</c> count type.
    /// </summary>
    public class IntHistogram : HistogramBase
    {
        private readonly int[] _counts;

        // We try to cache the LongBuffer used in output cases, as repeated output from the same histogram using the same buffer is likely.
        private IntBuffer _cachedDstIntBuffer;
        private ByteBuffer _cachedDstByteBuffer;
        private int _cachedDstByteBufferPosition;

        /// <summary>
        /// Construct a IntHistogram given the Highest value to be tracked and a number of significant decimal digits. 
        /// The histogram will be constructed to implicitly track(distinguish from 0) values as low as 1. 
        /// </summary>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= 2.</param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation.Must be a non-negative integer between 0 and 5.</param>
        public IntHistogram(long highestTrackableValue, int numberOfSignificantValueDigits)
            : this(1, highestTrackableValue, numberOfSignificantValueDigits)
        {
        }
        
        /// <summary>
        /// Construct a IntHistogram given the Lowest and Highest values to be tracked and a number of significant decimal digits.
        /// Providing a lowestTrackableValue is useful is situations where the units used for the histogram's values are much smaller that the minimal accuracy required.
        /// E.g. when tracking time values stated in nanosecond units, where the minimal accuracy required is a microsecond, the proper value for lowestTrackableValue would be 1000.
        /// </summary>
        /// <param name="lowestTrackableValue">The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1. May be internally rounded down to nearest power of 2.</param>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= (2 * lowestTrackableValue).</param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation.Must be a non-negative integer between 0 and 5.</param>
        public IntHistogram(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
            : base(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits)
        {
            _counts = new int[CountsArrayLength];
        }


        public override long TotalCount { get; protected set; }

        protected override int WordSizeInBytes => 4;

        public override HistogramBase Copy()
        {
            var copy = new IntHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
            copy.Add(this);
            return copy;
        }

        public override HistogramBase CopyCorrectedForCoordinatedOmission(long expectedIntervalBetweenValueSamples)
        {
            var toHistogram = new IntHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
            toHistogram.AddWhileCorrectingForCoordinatedOmission(this, expectedIntervalBetweenValueSamples);
            return toHistogram;
        }

        /// <summary>
        /// Construct a new histogram by decoding it from a ByteBuffer.
        /// </summary>
        /// <param name="buffer">The buffer to decode from</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <returns>The newly constructed histogram</returns>
        public static IntHistogram DecodeFromByteBuffer(ByteBuffer buffer, long minBarForHighestTrackableValue)
        {
            return DecodeFromByteBuffer<IntHistogram>(buffer, minBarForHighestTrackableValue);
        }

        /// <summary>
        /// Construct a new histogram by decoding it from a compressed form in a ByteBuffer.
        /// </summary>
        /// <param name="buffer">The buffer to encode into</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <returns>The newly constructed histogram</returns>
        public static IntHistogram DecodeFromCompressedByteBuffer(ByteBuffer buffer, long minBarForHighestTrackableValue)
        {
            return (IntHistogram)DecodeFromCompressedByteBuffer(buffer, typeof(IntHistogram), minBarForHighestTrackableValue);
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
            _counts[index] += (int)value;
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
                buffer.asIntBuffer().get(_counts, 0, length);
            }
        }

        protected override void FillBufferFromCountsArray(ByteBuffer buffer, int length)
        {
            lock (UpdateLock)
            {
                if ((_cachedDstIntBuffer == null) ||
                    (buffer != _cachedDstByteBuffer) ||
                    (buffer.position() != _cachedDstByteBufferPosition))
                {
                    _cachedDstByteBuffer = buffer;
                    _cachedDstByteBufferPosition = buffer.position();
                    _cachedDstIntBuffer = buffer.asIntBuffer();
                }
                _cachedDstIntBuffer.rewind();
                _cachedDstIntBuffer.put(_counts, 0, length);
            }
        }
    }
}
