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
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace HdrHistogram.Examples
{
    /**
     * A simple example of using HdrHistogram: run for 20 seconds collecting the
     * time it takes to perform a simple Datagram Socket create/close operation,
     * and report a histogram of the times at the end.
     */
    static class SimpleHistogramExample
    {
        private static readonly LongHistogram Histogram = new LongHistogram(TimeSpan.TicksPerHour, 3);
        private static volatile Socket _socket;
        private static readonly Lazy<AddressFamily> AddressFamily = new Lazy<AddressFamily>(() => GetAddressFamily("google.com"));

        private static readonly TimeSpan RunPeriod = TimeSpan.FromSeconds(10);

        public static void Run()
        {
            Console.WriteLine($"Running for {RunPeriod.TotalSeconds}sec.");

            RecordMeasurements();

            OutputMeasurements();
        }

        /// <summary>
        /// Shows a sample loop where an action is executed, and the latency of each execution is recorded.
        /// </summary>
        private static void RecordMeasurements()
        {
            var timer = Stopwatch.StartNew();
            do
            {
                Histogram.RecordLatency(CreateAndCloseDatagramSocket);
            } while (timer.Elapsed < RunPeriod);
        }

        /// <summary>
        /// Write to the console the Memory footprint of the histogram instance and
        /// the percentile distribution of all the recorded values.
        /// </summary>
        private static void OutputMeasurements()
        {
            var size = Histogram.GetEstimatedFootprintInBytes();
            Console.WriteLine("Histogram size = {0} bytes ({1:F2} MB)", size, size / 1024.0 / 1024.0);

            Console.WriteLine("Recorded latencies [in ticks] for Create+Close of a DatagramSocket:");
            Histogram.OutputPercentileDistribution(Console.Out, outputValueUnitScalingRatio: OutputScalingFactor.None);
            Console.WriteLine();

            Console.WriteLine("Recorded latencies [in usec] for Create+Close of a DatagramSocket:");
            Histogram.OutputPercentileDistribution(Console.Out, outputValueUnitScalingRatio: OutputScalingFactor.TicksToMicroseconds);
            Console.WriteLine();

            Console.WriteLine("Recorded latencies [in msec] for Create+Close of a DatagramSocket:");
            Histogram.OutputPercentileDistribution(Console.Out, outputValueUnitScalingRatio: OutputScalingFactor.TicksToMilliseconds);
            Console.WriteLine();

            Console.WriteLine("Recorded latencies [in sec] for Create+Close of a DatagramSocket:");
            Histogram.OutputPercentileDistribution(Console.Out, outputValueUnitScalingRatio: OutputScalingFactor.TicksToSeconds);
        }

        private static void CreateAndCloseDatagramSocket()
        {
            try
            {
                _socket = new Socket(AddressFamily.Value, SocketType.Stream, ProtocolType.Tcp);
            }
            catch (SocketException)
            {
            }
            finally
            {
                _socket.Dispose();
            }
        }

        private static AddressFamily GetAddressFamily(string url)
        {
            var ipHostEntries = Dns.GetHostEntryAsync(url).Result;
            var hostIpAddress = ipHostEntries.AddressList[0];
            var hostIpEndPoint = new IPEndPoint(hostIpAddress, 80);
            return hostIpEndPoint.AddressFamily;
        }
    }
}
