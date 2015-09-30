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
using HdrHistogram.NET.Iteration;
using NUnit.Framework;
using Assert = HdrHistogram.NET.Test.AssertEx;

namespace HdrHistogram.NET.Test
{
    /**
     * JUnit test for {@link HistogramData}
     */
    public class HistogramDataAccessTest 
    {
        static readonly long highestTrackableValue = 3600L * 1000 * 1000; // 1 hour in usec units
        static readonly int numberOfSignificantValueDigits = 3; // Maintain at least 3 decimal points of accuracy
        static readonly Histogram histogram;
        static readonly Histogram scaledHistogram;
        static readonly Histogram rawHistogram;
        static readonly Histogram scaledRawHistogram;
        static readonly /*Histogram*/ HistogramBase postCorrectedHistogram;
        static readonly /*Histogram*/ HistogramBase postCorrectedScaledHistogram;

        static HistogramDataAccessTest()
        {
            histogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            scaledHistogram = new Histogram(1000, highestTrackableValue * 512, numberOfSignificantValueDigits);
            rawHistogram = new Histogram(highestTrackableValue, numberOfSignificantValueDigits);
            scaledRawHistogram = new Histogram(1000, highestTrackableValue * 512, numberOfSignificantValueDigits);
            // Log hypothetical scenario: 100 seconds of "perfect" 1msec results, sampled
            // 100 times per second (10,000 results), followed by a 100 second pause with
            // a single (100 second) recorded result. Recording is done indicating an expected
            // interval between samples of 10 msec:
            for (int i = 0; i < 10000; i++) 
            {
                histogram.RecordValueWithExpectedInterval(1000 /* 1 msec */, 10000 /* 10 msec expected interval */);
                scaledHistogram.RecordValueWithExpectedInterval(1000 * 512 /* 1 msec */, 10000 * 512 /* 10 msec expected interval */);
                rawHistogram.RecordValue(1000 /* 1 msec */);
                scaledRawHistogram.RecordValue(1000 * 512/* 1 msec */);
            }
            histogram.RecordValueWithExpectedInterval(100000000L /* 100 sec */, 10000 /* 10 msec expected interval */);
            scaledHistogram.RecordValueWithExpectedInterval(100000000L * 512 /* 100 sec */, 10000 * 512 /* 10 msec expected interval */);
            rawHistogram.RecordValue(100000000L /* 100 sec */);
            scaledRawHistogram.RecordValue(100000000L * 512 /* 100 sec */);

            postCorrectedHistogram = rawHistogram.CopyCorrectedForCoordinatedOmission(10000 /* 10 msec expected interval */);
            postCorrectedScaledHistogram = scaledRawHistogram.CopyCorrectedForCoordinatedOmission(10000 * 512 /* 10 msec expected interval */);
        }

        [Test]
        public void testScalingEquivalence() 
        {
            Assert.assertEquals("averages should be equivalent",
                    histogram.GetMean() * 512,
                    scaledHistogram.GetMean(), scaledHistogram.GetMean() * 0.000001);
            Assert.assertEquals("total count should be the same",
                    histogram.TotalCount,
                    scaledHistogram.TotalCount);
            Assert.assertEquals("99%'iles should be equivalent",
                    histogram.LowestEquivalentValue(histogram.GetValueAtPercentile(99.0)) * 512,
                    scaledHistogram.LowestEquivalentValue(scaledHistogram.GetValueAtPercentile(99.0)));
            Assert.assertEquals("Max should be equivalent",
                    histogram.GetMaxValue() * 512,
                    scaledHistogram.GetMaxValue());
            // Same for post-corrected:
            Assert.assertEquals("averages should be equivalent",
                    histogram.GetMean() * 512,
                    scaledHistogram.GetMean(), scaledHistogram.GetMean() * 0.000001);
            Assert.assertEquals("total count should be the same",
                    postCorrectedHistogram.TotalCount,
                    postCorrectedScaledHistogram.TotalCount);
            Assert.assertEquals("99%'iles should be equivalent",
                    postCorrectedHistogram.LowestEquivalentValue(postCorrectedHistogram.GetValueAtPercentile(99.0)) * 512,
                    postCorrectedScaledHistogram.LowestEquivalentValue(postCorrectedScaledHistogram.GetValueAtPercentile(99.0)));
            Assert.assertEquals("Max should be equivalent",
                    postCorrectedHistogram.GetMaxValue() * 512,
                    postCorrectedScaledHistogram.GetMaxValue());
        }

        [Test]
        public void testPreVsPostCorrectionValues()  
        {
            // Loop both ways (one would be enough, but good practice just for fun:

            Assert.assertEquals("pre and post corrected count totals ",
                    histogram.TotalCount, postCorrectedHistogram.TotalCount);

            // The following comparison loops would have worked in a perfect accuracy world, but since post
            // correction is done based on the value extracted from the bucket, and the during-recording is done
            // based on the actual (not pixelized) value, there will be subtle differences due to roundoffs:

            //        for (HistogramIterationValue v : histogram.AllValues()) {
            //            long preCorrectedCount = v.getCountAtValueIteratedTo;
            //            long postCorrectedCount = postCorrectedHistogram.getCountAtValue(v.getValueIteratedTo);
            //            Assert.assertEquals("pre and post corrected count at value " + v.getValueIteratedTo,
            //                    preCorrectedCount, postCorrectedCount);
            //        }
            //
            //        for (HistogramIterationValue v : postCorrectedHistogram.AllValues()) {
            //            long preCorrectedCount = v.getCountAtValueIteratedTo;
            //            long postCorrectedCount = histogram.getCountAtValue(v.getValueIteratedTo);
            //            Assert.assertEquals("pre and post corrected count at value " + v.getValueIteratedTo(),
            //                    preCorrectedCount, postCorrectedCount);
            //        }
        }

        [Test]
        public void testGetTotalCount()  
        {
            // The overflow value should count in the total count:
            Assert.assertEquals("Raw total count is 10,001",
                    10001L, rawHistogram.TotalCount);
            Assert.assertEquals("Total count is 20,000",
                    20000L, histogram.TotalCount);
        }

        [Test]
        public void testGetMaxValue()  
        {
            Assert.assertTrue(
                    histogram.ValuesAreEquivalent(100L * 1000 * 1000,
                            histogram.GetMaxValue()));
        }

        [Test]
        public void testGetMinValue()  
        {
            Assert.assertTrue(
                    histogram.ValuesAreEquivalent(1000,
                            histogram.GetMinValue()));
        }

        [Test]
        public void testGetMean()  
        {
            double expectedRawMean = ((10000.0 * 1000) + (1.0 * 100000000))/10001; /* direct avg. of raw results */
            double expectedMean = (1000.0 + 50000000.0)/2; /* avg. 1 msec for half the time, and 50 sec for other half */
            // We expect to see the mean to be accurate to ~3 decimal points (~0.1%):
            Assert.assertEquals("Raw mean is " + expectedRawMean + " +/- 0.1%",
                    expectedRawMean, rawHistogram.GetMean(), expectedRawMean * 0.001);
            Assert.assertEquals("Mean is " + expectedMean + " +/- 0.1%",
                    expectedMean, histogram.GetMean(), expectedMean * 0.001);
        }

        [Test]
        public void testGetStdDeviation()  
        {
            double expectedRawMean = ((10000.0 * 1000) + (1.0 * 100000000))/10001; /* direct avg. of raw results */
            double expectedRawStdDev =
                    Math.Sqrt(
                        ((10000.0 * Math.Pow((1000.0 - expectedRawMean), 2)) +
                                Math.Pow((100000000.0 - expectedRawMean), 2)) /
                                10001);

            double expectedMean = (1000.0 + 50000000.0)/2; /* avg. 1 msec for half the time, and 50 sec for other half */
            double expectedSquareDeviationSum = 10000 * Math.Pow((1000.0 - expectedMean), 2);
            for (long value = 10000; value <= 100000000; value += 10000) {
                expectedSquareDeviationSum += Math.Pow((value - expectedMean), 2);
            }
            double expectedStdDev = Math.Sqrt(expectedSquareDeviationSum / 20000);

            // We expect to see the standard deviations to be accurate to ~3 decimal points (~0.1%):
            Assert.assertEquals("Raw standard deviation is " + expectedRawStdDev + " +/- 0.1%",
                    expectedRawStdDev, rawHistogram.GetStdDeviation(), expectedRawStdDev * 0.001);
            Assert.assertEquals("Standard deviation is " + expectedStdDev + " +/- 0.1%",
                    expectedStdDev, histogram.GetStdDeviation(), expectedStdDev * 0.001);
        }

        [Test]
        public void testGetValueAtPercentile()  
        {
            Assert.assertEquals("raw 30%'ile is 1 msec +/- 0.1%",
                    1000.0, (double) rawHistogram.GetValueAtPercentile(30.0),
                    1000.0 * 0.001);
            Assert.assertEquals("raw 99%'ile is 1 msec +/- 0.1%",
                    1000.0, (double) rawHistogram.GetValueAtPercentile(99.0),
                    1000.0 * 0.001);
            Assert.assertEquals("raw 99.99%'ile is 1 msec +/- 0.1%",
                    1000.0, (double) rawHistogram.GetValueAtPercentile(99.99)
                    , 1000.0 * 0.001);
            Assert.assertEquals("raw 99.999%'ile is 100 sec +/- 0.1%",
                    100000000.0, (double) rawHistogram.GetValueAtPercentile(99.999),
                    100000000.0 * 0.001);
            Assert.assertEquals("raw 100%'ile is 100 sec +/- 0.1%",
                    100000000.0, (double) rawHistogram.GetValueAtPercentile(100.0),
                    100000000.0 * 0.001);

            Assert.assertEquals("30%'ile is 1 msec +/- 0.1%",
                    1000.0, (double) histogram.GetValueAtPercentile(30.0),
                    1000.0 * 0.001);
            Assert.assertEquals("50%'ile is 1 msec +/- 0.1%",
                    1000.0, (double) histogram.GetValueAtPercentile(50.0),
                    1000.0 * 0.001);
            Assert.assertEquals("75%'ile is 50 sec +/- 0.1%",
                    50000000.0, (double) histogram.GetValueAtPercentile(75.0),
                    50000000.0 * 0.001);
            Assert.assertEquals("90%'ile is 80 sec +/- 0.1%",
                    80000000.0, (double) histogram.GetValueAtPercentile(90.0),
                    80000000.0 * 0.001);
            Assert.assertEquals("99%'ile is 98 sec +/- 0.1%",
                    98000000.0, (double) histogram.GetValueAtPercentile(99.0),
                    98000000.0 * 0.001);
            Assert.assertEquals("99.999%'ile is 100 sec +/- 0.1%",
                    100000000.0, (double) histogram.GetValueAtPercentile(99.999),
                    100000000.0 * 0.001);
            Assert.assertEquals("100%'ile is 100 sec +/- 0.1%",
                    100000000.0, (double) histogram.GetValueAtPercentile(100.0),
                    100000000.0 * 0.001);
        }

        [Test]
        public void testGetValueAtPercentileForLargeHistogram() 
        {
            long largestValue = 1000000000000L;
            Histogram h = new Histogram(largestValue, 5);
            h.RecordValue(largestValue);

            Assert.assertTrue(h.GetValueAtPercentile(100.0) > 0);
        }

        [Test]
        public void testGetPercentileAtOrBelowValue()  
        {
            Assert.assertEquals("Raw percentile at or below 5 msec is 99.99% +/- 0.0001",
                    99.99,
                    rawHistogram.GetPercentileAtOrBelowValue(5000), 0.0001);
            Assert.assertEquals("Percentile at or below 5 msec is 50% +/- 0.0001%",
                    50.0,
                    histogram.GetPercentileAtOrBelowValue(5000), 0.0001);
            Assert.assertEquals("Percentile at or below 100 sec is 100% +/- 0.0001%",
                    100.0,
                    histogram.GetPercentileAtOrBelowValue(100000000L), 0.0001);
        }

        [Test]
        public void testGetCountBetweenValues()  
        {
            Assert.assertEquals("Count of raw values between 1 msec and 1 msec is 1",
                    10000, rawHistogram.GetCountBetweenValues(1000L, 1000L));
            Assert.assertEquals("Count of raw values between 5 msec and 150 sec is 1",
                    1, rawHistogram.GetCountBetweenValues(5000L, 150000000L));
            Assert.assertEquals("Count of values between 5 msec and 150 sec is 10,000",
                    10000, histogram.GetCountBetweenValues(5000L, 150000000L));
        }

        [Test]
        public void testGetCountAtValue()  
        {
            Assert.assertEquals("Count of raw values at 10 msec is 0",
                    0, rawHistogram.GetCountBetweenValues(10000L, 10010L));
            Assert.assertEquals("Count of values at 10 msec is 0",
                    1, histogram.GetCountBetweenValues(10000L, 10010L));
            Assert.assertEquals("Count of raw values at 1 msec is 10,000",
                    10000, rawHistogram.GetCountAtValue(1000L));
            Assert.assertEquals("Count of values at 1 msec is 10,000",
                    10000, histogram.GetCountAtValue(1000L));
        }

        [Test]
        public void testPercentiles()  
        {
            foreach (HistogramIterationValue v  in histogram.Percentiles(5 /* ticks per half */)) 
            {
                Assert.assertEquals("Value at Iterated-to Percentile is the same as the matching getValueAtPercentile():\n" +
                        "getPercentileLevelIteratedTo = " + v.PercentileLevelIteratedTo +
                        "\ngetValueIteratedTo = " + v.ValueIteratedTo +
                        "\ngetValueIteratedFrom = " + v.ValueIteratedFrom +
                        "\ngetValueAtPercentile(getPercentileLevelIteratedTo()) = " +
                        histogram.GetValueAtPercentile(v.PercentileLevelIteratedTo) +
                        "\ngetPercentile = " + v.Percentile +
                        "\ngetValueAtPercentile(Percentile())" +
                        histogram.GetValueAtPercentile(v.Percentile) +
                        "\nequivalent1 = " +
                        histogram.HighestEquivalentValue(histogram.GetValueAtPercentile(v.PercentileLevelIteratedTo)) +
                        "\nequivalent2 = " +
                        histogram.HighestEquivalentValue(histogram.GetValueAtPercentile(v.Percentile)) +
                        "\n"
                        ,
                        v.ValueIteratedTo,
                        histogram.HighestEquivalentValue(histogram.GetValueAtPercentile(v.Percentile)));
            }
        }

        [Test]
        public void testLinearBucketValues()  
        {
            int index = 0;
            // Note that using linear buckets should work "as expected" as long as the number of linear buckets
            // is lower than the resolution level determined by largestValueWithSingleUnitResolution
            // (2000 in this case). Above that count, some of the linear buckets can end up rounded up in size
            // (to the nearest local resolution unit level), which can result in a smaller number of buckets that
            // expected covering the range.

            // Iterate raw data using linear buckets of 100 msec each.
            foreach (HistogramIterationValue v in rawHistogram.LinearBucketValues(100000))
            {
                long countAddedInThisBucket = v.CountAddedInThisIterationStep;
                if (index == 0) 
                {
                    Assert.assertEquals("Raw Linear 100 msec bucket # 0 added a count of 10000",
                            10000, countAddedInThisBucket);
                } 
                else if (index == 999) 
                {
                    Assert.assertEquals("Raw Linear 100 msec bucket # 999 added a count of 1",
                            1, countAddedInThisBucket);
                } 
                else 
                {
                    Assert.assertEquals("Raw Linear 100 msec bucket # " + index + " added a count of 0",
                            0 , countAddedInThisBucket);
                }
                index++;
            }
            Assert.assertEquals(1000, index);

            index = 0;
            long totalAddedCounts = 0;
            // Iterate data using linear buckets of 10 msec each.
            foreach (HistogramIterationValue v in histogram.LinearBucketValues(10000))
            {
                long countAddedInThisBucket = v.CountAddedInThisIterationStep;
                if (index == 0) 
                {
                    Assert.assertEquals("Linear 1 sec bucket # 0 [" +
                            v.ValueIteratedFrom + ".." + v.ValueIteratedTo +
                            "] added a count of 10001",
                            10001, countAddedInThisBucket);
                }
                // Because value resolution is low enough (3 digits) that multiple linear buckets will end up
                // residing in a single value-equivalent range, some linear buckets will have counts of 2 or
                // more, and some will have 0 (when the first bucket in the equivalent range was the one that
                // got the total count bump).
                // However, we can still verify the sum of counts added in all the buckets...
                totalAddedCounts += v.CountAddedInThisIterationStep;
                index++;
            }
            Assert.assertEquals("There should be 10000 linear buckets of size 10000 usec between 0 and 100 sec.",
                    10000, index);
            Assert.assertEquals("Total added counts should be 20000", 20000, totalAddedCounts);

            index = 0;
            totalAddedCounts = 0;
            // Iterate data using linear buckets of 1 msec each.
            foreach (HistogramIterationValue v in histogram.LinearBucketValues(1000)) 
            {
                long countAddedInThisBucket = v.CountAddedInThisIterationStep;
                if (index == 0) 
                {
                    Assert.assertEquals("Linear 1 sec bucket # 0 [" +
                            v.ValueIteratedFrom + ".." + v.ValueIteratedTo +
                            "] added a count of 10000",
                            10000, countAddedInThisBucket);
                }
                // Because value resolution is low enough (3 digits) that multiple linear buckets will end up
                // residing in a single value-equivalent range, some linear buckets will have counts of 2 or
                // more, and some will have 0 (when the first bucket in the equivalent range was the one that
                // got the total count bump).
                // However, we can still verify the sum of counts added in all the buckets...
                totalAddedCounts += v.CountAddedInThisIterationStep;
                index++;
            }
            // You may ask "why 100007 and not 100000?" for the value below? The answer is that at this fine
            // a linear stepping resolution, the final populated sub-bucket (at 100 seconds with 3 decimal
            // point resolution) is larger than our liner stepping, and holds more than one linear 1 msec
            // step in it.
            // Since we only know we're done with linear iteration when the next iteration step will step
            // out of the last populated bucket, there is not way to tell if the iteration should stop at
            // 100000 or 100007 steps. The proper thing to do is to run to the end of the sub-bucket quanta...
            Assert.assertEquals("There should be 100007 linear buckets of size 1000 usec between 0 and 100 sec.",
                    100007, index);
            Assert.assertEquals("Total added counts should be 20000", 20000, totalAddedCounts);
        }

        [Test]
        public void testLogarithmicBucketValues()  
        {
            int index = 0;
            // Iterate raw data using logarithmic buckets starting at 10 msec.
            foreach (HistogramIterationValue v in rawHistogram.LogarithmicBucketValues(10000, 2)) 
            {
                long countAddedInThisBucket = v.CountAddedInThisIterationStep;
                if (index == 0) 
                {
                    Assert.assertEquals("Raw Logarithmic 10 msec bucket # 0 added a count of 10000",
                            10000, countAddedInThisBucket);
                } 
                else if (index == 14) 
                {
                    Assert.assertEquals("Raw Logarithmic 10 msec bucket # 14 added a count of 1",
                            1, countAddedInThisBucket);
                } 
                else 
                {
                    Assert.assertEquals("Raw Logarithmic 100 msec bucket # " + index + " added a count of 0",
                            0, countAddedInThisBucket);
                }
                index++;
            }
            Assert.assertEquals(14, index - 1);

            index = 0;
            long totalAddedCounts = 0;
            // Iterate data using linear buckets of 1 sec each.
            foreach (HistogramIterationValue v in histogram.LogarithmicBucketValues(10000, 2)) 
            {
                long countAddedInThisBucket = v.CountAddedInThisIterationStep;
                if (index == 0) {
                    Assert.assertEquals("Logarithmic 10 msec bucket # 0 [" +
                            v.ValueIteratedFrom + ".." + v.ValueIteratedTo +
                            "] added a count of 10001",
                            10001, countAddedInThisBucket);
                }
                totalAddedCounts += v.CountAddedInThisIterationStep;
                index++;
            }
            Assert.assertEquals("There should be 14 Logarithmic buckets of size 10000 usec between 0 and 100 sec.",
                    14, index - 1);
            Assert.assertEquals("Total added counts should be 20000", 20000, totalAddedCounts);
        }

        [Test]
        public void testRecordedValues()
        {
            int index = 0;
            // Iterate raw data by stepping through every value that has a count recorded:
            foreach (HistogramIterationValue v in rawHistogram.RecordedValues()) 
            {
                long countAddedInThisBucket = v.CountAddedInThisIterationStep;
                if (index == 0) 
                {
                    Assert.assertEquals("Raw recorded value bucket # 0 added a count of 10000",
                            10000, countAddedInThisBucket);
                } 
                else 
                {
                    Assert.assertEquals("Raw recorded value bucket # " + index + " added a count of 1",
                            1, countAddedInThisBucket);
                }
                index++;
            }
            Assert.assertEquals(2, index);

            index = 0;
            long totalAddedCounts = 0;
            // Iterate data using linear buckets of 1 sec each.
            foreach (HistogramIterationValue v in histogram.RecordedValues()) 
            {
                long countAddedInThisBucket = v.CountAddedInThisIterationStep;
                if (index == 0) 
                {
                    Assert.assertEquals("Recorded bucket # 0 [" +
                            v.ValueIteratedFrom + ".." + v.ValueIteratedTo +
                            "] added a count of 10000",
                            10000, countAddedInThisBucket);
                }
                Assert.assertTrue("The count in recorded bucket #" + index + " is not 0",
                        v.CountAtValueIteratedTo != 0);
                Assert.assertEquals("The count in recorded bucket #" + index +
                        " is exactly the amount added since the last iteration ",
                        v.CountAtValueIteratedTo, v.CountAddedInThisIterationStep);
                totalAddedCounts += v.CountAddedInThisIterationStep;
                index++;
            }
            Assert.assertEquals("Total added counts should be 20000", 20000, totalAddedCounts);
        }

        [Test]
        public void testAllValues() 
        {
            int index = 0;
            long latestValueAtIndex = 0;
            // Iterate raw data by stepping through every value that ahs a count recorded:
            foreach (HistogramIterationValue v in rawHistogram.AllValues()) 
            {
                long countAddedInThisBucket = v.CountAddedInThisIterationStep;
                if (index == 1000) 
                {
                    Assert.assertEquals("Raw AllValues bucket # 0 added a count of 10000",
                            10000, countAddedInThisBucket);
                } 
                else if (histogram.ValuesAreEquivalent(v.ValueIteratedTo, 100000000)) 
                {
                    Assert.assertEquals("Raw AllValues value bucket # " + index + " added a count of 1",
                            1, countAddedInThisBucket);
                } 
                else 
                {
                    Assert.assertEquals("Raw AllValues value bucket # " + index + " added a count of 0",
                            0, countAddedInThisBucket);
                }
                latestValueAtIndex = v.ValueIteratedTo;
                index++;
            }
            Assert.assertEquals("Count at latest value iterated to is 1",
                    1, rawHistogram.GetCountAtValue(latestValueAtIndex));

            index = 0;
            long totalAddedCounts = 0;
            // Iterate data using linear buckets of 1 sec each.
            foreach (HistogramIterationValue v in histogram.AllValues()) 
            {
                long countAddedInThisBucket = v.CountAddedInThisIterationStep;
                if (index == 1000) 
                {
                    Assert.assertEquals("AllValues bucket # 0 [" +
                            v.ValueIteratedFrom + ".." + v.ValueIteratedTo +
                            "] added a count of 10000",
                            10000, countAddedInThisBucket);
                }
                Assert.assertEquals("The count in AllValues bucket #" + index +
                        " is exactly the amount added since the last iteration ",
                        v.CountAtValueIteratedTo, v.CountAddedInThisIterationStep);
                totalAddedCounts += v.CountAddedInThisIterationStep;
                index++;
            }
            Assert.assertEquals("Total added counts should be 20000", 20000, totalAddedCounts);
        }
    }
}
