using System;

namespace HdrHistogram.Utilities
{
    /// <summary>
    /// Provides helper methods for working with times in Unix Epoch convention.
    /// </summary>
    public static class UnixTimeExtensions
    {
        private const long EpochInTicks = 621355968000000000;

        /// <summary>
        /// Gets the seconds elapsed since the Unix Epoch (01-Jan-1970 UTC)
        /// </summary>
        /// <param name="source">The source time.</param>
        /// <returns>Returns the number whole and partial seconds elapsed since the Unix Epoch.</returns>
        public static double SecondsSinceUnixEpoch(this DateTimeOffset source)
        {
            return (source.UtcTicks - EpochInTicks) / (double)TimeSpan.TicksPerSecond;
        }

        /// <summary>
        /// Returns the date and time specified by the seconds since the Unix Epoch
        /// </summary>
        /// <param name="secondsSinceUnixEpoch">The seconds since epoch</param>
        /// <returns>A DateTime value in UTC kind.</returns>
        public static DateTime ToDateFromSecondsSinceEpoch(double secondsSinceUnixEpoch)
        {
            var ticks = secondsSinceUnixEpoch * TimeSpan.TicksPerSecond;
            return new DateTime((long)ticks, DateTimeKind.Utc);
        }
    }
}