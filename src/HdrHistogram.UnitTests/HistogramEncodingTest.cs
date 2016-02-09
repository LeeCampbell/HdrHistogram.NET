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
using System.IO;
using HdrHistogram.Persistence;
using HdrHistogram.Utilities;
using NUnit.Framework;

namespace HdrHistogram.UnitTests
{
    public class HistogramEncodingTest
    {
        private const long HighestTrackableValue = 3600L * 1000 * 1000; // e.g. for 1 hr in usec units

        [Test]
        public void TestHistogramEncoding()
        {
            var shortHistogram = new ShortHistogram(HighestTrackableValue, 3);
            var intHistogram = new IntHistogram(HighestTrackableValue, 3);
            var longHistogram = new LongHistogram(HighestTrackableValue, 3);
            var synchronizedHistogram = new SynchronizedHistogram(HighestTrackableValue, 3);

            for (var i = 0; i < 10000; i++)
            {
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

            var shortHistogram2 = Histogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(shortHistogram, shortHistogram2);

            var targetCompressedBuffer = ByteBuffer.Allocate(shortHistogram.GetNeededByteBufferCapacity());
            shortHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var shortHistogram3 = Histogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.AreEqual(shortHistogram, shortHistogram3);

            Console.WriteLine("\n\nTesting encoding of a IntHistogram:");
            targetBuffer = ByteBuffer.Allocate(intHistogram.GetNeededByteBufferCapacity());
            intHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position = {1}), intHistogram size = {2}", 
            //                targetBuffer.capacity(), targetBuffer.position(), intHistogram.getTotalCount());
            targetBuffer.Position = 0;

            var intHistogram2 = Histogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(intHistogram, intHistogram2);

            targetCompressedBuffer = ByteBuffer.Allocate(intHistogram.GetNeededByteBufferCapacity());
            intHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var intHistogram3 = Histogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.AreEqual(intHistogram, intHistogram3);

            Console.WriteLine("\n\nTesting encoding of a Histogram (long):");
            targetBuffer = ByteBuffer.Allocate(longHistogram.GetNeededByteBufferCapacity());
            longHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position = {1}), histogram size = {2}",
            //                targetBuffer.capacity(), targetBuffer.position(), histogram.getTotalCount());
            targetBuffer.Position = 0;

            var histogram2 = Histogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(longHistogram, histogram2);

            targetCompressedBuffer = ByteBuffer.Allocate(longHistogram.GetNeededByteBufferCapacity());
            longHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var histogram3 = Histogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.AreEqual(longHistogram, histogram3);

            targetBuffer.Position = 0;


            targetCompressedBuffer.Position = 0;

            Console.WriteLine("\n\nTesting encoding of a SynchronizedHistogram:");
            targetBuffer = ByteBuffer.Allocate(synchronizedHistogram.GetNeededByteBufferCapacity());
            synchronizedHistogram.EncodeIntoByteBuffer(targetBuffer);
            //Console.WriteLine("After ENCODING TargetBuffer length = {0} (position {1}), synchronizedHistogram size = {2}",
            //                targetBuffer.capacity(), targetBuffer.position(), synchronizedHistogram.getTotalCount());
            targetBuffer.Position = 0;

            var synchronizedHistogram2 = Histogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(synchronizedHistogram, synchronizedHistogram2);

            targetCompressedBuffer = ByteBuffer.Allocate(synchronizedHistogram.GetNeededByteBufferCapacity());
            synchronizedHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var synchronizedHistogram3 = Histogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
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

            var histogram2 = Histogram.DecodeFromByteBuffer(targetBuffer, 0);
            Assert.AreEqual(longHistogram, histogram2);

            var targetCompressedBuffer = ByteBuffer.Allocate(longHistogram.GetNeededByteBufferCapacity());
            longHistogram.EncodeIntoCompressedByteBuffer(targetCompressedBuffer);
            targetCompressedBuffer.Position = 0;

            var histogram3 = Histogram.DecodeFromCompressedByteBuffer(targetCompressedBuffer, 0);
            Assert.AreEqual(longHistogram, histogram3);

            Console.WriteLine();
            histogram3.OutputPercentileDistribution(Console.Out);
        }

        [Test]
        public void emptyLog()
        {
            byte[] data;
            var startTimeWritten = DateTimeOffset.Now;
            using (var writerStream = new MemoryStream())
            {
                var writer = new HistogramLogWriter(writerStream);
                writer.OutputLogFormatVersion();
                writer.OutputStartTime(startTimeWritten);
                writer.OutputLogFormatVersion();
                writer.OutputLegend();
                data = writerStream.ToArray();
            }

            var readerStream = new MemoryStream(data);
            var reader = new HistogramLogReader(readerStream);
            var histogram = reader.NextIntervalHistogram();
            Assert.IsNull(histogram);
            Assert.Equals(startTimeWritten, reader.GetStartTime());
        }

        [Test]
        public void CanReadv2Logs()
        {
            var readerStream = File.OpenRead("Resources\\jHiccup-2.0.7S.logV2.hlog");
            HistogramLogReader reader = new HistogramLogReader(readerStream);
            int histogramCount = 0;
            long totalCount = 0;
            HistogramBase encodeableHistogram = null;
            //LongHistogram accumulatedHistogram = new LongHistogram(TimeSpan.TicksPerHour, 3);
            //var accumulatedHistogram = new LongHistogram(TimeSpan.TicksPerDay * 99, 3);
            var accumulatedHistogram = new LongHistogram(85899345920838, 3);
            foreach (var histogram in reader.NextIntervalHistogram())
            {
                histogramCount++;
                Assert.IsInstanceOf<HistogramBase>(histogram, "Expected integer value histograms in log file");

                totalCount += histogram.TotalCount;
                accumulatedHistogram.Add(histogram);
            }

            Assert.AreEqual(62, histogramCount);
            Assert.AreEqual(48761, totalCount);
            Assert.AreEqual(1745879039, accumulatedHistogram.GetValueAtPercentile(99.9));
            Assert.AreEqual(1796210687, accumulatedHistogram.GetMaxValue());
            //Assert.Equals(1441812279.474, reader.StartTimeSec);   //TODO: What is this testing? -LC
        }
        /*
        
         @Test
    public void emptyLog() throws Exception {
        File temp = File.createTempFile("hdrhistogramtesting", "hist");
        FileOutputStream writerStream = new FileOutputStream(temp);
        HistogramLogWriter writer = new HistogramLogWriter(writerStream);
        writer.outputLogFormatVersion();
        long startTimeWritten = 1000;
        writer.outputStartTime(startTimeWritten);
        writer.outputLogFormatVersion();
        writer.outputLegend();
        writerStream.close();

        FileInputStream readerStream = new FileInputStream(temp);
        HistogramLogReader reader = new HistogramLogReader(readerStream);
        EncodableHistogram histogram = reader.nextIntervalHistogram();
        Assert.assertNull(histogram);
        Assert.assertEquals(1.0, reader.getStartTimeSec());
    }

    @Test
    public void jHiccupV2Log() throws Exception {
        InputStream readerStream = HistogramLogReaderWriterTest.class.getResourceAsStream("jHiccup-2.0.7S.logV2.hlog");

        HistogramLogReader reader = new HistogramLogReader(readerStream);
        int histogramCount = 0;
        long totalCount = 0;
        EncodableHistogram encodeableHistogram = null;
        Histogram accumulatedHistogram = new Histogram(3);
        while ((encodeableHistogram = reader.nextIntervalHistogram()) != null) {
            histogramCount++;
            Assert.assertTrue("Expected integer value histogramsin log file", encodeableHistogram instanceof Histogram);
            Histogram histogram = (Histogram) encodeableHistogram;
            totalCount += histogram.getTotalCount();
            accumulatedHistogram.add(histogram);
        }
        Assert.assertEquals(62, histogramCount);
        Assert.assertEquals(48761, totalCount);
        Assert.assertEquals(1745879039, accumulatedHistogram.getValueAtPercentile(99.9));
        Assert.assertEquals(1796210687, accumulatedHistogram.getMaxValue());
        Assert.assertEquals(1441812279.474, reader.getStartTimeSec());

        readerStream = HistogramLogReaderWriterTest.class.getResourceAsStream("jHiccup-2.0.7S.logV2.hlog");
        reader = new HistogramLogReader(readerStream);
        histogramCount = 0;
        totalCount = 0;
        accumulatedHistogram.reset();
        while ((encodeableHistogram = reader.nextIntervalHistogram(5, 20)) != null) {
            histogramCount++;
            Histogram histogram = (Histogram) encodeableHistogram;
            totalCount += histogram.getTotalCount();
            accumulatedHistogram.add(histogram);
        }
        Assert.assertEquals(15, histogramCount);
        Assert.assertEquals(11664, totalCount);
        Assert.assertEquals(1536163839, accumulatedHistogram.getValueAtPercentile(99.9));
        Assert.assertEquals(1544552447, accumulatedHistogram.getMaxValue());

        readerStream = HistogramLogReaderWriterTest.class.getResourceAsStream("jHiccup-2.0.7S.logV2.hlog");
        reader = new HistogramLogReader(readerStream);
        histogramCount = 0;
        totalCount = 0;
        accumulatedHistogram.reset();
        while ((encodeableHistogram = reader.nextIntervalHistogram(40, 60)) != null) {
            histogramCount++;
            Histogram histogram = (Histogram) encodeableHistogram;
            totalCount += histogram.getTotalCount();
            accumulatedHistogram.add(histogram);
        }
        Assert.assertEquals(20, histogramCount);
        Assert.assertEquals(15830, totalCount);
        Assert.assertEquals(1779433471, accumulatedHistogram.getValueAtPercentile(99.9));
        Assert.assertEquals(1796210687, accumulatedHistogram.getMaxValue());
    }

        
        */

        [Test]
        public void CanRoundTrip()
        {
            var bufferLength = 20;
            var input = new long[] { 1234, 5678, 9101112, 13141516, 17181920, 21222324 };
            var buffer = ByteBuffer.Allocate(bufferLength);
            foreach (var value in input)
            {
                ZigZagEncoding.PutLong(buffer, value);
            }

            buffer.Position = 0;
            var output = new long[input.Length];
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = ZigZagEncoding.GetLong(buffer);
            }

            CollectionAssert.AreEqual(input, output);
        }

        [Test]
        public void CanDecodeZigZagEncodedLongArray()
        {
            var buffer = ByteBuffer.Allocate(new byte[] { 24, 18, 18, 10, 16, 22, 28, 22, 8, 10, 16, 26, 18, 18, 12, 66, 74, 92, 46, 78, 150, 2, 172, 1, 218, 2, 44, 16, 163, 1, 2, 119, 2 });
            var expected = new long[170]; 
            expected[0] = 12;
            expected[1] = 9;
            expected[2] = 9;
            expected[3] = 5;
            expected[4] = 8;
            expected[5] = 11;
            expected[6] = 14;
            expected[7] = 11;
            expected[8] = 4;
            expected[9] = 5;
            expected[10] = 8;
            expected[11] = 13;
            expected[12] = 9;
            expected[13] = 9;
            expected[14] = 6;
            expected[15] = 33;
            expected[16] = 37;
            expected[17] = 46;
            expected[18] = 23;
            expected[19] = 39;
            expected[20] = 139;
            expected[21] = 86;
            expected[22] = 173;
            expected[23] = 22;
            expected[24] = 8;
            //Zero counts skipped.
            expected[107] = 1;
            //Zero counts skipped.
            expected[168] = 1;

            var sut = CountsDecoder.GetDecoderForWordSize(9);

            var actual = new long[expected.Length];
            sut.ReadCounts(buffer, buffer.Capacity(), (idx, count) => actual[idx] = count);

            CollectionAssert.AreEqual(expected, actual);
        }

        
    }
}
