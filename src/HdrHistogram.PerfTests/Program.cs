using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HdrHistogram.PerfTests.Throughput;

namespace HdrHistogram.PerfTests
{
    class Program
    {
        private static readonly int[] RunSizes =
        {
            //10000000,
            //100000000,
            1000000000,
        };

        public static void Main()
        {
            var testTypes = GetHistorgramThroughputTests();

            var testResultRunner = from testType in testTypes
                                   from messageCount in RunSizes
                                   select testType.MeasureRawRecordingSpeed(messageCount);

            Console.WriteLine("Running warm up cycle...");
            var warmup = testResultRunner.ToArray();
            Console.WriteLine($"Warm up cycle complete. {warmup.Length}");

            Console.WriteLine("Running tests...");
            var testResults = testResultRunner.ToArray();
            Console.WriteLine("Tests complete.");

            var outputCsvPath = "PerfTestResults.csv";

            Console.WriteLine($"Writing results to '{outputCsvPath}'");
            PrintToCsv(outputCsvPath, testResults);
            Console.WriteLine("Complete.");
        }

        private static IEnumerable<HistogramThoughputTestBase> GetHistorgramThroughputTests()
        {
            var testTypes = from module in typeof(Program).Assembly.GetModules()
                            from type in module.GetTypes()
                            where !type.IsAbstract
                            where type.IsSubclassOf(typeof(Throughput.HistogramThoughputTestBase))
                            select (HistogramThoughputTestBase)Activator.CreateInstance(type, true);

            return testTypes.ToArray();
        }

        private static void PrintToCsv(string outputCsvPath, IEnumerable<ThroughputTestResult> testResults)
        {
            var sb = new StringBuilder();
            //Header
            sb.AppendLine($"Label,Messages,TotalSeconds,Gen0Collections,Gen1Collections,Gen2Collections,TotalBytesAllocated");
            foreach (var testResult in testResults)
            {
                sb.AppendLine($"{testResult.Label},{testResult.Messages},{testResult.Elapsed.TotalSeconds},{testResult.GarbageCollections.Gen0},{testResult.GarbageCollections.Gen1},{testResult.GarbageCollections.Gen2},{testResult.GarbageCollections.TotalBytesAllocated}");
            }
            File.WriteAllText(outputCsvPath, sb.ToString());
        }
    }
}
