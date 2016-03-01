using System;
using HdrHistogram.Utilities;

namespace HdrHistogram.Encoding
{
    public class HistogramEncoderV2 : IEncoder
    {
        public static readonly HistogramEncoderV2 Instance = new HistogramEncoderV2();

        public int Encode(IRecordedData data, ByteBuffer buffer)
        {
            //TODO: I need to grok what the bucket sub-bucket relationship is. -LC
            // ->Calculate the required buffer space required.

            //var maxValue = histogram.GetMaxValue();
            //var relevantLength = histogram.GetLengthForNumberOfBuckets(histogram.GetBucketsNeededToCoverValue(maxValue));
            //var requiredLength = GetNeededByteBufferCapacity(-1, data.WordSizeInBytes);
            //if (buffer.Capacity() < requiredLength)
            //{
            //    throw new ArgumentOutOfRangeException("buffer does not have capacity for" + histogram.GetNeededByteBufferCapacity(relevantLength) + " bytes");
            //}
            //buffer.PutInt(data.Cookie);
            //buffer.PutInt(data.NumberOfSignificantValueDigits);
            //buffer.PutLong(data.LowestTrackableValue);
            //buffer.PutLong(data.HighestTrackableValue);
            //buffer.PutLong(data.TotalCount); // Needed because overflow situations may lead this to differ from counts totals

            ////Write buffer with approriate wordsize writer.
            //histogram.FillBufferFromCountsArray(buffer, relevantLength * histogram.WordSizeInBytes);
            ////-->buffer.BlockCopy(src: values, srcOffset: index, dstOffset: byteBuffer.Position/*????*/, count: length);


            //return requiredLength;
            ////Should this be
            ////  return currentPos - initialPos;  //??
            int initialPosition = buffer.Position;
            buffer.PutInt(data.Cookie);
            //TODO: int payloadLengthPosition = buffer.Position;
            buffer.PutInt(0); // Placeholder for payload length in bytes.
            buffer.PutInt(data.NormalizingIndexOffset);
            buffer.PutInt(data.NumberOfSignificantValueDigits);
            buffer.PutLong(data.LowestDiscernibleValue);
            buffer.PutLong(data.HighestTrackableValue);
            buffer.PutDouble(data.IntegerToDoubleValueConversionRatio);


            int payloadStartPosition = buffer.Position;
            FillBufferFromCountsArray(buffer, data);

            buffer.PutInt(initialPosition + 4, buffer.Position - payloadStartPosition); // Record the payload length
            //TODO - Replace with:
            //var payloadLength = buffer.Position - payloadStartPosition;
            //buffer.PutInt(payloadLengthPosition, payloadLength);

            var bytesWritten = buffer.Position - initialPosition;
            return bytesWritten;
        }

        private int GetNeededByteBufferCapacity(int relevantLength, int wordSizeInBytes)
        {
            return (relevantLength * wordSizeInBytes) + 32;
        }

        private void Write<T>(ByteBuffer byteBuffer, T[] values, int index, int length)
        {
            byteBuffer.BlockCopy(src: values, srcOffset: index, dstOffset: byteBuffer.Position/*????*/, count: length);
        }

        //synchronized
        private static void FillBufferFromCountsArray(ByteBuffer buffer, IRecordedData data)
        {
            //TODO: Only fill the IRecordedData.Counts array with data to the max value. Thus this is just data.Counts.Length
            //int countsLimit = countsArrayIndex(maxValue) + 1;
            int srcIndex = 0;

            while (srcIndex < data.Counts.Length)
            {
                // V2 encoding format uses a ZigZag LEB128-64b9B encoded long. 
                // Positive values are counts, while negative values indicate a repeat zero counts. i.e. -4 indicates 4 sequential buckets with 0 counts.
                long count = GetCountAtIndex(srcIndex++, data);
                if (count < 0)
                {
                    throw new InvalidOperationException($"Cannot encode histogram containing negative counts ({count}) at index {srcIndex}");
                    //+ " corresponding the value range [{}" +
                    //    lowestEquivalentValue(valueFromIndex(srcIndex)) + "," +
                    //    nextNonEquivalentValue(valueFromIndex(srcIndex)) + ")");
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
        }

        private static long GetCountAtIndex(int idx, IRecordedData data)
        {
            return data.Counts[idx];
            //var normalizedIdx = NormalizeIndex(idx, data.NormalizingIndexOffset, data.Counts.Length);
            //return data.Counts[idx];

        }

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

    public interface IEncoder
    {
        int Encode(IRecordedData data, ByteBuffer buffer);
    }
}