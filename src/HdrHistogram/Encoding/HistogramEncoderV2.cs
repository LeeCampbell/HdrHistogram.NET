using System;
using HdrHistogram.Utilities;

namespace HdrHistogram.Encoding
{
    /// <summary>
    /// An implementation of <see cref="IEncoder"/> for the V2 HdrHistogram log format.
    /// </summary>
    public class HistogramEncoderV2 : IEncoder
    {
        /// <summary>
        /// A singleton instance of the <see cref="HistogramEncoderV2"/>.
        /// </summary>
        public static readonly HistogramEncoderV2 Instance = new HistogramEncoderV2();


        /// <summary>
        /// Encodes the supplied <see cref="IRecordedData"/> into the supplied <see cref="ByteBuffer"/>.
        /// </summary>
        /// <param name="data">The data to encode.</param>
        /// <param name="buffer">The target <see cref="ByteBuffer"/> to write to.</param>
        /// <returns>The number of bytes written.</returns>
        public int Encode(IRecordedData data, ByteBuffer buffer)
        {
            int initialPosition = buffer.Position;
            buffer.PutInt(data.Cookie);
            int payloadLengthPosition = buffer.Position;
            buffer.PutInt(0); // Placeholder for payload length in bytes.
            buffer.PutInt(data.NormalizingIndexOffset);
            buffer.PutInt(data.NumberOfSignificantValueDigits);
            buffer.PutLong(data.LowestDiscernibleValue);
            buffer.PutLong(data.HighestTrackableValue);
            buffer.PutDouble(data.IntegerToDoubleValueConversionRatio);

            var payloadLength = FillBufferFromCountsArray(buffer, data);
            buffer.PutInt(payloadLengthPosition, payloadLength);

            var bytesWritten = buffer.Position - initialPosition;
            return bytesWritten;
        }

        private static int FillBufferFromCountsArray(ByteBuffer buffer, IRecordedData data)
        {
            int startPosition = buffer.Position;
            int srcIndex = 0;

            while (srcIndex < data.Counts.Length)
            {
                // V2 encoding format uses a ZigZag LEB128-64b9B encoded long. 
                // Positive values are counts, while negative values indicate a repeat zero counts. i.e. -4 indicates 4 sequential buckets with 0 counts.
                long count = GetCountAtIndex(srcIndex++, data);
                if (count < 0)
                {
                    throw new InvalidOperationException($"Cannot encode histogram containing negative counts ({count}) at index {srcIndex}");
                }
                // Count trailing 0s (which follow this count):
                long zerosCount = 0;
                if (count == 0)
                {
                    zerosCount = 1;
                    while ((srcIndex < data.Counts.Length) && (GetCountAtIndex(srcIndex, data) == 0))
                    {
                        zerosCount++;
                        srcIndex++;
                    }
                }
                if (zerosCount > 1)
                {
                    ZigZagEncoding.PutLong(buffer, -zerosCount);
                }
                else
                {
                    ZigZagEncoding.PutLong(buffer, count);
                }
            }
            return buffer.Position - startPosition;
        }

        private static long GetCountAtIndex(int idx, IRecordedData data)
        {
            return data.Counts[idx];
            //var normalizedIdx = NormalizeIndex(idx, data.NormalizingIndexOffset, data.Counts.Length);
            //return data.Counts[idx];

        }

        //TODO: Add normalization features to Encoding. -LC
        private static int NormalizeIndex(int index, int normalizingIndexOffset, int arrayLength)
        {
            if (normalizingIndexOffset == 0)
            {
                // Fast path out of normalization. Keeps integer value histograms fast while allowing
                // others (like DoubleHistogram) to use normalization at a cost...
                return index;
            }
            if ((index > arrayLength) || (index < 0))
            {
                throw new IndexOutOfRangeException("index out of covered value range");
            }
            int normalizedIndex = index - normalizingIndexOffset;
            // The following is the same as an unsigned remainder operation, as long as no double wrapping happens
            // (which shouldn't happen, as normalization is never supposed to wrap, since it would have overflowed
            // or underflowed before it did). This (the + and - tests) seems to be faster than a % op with a
            // correcting if < 0...:
            if (normalizedIndex < 0)
            {
                normalizedIndex += arrayLength;
            }
            else if (normalizedIndex >= arrayLength)
            {
                normalizedIndex -= arrayLength;
            }
            return normalizedIndex;
        }
    }
}