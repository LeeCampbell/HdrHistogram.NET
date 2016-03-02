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
}
