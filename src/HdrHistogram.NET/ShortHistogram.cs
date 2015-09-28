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
using ShortBuffer = HdrHistogram.NET.Utilities.WrappedBuffer<short>;

namespace HdrHistogram.NET
{
    /**
     * <h3>A High Dynamic Range (HDR) Histogram using a <b><code>short</code></b> count type </h3>
     * <p>
     * See package description for {@link org.HdrHistogram} for details.
     */
    public class ShortHistogram : AbstractHistogram 
    {
        long totalCount;
        readonly short[] counts;

        protected override int WordSizeInBytes => 2;

        protected override long GetCountAtIndex(int index) 
        {
            return counts[index];
        }

        protected override void IncrementCountAtIndex(int index) 
        {
            counts[index]++;
        }

        protected override void AddToCountAtIndex(int index, long value) 
        {
            counts[index] += (short)value;
        }

        protected override void ClearCounts() 
        {
            Array.Clear(counts, 0, counts.Length);
            totalCount = 0;
        }

        public override /*ShortHistogram*/ AbstractHistogram Copy() 
        {
          ShortHistogram copy = new ShortHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
          copy.Add(this);
          return copy;
        }

        public override /*ShortHistogram*/ AbstractHistogram CopyCorrectedForCoordinatedOmission(long expectedIntervalBetweenValueSamples) 
        {
            ShortHistogram toHistogram = new ShortHistogram(LowestTrackableValue, HighestTrackableValue, NumberOfSignificantValueDigits);
            toHistogram.AddWhileCorrectingForCoordinatedOmission(this, expectedIntervalBetweenValueSamples);
            return toHistogram;
        }

        public override long GetTotalCount() 
        {
            return totalCount;
        }

        protected override void SetTotalCount(long totalCount) 
        {
            this.totalCount = totalCount;
        }

        protected override void IncrementTotalCount() 
        {
            totalCount++;
        }

        protected override void AddToTotalCount(long value) 
        {
            totalCount += value;
        }

        protected override int _getEstimatedFootprintInBytes()
        {
            return (512 + (2 * counts.Length));
        }

        /**
         * Construct a ShortHistogram given the Highest value to be tracked and a number of significant decimal digits. The
         * histogram will be constructed to implicitly track (distinguish from 0) values as low as 1.
         *
         * @param highestTrackableValue The highest value to be tracked by the histogram. Must be a positive
         *                              integer that is {@literal >=} 2.
         * @param numberOfSignificantValueDigits The number of significant decimal digits to which the histogram will
         *                                       maintain value resolution and separation. Must be a non-negative
         *                                       integer between 0 and 5.
         */
        public ShortHistogram(long highestTrackableValue, int numberOfSignificantValueDigits) 
            : this(1, highestTrackableValue, numberOfSignificantValueDigits)
        {
        }

        /**
         * Construct a ShortHistogram given the Lowest and Highest values to be tracked and a number of significant
         * decimal digits. Providing a lowestTrackableValue is useful is situations where the units used
         * for the histogram's values are much smaller that the minimal accuracy required. E.g. when tracking
         * time values stated in nanosecond units, where the minimal accuracy required is a microsecond, the
         * proper value for lowestTrackableValue would be 1000.
         *
         * @param lowestTrackableValue The lowest value that can be tracked (distinguished from 0) by the histogram.
         *                             Must be a positive integer that is {@literal >=} 1. May be internally rounded down to nearest
         *                             power of 2.
         * @param highestTrackableValue The highest value to be tracked by the histogram. Must be a positive
         *                              integer that is {@literal >=} (2 * lowestTrackableValue).
         * @param numberOfSignificantValueDigits The number of significant decimal digits to which the histogram will
         *                                       maintain value resolution and separation. Must be a non-negative
         *                                       integer between 0 and 5.
         */
        public ShortHistogram(long lowestTrackableValue, long highestTrackableValue, int numberOfSignificantValueDigits)
            : base(lowestTrackableValue, highestTrackableValue, numberOfSignificantValueDigits)
        {
            counts = new short[CountsArrayLength];
        }

        /**
         * Construct a new histogram by decoding it from a ByteBuffer.
         * @param buffer The buffer to decode from
         * @param minBarForHighestTrackableValue Force highestTrackableValue to be set at least this high
         * @return The newly constructed histogram
         */
        public static ShortHistogram decodeFromByteBuffer(ByteBuffer buffer,
                                                          long minBarForHighestTrackableValue) 
        {
            return (ShortHistogram) DecodeFromByteBuffer(buffer, typeof(ShortHistogram), minBarForHighestTrackableValue);
        }

        /**
         * Construct a new histogram by decoding it from a compressed form in a ByteBuffer.
         * @param buffer The buffer to encode into
         * @param minBarForHighestTrackableValue Force highestTrackableValue to be set at least this high
         * @return The newly constructed histogram
         * @throws DataFormatException on error parsing/decompressing the buffer
         */
        public static ShortHistogram decodeFromCompressedByteBuffer(ByteBuffer buffer,
                                                                    long minBarForHighestTrackableValue) //throws DataFormatException 
        {
            return (ShortHistogram)DecodeFromCompressedByteBuffer(buffer, typeof(ShortHistogram), minBarForHighestTrackableValue);
        }

        //private void readObject(ObjectInputStream o)
        //        throws IOException, ClassNotFoundException {
        //    o.defaultReadObject();
        //}

        protected override void FillCountsArrayFromBuffer(ByteBuffer buffer, int length) 
        {
            lock (UpdateLock)
            {
                buffer.asShortBuffer().get(counts, 0, length);
            }
        }

        // We try to cache the LongBuffer used in output cases, as repeated
        // output form the same histogram using the same buffer is likely:
        private ShortBuffer cachedDstShortBuffer = null;
        private ByteBuffer cachedDstByteBuffer = null;
        private int cachedDstByteBufferPosition = 0;

        protected override void FillBufferFromCountsArray(ByteBuffer buffer, int length)
        {
            lock (UpdateLock)
            {
                if ((cachedDstShortBuffer == null) ||
                    (buffer != cachedDstByteBuffer) ||
                    (buffer.position() != cachedDstByteBufferPosition))
                {
                    cachedDstByteBuffer = buffer;
                    cachedDstByteBufferPosition = buffer.position();
                    cachedDstShortBuffer = buffer.asShortBuffer();
                }
                cachedDstShortBuffer.rewind();
                cachedDstShortBuffer.put(counts, 0, length);
            }
        }
    }
}
