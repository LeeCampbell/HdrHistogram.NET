/*
 * Written by Matt Warren, and released to the public domain,
 * as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 *
 * This is a .NET port of the original Java version, which was written by
 * Gil Tene as described in
 * https://github.com/HdrHistogram/HdrHistogram
 */

using HdrHistogram.NET.Utilities;
using NUnit.Framework;
using System;
using Assert = HdrHistogram.NET.Test.AssertEx;

namespace HdrHistogram.NET.Test
{
    /**
     * JUnit test for {@link org.HdrHistogram.Histogram}
     */
    public class HistogramEncodingTest 
    {
        static readonly long highestTrackableValue = 3600L * 1000 * 1000; // e.g. for 1 hr in usec units
        static readonly int numberOfSignificantValueDigits = 3;
        // static readonly long testValueLevel = 12340;
        static readonly long testValueLevel = 4;

        [Test]
        public void testHistogramEncoding() //throws Exception 
        {
            ShortHistogram shortHistogram = new ShortHistogram(highestTrackableValue, 3);
            IntHistogram intHistogram = new IntHistogram(highestTrackableValue, 3);
            Histogram histogram = new Histogram(highestTrackableValue, 3);
            SynchronizedHistogram synchronizedHistogram = new SynchronizedHistogram(highestTrackableValue, 3);

            for (int i = 0; i < 10000; i++) {
                shortHistogram.RecordValueWithExpectedInterval(1000 /* 1 msec */, 10000 /* 10 msec expected interval */);
                intHistogram.RecordValueWithExpectedInterval(2000 /* 1 msec */, 10000 /* 10 msec expected interval */);
                histogram.RecordValueWithExpectedInterval(3000 /* 1 msec */, 10000 /* 10 msec expected interval */);
                synchronizedHistogram.RecordValueWithExpectedInterval(5000 /* 1 msec */, 10000 /* 10 msec expected interval */);
            }

            Console.WriteLine("\n\nTesting encoding of a ShortHistogram:");
            ByteBuffer targetBuffer = ByteBuffer.allocate(shortHistogram.GetNeededByteBufferCapacity());
            shortHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position {1}), shortHistogram size = {2}",
            //                targetBuffer.capacity(), targetBuffer.position(), shortHistogram.getTotalCount());
            targetBuffer.rewind();

            ShortHistogram shortHistogram2 = ShortHistogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.assertEquals(shortHistogram, shortHistogram2);

            ByteBuffer targetCompressedBuffer = ByteBuffer.allocate(shortHistogram.GetNeededByteBufferCapacity());
            shortHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.rewind();

            ShortHistogram shortHistogram3 = ShortHistogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.assertEquals(shortHistogram, shortHistogram3);

            Console.WriteLine("\n\nTesting encoding of a IntHistogram:");
            targetBuffer = ByteBuffer.allocate(intHistogram.GetNeededByteBufferCapacity());
            intHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position = {1}), intHistogram size = {2}", 
            //                targetBuffer.capacity(), targetBuffer.position(), intHistogram.getTotalCount());
            targetBuffer.rewind();

            IntHistogram intHistogram2 = IntHistogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.assertEquals(intHistogram, intHistogram2);

            targetCompressedBuffer = ByteBuffer.allocate(intHistogram.GetNeededByteBufferCapacity());
            intHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.rewind();

            IntHistogram intHistogram3 = IntHistogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.assertEquals(intHistogram, intHistogram3);

            Console.WriteLine("\n\nTesting encoding of a Histogram (long):");
            targetBuffer = ByteBuffer.allocate(histogram.GetNeededByteBufferCapacity());
            histogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position = {1}), histogram size = {2}",
            //                targetBuffer.capacity(), targetBuffer.position(), histogram.getTotalCount());
            targetBuffer.rewind();

            Histogram histogram2 = Histogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.assertEquals(histogram, histogram2);

            targetCompressedBuffer = ByteBuffer.allocate(histogram.GetNeededByteBufferCapacity());
            histogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.rewind();

            Histogram histogram3 = Histogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.assertEquals(histogram, histogram3);

            targetBuffer.rewind();


            targetCompressedBuffer.rewind();

            Console.WriteLine("\n\nTesting encoding of a SynchronizedHistogram:");
            targetBuffer = ByteBuffer.allocate(synchronizedHistogram.GetNeededByteBufferCapacity());
            synchronizedHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position {1}), synchronizedHistogram size = {2}",
            //                targetBuffer.capacity(), targetBuffer.position(), synchronizedHistogram.getTotalCount());
            targetBuffer.rewind();

            SynchronizedHistogram synchronizedHistogram2 = SynchronizedHistogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.assertEquals(synchronizedHistogram, synchronizedHistogram2);

            targetCompressedBuffer = ByteBuffer.allocate(synchronizedHistogram.GetNeededByteBufferCapacity());
            synchronizedHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.rewind();

            SynchronizedHistogram synchronizedHistogram3 = SynchronizedHistogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.assertEquals(synchronizedHistogram, synchronizedHistogram3);
        }

        [Test]
        public void testHistogramEncodingFullRangeOfValues() //throws Exception 
        {
            Histogram histogram = new Histogram(highestTrackableValue, 3);

            for (long i = 0; i < highestTrackableValue; i += 100) 
            {
                histogram.RecordValue(i);
            }
            histogram.RecordValue(highestTrackableValue);

            Console.WriteLine("\n\nTesting encoding of a Histogram (long):");
            var targetBuffer = ByteBuffer.allocate(histogram.GetNeededByteBufferCapacity());
            histogram.EncodeIntoByteBuffer(targetBuffer);
            targetBuffer.rewind();

            Histogram histogram2 = Histogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.assertEquals(histogram, histogram2);

            var targetCompressedBuffer = ByteBuffer.allocate(histogram.GetNeededByteBufferCapacity());
            histogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.rewind();

            Histogram histogram3 = Histogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.assertEquals(histogram, histogram3);

            Console.WriteLine();
            histogram3.OutputPercentileDistribution(Console.Out);
        }
    }
}
