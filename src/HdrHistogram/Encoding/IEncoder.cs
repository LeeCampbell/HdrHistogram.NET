using HdrHistogram.Utilities;

namespace HdrHistogram.Encoding
{
    public interface IEncoder
    {
        int Encode(IRecordedData data, ByteBuffer buffer);
    }
}