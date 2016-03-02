namespace HdrHistogram.Encoding
{
    public interface IHeader
    {
        int Cookie { get; }
        int PayloadLengthInBytes { get; }
        int NormalizingIndexOffset { get; }
        int NumberOfSignificantValueDigits { get; }
        long LowestTrackableUnitValue { get; }
        long HighestTrackableValue { get; }
        double IntegerToDoubleValueConversionRatio { get; }
    }
}