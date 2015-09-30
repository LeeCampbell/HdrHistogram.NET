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
using HdrHistogram.NET.Utilities;

namespace HdrHistogram.NET
{
    /// <summary>
    /// An internally synchronized High Dynamic Range (HDR) Histogram using a <c>long</c> count type.
    /// </summary>
    public class SynchronizedHistogram : HistogramBase
    {
        private readonly long[] _counts;
        private long _totalCount;

        // We try to cache the LongBuffer used in output cases, as repeated output from the same histogram using the same buffer is likely:
        private WrappedBuffer<long> _cachedDstLongBuffer = null;
        private ByteBuffer _cachedDstByteBuffer = null;
        private int _cachedDstByteBufferPosition = 0;


        /// <summary>
        /// Construct a SynchronizedHistogram given the Highest value to be tracked and a number of significant decimal digits. 
        /// The histogram will be constructed to implicitly track(distinguish from 0) values as low as 1.
        /// </summary>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. Must be a positive integer that is &gt;= 2.</param>
        /// <param name="numberOfSignificantValueDigits">
        /// The number of significant decimal digits to which the histogram will maintain value resolution and separation.
        /// Must be a non-negative integer between 0 and 5.
        /// </param>
        public SynchronizedHistogram(long highestTrackableValue, int numberOfSignificantValueDigits)
            : this(1, highestTrackableValue, numberOfSignificantValueDigits)
        {
        }

        /// <summary>
        /// Construct a SynchronizedHistogram given the Lowest and Highest values to be tracked and a number of significant decimal digits.
        /// Providing a lowestTrackableValue is useful is situations where the units used for the histogram's values are much smaller that the minimal accuracy required. 
        /// E.g. when tracking time values stated in nanosecond units, where the minimal accuracy required is a microsecond, the proper value for lowestTrackableValue would be 1000.
        /// </summary>
        /// <param name="lowestTrackableValue">The lowest value that can be tracked (distinguished from 0) by the histogram.
        /// Must be a positive integer that is &gt;= 1. May be internally rounded down to nearest power of 2.</param>
        /// <param name="highestTrackableValue">The highest value to be tracked by the histogram. 
        /// Must be a positive integer that is &gt;= (2 * lowestTrackableValue).</param>
        /// <param name="numberOfSignificantValueDigits">The number of significant decimal digits to which the histogram will maintain value resolution and separation. 
        /// Must be a non-negative integer between 0 and 5.
        /// </param>
        public SynchronizedHistogram(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
            : base(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits)
        {
            _counts = new long[CountsArrayLength];
        }

        public override long TotalCount
        {
            get { return _totalCount; }
            protected set
            {
                lock (UpdateLock)
                {
                    _totalCount = value;
                }
            }
        }

        public override void Add(HistogramBase other)
        {
            // Synchronize add(). Avoid deadlocks by synchronizing in order of construction identity count.
            if (Identity < other.Identity)
            {
                lock (UpdateLock)
                {
                    lock (other)
                    {
                        base.Add(other);
                    }
                }
            }
            else
            {
                lock (other)
                {
                    lock (UpdateLock)
                    {
                        base.Add(other);
                    }
                }
            }
        }

        public override HistogramBase Copy()
        {
            SynchronizedHistogram copy = new SynchronizedHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
            copy.Add(this);
            return copy;
        }

        public override HistogramBase CopyCorrectedForCoordinatedOmission(long expectedIntervalBetweenValueSamples)
        {
            SynchronizedHistogram toHistogram = new SynchronizedHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
            toHistogram.AddWhileCorrectingForCoordinatedOmission(this, expectedIntervalBetweenValueSamples);
            return toHistogram;
        }

        public override int GetEstimatedFootprintInBytes()
        {
            return (512 + (8 * _counts.Length));
        }

        /// <summary>
        /// Construct a new histogram by decoding it from a ByteBuffer.
        /// </summary>
        /// <param name="buffer">The buffer to decode from</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <returns>The newly constructed histogram</returns>
        public static SynchronizedHistogram DecodeFromByteBuffer(ByteBuffer buffer, long minBarForHighestTrackableValue)
        {
            return (SynchronizedHistogram)DecodeFromByteBuffer(buffer, typeof(SynchronizedHistogram), minBarForHighestTrackableValue);
        }

        /// <summary>
        /// Construct a new histogram by decoding it from a compressed form in a ByteBuffer.
        /// </summary>
        /// <param name="buffer">The buffer to encode into</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <returns>The newly constructed histogram</returns>
        public static SynchronizedHistogram DecodeFromCompressedByteBuffer(ByteBuffer buffer, long minBarForHighestTrackableValue)
        {
            return (SynchronizedHistogram)DecodeFromCompressedByteBuffer(buffer, typeof(SynchronizedHistogram), minBarForHighestTrackableValue);
        }


        protected override int WordSizeInBytes => 8;

        protected override long GetCountAtIndex(int index)
        {
            return _counts[index];
        }

        protected override void IncrementCountAtIndex(int index)
        {
            lock (UpdateLock)
            {
                _counts[index]++;
            }
        }

        protected override void AddToCountAtIndex(int index, long value)
        {
            lock (UpdateLock)
            {
                _counts[index] += value;
            }
        }

        protected override void ClearCounts()
        {
            lock (UpdateLock)
            {
                Array.Clear(_counts, 0, _counts.Length);
                TotalCount = 0;
            }
        }

        protected override void IncrementTotalCount()
        {
            lock (UpdateLock)
            {
                _totalCount++;
            }
        }

        protected override void AddToTotalCount(long value)
        {
            lock (UpdateLock)
            {
                _totalCount += value;
            }
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
