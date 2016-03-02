/*
 * Written by Matt Warren, and released to the public domain,
 * as explained at
 * http://creativecommons.org/publicdomain/zero/1.0/
 *
 * This is a .NET port of the original Java version, which was written by
 * Gil Tene as described in
 * https://github.com/HdrHistogram/HdrHistogram
 */

using HdrHistogram.Encoding;
using HdrHistogram.Utilities;
using NUnit.Framework;

namespace HdrHistogram.UnitTests
{
    public abstract class HistogramEncodingTestBase<T> where T : HistogramBase
    {
        private static readonly HistogramEncoderV2 EncoderV2 = new Encoding.HistogramEncoderV2();
        private const long HighestTrackableValue = 3600L * 1000 * 1000; // e.g. for 1 hr in usec units

        protected abstract T Create(long highestTrackableValue, int numberOfSignificantDigits);

        [Test]
        public void Given_a_populated_Histogram_When_encoded_and_decoded_Then_data_is_preserved()
        {
            var source = Create(HighestTrackableValue, 3);
            Load(source);
            var result = EncodeDecode(source);
            HistogramAssert.AreEqual(source, result);
        }

        [Test]
        public void Given_a_populated_Histogram_When_encoded_and_decoded_with_compression_Then_data_is_preserved()
        {
            var source = Create(HighestTrackableValue, 3);
            Load(source);
            var result = CompressedEncodeDecode(source);
            HistogramAssert.AreEqual(source, result);
        }

        [Test]
        public void Given_a_Histogram_populated_with_full_range_of_values_When_encoded_and_decoded_Then_data_is_preserved()
        {
            var source = Create(HighestTrackableValue, 3);
            LoadFullRange(source);
            var result = EncodeDecode(source);
            HistogramAssert.AreEqual(source, result);
        }

        [Test]
        public void Given_a_Histogram_populated_with_full_range_of_values_When_encoded_and_decoded_with_compression_Then_data_is_preserved()
        {
            var source = Create(HighestTrackableValue, 3);
            LoadFullRange(source);
            var result = CompressedEncodeDecode(source);
            HistogramAssert.AreEqual(source, result);
        }

        private static T EncodeDecode(T source)
        {
            var targetBuffer = ByteBuffer.Allocate(source.GetNeededByteBufferCapacity());
            source.Encode(targetBuffer, EncoderV2);
            targetBuffer.Position = 0;
            return Histogram.DecodeFromByteBuffer<T>(targetBuffer, 0);
        }

        private static T CompressedEncodeDecode(T source)
        {
            var targetBuffer = ByteBuffer.Allocate(source.GetNeededByteBufferCapacity());
            source.EncodeIntoCompressedByteBuffer(targetBuffer);
            targetBuffer.Position = 0;
            return Histogram.DecodeFromCompressedByteBuffer<T>(targetBuffer, 0);
        }

        private static void Load(T source)
        {
            for (long i = 0L; i < 10000L; i++)
            {
                source.RecordValue(1000L * i);
            }
        }

        private static void LoadFullRange(T source)
        {
            for (long i = 0L; i < HighestTrackableValue; i += 100L)
            {
                source.RecordValue(i);
            }
            source.RecordValue(HighestTrackableValue);
        }

    }

    [TestFixture]
    public sealed class ShortHistogramEncodingTests : HistogramEncodingTestBase<ShortHistogram>
    {
        protected override ShortHistogram Create(long highestTrackableValue, int numberOfSignificantDigits)
        {
            return new ShortHistogram(highestTrackableValue, numberOfSignificantDigits);
        }
    }

    [TestFixture]
    public sealed class IntHistogramEncodingTests : HistogramEncodingTestBase<IntHistogram>
    {
        protected override IntHistogram Create(long highestTrackableValue, int numberOfSignificantDigits)
        {
            return new IntHistogram(highestTrackableValue, numberOfSignificantDigits);
        }
    }

    [TestFixture]
    public sealed class LongHistogramEncodingTests : HistogramEncodingTestBase<LongHistogram>
    {
        protected override LongHistogram Create(long highestTrackableValue, int numberOfSignificantDigits)
        {
            return new LongHistogram(highestTrackableValue, numberOfSignificantDigits);
        }
    }

    [TestFixture]
    public sealed class SynchronizedHistogramEncodingTests : HistogramEncodingTestBase<SynchronizedHistogram>
    {
        protected override SynchronizedHistogram Create(long highestTrackableValue, int numberOfSignificantDigits)
        {
            return new SynchronizedHistogram(highestTrackableValue, numberOfSignificantDigits);
        }
    }
}
