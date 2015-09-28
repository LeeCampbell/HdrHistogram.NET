/*
 * Written by Matt Warren, and released to the public domain,
 * as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 *
 * This is a .NET port of the original Java version, which was written by
 * Gil Tene as described in
 * https://github.com/HdrHistogram/HdrHistogram
 */

using System.Threading;
using HdrHistogram.NET.Iteration;
using HdrHistogram.NET.Utilities;

namespace HdrHistogram.NET
{
    public abstract class AbstractHistogramBase
    {
        
        // "Cold" accessed fields. Not used in the recording code path:
        

        internal long highestTrackableValue;
        internal long lowestTrackableValue;
        internal int numberOfSignificantValueDigits;

        internal int bucketCount;
        internal int subBucketCount;
        internal int countsArrayLength;
        internal int wordSizeInBytes;

        

        internal PercentileIterator percentileIterator;
        internal RecordedValuesIterator recordedValuesIterator;

        internal ByteBuffer intermediateUncompressedByteBuffer = null;

        protected object updateLock = new object();
    }
}