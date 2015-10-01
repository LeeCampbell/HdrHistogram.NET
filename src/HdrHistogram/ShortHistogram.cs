﻿/*
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
using ShortBuffer = HdrHistogram.Utilities.WrappedBuffer<short>;

namespace HdrHistogram
{
    /**
     * <h3>A High Dynamic Range (HDR) Histogram using a <b><code>short</code></b> count type </h3>
     * <p>
     * See package description for {@link org.HdrHistogram} for details.
     */
    public sealed class ShortHistogram : HistogramBase 
    {
        private readonly short[] _counts;

        // We try to cache the LongBuffer used in output cases, as repeated output from the same histogram using the same buffer is likely.
        private ShortBuffer _cachedDstShortBuffer;
        private ByteBuffer _cachedDstByteBuffer;
        private int _cachedDstByteBufferPosition;
        
        /// <summary>
        /// Construct a ShortHistogram given the Highest value to be tracked and a number of significant decimal digits. 
        /// The histogram will be constructed to implicitly track (distinguish from 0) values as low as 1. 
        /// </summary>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= 2.</param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation.Must be a non-negative integer between 0 and 5.</param>
        public ShortHistogram(long highestTrackableValue, int numberOfSignificantValueDigits)
            : this(1, highestTrackableValue, numberOfSignificantValueDigits)
        {
        }
        
        /// <summary>
        /// Construct a ShortHistogram given the Lowest and Highest values to be tracked and a number of significant
        /// decimal digits.Providing a lowestTrackableValue is useful is situations where the units used
        /// for the histogram's values are much smaller that the minimal accuracy required. E.g. when tracking
        /// time values stated in nanosecond units, where the minimal accuracy required is a microsecond, the
        /// proper value for lowestTrackableValue would be 1000.
        /// </summary>
        /// <param name="lowestTrackableValue">The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1. May be internally rounded down to nearest power of 2.</param>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= (2 * lowestTrackableValue).</param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation.
        /// Must be a non-negative integer between 0 and 5.</param>
        public ShortHistogram(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
            : base(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits)
        {
            _counts = new short[CountsArrayLength];
        }

        public override long TotalCount { get; protected set; }

        protected override int WordSizeInBytes => 2;
        
        public override HistogramBase Copy() 
        {
          var copy = new ShortHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
          copy.Add(this);
          return copy;
        }

        public override HistogramBase CopyCorrectedForCoordinatedOmission(long expectedIntervalBetweenValueSamples) 
        {
            var toHistogram = new ShortHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
            toHistogram.AddWhileCorrectingForCoordinatedOmission(this, expectedIntervalBetweenValueSamples);
            return toHistogram;
        }

        public override int GetEstimatedFootprintInBytes()
        {
            return (512 + (2 * _counts.Length));
        }
        
        /// <summary>
        /// Construct a new histogram by decoding it from a <see cref="ByteBuffer"/>.
        /// </summary>
        /// <param name="buffer">The buffer to decode from</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <returns>The newly constructed histogram</returns>
        public static ShortHistogram DecodeFromByteBuffer(ByteBuffer buffer,
                                                          long minBarForHighestTrackableValue)
        {
            return (ShortHistogram)DecodeFromByteBuffer(buffer, typeof(ShortHistogram), minBarForHighestTrackableValue);
        }

        /// <summary>
        /// Construct a new histogram by decoding it from a compressed form in a <see cref="ByteBuffer"/>.
        /// </summary>
        /// <param name="buffer">The buffer to encode into</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <returns>The newly constructed histogram</returns>
        public static ShortHistogram DecodeFromCompressedByteBuffer(ByteBuffer buffer, long minBarForHighestTrackableValue)
        {
            return (ShortHistogram)DecodeFromCompressedByteBuffer(buffer, typeof(ShortHistogram), minBarForHighestTrackableValue);
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
            _counts[index] += (short)value;
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
                buffer.asShortBuffer().get(_counts, 0, length);
            }
        }
        
        protected override void FillBufferFromCountsArray(ByteBuffer buffer, int length)
        {
            lock (UpdateLock)
            {
                if ((_cachedDstShortBuffer == null) ||
                    (buffer != _cachedDstByteBuffer) ||
                    (buffer.position() != _cachedDstByteBufferPosition))
                {
                    _cachedDstByteBuffer = buffer;
                    _cachedDstByteBufferPosition = buffer.position();
                    _cachedDstShortBuffer = buffer.asShortBuffer();
                }
                _cachedDstShortBuffer.rewind();
                _cachedDstShortBuffer.put(_counts, 0, length);
            }
        }
    }
}
