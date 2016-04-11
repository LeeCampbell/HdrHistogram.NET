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
using NUnit.Framework;

namespace HdrHistogram.UnitTests
{
    [TestFixture]
    public class HistogramTest
    {
        private const long HighestTrackableValue = TimeSpan.TicksPerHour; // e.g. for 1 hr in ticks
        private const int NumberOfSignificantValueDigits = 3;
        private const long TestValueLevel = 4;

        [Test]
        public void TestConstructionArgumentRanges()
        {
            Assert.Throws<ArgumentException>(() => { new LongHistogram(1, NumberOfSignificantValueDigits); });
            Assert.Throws<ArgumentException>(() => { new LongHistogram(HighestTrackableValue, 6); });
            Assert.Throws<ArgumentException>(() => { new LongHistogram(HighestTrackableValue, -1); });
        }

        [Test]
        public void TestConstructionArgumentGets()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(1, longHistogram.LowestTrackableValue);
            Assert.AreEqual(HighestTrackableValue, longHistogram.HighestTrackableValue);
            Assert.AreEqual(NumberOfSignificantValueDigits, longHistogram.NumberOfSignificantValueDigits);
            var histogram2 = new LongHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(1000, histogram2.LowestTrackableValue);
        }
        
        [Test]
        public void TestGetEstimatedFootprintInBytes2()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            var largestValueWithSingleUnitResolution = 2 * (long)Math.Pow(10, NumberOfSignificantValueDigits);
            var subBucketCountMagnitude = (int)Math.Ceiling(Math.Log(largestValueWithSingleUnitResolution) / Math.Log(2));
            var subBucketSize = (int)Math.Pow(2, (subBucketCountMagnitude));
            var bucketCount = GetBucketsNeededToCoverValue(subBucketSize, HighestTrackableValue);

            var header = 512;
            var width = sizeof (long);
            var length = (bucketCount + 1) * (subBucketSize / 2);
            var expectedSize = header + (width * length);

            Assert.AreEqual(expectedSize, longHistogram.GetEstimatedFootprintInBytes());
        }
        
        [Test]
        public void TestRecordValue()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            longHistogram.RecordValue(TestValueLevel);
            Assert.AreEqual(1L, longHistogram.GetCountAtValue(TestValueLevel));
            Assert.AreEqual(1L, longHistogram.TotalCount);
        }

        [Test]
        public void testRecordValue_Overflow_ShouldThrowException()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.Catch<IndexOutOfRangeException>(()=>longHistogram.RecordValue(HighestTrackableValue * 3));
        }

        [Test]
        public void TestRecordValueWithExpectedInterval()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            longHistogram.RecordValueWithExpectedInterval(TestValueLevel, TestValueLevel / 4);
            var rawHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            rawHistogram.RecordValue(TestValueLevel);
            // The data will include corrected samples:
            Assert.AreEqual(1L, longHistogram.GetCountAtValue((TestValueLevel * 1) / 4));
            Assert.AreEqual(1L, longHistogram.GetCountAtValue((TestValueLevel * 2) / 4));
            Assert.AreEqual(1L, longHistogram.GetCountAtValue((TestValueLevel * 3) / 4));
            Assert.AreEqual(1L, longHistogram.GetCountAtValue((TestValueLevel * 4) / 4));
            Assert.AreEqual(4L, longHistogram.TotalCount);
            // But the raw data will not:
            Assert.AreEqual(0L, rawHistogram.GetCountAtValue((TestValueLevel * 1) / 4));
            Assert.AreEqual(0L, rawHistogram.GetCountAtValue((TestValueLevel * 2) / 4));
            Assert.AreEqual(0L, rawHistogram.GetCountAtValue((TestValueLevel * 3) / 4));
            Assert.AreEqual(1L, rawHistogram.GetCountAtValue((TestValueLevel * 4) / 4));
            Assert.AreEqual(1L, rawHistogram.TotalCount);
        }

        [Test]
        public void TestReset()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            longHistogram.RecordValue(TestValueLevel);
            longHistogram.Reset();
            Assert.AreEqual(0L, longHistogram.GetCountAtValue(TestValueLevel));
            Assert.AreEqual(0L, longHistogram.TotalCount);
        }

        [Test]
        public void TestAdd()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            var other = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            longHistogram.RecordValue(TestValueLevel);
            longHistogram.RecordValue(TestValueLevel * 1000);
            other.RecordValue(TestValueLevel);
            other.RecordValue(TestValueLevel * 1000);
            longHistogram.Add(other);
            Assert.AreEqual(2L, longHistogram.GetCountAtValue(TestValueLevel));
            Assert.AreEqual(2L, longHistogram.GetCountAtValue(TestValueLevel * 1000));
            Assert.AreEqual(4L, longHistogram.TotalCount);

            var biggerOther = new LongHistogram(HighestTrackableValue * 2, NumberOfSignificantValueDigits);
            biggerOther.RecordValue(TestValueLevel);
            biggerOther.RecordValue(TestValueLevel * 1000);

            // Adding the smaller histogram to the bigger one should work:
            biggerOther.Add(longHistogram);
            Assert.AreEqual(3L, biggerOther.GetCountAtValue(TestValueLevel));
            Assert.AreEqual(3L, biggerOther.GetCountAtValue(TestValueLevel * 1000));
            Assert.AreEqual(6L, biggerOther.TotalCount);

            // But trying to add a larger histogram into a smaller one should throw an AIOOB:
            Assert.Throws<ArgumentOutOfRangeException>(() => { longHistogram.Add(biggerOther); });
        }

        [Test]
        public void TestSizeOfEquivalentValueRange()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(1, longHistogram.SizeOfEquivalentValueRange(1), "Size of equivalent range for value 1 is 1");
            Assert.AreEqual(2, longHistogram.SizeOfEquivalentValueRange(2500), "Size of equivalent range for value 2500 is 2");
            Assert.AreEqual(4, longHistogram.SizeOfEquivalentValueRange(8191), "Size of equivalent range for value 8191 is 4");
            Assert.AreEqual(8, longHistogram.SizeOfEquivalentValueRange(8192), "Size of equivalent range for value 8192 is 8");
            Assert.AreEqual(8, longHistogram.SizeOfEquivalentValueRange(10000), "Size of equivalent range for value 10000 is 8");
        }

        [Test]
        public void TestScaledSizeOfEquivalentValueRange()
        {
            var longHistogram = new LongHistogram(1024, HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(1 * 1024, longHistogram.SizeOfEquivalentValueRange(1 * 1024), "Size of equivalent range for value 1 * 1024 is 1 * 1024");
            Assert.AreEqual(2 * 1024, longHistogram.SizeOfEquivalentValueRange(2500 * 1024), "Size of equivalent range for value 2500 * 1024 is 2 * 1024");
            Assert.AreEqual(4 * 1024, longHistogram.SizeOfEquivalentValueRange(8191 * 1024), "Size of equivalent range for value 8191 * 1024 is 4 * 1024");
            Assert.AreEqual(8 * 1024, longHistogram.SizeOfEquivalentValueRange(8192 * 1024), "Size of equivalent range for value 8192 * 1024 is 8 * 1024");
            Assert.AreEqual(8 * 1024, longHistogram.SizeOfEquivalentValueRange(10000 * 1024), "Size of equivalent range for value 10000 * 1024 is 8 * 1024");
        }

        [Test]
        public void TestLowestEquivalentValue()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(10000, longHistogram.LowestEquivalentValue(10007), "The lowest equivalent value to 10007 is 10000");
            Assert.AreEqual(10008, longHistogram.LowestEquivalentValue(10009), "The lowest equivalent value to 10009 is 10008");
        }

        [Test]
        public void TestScaledLowestEquivalentValue()
        {
            var longHistogram = new LongHistogram(1024, HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(10000 * 1024, longHistogram.LowestEquivalentValue(10007 * 1024), "The lowest equivalent value to 10007 * 1024 is 10000 * 1024");
            Assert.AreEqual(10008 * 1024, longHistogram.LowestEquivalentValue(10009 * 1024), "The lowest equivalent value to 10009 * 1024 is 10008 * 1024");
        }

        [Test]
        public void TestHighestEquivalentValue()
        {
            var longHistogram = new LongHistogram(1024, HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(8183 * 1024 + 1023, longHistogram.HighestEquivalentValue(8180 * 1024), "The highest equivalent value to 8180 * 1024 is 8183 * 1024 + 1023");
            Assert.AreEqual(8191 * 1024 + 1023, longHistogram.HighestEquivalentValue(8191 * 1024), "The highest equivalent value to 8187 * 1024 is 8191 * 1024 + 1023");
            Assert.AreEqual(8199 * 1024 + 1023, longHistogram.HighestEquivalentValue(8193 * 1024), "The highest equivalent value to 8193 * 1024 is 8199 * 1024 + 1023");
            Assert.AreEqual(9999 * 1024 + 1023, longHistogram.HighestEquivalentValue(9995 * 1024), "The highest equivalent value to 9995 * 1024 is 9999 * 1024 + 1023");
            Assert.AreEqual(10007 * 1024 + 1023, longHistogram.HighestEquivalentValue(10007 * 1024), "The highest equivalent value to 10007 * 1024 is 10007 * 1024 + 1023");
            Assert.AreEqual(10015 * 1024 + 1023, longHistogram.HighestEquivalentValue(10008 * 1024), "The highest equivalent value to 10008 * 1024 is 10015 * 1024 + 1023");
        }

        [Test]
        public void TestScaledHighestEquivalentValue()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(8183, longHistogram.HighestEquivalentValue(8180), "The highest equivalent value to 8180 is 8183");
            Assert.AreEqual(8191, longHistogram.HighestEquivalentValue(8191), "The highest equivalent value to 8187 is 8191");
            Assert.AreEqual(8199, longHistogram.HighestEquivalentValue(8193), "The highest equivalent value to 8193 is 8199");
            Assert.AreEqual(9999, longHistogram.HighestEquivalentValue(9995), "The highest equivalent value to 9995 is 9999");
            Assert.AreEqual(10007, longHistogram.HighestEquivalentValue(10007), "The highest equivalent value to 10007 is 10007");
            Assert.AreEqual(10015, longHistogram.HighestEquivalentValue(10008), "The highest equivalent value to 10008 is 10015");
        }

        [Test]
        public void TestMedianEquivalentValue()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(4, longHistogram.MedianEquivalentValue(4), "The median equivalent value to 4 is 4");
            Assert.AreEqual(5, longHistogram.MedianEquivalentValue(5), "The median equivalent value to 5 is 5");
            Assert.AreEqual(4001, longHistogram.MedianEquivalentValue(4000), "The median equivalent value to 4000 is 4001");
            Assert.AreEqual(8002, longHistogram.MedianEquivalentValue(8000), "The median equivalent value to 8000 is 8002");
            Assert.AreEqual(10004, longHistogram.MedianEquivalentValue(10007), "The median equivalent value to 10007 is 10004");
        }

        [Test]
        public void TestScaledMedianEquivalentValue()
        {
            var longHistogram = new LongHistogram(1024, HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.AreEqual(4 * 1024 + 512, longHistogram.MedianEquivalentValue(4 * 1024), "The median equivalent value to 4 * 1024 is 4 * 1024 + 512");
            Assert.AreEqual(5 * 1024 + 512, longHistogram.MedianEquivalentValue(5 * 1024), "The median equivalent value to 5 * 1024 is 5 * 1024 + 512");
            Assert.AreEqual(4001 * 1024, longHistogram.MedianEquivalentValue(4000 * 1024), "The median equivalent value to 4000 * 1024 is 4001 * 1024");
            Assert.AreEqual(8002 * 1024, longHistogram.MedianEquivalentValue(8000 * 1024), "The median equivalent value to 8000 * 1024 is 8002 * 1024");
            Assert.AreEqual(10004 * 1024, longHistogram.MedianEquivalentValue(10007 * 1024), "The median equivalent value to 10007 * 1024 is 10004 * 1024");
        }

        [Test]
        public void TestNextNonEquivalentValue()
        {
            //HACK: WTF does this prove? -LC
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            Assert.IsNotNull(longHistogram);
        }

        private static void AssertEqual(HistogramBase expectedHistogram, HistogramBase actualHistogram)
        {
            Assert.AreEqual(expectedHistogram, actualHistogram);
            Assert.AreEqual(
                    expectedHistogram.GetCountAtValue(TestValueLevel),
                    actualHistogram.GetCountAtValue(TestValueLevel));
            Assert.AreEqual(
                    expectedHistogram.GetCountAtValue(TestValueLevel * 10),
                    actualHistogram.GetCountAtValue(TestValueLevel * 10));
            Assert.AreEqual(
                    expectedHistogram.TotalCount,
                    actualHistogram.TotalCount);
        }

        [Test]
        public void TestOverflow()
        {
            var histogram = new ShortHistogram(HighestTrackableValue, 2);
            histogram.RecordValue(TestValueLevel);
            histogram.RecordValue(TestValueLevel * 10);
            Assert.False(histogram.HasOverflowed());
            // This should overflow a ShortHistogram:
            histogram.RecordValueWithExpectedInterval(histogram.HighestTrackableValue - 1, 500);
            Assert.True(histogram.HasOverflowed());
            Console.WriteLine("Histogram percentile output should show overflow:");
            histogram.OutputPercentileDistribution(Console.Out, 5, 100.0);
            Console.WriteLine("\nHistogram percentile output should be in CSV format and show overflow:");
            histogram.OutputPercentileDistribution(Console.Out, 5, 100.0, true);
            Console.WriteLine("");
        }

        [Test]
        public void TestReestablishTotalCount()
        {
            var histogram = new ShortHistogram(HighestTrackableValue, 2);
            histogram.RecordValue(TestValueLevel);
            histogram.RecordValue(TestValueLevel * 10);
            Assert.False(histogram.HasOverflowed());
            // This should overflow a ShortHistogram:
            histogram.RecordValueWithExpectedInterval(histogram.HighestTrackableValue - 1, 500);
            Assert.True(histogram.HasOverflowed());
            histogram.ReestablishTotalCount();
            Assert.False(histogram.HasOverflowed());
        }

        [Test]
        public void TestCopy()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            longHistogram.RecordValue(TestValueLevel);
            longHistogram.RecordValue(TestValueLevel * 10);
            longHistogram.RecordValueWithExpectedInterval(longHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of Histogram:");
            AssertEqual(longHistogram, longHistogram.Copy());

            var intHistogram = new IntHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            intHistogram.RecordValue(TestValueLevel);
            intHistogram.RecordValue(TestValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of IntHistogram:");
            AssertEqual(intHistogram, intHistogram.Copy());

            var shortHistogram = new ShortHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            shortHistogram.RecordValue(TestValueLevel);
            shortHistogram.RecordValue(TestValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of ShortHistogram:");
            AssertEqual(shortHistogram, shortHistogram.Copy());

            var syncHistogram = new SynchronizedHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            syncHistogram.RecordValue(TestValueLevel);
            syncHistogram.RecordValue(TestValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of SynchronizedHistogram:");
            AssertEqual(syncHistogram, syncHistogram.Copy());
        }

        [Test]
        public void TestScaledCopy()
        {
            var longHistogram = new LongHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            longHistogram.RecordValue(TestValueLevel);
            longHistogram.RecordValue(TestValueLevel * 10);
            longHistogram.RecordValueWithExpectedInterval(longHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of scaled Histogram:");
            AssertEqual(longHistogram, longHistogram.Copy());

            var intHistogram = new IntHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            intHistogram.RecordValue(TestValueLevel);
            intHistogram.RecordValue(TestValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of scaled IntHistogram:");
            AssertEqual(intHistogram, intHistogram.Copy());

            var shortHistogram = new ShortHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            shortHistogram.RecordValue(TestValueLevel);
            shortHistogram.RecordValue(TestValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of scaled ShortHistogram:");
            AssertEqual(shortHistogram, shortHistogram.Copy());

            var syncHistogram = new SynchronizedHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            syncHistogram.RecordValue(TestValueLevel);
            syncHistogram.RecordValue(TestValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of scaled SynchronizedHistogram:");
            AssertEqual(syncHistogram, syncHistogram.Copy());
        }

        [Test]
        public void TestCopyInto()
        {
            var longHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            var targetLongHistogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            longHistogram.RecordValue(TestValueLevel);
            longHistogram.RecordValue(TestValueLevel * 10);
            longHistogram.RecordValueWithExpectedInterval(longHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for Histogram:");
            longHistogram.CopyInto(targetLongHistogram);
            AssertEqual(longHistogram, targetLongHistogram);

            longHistogram.RecordValue(TestValueLevel * 20);

            longHistogram.CopyInto(targetLongHistogram);
            AssertEqual(longHistogram, targetLongHistogram);

            var intHistogram = new IntHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            var targetIntHistogram = new IntHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            intHistogram.RecordValue(TestValueLevel);
            intHistogram.RecordValue(TestValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for IntHistogram:");
            intHistogram.CopyInto(targetIntHistogram);
            AssertEqual(intHistogram, targetIntHistogram);

            intHistogram.RecordValue(TestValueLevel * 20);

            intHistogram.CopyInto(targetIntHistogram);
            AssertEqual(intHistogram, targetIntHistogram);

            var shortHistogram = new ShortHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            var targetShortHistogram = new ShortHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            shortHistogram.RecordValue(TestValueLevel);
            shortHistogram.RecordValue(TestValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for ShortHistogram:");
            shortHistogram.CopyInto(targetShortHistogram);
            AssertEqual(shortHistogram, targetShortHistogram);

            shortHistogram.RecordValue(TestValueLevel * 20);

            shortHistogram.CopyInto(targetShortHistogram);
            AssertEqual(shortHistogram, targetShortHistogram);

            Console.WriteLine("Testing copyInto for AtomicHistogram:");

            var syncHistogram = new SynchronizedHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            var targetSyncHistogram = new SynchronizedHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            syncHistogram.RecordValue(TestValueLevel);
            syncHistogram.RecordValue(TestValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.HighestTrackableValue - 1, 31000); // Should this really be 31, if it is the test takes 1min!!!);

            Console.WriteLine("Testing copyInto for SynchronizedHistogram:");
            syncHistogram.CopyInto(targetSyncHistogram);
            AssertEqual(syncHistogram, targetSyncHistogram);

            syncHistogram.RecordValue(TestValueLevel * 20);

            syncHistogram.CopyInto(targetSyncHistogram);
            AssertEqual(syncHistogram, targetSyncHistogram);
        }

        [Test]
        public void TestScaledCopyInto()
        {
            var longHistogram = new LongHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            var targetLongHistogram = new LongHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            longHistogram.RecordValue(TestValueLevel);
            longHistogram.RecordValue(TestValueLevel * 10);
            longHistogram.RecordValueWithExpectedInterval(longHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled Histogram:");
            longHistogram.CopyInto(targetLongHistogram);
            AssertEqual(longHistogram, targetLongHistogram);

            longHistogram.RecordValue(TestValueLevel * 20);

            longHistogram.CopyInto(targetLongHistogram);
            AssertEqual(longHistogram, targetLongHistogram);

            var intHistogram = new IntHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            var targetIntHistogram = new IntHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            intHistogram.RecordValue(TestValueLevel);
            intHistogram.RecordValue(TestValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled IntHistogram:");
            intHistogram.CopyInto(targetIntHistogram);
            AssertEqual(intHistogram, targetIntHistogram);

            intHistogram.RecordValue(TestValueLevel * 20);

            intHistogram.CopyInto(targetIntHistogram);
            AssertEqual(intHistogram, targetIntHistogram);

            var shortHistogram = new ShortHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            var targetShortHistogram = new ShortHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            shortHistogram.RecordValue(TestValueLevel);
            shortHistogram.RecordValue(TestValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled ShortHistogram:");
            shortHistogram.CopyInto(targetShortHistogram);
            AssertEqual(shortHistogram, targetShortHistogram);

            shortHistogram.RecordValue(TestValueLevel * 20);

            shortHistogram.CopyInto(targetShortHistogram);
            AssertEqual(shortHistogram, targetShortHistogram);

            var syncHistogram = new SynchronizedHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            var targetSyncHistogram = new SynchronizedHistogram(1000, HighestTrackableValue, NumberOfSignificantValueDigits);
            syncHistogram.RecordValue(TestValueLevel);
            syncHistogram.RecordValue(TestValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled SynchronizedHistogram:");
            syncHistogram.CopyInto(targetSyncHistogram);
            AssertEqual(syncHistogram, targetSyncHistogram);

            syncHistogram.RecordValue(TestValueLevel * 20);

            syncHistogram.CopyInto(targetSyncHistogram);
            AssertEqual(syncHistogram, targetSyncHistogram);
        }


        private static int GetBucketsNeededToCoverValue(int subBucketSize, long value)
        {
            long trackableValue = (subBucketSize - 1);// << _unitMagnitude;
            int bucketsNeeded = 1;
            while (trackableValue < value)
            {
                trackableValue <<= 1;
                bucketsNeeded++;
            }
            return bucketsNeeded;
        }
    }
}
