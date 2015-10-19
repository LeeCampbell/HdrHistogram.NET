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
using NUnit.Framework;

namespace HdrHistogram.UnitTests
{
    public class HistogramEncodingTest 
    {
        private const long HighestTrackableValue = 3600L*1000*1000; // e.g. for 1 hr in usec units

        [Test]
        public void TestHistogramEncoding()
        {
            var shortHistogram = new ShortHistogram(HighestTrackableValue, 3);
            var intHistogram = new IntHistogram(HighestTrackableValue, 3);
            var longHistogram = new LongHistogram(HighestTrackableValue, 3);
            var synchronizedHistogram = new SynchronizedHistogram(HighestTrackableValue, 3);

            for (var i = 0; i < 10000; i++) {
                shortHistogram.RecordValueWithExpectedInterval(1000 /* 1 msec */, 10000 /* 10 msec expected interval */);
                intHistogram.RecordValueWithExpectedInterval(2000 /* 1 msec */, 10000 /* 10 msec expected interval */);
                longHistogram.RecordValueWithExpectedInterval(3000 /* 1 msec */, 10000 /* 10 msec expected interval */);
                synchronizedHistogram.RecordValueWithExpectedInterval(5000 /* 1 msec */, 10000 /* 10 msec expected interval */);
            }

            Console.WriteLine("\n\nTesting encoding of a ShortHistogram:");
            var targetBuffer = ByteBuffer.Allocate(shortHistogram.GetNeededByteBufferCapacity());
            shortHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position {1}), shortHistogram size = {2}",
            //                targetBuffer.capacity(), targetBuffer.position(), shortHistogram.getTotalCount());
            targetBuffer.Position = 0;

            var shortHistogram2 = ShortHistogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(shortHistogram, shortHistogram2);

            var targetCompressedBuffer = ByteBuffer.Allocate(shortHistogram.GetNeededByteBufferCapacity());
            shortHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var shortHistogram3 = ShortHistogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.AreEqual(shortHistogram, shortHistogram3);

            Console.WriteLine("\n\nTesting encoding of a IntHistogram:");
            targetBuffer = ByteBuffer.Allocate(intHistogram.GetNeededByteBufferCapacity());
            intHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position = {1}), intHistogram size = {2}", 
            //                targetBuffer.capacity(), targetBuffer.position(), intHistogram.getTotalCount());
            targetBuffer.Position = 0;

            var intHistogram2 = IntHistogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(intHistogram, intHistogram2);

            targetCompressedBuffer = ByteBuffer.Allocate(intHistogram.GetNeededByteBufferCapacity());
            intHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var intHistogram3 = IntHistogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.AreEqual(intHistogram, intHistogram3);

            Console.WriteLine("\n\nTesting encoding of a Histogram (long):");
            targetBuffer = ByteBuffer.Allocate(longHistogram.GetNeededByteBufferCapacity());
            longHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position = {1}), histogram size = {2}",
            //                targetBuffer.capacity(), targetBuffer.position(), histogram.getTotalCount());
            targetBuffer.Position = 0;

            var histogram2 = LongHistogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(longHistogram, histogram2);

            targetCompressedBuffer = ByteBuffer.Allocate(longHistogram.GetNeededByteBufferCapacity());
            longHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var histogram3 = LongHistogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.AreEqual(longHistogram, histogram3);

            targetBuffer.Position = 0;


            targetCompressedBuffer.Position = 0;

            Console.WriteLine("\n\nTesting encoding of a SynchronizedHistogram:");
            targetBuffer = ByteBuffer.Allocate(synchronizedHistogram.GetNeededByteBufferCapacity());
            synchronizedHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position {1}), synchronizedHistogram size = {2}",
            //                targetBuffer.capacity(), targetBuffer.position(), synchronizedHistogram.getTotalCount());
            targetBuffer.Position = 0;

            var synchronizedHistogram2 = SynchronizedHistogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(synchronizedHistogram, synchronizedHistogram2);

            targetCompressedBuffer = ByteBuffer.Allocate(synchronizedHistogram.GetNeededByteBufferCapacity());
            synchronizedHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var synchronizedHistogram3 = SynchronizedHistogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.AreEqual(synchronizedHistogram, synchronizedHistogram3);
        }

        [Test]
        public void TestHistogramEncodingFullRangeOfValues()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, 3);

            for (long i = 0; i < HighestTrackableValue; i += 100) 
            {
                longHistogram.RecordValue(i);
            }
            longHistogram.RecordValue(HighestTrackableValue);

            Console.WriteLine("\n\nTesting encoding of a Histogram (long):");
            var targetBuffer = ByteBuffer.Allocate(longHistogram.GetNeededByteBufferCapacity());
            longHistogram.EncodeIntoByteBuffer(targetBuffer);
            targetBuffer.Position = 0;

            var histogram2 = LongHistogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(longHistogram, histogram2);

            var targetCompressedBuffer = ByteBuffer.Allocate(longHistogram.GetNeededByteBufferCapacity());
            longHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var histogram3 = LongHistogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.AreEqual(longHistogram, histogram3);

            Console.WriteLine();
            histogram3.OutputPercentileDistribution(Console.Out);
        }
    }
}
