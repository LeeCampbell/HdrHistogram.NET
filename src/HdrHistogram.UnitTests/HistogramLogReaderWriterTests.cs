using System;
using System.IO;
using System.Linq;
using HdrHistogram.Utilities;
using NUnit.Framework;

namespace HdrHistogram.UnitTests
{
    [TestFixture]
    public class HistogramLogReaderWriterTests
    {
        [Test]
        public void CanReadEmptyLog()
        {
            byte[] data;
            var startTimeWritten = DateTime.Now;
            var expectedStartTime = startTimeWritten.SecondsSinceUnixEpoch()
                .Round(3)
                .ToDateFromSecondsSinceEpoch();

            using (var writerStream = new MemoryStream())
            {
                var writer = new HistogramLogWriter(writerStream);
                writer.Write(startTimeWritten);
                data = writerStream.ToArray();
            }

            using (var readerStream = new MemoryStream(data))
            {
                var reader = new HistogramLogReader(readerStream);
                var histograms = reader.ReadHistograms();
                CollectionAssert.IsEmpty(histograms.ToList());
                var actual = reader.GetStartTime();
                Assert.AreEqual(expectedStartTime, actual);
            }
        }

        [Test]
        public void CanRoundTripSingleHsitogram()
        {
            var histogram = CreatePopulatedHistogram(1000);
            var startTimeWritten = DateTime.Now;
            var endTimeWritten = startTimeWritten.AddMinutes(30);
            
            histogram.StartTimeStamp = (long)(startTimeWritten.SecondsSinceUnixEpoch()*1000L);
            histogram.EndTimeStamp = (long)(endTimeWritten.SecondsSinceUnixEpoch()*1000L);

            var data = WriteLog(startTimeWritten, histogram);
            var actualHistograms = ReadHistograms(data);

            Assert.AreEqual(1, actualHistograms.Length);
            HistogramAssert.AreEqual(histogram, actualHistograms.Single());
        }

        private static HistogramBase[] ReadHistograms(byte[] data)
        {
            HistogramBase[] actualHistograms;
            using (var readerStream = new MemoryStream(data))
            {
                var reader = new HistogramLogReader(readerStream);
                actualHistograms = reader.ReadHistograms().ToArray();
            }
            return actualHistograms;
        }

        private static byte[] WriteLog(DateTime startTimeWritten, LongHistogram histogram)
        {
            byte[] data;
            using (var writerStream = new MemoryStream())
            {
                var writer = new HistogramLogWriter(writerStream);
                writer.Write(startTimeWritten, histogram);
                data = writerStream.ToArray();
            }
            return data;
        }

        private static LongHistogram CreatePopulatedHistogram(long multiplier)
        {
            var histogram = new LongHistogram(3600L * 1000 * 1000, 3);
            for (int i = 0; i < 10000; i++)
            {
                histogram.RecordValue(i * multiplier);
            }
            return histogram;
        }

        [TestCase("Resources\\jHiccup-2.0.7S.logV2.hlog")]
        public void CanReadv2Logs(string logPath)
        {
            var readerStream = File.OpenRead(logPath);
            HistogramLogReader reader = new HistogramLogReader(readerStream);
            int histogramCount = 0;
            long totalCount = 0;
            HistogramBase encodeableHistogram = null;
            var accumulatedHistogram = new LongHistogram(85899345920838, 3);
            foreach (var histogram in reader.ReadHistograms())
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
            Assert.AreEqual(1441812279.474, reader.GetStartTime().SecondsSinceUnixEpoch());
        }
    }
}