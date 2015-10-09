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

namespace HdrHistogram.Examples
{
    /**
     * A simple example of using HdrHistogram: run for 20 seconds collecting the
     * time it takes to perform a simple Datagram Socket create/close operation,
     * and report a histogram of the times at the end.
     */
    public class SimpleHistogramExample
    {
        private const long NanosPerHour = TimeSpan.TicksPerHour * 100L;
        private const double MicrosPerNano = 1000.0;
        private static readonly LongHistogram Histogram = new LongHistogram(NanosPerHour, 3);
        private static volatile Socket _socket;
        private static readonly Lazy<AddressFamily> AddressFamily = new Lazy<AddressFamily>(() => GetAddressFamily("google.com"));

        private static readonly TimeSpan WarmUpPeriod = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan RunPeriod = TimeSpan.FromSeconds(20);

        static void RecordLatency(Action action)
        {
            var startTime = Stopwatch.GetTimestamp();
            action();
            long elapsedNanos = (Stopwatch.GetTimestamp() - startTime) * 100;
            Histogram.RecordValue(elapsedNanos);
        }
        static void CreateAndCloseDatagramSocket()
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
                _socket.Close();
            }
        }

        private static AddressFamily GetAddressFamily(string url)
        {
            var hostIpAddress = Dns.GetHostEntry(url).AddressList[0];
            var hostIpEndPoint = new IPEndPoint(hostIpAddress, 80);
            return hostIpEndPoint.AddressFamily;
        }

        public static void Run()
        {
            var timer = Stopwatch.StartNew();

            do
            {
                RecordLatency(CreateAndCloseDatagramSocket);
            } while (timer.Elapsed < WarmUpPeriod);

            Histogram.Reset();

            do
            {
                RecordLatency(CreateAndCloseDatagramSocket);
            } while (timer.Elapsed < RunPeriod);

            Console.WriteLine("Recorded latencies [in usec] for Create+Close of a DatagramSocket:");

            var size = Histogram.GetEstimatedFootprintInBytes();
            Console.WriteLine("Histogram size = {0} bytes ({1:F2} MB)", size, size / 1024.0 / 1024.0);

            Histogram.OutputPercentileDistribution(Console.Out, outputValueUnitScalingRatio: MicrosPerNano);
        }
    }
}
