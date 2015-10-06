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
using System.Diagnostics;
using System.Threading;
using HdrHistogram.Utilities;
using NUnit.Framework;

namespace HdrHistogram.Test
{
    /**
     * JUnit test for {@link Histogram}
     */
    [Category("Performance")]
    public class HistogramPerfTest
    {
        /// <summary> 3,600,000,000 (3600L * 1000 * 1000, e.g. for 1 hr in usec units) </summary>
        private const long HighestTrackableValue = 3600L * 1000 * 1000; // e.g. for 1 hr in usec units
        private const int NumberOfSignificantValueDigits = 3;
        private const long TestValueLevel = 12340;
        private const long WarmupLoopLength = 50 * 1000;
        private const long RawtimingLoopCount = 400 * 1000 * 1000L;
        private const long SynchronizedTimingLoopCount = 40 * 1000 * 1000L;
        private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

        [Test]
        public void TestRawRecordingSpeed()
        {
            HistogramBase histogram = new LongHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            Console.WriteLine("\n\nTiming Histogram:");
            TestRawRecordingSpeedAtExpectedInterval("Histogram: ", histogram, 1000000000, RawtimingLoopCount);

            // Check that the histogram contains as many values are we wrote to it
            Assert.AreEqual(RawtimingLoopCount, histogram.TotalCount);
        }

        [Test]
        public void TestRawSyncronizedRecordingSpeed()
        {
            HistogramBase histogram = new SynchronizedHistogram(HighestTrackableValue, NumberOfSignificantValueDigits);
            Console.WriteLine("\n\nTiming SynchronizedHistogram:");
            TestRawRecordingSpeedAtExpectedInterval("SynchronizedHistogram: ", histogram, 1000000000, SynchronizedTimingLoopCount);

            // Check that the histogram contains as many values are we wrote to it
            Assert.AreEqual(SynchronizedTimingLoopCount, histogram.TotalCount);
        }

        //[Test]
        //public void testRawSyncronizedRecordingSpeedMultithreaded()
        //{
        //    HistogramBase histogram;
        //    histogram = new SynchronizedHistogram(highestTrackableValue, numberOfSignificantValueDigits);
        //    Console.WriteLine("\n\nTiming SynchronizedHistogram - Multithreaded:");

        //    var task1 = Task.Factory.StartNew(() =>
        //        testRawRecordingSpeedAtExpectedInterval("SynchronizedHistogram: ", histogram, 1000000000, synchronizedTimingLoopCount, assertNoGC: false, multiThreaded: true));
        //    var task2 = Task.Factory.StartNew(() =>
        //        testRawRecordingSpeedAtExpectedInterval("SynchronizedHistogram: ", histogram, 1000000000, synchronizedTimingLoopCount, assertNoGC: false, multiThreaded: true));
        //    var task3 = Task.Factory.StartNew(() =>
        //        testRawRecordingSpeedAtExpectedInterval("SynchronizedHistogram: ", histogram, 1000000000, synchronizedTimingLoopCount, assertNoGC: false, multiThreaded: true));

        //    Task.WaitAll(task1, task2, task3);

        //    // Check that the histogram contains as many values are we wrote to it
        //    Assert.AreEqual(synchronizedTimingLoopCount * 3L, histogram.getTotalCount());
        //}

        //[Test]
        //public void testRawAtomicRecordingSpeedMultithreaded()
        //{
        //    HistogramBase histogram;
        //    histogram = new AtomicHistogram(highestTrackableValue, numberOfSignificantValueDigits);
        //    Console.WriteLine("\n\nTiming AtomicHistogram - Multithreaded:");

        //    var task1 = Task.Factory.StartNew(() =>
        //        testRawRecordingSpeedAtExpectedInterval("AtomicHistogram: ", histogram, 1000000000, atomicTimingLoopCount, assertNoGC: false, multiThreaded: true));
        //    var task2 = Task.Factory.StartNew(() =>
        //        testRawRecordingSpeedAtExpectedInterval("AtomicHistogram: ", histogram, 1000000000, atomicTimingLoopCount, assertNoGC: false, multiThreaded: true));
        //    var task3 = Task.Factory.StartNew(() =>
        //        testRawRecordingSpeedAtExpectedInterval("AtomicHistogram: ", histogram, 1000000000, atomicTimingLoopCount, assertNoGC: false, multiThreaded: true));

        //    Task.WaitAll(task1, task2, task3);

        //    // Check that the histogram contains as many values are we wrote to it
        //    Assert.AreEqual(atomicTimingLoopCount * 3L, histogram.getTotalCount());
        //}

        [Test]
        public void TestLeadingZerosSpeed()
        {
            Console.WriteLine("\nTiming LeadingZerosSpeed :");
            long deltaUsec = MicrosecondsToExecute(() => LeadingZerosSpeedLoop(WarmupLoopLength));
            long rate = 1000000 * WarmupLoopLength / deltaUsec;
            Console.WriteLine("Warmup:\n{0:N0} Leading Zero loops completed in {1:N0} usec, rate = {2:N0} value recording calls per sec.", WarmupLoopLength, deltaUsec, rate);
            // Wait a bit to make sure compiler had a chance to do it's stuff:
            try
            {
                Thread.Sleep(1000);
            }
            catch (Exception)
            {
            }

            var gcBefore = PrintGcAndMemoryStats("GC Before");
            var loopCount = RawtimingLoopCount;
            deltaUsec = MicrosecondsToExecute(() => LeadingZerosSpeedLoop(loopCount));
            var gcAfter = PrintGcAndMemoryStats("GC After ");
            // Each time round the loop, LeadingZerosSpeedLoop calls MiscUtils.NumberOfLeadingZeros(..) 8 times
            rate = 1000000 * loopCount / deltaUsec;

            Console.WriteLine("Hot code timing:");
            Console.WriteLine("{0:N0} leading Zero loops completed in {1:N0} usec, rate = {2:N0} value recording calls per sec.", loopCount, deltaUsec, rate);

            // TODO work out why we always seems to get at least 1 GC here, maybe it's due to the length of the test run??
            Assert.LessOrEqual(gcAfter.Gen0 - gcBefore.Gen0, 1, "There should be at MOST 1 Gen0 GC Collections");
            Assert.LessOrEqual(gcAfter.Gen1 - gcBefore.Gen1, 1, "There should be at MOST 1 Gen1 GC Collections");
            Assert.LessOrEqual(gcAfter.Gen2 - gcBefore.Gen2, 1, "There should be at MOST 1 Gen2 GC Collections");
        }


        private static void TestRawRecordingSpeedAtExpectedInterval(String label, HistogramBase histogram,
                                                            long expectedInterval, long timingLoopCount,
                                                            bool assertNoGc = true, bool multiThreaded = false)
        {
            Console.WriteLine("\nTiming recording speed with expectedInterval = " + expectedInterval + " :");
            // Warm up:
            long deltaUsec = MicrosecondsToExecute(
                    () => RecordLoopWithExpectedInterval(histogram, WarmupLoopLength, expectedInterval));


            long rate = 1000000 * WarmupLoopLength / deltaUsec;
            Console.WriteLine("{0}Warmup:\n{1:N0} value recordings completed in {2:N0} usec, rate = {3:N0} value recording calls per sec.",
                                label, WarmupLoopLength, deltaUsec, rate);
            histogram.Reset();
            // Wait a bit to make sure compiler had a chance to do it's stuff:
            try
            {
                Thread.Sleep(1000);
            }
            catch (Exception)
            {
            }

            var gcBefore = PrintGcAndMemoryStats("GC Before");
            deltaUsec = MicrosecondsToExecute(
                () => RecordLoopWithExpectedInterval(histogram, timingLoopCount, expectedInterval));
            var gcAfter = PrintGcAndMemoryStats("GC After ");

            rate = 1000000 * timingLoopCount / deltaUsec;

            Console.WriteLine(label + "Hot code timing:");
            Console.WriteLine("{0}{1:N0} value recordings completed in {2:N0} usec, rate = {3:N0} value recording calls per sec.",
                                label, timingLoopCount, deltaUsec, rate);
            if (multiThreaded == false)
            {
                rate = 1000000 * histogram.TotalCount / deltaUsec;
                Console.WriteLine("{0}{1:N0} raw recorded entries completed in {2:N0} usec, rate = {3:N0} recorded values per sec.",
                                    label, histogram.TotalCount, deltaUsec, rate);
            }

            if (assertNoGc)
            {
                // TODO work out why we always seems to get at least 1 GC here, maybe it's due to the length of the test run??
                Assert.LessOrEqual(gcAfter.Gen0 - gcBefore.Gen0, 1, "There should be at MOST 1 Gen0 GC Collections");
                Assert.LessOrEqual(gcAfter.Gen1 - gcBefore.Gen1, 1, "There should be at MOST 1 Gen1 GC Collections");
                Assert.LessOrEqual(gcAfter.Gen2 - gcBefore.Gen2, 1, "There should be at MOST 1 Gen2 GC Collections");
            }
        }

        private static GcInfo PrintGcAndMemoryStats(string label)
        {
            var bytesUsed = GC.GetTotalMemory(forceFullCollection: false);
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);
            Console.WriteLine("{0}: {1:0.00} MB ({2:N0} bytes), Gen0 {3}, Gen1 {4}, Gen2 {5}",
                                label, bytesUsed / 1024.0 / 1024.0, bytesUsed, gen0, gen1, gen2);

            return new GcInfo(gen0, gen1, gen2);
        }

        private static void RecordLoopWithExpectedInterval(HistogramBase histogram, long loopCount, long expectedInterval)
        {
            for (long i = 0; i < loopCount; i++)
                histogram.RecordValueWithExpectedInterval(TestValueLevel + (i & 0x8000), expectedInterval);
        }

        private static long LeadingZerosSpeedLoop(long loopCount)
        {
            long sum = 0;
            for (long i = 0; i < loopCount; i++)
            {
                long val = TestValueLevel;
                sum += MiscUtilities.NumberOfLeadingZeros(val);
                sum += MiscUtilities.NumberOfLeadingZeros(val);
                sum += MiscUtilities.NumberOfLeadingZeros(val);
                sum += MiscUtilities.NumberOfLeadingZeros(val);
                sum += MiscUtilities.NumberOfLeadingZeros(val);
                sum += MiscUtilities.NumberOfLeadingZeros(val);
                sum += MiscUtilities.NumberOfLeadingZeros(val);
                sum += MiscUtilities.NumberOfLeadingZeros(val);
            }
            return sum;
        }

        private static long MicrosecondsToExecute(Action action)
        {
            // 1 millisecond (ms) = 1000 microsoecond (µs or usec)
            // 1 microsecond (µs or usec) = 1000 nanosecond (ns or nsec)
            // 1 second = 1,000,000 usec or 1,000 ms

            var startTs = Stopwatch.GetTimestamp();
            action();
            var deltaTicks = Stopwatch.GetTimestamp() - startTs;
            return deltaTicks * TicksPerMicrosecond;
        }

        private class GcInfo
        {
            public int Gen0 { get; }
            public int Gen1 { get; }
            public int Gen2 { get; }

            public GcInfo(int gen0, int gen1, int gen2)
            {
                Gen0 = gen0;
                Gen1 = gen1;
                Gen2 = gen2;
            }
        }

        public static void Main(string[] args)
        {
            try
            {
                HistogramPerfTest test = new HistogramPerfTest();
                test.TestRawRecordingSpeed();
                Console.WriteLine("");
                test.TestRawSyncronizedRecordingSpeed();
                Console.WriteLine("");
                test.TestLeadingZerosSpeed();
                Console.WriteLine("");

                //Thread.sleep(1000000);
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e);
            }
        }
    }
}