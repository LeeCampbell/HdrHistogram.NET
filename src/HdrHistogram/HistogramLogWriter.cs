using System;
using System.IO;
using HdrHistogram.Utilities;

namespace HdrHistogram
{
    public sealed class HistogramLogWriter : IDisposable
    {
        private const string HistogramLogFormatVersion = "1.2";

        private readonly TextWriter _log;

        /// <summary>
        /// Creates a <see cref="HistogramLogWriter"/> that writes to an underlying <see cref="Stream"/>.
        /// </summary>
        /// <param name="outputStream"></param>
        public HistogramLogWriter(Stream outputStream)
        {
            //TODO: Validate the Encoding. -LC
            _log = new StreamWriter(outputStream, System.Text.Encoding.BigEndianUnicode);
            _log.NewLine = "\n";
        }

        public void Write(DateTime startTime, params HistogramBase[] histograms)
        {
            WriteLogFormatVersion();
            WriteStartTime(startTime);
            //WriteLogFormatVersion();
            WriteLegend();
            foreach (var histogram in histograms)
            {
                WriteHistogram(histogram);
            }
        }
        
        /// <summary>
        /// Output a log format version to the log.
        /// </summary>
        private void WriteLogFormatVersion()
        {
            _log.WriteLine($"#[Histogram log format version {HistogramLogFormatVersion}]");
            _log.Flush();
        }

        /// <summary>
        /// Log a start time in the log.
        /// </summary>
        /// <param name="startTimeWritten">Time the log was started.</param>
        private void WriteStartTime(DateTime startTimeWritten)
        {
            var secondsSinceEpoch = startTimeWritten.SecondsSinceUnixEpoch();
            _log.WriteLine($"#[StartTime: {secondsSinceEpoch:F3} (seconds since epoch), {startTimeWritten:o}]");
            _log.Flush();
        }

        private void WriteLegend()
        {
            _log.WriteLine("\"StartTimestamp\",\"Interval_Length\",\"Interval_Max\",\"Interval_Compressed_Histogram\"");
            _log.Flush();
        }

        private void WriteHistogram( HistogramBase histogram)
        {
            var targetBuffer = ByteBuffer.Allocate(histogram.GetNeededByteBufferCapacity());
            var compressedLength = histogram.EncodeIntoCompressedByteBuffer(targetBuffer);
            byte[] compressedArray = new byte[compressedLength];
            targetBuffer.BlockGet(compressedArray, 0, 0, compressedLength);

            var startTimeStampSec = histogram.StartTimeStamp/1000.0;
            var endTimeStampSec = histogram.EndTimeStamp /1000.0;
            var intervalLength = endTimeStampSec - startTimeStampSec;
            var maxValueUnitRatio = 1000000.0;
            var intervalMax = histogram.GetMaxValue()/maxValueUnitRatio;
            
            var binary = Convert.ToBase64String(compressedArray);
            var payload = $"{startTimeStampSec:F3},{intervalLength:F3},{intervalMax:F3},{binary}";
            _log.WriteLine(payload);
            _log.Flush();
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            using (_log) { }
        }
    }
}