using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HdrHistogram.Utilities;

namespace HdrHistogram
{
    
    public class HistogramLogWriter
    {
        private const string HistogramLogFormatVersion = "1.2";
        
        
        private readonly TextWriter _log;

        /// <summary>
        /// Creates a <see cref="HistogramLogWriter"/> that writes to an underlying <see cref="Stream"/>.
        /// </summary>
        /// <param name="outputStream"></param>
        public HistogramLogWriter(Stream outputStream)
        {
            _log = new StreamWriter(outputStream);
        }

        /// <summary>
        /// Output a log format version to the log.
        /// </summary>
        public void OutputLogFormatVersion()
        {
            _log.WriteLine($"#[Histogram log format version {HistogramLogFormatVersion}]");
        }

        /// <summary>
        /// Log a start time in the log.
        /// </summary>
        /// <param name="startTimeWritten">Time the log was started.</param>
        public void OutputStartTime(DateTimeOffset startTimeWritten)
        {
            var secondsSinceEpoch = startTimeWritten.SecondsSinceUnixEpoch();
            _log.WriteLine($"#[StartTime: {secondsSinceEpoch:F3} (seconds since epoch), {startTimeWritten:o}]");
        }

        public void OutputLegend()
        {
            throw new System.NotImplementedException();
        }
    }

    public class HistogramLogReader
    {
        private TextReader _log;
        private static readonly Regex StartTimeMatcher = new Regex(@"#\[StartTime: (?<seconds>\d*\.\d{1,3}) ", RegexOptions.Compiled);
        private static readonly Regex BaseTimeMatcher = new Regex(@"#\[BaseTime: (?<seconds>\d*\.\d{1,3}) ", RegexOptions.Compiled);
        //Content lines - format =  startTimestamp, intervalLength, maxTime, histogramPayload
        private static readonly Regex LogLineMatcher = new Regex(@"(?<startTime>\d*\.\d*),(?<interval>\d*\.\d*),(?<max>\d*\.\d*),(?<payload>.*)", RegexOptions.Compiled);


        public HistogramLogReader(Stream inputStream)
        {
            _log = new StreamReader(inputStream);

        }

        public IEnumerable<HistogramBase> NextIntervalHistogram(bool isAbsolute = true)
        {
            double startTimeInSeconds = 0;
            double baseTimeInSeconds = 0;
            bool hasStartTime = false;
            bool hasBaseTime = false;
            foreach (var line in ReadLines())
            {
                //Comments (and header metadata)
                if (IsComment(line))
                {
                    if (IsStartTime(line))
                    {
                        startTimeInSeconds = ParseStartTime(line);
                        hasStartTime = true;
                    }
                    else if(IsBaseTime(line))
                    {
                        baseTimeInSeconds = ParseBaseTime(line);
                        hasBaseTime = true;
                    }
                }
                //Legend/Column headers
                else if(IsLegend(line))
                {
                    //Ignore
                }

                else
                {
                    //Content lines - format =  startTimestamp, intervalLength, maxTime, histogramPayload

                    var match = LogLineMatcher.Match(line);
                    var logTimeStampInSec = ParseDouble(match,"startTime");
                    var intervalLength = ParseDouble(match, "interval");
                    var maxTime = ParseDouble(match, "max");    //Ignored as it can be inferred -LC
                    var payload = match.Groups["payload"].Value;

                    if (!hasStartTime)
                    {
                        // No explicit start time noted. Use 1st observed time:
                        startTimeInSeconds = logTimeStampInSec;
                        hasStartTime = true;
                    }
                    if (!hasBaseTime)
                    {
                        // No explicit base time noted. Deduce from 1st observed time (compared to start time):
                        if (logTimeStampInSec < startTimeInSeconds - (365 * 24 * 3600.0))
                        //if (UnixTimeExtensions.ToDateFromSecondsSinceEpoch(logTimeStampInSec) < startTime.AddYears(1))
                        {
                            // Criteria Note: if log timestamp is more than a year in the past (compared to
                            // StartTime), we assume that timestamps in the log are not absolute
                            baseTimeInSeconds = startTimeInSeconds;
                        }
                        else {
                            // Timestamps are absolute
                            baseTimeInSeconds = 0.0;
                        }
                        hasBaseTime = true;
                    }

                    double absoluteStartTimeStampSec = logTimeStampInSec + baseTimeInSeconds;
                    double offsetStartTimeStampSec = absoluteStartTimeStampSec - startTimeInSeconds;
                    double absoluteEndTimeStampSec = absoluteStartTimeStampSec + intervalLength;
                    double startTimeStampToCheckRangeOn = isAbsolute ? absoluteStartTimeStampSec : offsetStartTimeStampSec;


                    //TODO: Port what ever this is -LC
                    //if (startTimeStampToCheckRangeOn < rangeStartTimeSec)
                    //{
                    //    scanner.nextLine();
                    //    continue;
                    //}

                    //if (startTimeStampToCheckRangeOn > rangeEndTimeSec)
                    //{
                    //    return null;
                    //}

                    byte[] bytes = Convert.FromBase64String(payload);
                    var buffer = ByteBuffer.Allocate(bytes);
                    var histogram = DecodeHistogram(buffer, 0);
                    histogram.StartTimeStamp = (long)(absoluteStartTimeStampSec * 1000.0);
                    histogram.EndTimeStamp = (long)(absoluteEndTimeStampSec * 1000.0);
                    yield return histogram;
                }
            }
        }


        private static HistogramBase DecodeHistogram(ByteBuffer buffer, long minBarForHighestTrackableValue)
        {
            //int cookie = buffer.GetInt();
            //if (IsCompressedDoubleHistogramCookie(cookie) || IsNonCompressedDoubleHistogramCookie(cookie))
            //{
            //    throw new NotSupportedException();
            //    //return typeof (DoubleHistogram);
            //}
            //else
            //{
                return Histogram.DecodeFromCompressedByteBuffer(buffer, minBarForHighestTrackableValue);
            //}
        }




        private const int UncompressedDoubleHistogramEncodingCookie = 0x0c72124e;
        private const int CompressedDoubleHistogramEncodingCookie = 0x0c72124f;
        private static bool IsCompressedDoubleHistogramCookie(int cookie)
        {
            return (cookie == CompressedDoubleHistogramEncodingCookie);
        }
        private static bool IsNonCompressedDoubleHistogramCookie(int cookie)
        {
            return cookie == UncompressedDoubleHistogramEncodingCookie;
        }

        private static bool IsComment(string line)
        {
            return line.StartsWith("#");
        }

        private static bool IsStartTime(string line)
        {
            return line.StartsWith("#[StartTime: ");
        }

        private static bool IsBaseTime(string line)
        {
            return line.StartsWith("#[BaseTime: ");
        }

        private static bool IsLegend(string line)
        {
            var legend = "\"StartTimestamp\",\"Interval_Length\",\"Interval_Max\",\"Interval_Compressed_Histogram\"";
            return line.Equals(legend);
        }

        private static double ParseStartTime(string line)
        {
            var match = StartTimeMatcher.Match(line);
            return ParseDouble(match,"seconds");
        }

        private static double ParseBaseTime(string line)
        {
            var match = BaseTimeMatcher.Match(line);
            return ParseDouble(match, "seconds");
        }

        public DateTimeOffset GetStartTime()
        {
            throw new System.NotImplementedException();
        }

        private IEnumerable<string> ReadLines()
        {
            while (true)
            {
                var line = _log.ReadLine();
                if (line == null)
                    yield break;
                yield return line;
            }
        }

        private static double ParseDouble(Match match, string group)
        {
            var value = match.Groups[group].Value;
            return double.Parse(value);
        }
    }
}