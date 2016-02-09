using System.Collections.Generic;
using System.Linq;

namespace HdrHistogram.Persistence
{
    public static class CountsDecoder
    {
        private static readonly IDictionary<int, ICountsDecoder> Decoders;

        static CountsDecoder()
        {
            Decoders = new ICountsDecoder[]
            {
                new ShortCountsDecoder(),
                new IntCountsDecoder(),
                new LongCountsDecoder(),
                new V2MaxWordSizeCountsDecoder(),
            }.ToDictionary(cd => cd.WordSize);
        }

        public static ICountsDecoder GetDecoderForWordSize(int wordSize)
        {
            return Decoders[wordSize];
        }
    }
}
