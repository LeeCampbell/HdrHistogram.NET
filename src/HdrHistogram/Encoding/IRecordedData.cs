namespace HdrHistogram.Encoding
{
    public interface IRecordedData
    {
        int Cookie { get; }
        int NormalizingIndexOffset { get; } //Required? What is it?
        int NumberOfSignificantValueDigits { get; }
        long LowestDiscernibleValue { get; }
        long HighestTrackableValue { get; }
        double IntegerToDoubleValueConversionRatio { get; }
        long[] Counts { get; }
    }

    sealed class RecordedData : IRecordedData
    {
        public RecordedData(int cookie, int normalizingIndexOffset, int numberOfSignificantValueDigits, long lowestDiscernibleValue, long highestTrackableValue, double integerToDoubleValueConversionRatio, long[] counts)
        {
            Cookie = cookie;
            NormalizingIndexOffset = normalizingIndexOffset;
            NumberOfSignificantValueDigits = numberOfSignificantValueDigits;
            LowestDiscernibleValue = lowestDiscernibleValue;
            HighestTrackableValue = highestTrackableValue;
            IntegerToDoubleValueConversionRatio = integerToDoubleValueConversionRatio;
            Counts = counts;
        }

        public int Cookie { get; }
        public int NormalizingIndexOffset { get; }
        public int NumberOfSignificantValueDigits { get; }
        public long LowestDiscernibleValue { get; }
        public long HighestTrackableValue { get; }
        public double IntegerToDoubleValueConversionRatio { get; }
        public long[] Counts { get; }
    }
}
