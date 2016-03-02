using HdrHistogram.Persistence;
using HdrHistogram.Utilities;
using NUnit.Framework;

namespace HdrHistogram.UnitTests
{
    [TestFixture]
    public class ZigZagEncodingTests
    {
        [Test]
        public void CanRoundTrip()
        {
            var bufferLength = 20;
            var input = new long[] { 1234, 5678, 9101112, 13141516, 17181920, 21222324 };
            var buffer = ByteBuffer.Allocate(bufferLength);
            foreach (var value in input)
            {
                ZigZagEncoding.PutLong(buffer, value);
            }

            buffer.Position = 0;
            var output = new long[input.Length];
            for (int i = 0; i < output.Length; i++)
            {
                output[i] = ZigZagEncoding.GetLong(buffer);
            }

            CollectionAssert.AreEqual(input, output);
        }


        [Test]
        public void CanDecodeZigZagEncodedLongArray()
        {
            var buffer = ByteBuffer.Allocate(new byte[] { 24, 18, 18, 10, 16, 22, 28, 22, 8, 10, 16, 26, 18, 18, 12, 66, 74, 92, 46, 78, 150, 2, 172, 1, 218, 2, 44, 16, 163, 1, 2, 119, 2 });
            var expected = new long[170];
            expected[0] = 12;
            expected[1] = 9;
            expected[2] = 9;
            expected[3] = 5;
            expected[4] = 8;
            expected[5] = 11;
            expected[6] = 14;
            expected[7] = 11;
            expected[8] = 4;
            expected[9] = 5;
            expected[10] = 8;
            expected[11] = 13;
            expected[12] = 9;
            expected[13] = 9;
            expected[14] = 6;
            expected[15] = 33;
            expected[16] = 37;
            expected[17] = 46;
            expected[18] = 23;
            expected[19] = 39;
            expected[20] = 139;
            expected[21] = 86;
            expected[22] = 173;
            expected[23] = 22;
            expected[24] = 8;
            //Zero counts skipped.
            expected[107] = 1;
            //Zero counts skipped.
            expected[168] = 1;

            var sut = CountsDecoder.GetDecoderForWordSize(9);

            var actual = new long[expected.Length];
            sut.ReadCounts(buffer, buffer.Capacity(), expected.Length, (idx, count) => actual[idx] = count);

            CollectionAssert.AreEqual(expected, actual);
        }
    }
}