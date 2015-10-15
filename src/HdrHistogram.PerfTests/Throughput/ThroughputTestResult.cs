using System;
using System.Diagnostics;

namespace HdrHistogram.PerfTests.Throughput
{
    public sealed class ThroughputTestResult
    {
        public string Label { get; private set; }
        public long Messages { get; private set; }
        public TimeSpan Elapsed { get; private set; }
        public GcInfo GarbageCollections { get; private set; }

        public static ThroughputTestResult Capture(string label, long messages, Action action)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            //TODO : This obviously allocates. Change to values on the stack. -LC
            var initial = GcInfo.Current();
            var start = Stopwatch.GetTimestamp();
            action();
            var elapsedTicks = Stopwatch.GetTimestamp() - start;
            var gcDelta = GcInfo.Current().Delta(initial);

            return new ThroughputTestResult
            {
                Label =  label,
                Messages = messages,
                Elapsed = TimeSpan.FromTicks(elapsedTicks),
                GarbageCollections = gcDelta
            };
        }
    }
}