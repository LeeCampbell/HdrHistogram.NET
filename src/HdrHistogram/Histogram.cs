using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using HdrHistogram.Encoding;
using HdrHistogram.Utilities;

namespace HdrHistogram
{
    public static class Histogram
    {
        private const int UncompressedDoubleHistogramEncodingCookie = 0x0c72124e;
        private const int CompressedDoubleHistogramEncodingCookie = 0x0c72124f;

        private const int EncodingCookieBaseV2 = 0x1c849303;
        private const int EncodingCookieBaseV1 = 0x1c849301;
        private const int EncodingCookieBaseV0 = 0x1c849308;

        private const int CompressedEncodingCookieBaseV0 = 0x1c849309;
        private const int CompressedEncodingCookieBaseV1 = 0x1c849302;
        private const int CompressedEncodingCookieBaseV2 = 0x1c849304;
        private const int EncodingHeaderSizeV0 = 32;
        private const int EncodingHeaderSizeV1 = 40;
        private const int EncodingHeaderSizeV2 = 40;

        private const int V2MaxWordSizeInBytes = 9; // LEB128-64b9B + ZigZag require up to 9 bytes per word
        private const int Rfc1950HeaderLength = 2;

        private static readonly Type[] HistogramClassConstructorArgsTypes = { typeof(long), typeof(long), typeof(int) };

        /// <summary>
        /// Construct a new histogram by decoding it from a compressed form in a ByteBuffer.
        /// </summary>
        /// <param name="buffer">The buffer to decode from</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <returns>The newly constructed histogram</returns>
        public static T DecodeFromCompressedByteBuffer<T>(ByteBuffer buffer, long minBarForHighestTrackableValue) where T : HistogramBase
        {
            var cookie = buffer.GetInt();
            var headerSize = GetHeaderSize(cookie);
            var lengthOfCompressedContents = buffer.GetInt();
            T histogram;
            //Skip the first two bytes (from the RFC 1950 specification) and move to the deflate specification (RFC 1951)
            //  http://george.chiramattel.com/blog/2007/09/deflatestream-block-length-does-not-match.html
            using (var inputStream = new MemoryStream(buffer.ToArray(), buffer.Position + Rfc1950HeaderLength, lengthOfCompressedContents - Rfc1950HeaderLength))
            using (var decompressor = new DeflateStream(inputStream, CompressionMode.Decompress))
            {
                var headerBuffer = ByteBuffer.Allocate(headerSize);
                headerBuffer.ReadFrom(decompressor, headerSize);
                histogram = DecodeFromByteBuffer<T>(headerBuffer, minBarForHighestTrackableValue, decompressor);
                var countsLength = histogram.GetNeededByteBufferCapacity() - headerSize;
            }
            return histogram;
        }
        
        /// <summary>
        /// Construct a new histogram by decoding it from a <see cref="ByteBuffer"/>.
        /// </summary>
        /// <typeparam name="T">The type of histogram to decode into</typeparam>
        /// <param name="buffer">The buffer to decode from</param>
        /// <param name="minBarForHighestTrackableValue">Force highestTrackableValue to be set at least this high</param>
        /// <param name="decompressor">The <see cref="DeflateStream"/> that is being used to decompress the payload. Optional.</param>
        /// <returns>The newly constructed histogram</returns>
        public static T DecodeFromByteBuffer<T>(ByteBuffer buffer, long minBarForHighestTrackableValue,
           DeflateStream decompressor = null) where T : HistogramBase
        {
            var header = ReadHeader(buffer);
            var histogram = Create<T>(header, minBarForHighestTrackableValue);

            int expectedCapacity = Math.Min(histogram.GetNeededByteBufferCapacity(), header.PayloadLengthInBytes);
            var payLoadSourceBuffer = PayLoadSourceBuffer(buffer, decompressor, expectedCapacity, header);

            var filledLength = histogram.FillCountsFromBuffer(payLoadSourceBuffer, expectedCapacity, GetWordSizeInBytesFromCookie(header.Cookie));
            histogram.EstablishInternalTackingValues(filledLength);

            //TODO: Rationalise. This seems misplaced (lost in translation) -LC
            if (header.Cookie != GetEncodingCookie(histogram))
            {
                //throw new ArgumentException($"The buffer's encoded value byte size ({GetWordSizeInBytesFromCookie(cookie)}) does not match the Histogram's ({histogram.WordSizeInBytes})");
                throw new ArgumentException();
            }
            return histogram;
        }

        /// <summary>
        /// Encode this histogram in compressed form into a <see cref="ByteBuffer"/>.
        /// </summary>
        /// <param name="source">The histogram to encode</param>
        /// <param name="targetBuffer">The buffer to write to</param>
        /// <returns>The number of bytes written to the buffer</returns>
        public static int EncodeIntoCompressedByteBuffer(this HistogramBase source, ByteBuffer targetBuffer)
        {
            int neededCapacity = source.GetNeededByteBufferCapacity();
            var intermediateUncompressedByteBuffer = ByteBuffer.Allocate(neededCapacity);
            source.Encode(intermediateUncompressedByteBuffer, HistogramEncoderV2.Instance);

            int initialTargetPosition = targetBuffer.Position;
            targetBuffer.PutInt(source.GetCompressedEncodingCookie());
            int compressedContentsHeaderPosition = targetBuffer.Position;
            targetBuffer.PutInt(0); // Placeholder for compressed contents length
            var compressedDataLength = targetBuffer.CompressedCopy(intermediateUncompressedByteBuffer, targetBuffer.Position);

            targetBuffer.PutInt(compressedContentsHeaderPosition, compressedDataLength); // Record the compressed length
            int bytesWritten = compressedDataLength + 8;
            targetBuffer.Position = (initialTargetPosition + bytesWritten);
            return bytesWritten;
        }

        private static IHeader ReadHeader(ByteBuffer buffer)
        {
            var cookie = buffer.GetInt();
            var cookieBase = GetCookieBase(cookie);
            var wordsize = GetWordSizeInBytesFromCookie(cookie);
            if ((cookieBase == EncodingCookieBaseV2) || (cookieBase == EncodingCookieBaseV1))
            {
                if (cookieBase == EncodingCookieBaseV2)
                {
                    if (wordsize != V2MaxWordSizeInBytes)
                    {
                        throw new ArgumentException("The buffer does not contain a Histogram (no valid cookie found)");
                    }
                }
                return new V1Header(cookie, buffer);
            }
            else if (cookieBase == EncodingCookieBaseV0)
            {
                return new V0Header(cookie, buffer);
            }
            throw new NotSupportedException("The buffer does not contain a Histogram (no valid cookie found)");
        }
        
        private static T Create<T>(IHeader header, long minBarForHighestTrackableValue) where T : HistogramBase
        {
            var histogramClass = typeof(T);
            var constructor = TypeHelper.GetConstructor(histogramClass, HistogramClassConstructorArgsTypes);
            if (constructor == null)
                throw new ArgumentException("The target type does not have a supported constructor", nameof(histogramClass));

            var highestTrackableValue = Math.Max(header.HighestTrackableValue, minBarForHighestTrackableValue);
            try
            {
                var histogram = (T)constructor.Invoke(new object[]
                {
                    header.LowestTrackableUnitValue,
                    highestTrackableValue,
                    header.NumberOfSignificantValueDigits
                });
                //histogram.TotalCount = totalCount; // Restore totalCount --Was a V0 way of doing things -LC


                //TODO: Java does this now. Need to follow this through -LC
                //histogram.IntegerToDoubleValueConversionRatio = header.IntegerToDoubleValueConversionRatio;
                //histogram.NormalizingIndexOffset = header.NormalizingIndexOffset;
                return histogram;
            }
            catch (Exception ex)
            {
                //As we are calling an unknown method (the ctor) we cant be sure of what of what type of exceptions we need to catch -LC
                throw new ArgumentException("Unable to create histogram of Type " + histogramClass.Name + ": " + ex.Message, ex);
            }
        }

        private static ByteBuffer PayLoadSourceBuffer(ByteBuffer buffer, DeflateStream decompressor, int expectedCapacity, IHeader header)
        {
            ByteBuffer payLoadSourceBuffer;
            if (decompressor == null)
            {
                // No compressed source buffer. Payload is in buffer, after header.
                if (expectedCapacity > buffer.Remaining())
                {
                    throw new ArgumentException("The buffer does not contain the full Histogram payload");
                }
                payLoadSourceBuffer = buffer;
            }
            else
            {
                payLoadSourceBuffer = ByteBuffer.Allocate(expectedCapacity);
                var decompressedByteCount = payLoadSourceBuffer.ReadFrom(decompressor, expectedCapacity);

                if ((header.PayloadLengthInBytes != int.MaxValue) && (decompressedByteCount < header.PayloadLengthInBytes))
                {
                    throw new ArgumentException("The buffer does not contain the indicated payload amount");
                }
            }
            return payLoadSourceBuffer;
        }

        private static int GetHeaderSize(int cookie)
        {
            var cookieBase = GetCookieBase(cookie);

            switch (cookieBase)
            {
                case CompressedEncodingCookieBaseV2:
                    return EncodingHeaderSizeV2;
                case CompressedEncodingCookieBaseV1:
                    return EncodingHeaderSizeV1;
                case CompressedEncodingCookieBaseV0:
                    return EncodingHeaderSizeV0;
                default:
                    throw new ArgumentException("The buffer does not contain a compressed Histogram");
            }
        }

        private static int GetCookieBase(int cookie)
        {
            return (cookie & ~0xf0);
        }

        public static int GetEncodingCookie(this HistogramBase histogram)
        {
            //return EncodingCookieBase + (histogram.WordSizeInBytes << 4);
            return EncodingCookieBaseV2 | 0x10; // LSBit of wordsize byte indicates TLZE Encoding            
        }

        private static int GetCompressedEncodingCookie(this HistogramBase histogram)
        {
            //return CompressedEncodingCookieBaseV2 + (histogram.WordSizeInBytes << 4);
            return CompressedEncodingCookieBaseV2 | 0x10; // LSBit of wordsize byte indicates TLZE Encoding
        }

        private static int GetWordSizeInBytesFromCookie(int cookie)
        {
            var cookieBase = GetCookieBase(cookie);
            if (cookieBase == EncodingCookieBaseV2 ||
                cookieBase == CompressedEncodingCookieBaseV2)
            {
                return V2MaxWordSizeInBytes;
            }
            //V1 & V0 word size.
            var sizeByte = (cookie & 0xf0) >> 4;
            return sizeByte & 0xe;
        }

        private static Type GetHistogramType(int cookie)
        {
            //TODO: need to validate this stuff. -LC
            var wordSize = GetWordSizeInBytesFromCookie(cookie);
            switch (wordSize)
            {
                case 2:
                    return typeof(ShortHistogram);
                case 4:
                    return typeof(IntHistogram);
                case 8:
                case 9:
                    return typeof(LongHistogram);
                default:
                    throw new NotSupportedException();
            }
        }
    }
}