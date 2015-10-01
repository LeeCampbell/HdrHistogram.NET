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
using Assert = HdrHistogram.Test.AssertEx;

namespace HdrHistogram.Test
{
    public class HistogramTest
    {
        static long highestTrackableValue = 3600L * 1000 * 1000; // e.g. for 1 hr in usec units
        static int numberOfSignificantValueDigits = 3;
        //static long testValueLevel = 12340;
        static long testValueLevel = 4;

        [Test]
        public void testConstructionArgumentRanges()  
        {
            Boolean thrown = false;
            LongHistogram longHistogram = null;

            try
            {
                // This should throw:
                longHistogram = new LongHistogram(1, numberOfSignificantValueDigits);
            }
            catch (ArgumentException) 
            {
                thrown = true;
            }
            Assert.assertTrue(thrown);
            Assert.assertEquals(longHistogram, null);

            thrown = false;
            try 
            {
                // This should throw:
                longHistogram = new LongHistogram(highestTrackableValue, 6);
            }
            catch (ArgumentException) 
            {
                thrown = true;
            }
            Assert.assertTrue(thrown);
            Assert.assertEquals(longHistogram, null);

            thrown = false;
            try 
            {
                // This should throw:
                longHistogram = new LongHistogram(highestTrackableValue, -1);
            }
            catch (ArgumentException) 
            {
                thrown = true;
            }
            Assert.assertTrue(thrown);
            Assert.assertEquals(longHistogram, null);
        }

        [Test]
        public void testConstructionArgumentGets()  
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals(1, longHistogram.LowestTrackableValue);
            Assert.assertEquals(highestTrackableValue, longHistogram.HighestTrackableValue);
            Assert.assertEquals(numberOfSignificantValueDigits, longHistogram.NumberOfSignificantValueDigits);
            LongHistogram histogram2 = new LongHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals(1000, histogram2.LowestTrackableValue);
        }

        [Test]
        public void testGetEstimatedFootprintInBytes()  
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            /*
            *     largestValueWithSingleUnitResolution = 2 * (10 ^ numberOfSignificantValueDigits);
            *     subBucketSize = roundedUpToNearestPowerOf2(largestValueWithSingleUnitResolution);

            *     expectedHistogramFootprintInBytes = 512 +
            *          ({primitive type size} / 2) *
            *          (log2RoundedUp((highestTrackableValue) / subBucketSize) + 2) *
            *          subBucketSize
            */
            long largestValueWithSingleUnitResolution = 2 * (long) Math.Pow(10, numberOfSignificantValueDigits);
            int subBucketCountMagnitude = (int)Math.Ceiling(Math.Log(largestValueWithSingleUnitResolution) / Math.Log(2));
            int subBucketSize = (int) Math.Pow(2, (subBucketCountMagnitude));

            long expectedSize = 512 +
                    ((8 *
                     ((long)(
                            Math.Ceiling(
                             Math.Log(highestTrackableValue / subBucketSize)
                                     / Math.Log(2)
                            )
                           + 2)) *
                        (1 << (64 - MiscUtilities.NumberOfLeadingZeros(2 * (long)Math.Pow(10, numberOfSignificantValueDigits))))
                     ) / 2);
            Assert.assertEquals(expectedSize, longHistogram.GetEstimatedFootprintInBytes());
        }

        [Test]
        public void testRecordValue()  
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            longHistogram.RecordValue(testValueLevel);
            Assert.assertEquals(1L, longHistogram.GetCountAtValue(testValueLevel));
            Assert.assertEquals(1L, longHistogram.TotalCount);
        }

        [Test]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void testRecordValue_Overflow_ShouldThrowException()  
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            longHistogram.RecordValue(highestTrackableValue * 3);
        }

        [Test]
        public void testRecordValueWithExpectedInterval()  
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            longHistogram.RecordValueWithExpectedInterval(testValueLevel, testValueLevel/4);
            LongHistogram rawHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            rawHistogram.RecordValue(testValueLevel);
            // The data will include corrected samples:
            Assert.assertEquals(1L, longHistogram.GetCountAtValue((testValueLevel * 1 )/4));
            Assert.assertEquals(1L, longHistogram.GetCountAtValue((testValueLevel * 2 )/4));
            Assert.assertEquals(1L, longHistogram.GetCountAtValue((testValueLevel * 3 )/4));
            Assert.assertEquals(1L, longHistogram.GetCountAtValue((testValueLevel * 4 )/4));
            Assert.assertEquals(4L, longHistogram.TotalCount);
            // But the raw data will not:
            Assert.assertEquals(0L, rawHistogram.GetCountAtValue((testValueLevel * 1 )/4));
            Assert.assertEquals(0L, rawHistogram.GetCountAtValue((testValueLevel * 2 )/4));
            Assert.assertEquals(0L, rawHistogram.GetCountAtValue((testValueLevel * 3 )/4));
            Assert.assertEquals(1L, rawHistogram.GetCountAtValue((testValueLevel * 4 )/4));
            Assert.assertEquals(1L, rawHistogram.TotalCount);
        }

        [Test]
        public void testReset()  
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            longHistogram.RecordValue(testValueLevel);
            longHistogram.Reset();
            Assert.assertEquals(0L, longHistogram.GetCountAtValue(testValueLevel));
            Assert.assertEquals(0L, longHistogram.TotalCount);
        }

        [Test]
        public void testAdd()  
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            LongHistogram other = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            longHistogram.RecordValue(testValueLevel);
            longHistogram.RecordValue(testValueLevel * 1000);
            other.RecordValue(testValueLevel);
            other.RecordValue(testValueLevel * 1000);
            longHistogram.Add(other);
            Assert.assertEquals(2L, longHistogram.GetCountAtValue(testValueLevel));
            Assert.assertEquals(2L, longHistogram.GetCountAtValue(testValueLevel * 1000));
            Assert.assertEquals(4L, longHistogram.TotalCount);

            LongHistogram biggerOther = new LongHistogram(highestTrackableValue * 2, numberOfSignificantValueDigits);
            biggerOther.RecordValue(testValueLevel);
            biggerOther.RecordValue(testValueLevel * 1000);

            // Adding the smaller histogram to the bigger one should work:
            biggerOther.Add(longHistogram);
            Assert.assertEquals(3L, biggerOther.GetCountAtValue(testValueLevel));
            Assert.assertEquals(3L, biggerOther.GetCountAtValue(testValueLevel * 1000));
            Assert.assertEquals(6L, biggerOther.TotalCount);

            // But trying to add a larger histogram into a smaller one should throw an AIOOB:
            bool thrown = false;
            try 
            {
                // This should throw:
                longHistogram.Add(biggerOther);
            }
            catch (ArgumentOutOfRangeException)
            {
                thrown = true;
            }
            Assert.assertTrue(thrown);
        }

        [Test]
        public void testSizeOfEquivalentValueRange() 
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("Size of equivalent range for value 1 is 1",
                    1, longHistogram.SizeOfEquivalentValueRange(1));
            Assert.assertEquals("Size of equivalent range for value 2500 is 2",
                    2, longHistogram.SizeOfEquivalentValueRange(2500));
            Assert.assertEquals("Size of equivalent range for value 8191 is 4",
                    4, longHistogram.SizeOfEquivalentValueRange(8191));
            Assert.assertEquals("Size of equivalent range for value 8192 is 8",
                    8, longHistogram.SizeOfEquivalentValueRange(8192));
            Assert.assertEquals("Size of equivalent range for value 10000 is 8",
                    8, longHistogram.SizeOfEquivalentValueRange(10000));
        }

        [Test]
        public void testScaledSizeOfEquivalentValueRange() 
        {
            LongHistogram longHistogram = new LongHistogram(1024, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("Size of equivalent range for value 1 * 1024 is 1 * 1024",
                    1 * 1024, longHistogram.SizeOfEquivalentValueRange(1 * 1024));
            Assert.assertEquals("Size of equivalent range for value 2500 * 1024 is 2 * 1024",
                    2 * 1024, longHistogram.SizeOfEquivalentValueRange(2500 * 1024));
            Assert.assertEquals("Size of equivalent range for value 8191 * 1024 is 4 * 1024",
                    4 * 1024, longHistogram.SizeOfEquivalentValueRange(8191 * 1024));
            Assert.assertEquals("Size of equivalent range for value 8192 * 1024 is 8 * 1024",
                    8 * 1024, longHistogram.SizeOfEquivalentValueRange(8192 * 1024));
            Assert.assertEquals("Size of equivalent range for value 10000 * 1024 is 8 * 1024",
                    8 * 1024, longHistogram.SizeOfEquivalentValueRange(10000 * 1024));
        }

        [Test]
        public void testLowestEquivalentValue() 
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The lowest equivalent value to 10007 is 10000",
                    10000, longHistogram.LowestEquivalentValue(10007));
            Assert.assertEquals("The lowest equivalent value to 10009 is 10008",
                    10008, longHistogram.LowestEquivalentValue(10009));
        }

        [Test]
        public void testScaledLowestEquivalentValue() 
        {
            LongHistogram longHistogram = new LongHistogram(1024, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The lowest equivalent value to 10007 * 1024 is 10000 * 1024",
                    10000 * 1024, longHistogram.LowestEquivalentValue(10007 * 1024));
            Assert.assertEquals("The lowest equivalent value to 10009 * 1024 is 10008 * 1024",
                    10008 * 1024, longHistogram.LowestEquivalentValue(10009 * 1024));
        }

        [Test]
        public void testHighestEquivalentValue() 
        {
            LongHistogram longHistogram = new LongHistogram(1024, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The highest equivalent value to 8180 * 1024 is 8183 * 1024 + 1023",
                    8183 * 1024 + 1023, longHistogram.HighestEquivalentValue(8180 * 1024));
            Assert.assertEquals("The highest equivalent value to 8187 * 1024 is 8191 * 1024 + 1023",
                    8191 * 1024 + 1023, longHistogram.HighestEquivalentValue(8191 * 1024));
            Assert.assertEquals("The highest equivalent value to 8193 * 1024 is 8199 * 1024 + 1023",
                    8199 * 1024 + 1023, longHistogram.HighestEquivalentValue(8193 * 1024));
            Assert.assertEquals("The highest equivalent value to 9995 * 1024 is 9999 * 1024 + 1023",
                    9999 * 1024 + 1023, longHistogram.HighestEquivalentValue(9995 * 1024));
            Assert.assertEquals("The highest equivalent value to 10007 * 1024 is 10007 * 1024 + 1023",
                    10007 * 1024 + 1023, longHistogram.HighestEquivalentValue(10007 * 1024));
            Assert.assertEquals("The highest equivalent value to 10008 * 1024 is 10015 * 1024 + 1023",
                    10015 * 1024 + 1023, longHistogram.HighestEquivalentValue(10008 * 1024));
        }

        [Test]
        public void testScaledHighestEquivalentValue() 
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The highest equivalent value to 8180 is 8183",
                    8183, longHistogram.HighestEquivalentValue(8180));
            Assert.assertEquals("The highest equivalent value to 8187 is 8191",
                    8191, longHistogram.HighestEquivalentValue(8191));
            Assert.assertEquals("The highest equivalent value to 8193 is 8199",
                    8199, longHistogram.HighestEquivalentValue(8193));
            Assert.assertEquals("The highest equivalent value to 9995 is 9999",
                    9999, longHistogram.HighestEquivalentValue(9995));
            Assert.assertEquals("The highest equivalent value to 10007 is 10007",
                    10007, longHistogram.HighestEquivalentValue(10007));
            Assert.assertEquals("The highest equivalent value to 10008 is 10015",
                    10015, longHistogram.HighestEquivalentValue(10008));
        }

        [Test]
        public void testMedianEquivalentValue() 
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The median equivalent value to 4 is 4",
                    4, longHistogram.MedianEquivalentValue(4));
            Assert.assertEquals("The median equivalent value to 5 is 5",
                    5, longHistogram.MedianEquivalentValue(5));
            Assert.assertEquals("The median equivalent value to 4000 is 4001",
                    4001, longHistogram.MedianEquivalentValue(4000));
            Assert.assertEquals("The median equivalent value to 8000 is 8002",
                    8002, longHistogram.MedianEquivalentValue(8000));
            Assert.assertEquals("The median equivalent value to 10007 is 10004",
                    10004, longHistogram.MedianEquivalentValue(10007));
        }

        [Test]
        public void testScaledMedianEquivalentValue() 
        {
            LongHistogram longHistogram = new LongHistogram(1024, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The median equivalent value to 4 * 1024 is 4 * 1024 + 512",
                    4 * 1024 + 512, longHistogram.MedianEquivalentValue(4 * 1024));
            Assert.assertEquals("The median equivalent value to 5 * 1024 is 5 * 1024 + 512",
                    5 * 1024 + 512, longHistogram.MedianEquivalentValue(5 * 1024));
            Assert.assertEquals("The median equivalent value to 4000 * 1024 is 4001 * 1024",
                    4001 * 1024, longHistogram.MedianEquivalentValue(4000 * 1024));
            Assert.assertEquals("The median equivalent value to 8000 * 1024 is 8002 * 1024",
                    8002 * 1024, longHistogram.MedianEquivalentValue(8000 * 1024));
            Assert.assertEquals("The median equivalent value to 10007 * 1024 is 10004 * 1024",
                    10004 * 1024, longHistogram.MedianEquivalentValue(10007 * 1024));
        }

        [Test]
        public void testNextNonEquivalentValue() 
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertNotSame(null, longHistogram);
        }

        //void testAbstractSerialization(HistogramBase histogram) throws Exception {
        //    histogram.recordValue(testValueLevel);
        //    histogram.recordValue(testValueLevel * 10);
        //    histogram.recordValueWithExpectedInterval(histogram.HighestTrackableValue - 1, 31);
        //    ByteArrayOutputStream bos = new ByteArrayOutputStream();
        //    ObjectOutput out = null;
        //    ByteArrayInputStream bis = null;
        //    ObjectInput in = null;
        //    HistogramBase newHistogram = null;
        //    try {
        //        out = new ObjectOutputStream(bos);
        //        out.writeObject(histogram);
        //        Deflater compresser = new Deflater();
        //        compresser.setInput(bos.toByteArray());
        //        compresser.finish();
        //        byte [] compressedOutput = new byte[1024*1024];
        //        int compressedDataLength = compresser.deflate(compressedOutput);
        //        Console.WriteLine("Serialized form of " + histogram.getClass() + " with highestTrackableValue = " +
        //                histogram.HighestTrackableValue + "\n and a numberOfSignificantValueDigits = " +
        //                histogram.getNumberOfSignificantValueDigits() + " is " + bos.toByteArray().length +
        //                " bytes long. Compressed form is " + compressedDataLength + " bytes long.");
        //        Console.WriteLine("   (estimated footprint was " + histogram.getEstimatedFootprintInBytes() + " bytes)");
        //        bis = new ByteArrayInputStream(bos.toByteArray());
        //        in = new ObjectInputStream(bis);
        //        newHistogram = (HistogramBase) in.readObject();
        //    } finally {
        //        if (out != null) out.close();
        //        bos.close();
        //        if (in !=null) in.close();
        //        if (bis != null) bis.close();
        //    }
        //    Assert.assertNotNull(newHistogram);
        //    assertEqual(histogram, newHistogram);
        //}

        private void assertEqual(HistogramBase expectedHistogram, HistogramBase actualHistogram)
        {
            Assert.assertEquals(expectedHistogram, actualHistogram);
            Assert.assertEquals(
                    expectedHistogram.GetCountAtValue(testValueLevel),
                    actualHistogram.GetCountAtValue(testValueLevel));
            Assert.assertEquals(
                    expectedHistogram.GetCountAtValue(testValueLevel * 10),
                    actualHistogram.GetCountAtValue(testValueLevel * 10));
            Assert.assertEquals(
                    expectedHistogram.TotalCount,
                    actualHistogram.TotalCount);
        }

        //[Test]
        //public void testSerialization() throws Exception {
        //    Histogram histogram = new Histogram(highestTrackableValue, 3);
        //    testAbstractSerialization(histogram);
        //    IntHistogram intHistogram = new IntHistogram(highestTrackableValue, 3);
        //    testAbstractSerialization(intHistogram);
        //    ShortHistogram shortHistogram = new ShortHistogram(highestTrackableValue, 3);
        //    testAbstractSerialization(shortHistogram);
        //    histogram = new Histogram(highestTrackableValue, 2);
        //    testAbstractSerialization(histogram);
        //    intHistogram = new IntHistogram(highestTrackableValue, 2);
        //    testAbstractSerialization(intHistogram);
        //    shortHistogram = new ShortHistogram(highestTrackableValue, 2);
        //    testAbstractSerialization(shortHistogram);
        //}

        [Test]
        public void testOverflow()  
        {
            ShortHistogram histogram = new ShortHistogram(highestTrackableValue, 2);
            histogram.RecordValue(testValueLevel);
            histogram.RecordValue(testValueLevel * 10);
            Assert.assertFalse(histogram.HasOverflowed());
            // This should overflow a ShortHistogram:
            histogram.RecordValueWithExpectedInterval(histogram.HighestTrackableValue - 1, 500);
            Assert.assertTrue(histogram.HasOverflowed());
            Console.WriteLine("Histogram percentile output should show overflow:");
            histogram.OutputPercentileDistribution(Console.Out, 5, 100.0);
            Console.WriteLine("\nHistogram percentile output should be in CSV format and show overflow:");
            histogram.OutputPercentileDistribution(Console.Out, 5, 100.0, true);
            Console.WriteLine("");
        }

        [Test]
        public void testReestablishTotalCount()  
        {
            ShortHistogram histogram = new ShortHistogram(highestTrackableValue, 2);
            histogram.RecordValue(testValueLevel);
            histogram.RecordValue(testValueLevel * 10);
            Assert.assertFalse(histogram.HasOverflowed());
            // This should overflow a ShortHistogram:
            histogram.RecordValueWithExpectedInterval(histogram.HighestTrackableValue - 1, 500);
            Assert.assertTrue(histogram.HasOverflowed());
            histogram.ReestablishTotalCount();
            Assert.assertFalse(histogram.HasOverflowed());
        }

        [Test]
        public void testCopy()
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            longHistogram.RecordValue(testValueLevel);
            longHistogram.RecordValue(testValueLevel * 10);
            longHistogram.RecordValueWithExpectedInterval(longHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of Histogram:");
            assertEqual(longHistogram, longHistogram.Copy());

            IntHistogram intHistogram = new IntHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            intHistogram.RecordValue(testValueLevel);
            intHistogram.RecordValue(testValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of IntHistogram:");
            assertEqual(intHistogram, intHistogram.Copy());

            ShortHistogram shortHistogram = new ShortHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            shortHistogram.RecordValue(testValueLevel);
            shortHistogram.RecordValue(testValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of ShortHistogram:");
            assertEqual(shortHistogram, shortHistogram.Copy());

            SynchronizedHistogram syncHistogram = new SynchronizedHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            syncHistogram.RecordValue(testValueLevel);
            syncHistogram.RecordValue(testValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of SynchronizedHistogram:");
            assertEqual(syncHistogram, syncHistogram.Copy());
        }

        [Test]
        public void testScaledCopy()  
        {
            LongHistogram longHistogram = new LongHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            longHistogram.RecordValue(testValueLevel);
            longHistogram.RecordValue(testValueLevel * 10);
            longHistogram.RecordValueWithExpectedInterval(longHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of scaled Histogram:");
            assertEqual(longHistogram, longHistogram.Copy());

            IntHistogram intHistogram = new IntHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            intHistogram.RecordValue(testValueLevel);
            intHistogram.RecordValue(testValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of scaled IntHistogram:");
            assertEqual(intHistogram, intHistogram.Copy());

            ShortHistogram shortHistogram = new ShortHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            shortHistogram.RecordValue(testValueLevel);
            shortHistogram.RecordValue(testValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of scaled ShortHistogram:");
            assertEqual(shortHistogram, shortHistogram.Copy());

            SynchronizedHistogram syncHistogram = new SynchronizedHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            syncHistogram.RecordValue(testValueLevel);
            syncHistogram.RecordValue(testValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copy of scaled SynchronizedHistogram:");
            assertEqual(syncHistogram, syncHistogram.Copy());
        }

        [Test]
        public void testCopyInto()  
        {
            LongHistogram longHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            LongHistogram targetLongHistogram = new LongHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            longHistogram.RecordValue(testValueLevel);
            longHistogram.RecordValue(testValueLevel * 10);
            longHistogram.RecordValueWithExpectedInterval(longHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for Histogram:");
            longHistogram.CopyInto(targetLongHistogram);
            assertEqual(longHistogram, targetLongHistogram);

            longHistogram.RecordValue(testValueLevel * 20);

            longHistogram.CopyInto(targetLongHistogram);
            assertEqual(longHistogram, targetLongHistogram);

            IntHistogram intHistogram = new IntHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            IntHistogram targetIntHistogram = new IntHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            intHistogram.RecordValue(testValueLevel);
            intHistogram.RecordValue(testValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for IntHistogram:");
            intHistogram.CopyInto(targetIntHistogram);
            assertEqual(intHistogram, targetIntHistogram);

            intHistogram.RecordValue(testValueLevel * 20);

            intHistogram.CopyInto(targetIntHistogram);
            assertEqual(intHistogram, targetIntHistogram);

            ShortHistogram shortHistogram = new ShortHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            ShortHistogram targetShortHistogram = new ShortHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            shortHistogram.RecordValue(testValueLevel);
            shortHistogram.RecordValue(testValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for ShortHistogram:");
            shortHistogram.CopyInto(targetShortHistogram);
            assertEqual(shortHistogram, targetShortHistogram);

            shortHistogram.RecordValue(testValueLevel * 20);

            shortHistogram.CopyInto(targetShortHistogram);
            assertEqual(shortHistogram, targetShortHistogram);

            Console.WriteLine("Testing copyInto for AtomicHistogram:");

            SynchronizedHistogram syncHistogram = new SynchronizedHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            SynchronizedHistogram targetSyncHistogram = new SynchronizedHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            syncHistogram.RecordValue(testValueLevel);
            syncHistogram.RecordValue(testValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.HighestTrackableValue - 1, 31000); // Should this really be 31, if it is the test takes 1min!!!);

            Console.WriteLine("Testing copyInto for SynchronizedHistogram:");
            syncHistogram.CopyInto(targetSyncHistogram);
            assertEqual(syncHistogram, targetSyncHistogram);

            syncHistogram.RecordValue(testValueLevel * 20);

            syncHistogram.CopyInto(targetSyncHistogram);
            assertEqual(syncHistogram, targetSyncHistogram);
        }

        [Test]
        public void testScaledCopyInto()  
        {
            LongHistogram longHistogram = new LongHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            LongHistogram targetLongHistogram = new LongHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            longHistogram.RecordValue(testValueLevel);
            longHistogram.RecordValue(testValueLevel * 10);
            longHistogram.RecordValueWithExpectedInterval(longHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled Histogram:");
            longHistogram.CopyInto(targetLongHistogram);
            assertEqual(longHistogram, targetLongHistogram);

            longHistogram.RecordValue(testValueLevel * 20);

            longHistogram.CopyInto(targetLongHistogram);
            assertEqual(longHistogram, targetLongHistogram);

            IntHistogram intHistogram = new IntHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            IntHistogram targetIntHistogram = new IntHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            intHistogram.RecordValue(testValueLevel);
            intHistogram.RecordValue(testValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled IntHistogram:");
            intHistogram.CopyInto(targetIntHistogram);
            assertEqual(intHistogram, targetIntHistogram);

            intHistogram.RecordValue(testValueLevel * 20);

            intHistogram.CopyInto(targetIntHistogram);
            assertEqual(intHistogram, targetIntHistogram);

            ShortHistogram shortHistogram = new ShortHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            ShortHistogram targetShortHistogram = new ShortHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            shortHistogram.RecordValue(testValueLevel);
            shortHistogram.RecordValue(testValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled ShortHistogram:");
            shortHistogram.CopyInto(targetShortHistogram);
            assertEqual(shortHistogram, targetShortHistogram);

            shortHistogram.RecordValue(testValueLevel * 20);

            shortHistogram.CopyInto(targetShortHistogram);
            assertEqual(shortHistogram, targetShortHistogram);

            SynchronizedHistogram syncHistogram = new SynchronizedHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            SynchronizedHistogram targetSyncHistogram = new SynchronizedHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            syncHistogram.RecordValue(testValueLevel);
            syncHistogram.RecordValue(testValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.HighestTrackableValue - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled SynchronizedHistogram:");
            syncHistogram.CopyInto(targetSyncHistogram);
            assertEqual(syncHistogram, targetSyncHistogram);

            syncHistogram.RecordValue(testValueLevel * 20);

            syncHistogram.CopyInto(targetSyncHistogram);
            assertEqual(syncHistogram, targetSyncHistogram);
        }
    }
}
