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
using NUnit.Framework;
using Assert = HdrHistogram.NET.Test.AssertEx;

namespace HdrHistogram.NET.Test
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
            Histogram histogram = null;

            try
            {
                // This should throw:
                histogram = new Histogram(1, numberOfSignificantValueDigits);
            }
            catch (ArgumentException) 
            {
                thrown = true;
            }
            Assert.assertTrue(thrown);
            Assert.assertEquals(histogram, null);

            thrown = false;
            try 
            {
                // This should throw:
                histogram = new Histogram(highestTrackableValue, 6);
            }
            catch (ArgumentException) 
            {
                thrown = true;
            }
            Assert.assertTrue(thrown);
            Assert.assertEquals(histogram, null);

            thrown = false;
            try 
            {
                // This should throw:
                histogram = new Histogram(highestTrackableValue, -1);
            }
            catch (ArgumentException) 
            {
                thrown = true;
            }
            Assert.assertTrue(thrown);
            Assert.assertEquals(histogram, null);
        }

        [Test]
        public void testConstructionArgumentGets()  
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals(1, histogram.GetLowestTrackableValue());
            Assert.assertEquals(highestTrackableValue, histogram.GetHighestTrackableValue());
            Assert.assertEquals(numberOfSignificantValueDigits, histogram.GetNumberOfSignificantValueDigits());
            Histogram histogram2 = new Histogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals(1000, histogram2.GetLowestTrackableValue());
        }

        [Test]
        public void testGetEstimatedFootprintInBytes()  
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
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
            Assert.assertEquals(expectedSize, histogram.GetEstimatedFootprintInBytes());
        }

        [Test]
        public void testRecordValue()  
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            histogram.RecordValue(testValueLevel);
            Assert.assertEquals(1L, histogram.GetCountAtValue(testValueLevel));
            Assert.assertEquals(1L, histogram.TotalCount);
        }

        [Test]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void testRecordValue_Overflow_ShouldThrowException()  
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            histogram.RecordValue(highestTrackableValue * 3);
        }

        [Test]
        public void testRecordValueWithExpectedInterval()  
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            histogram.RecordValueWithExpectedInterval(testValueLevel, testValueLevel/4);
            Histogram rawHistogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            rawHistogram.RecordValue(testValueLevel);
            // The data will include corrected samples:
            Assert.assertEquals(1L, histogram.GetCountAtValue((testValueLevel * 1 )/4));
            Assert.assertEquals(1L, histogram.GetCountAtValue((testValueLevel * 2 )/4));
            Assert.assertEquals(1L, histogram.GetCountAtValue((testValueLevel * 3 )/4));
            Assert.assertEquals(1L, histogram.GetCountAtValue((testValueLevel * 4 )/4));
            Assert.assertEquals(4L, histogram.TotalCount);
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
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            histogram.RecordValue(testValueLevel);
            histogram.Reset();
            Assert.assertEquals(0L, histogram.GetCountAtValue(testValueLevel));
            Assert.assertEquals(0L, histogram.TotalCount);
        }

        [Test]
        public void testAdd()  
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            Histogram other = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            histogram.RecordValue(testValueLevel);
            histogram.RecordValue(testValueLevel * 1000);
            other.RecordValue(testValueLevel);
            other.RecordValue(testValueLevel * 1000);
            histogram.Add(other);
            Assert.assertEquals(2L, histogram.GetCountAtValue(testValueLevel));
            Assert.assertEquals(2L, histogram.GetCountAtValue(testValueLevel * 1000));
            Assert.assertEquals(4L, histogram.TotalCount);

            Histogram biggerOther = new Histogram(highestTrackableValue * 2, numberOfSignificantValueDigits);
            biggerOther.RecordValue(testValueLevel);
            biggerOther.RecordValue(testValueLevel * 1000);

            // Adding the smaller histogram to the bigger one should work:
            biggerOther.Add(histogram);
            Assert.assertEquals(3L, biggerOther.GetCountAtValue(testValueLevel));
            Assert.assertEquals(3L, biggerOther.GetCountAtValue(testValueLevel * 1000));
            Assert.assertEquals(6L, biggerOther.TotalCount);

            // But trying to add a larger histogram into a smaller one should throw an AIOOB:
            bool thrown = false;
            try 
            {
                // This should throw:
                histogram.Add(biggerOther);
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
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("Size of equivalent range for value 1 is 1",
                    1, histogram.SizeOfEquivalentValueRange(1));
            Assert.assertEquals("Size of equivalent range for value 2500 is 2",
                    2, histogram.SizeOfEquivalentValueRange(2500));
            Assert.assertEquals("Size of equivalent range for value 8191 is 4",
                    4, histogram.SizeOfEquivalentValueRange(8191));
            Assert.assertEquals("Size of equivalent range for value 8192 is 8",
                    8, histogram.SizeOfEquivalentValueRange(8192));
            Assert.assertEquals("Size of equivalent range for value 10000 is 8",
                    8, histogram.SizeOfEquivalentValueRange(10000));
        }

        [Test]
        public void testScaledSizeOfEquivalentValueRange() 
        {
            Histogram histogram = new Histogram(1024, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("Size of equivalent range for value 1 * 1024 is 1 * 1024",
                    1 * 1024, histogram.SizeOfEquivalentValueRange(1 * 1024));
            Assert.assertEquals("Size of equivalent range for value 2500 * 1024 is 2 * 1024",
                    2 * 1024, histogram.SizeOfEquivalentValueRange(2500 * 1024));
            Assert.assertEquals("Size of equivalent range for value 8191 * 1024 is 4 * 1024",
                    4 * 1024, histogram.SizeOfEquivalentValueRange(8191 * 1024));
            Assert.assertEquals("Size of equivalent range for value 8192 * 1024 is 8 * 1024",
                    8 * 1024, histogram.SizeOfEquivalentValueRange(8192 * 1024));
            Assert.assertEquals("Size of equivalent range for value 10000 * 1024 is 8 * 1024",
                    8 * 1024, histogram.SizeOfEquivalentValueRange(10000 * 1024));
        }

        [Test]
        public void testLowestEquivalentValue() 
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The lowest equivalent value to 10007 is 10000",
                    10000, histogram.LowestEquivalentValue(10007));
            Assert.assertEquals("The lowest equivalent value to 10009 is 10008",
                    10008, histogram.LowestEquivalentValue(10009));
        }

        [Test]
        public void testScaledLowestEquivalentValue() 
        {
            Histogram histogram = new Histogram(1024, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The lowest equivalent value to 10007 * 1024 is 10000 * 1024",
                    10000 * 1024, histogram.LowestEquivalentValue(10007 * 1024));
            Assert.assertEquals("The lowest equivalent value to 10009 * 1024 is 10008 * 1024",
                    10008 * 1024, histogram.LowestEquivalentValue(10009 * 1024));
        }

        [Test]
        public void testHighestEquivalentValue() 
        {
            Histogram histogram = new Histogram(1024, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The highest equivalent value to 8180 * 1024 is 8183 * 1024 + 1023",
                    8183 * 1024 + 1023, histogram.HighestEquivalentValue(8180 * 1024));
            Assert.assertEquals("The highest equivalent value to 8187 * 1024 is 8191 * 1024 + 1023",
                    8191 * 1024 + 1023, histogram.HighestEquivalentValue(8191 * 1024));
            Assert.assertEquals("The highest equivalent value to 8193 * 1024 is 8199 * 1024 + 1023",
                    8199 * 1024 + 1023, histogram.HighestEquivalentValue(8193 * 1024));
            Assert.assertEquals("The highest equivalent value to 9995 * 1024 is 9999 * 1024 + 1023",
                    9999 * 1024 + 1023, histogram.HighestEquivalentValue(9995 * 1024));
            Assert.assertEquals("The highest equivalent value to 10007 * 1024 is 10007 * 1024 + 1023",
                    10007 * 1024 + 1023, histogram.HighestEquivalentValue(10007 * 1024));
            Assert.assertEquals("The highest equivalent value to 10008 * 1024 is 10015 * 1024 + 1023",
                    10015 * 1024 + 1023, histogram.HighestEquivalentValue(10008 * 1024));
        }

        [Test]
        public void testScaledHighestEquivalentValue() 
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The highest equivalent value to 8180 is 8183",
                    8183, histogram.HighestEquivalentValue(8180));
            Assert.assertEquals("The highest equivalent value to 8187 is 8191",
                    8191, histogram.HighestEquivalentValue(8191));
            Assert.assertEquals("The highest equivalent value to 8193 is 8199",
                    8199, histogram.HighestEquivalentValue(8193));
            Assert.assertEquals("The highest equivalent value to 9995 is 9999",
                    9999, histogram.HighestEquivalentValue(9995));
            Assert.assertEquals("The highest equivalent value to 10007 is 10007",
                    10007, histogram.HighestEquivalentValue(10007));
            Assert.assertEquals("The highest equivalent value to 10008 is 10015",
                    10015, histogram.HighestEquivalentValue(10008));
        }

        [Test]
        public void testMedianEquivalentValue() 
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The median equivalent value to 4 is 4",
                    4, histogram.MedianEquivalentValue(4));
            Assert.assertEquals("The median equivalent value to 5 is 5",
                    5, histogram.MedianEquivalentValue(5));
            Assert.assertEquals("The median equivalent value to 4000 is 4001",
                    4001, histogram.MedianEquivalentValue(4000));
            Assert.assertEquals("The median equivalent value to 8000 is 8002",
                    8002, histogram.MedianEquivalentValue(8000));
            Assert.assertEquals("The median equivalent value to 10007 is 10004",
                    10004, histogram.MedianEquivalentValue(10007));
        }

        [Test]
        public void testScaledMedianEquivalentValue() 
        {
            Histogram histogram = new Histogram(1024, highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertEquals("The median equivalent value to 4 * 1024 is 4 * 1024 + 512",
                    4 * 1024 + 512, histogram.MedianEquivalentValue(4 * 1024));
            Assert.assertEquals("The median equivalent value to 5 * 1024 is 5 * 1024 + 512",
                    5 * 1024 + 512, histogram.MedianEquivalentValue(5 * 1024));
            Assert.assertEquals("The median equivalent value to 4000 * 1024 is 4001 * 1024",
                    4001 * 1024, histogram.MedianEquivalentValue(4000 * 1024));
            Assert.assertEquals("The median equivalent value to 8000 * 1024 is 8002 * 1024",
                    8002 * 1024, histogram.MedianEquivalentValue(8000 * 1024));
            Assert.assertEquals("The median equivalent value to 10007 * 1024 is 10004 * 1024",
                    10004 * 1024, histogram.MedianEquivalentValue(10007 * 1024));
        }

        [Test]
        public void testNextNonEquivalentValue() 
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            Assert.assertNotSame(null, histogram);
        }

        //void testAbstractSerialization(HistogramBase histogram) throws Exception {
        //    histogram.recordValue(testValueLevel);
        //    histogram.recordValue(testValueLevel * 10);
        //    histogram.recordValueWithExpectedInterval(histogram.getHighestTrackableValue() - 1, 31);
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
        //                histogram.getHighestTrackableValue() + "\n and a numberOfSignificantValueDigits = " +
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
            histogram.RecordValueWithExpectedInterval(histogram.GetHighestTrackableValue() - 1, 500);
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
            histogram.RecordValueWithExpectedInterval(histogram.GetHighestTrackableValue() - 1, 500);
            Assert.assertTrue(histogram.HasOverflowed());
            histogram.ReestablishTotalCount();
            Assert.assertFalse(histogram.HasOverflowed());
        }

        [Test]
        public void testCopy()
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            histogram.RecordValue(testValueLevel);
            histogram.RecordValue(testValueLevel * 10);
            histogram.RecordValueWithExpectedInterval(histogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copy of Histogram:");
            assertEqual(histogram, histogram.Copy());

            IntHistogram intHistogram = new IntHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            intHistogram.RecordValue(testValueLevel);
            intHistogram.RecordValue(testValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copy of IntHistogram:");
            assertEqual(intHistogram, intHistogram.Copy());

            ShortHistogram shortHistogram = new ShortHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            shortHistogram.RecordValue(testValueLevel);
            shortHistogram.RecordValue(testValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copy of ShortHistogram:");
            assertEqual(shortHistogram, shortHistogram.Copy());

            SynchronizedHistogram syncHistogram = new SynchronizedHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            syncHistogram.RecordValue(testValueLevel);
            syncHistogram.RecordValue(testValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copy of SynchronizedHistogram:");
            assertEqual(syncHistogram, syncHistogram.Copy());
        }

        [Test]
        public void testScaledCopy()  
        {
            Histogram histogram = new Histogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            histogram.RecordValue(testValueLevel);
            histogram.RecordValue(testValueLevel * 10);
            histogram.RecordValueWithExpectedInterval(histogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copy of scaled Histogram:");
            assertEqual(histogram, histogram.Copy());

            IntHistogram intHistogram = new IntHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            intHistogram.RecordValue(testValueLevel);
            intHistogram.RecordValue(testValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copy of scaled IntHistogram:");
            assertEqual(intHistogram, intHistogram.Copy());

            ShortHistogram shortHistogram = new ShortHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            shortHistogram.RecordValue(testValueLevel);
            shortHistogram.RecordValue(testValueLevel * 10);
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copy of scaled ShortHistogram:");
            assertEqual(shortHistogram, shortHistogram.Copy());

            SynchronizedHistogram syncHistogram = new SynchronizedHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            syncHistogram.RecordValue(testValueLevel);
            syncHistogram.RecordValue(testValueLevel * 10);
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copy of scaled SynchronizedHistogram:");
            assertEqual(syncHistogram, syncHistogram.Copy());
        }

        [Test]
        public void testCopyInto()  
        {
            Histogram histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            Histogram targetHistogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            histogram.RecordValue(testValueLevel);
            histogram.RecordValue(testValueLevel * 10);
            histogram.RecordValueWithExpectedInterval(histogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copyInto for Histogram:");
            histogram.CopyInto(targetHistogram);
            assertEqual(histogram, targetHistogram);

            histogram.RecordValue(testValueLevel * 20);

            histogram.CopyInto(targetHistogram);
            assertEqual(histogram, targetHistogram);

            IntHistogram intHistogram = new IntHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            IntHistogram targetIntHistogram = new IntHistogram(highestTrackableValue, numberOfSignificantValueDigits);
            intHistogram.RecordValue(testValueLevel);
            intHistogram.RecordValue(testValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.GetHighestTrackableValue() - 1, 31000);

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
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.GetHighestTrackableValue() - 1, 31000);

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
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.GetHighestTrackableValue() - 1, 31000); // Should this really be 31, if it is the test takes 1min!!!);

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
            Histogram histogram = new Histogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            Histogram targetHistogram = new Histogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            histogram.RecordValue(testValueLevel);
            histogram.RecordValue(testValueLevel * 10);
            histogram.RecordValueWithExpectedInterval(histogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled Histogram:");
            histogram.CopyInto(targetHistogram);
            assertEqual(histogram, targetHistogram);

            histogram.RecordValue(testValueLevel * 20);

            histogram.CopyInto(targetHistogram);
            assertEqual(histogram, targetHistogram);

            IntHistogram intHistogram = new IntHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            IntHistogram targetIntHistogram = new IntHistogram(1000, highestTrackableValue, numberOfSignificantValueDigits);
            intHistogram.RecordValue(testValueLevel);
            intHistogram.RecordValue(testValueLevel * 10);
            intHistogram.RecordValueWithExpectedInterval(intHistogram.GetHighestTrackableValue() - 1, 31000);

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
            shortHistogram.RecordValueWithExpectedInterval(shortHistogram.GetHighestTrackableValue() - 1, 31000);

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
            syncHistogram.RecordValueWithExpectedInterval(syncHistogram.GetHighestTrackableValue() - 1, 31000);

            Console.WriteLine("Testing copyInto for scaled SynchronizedHistogram:");
            syncHistogram.CopyInto(targetSyncHistogram);
            assertEqual(syncHistogram, targetSyncHistogram);

            syncHistogram.RecordValue(testValueLevel * 20);

            syncHistogram.CopyInto(targetSyncHistogram);
            assertEqual(syncHistogram, targetSyncHistogram);
        }
    }
}
