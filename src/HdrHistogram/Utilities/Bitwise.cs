using System;

namespace HdrHistogram.Utilities
{
    //Code has been tested and taken from :
    //http://stackoverflow.com/questions/9543410/i-dont-think-numberofleadingzeroslong-i-in-long-java-is-based-floorlog2x/9543537#9543537
    //http://stackoverflow.com/questions/21888140/de-bruijn-algorithm-binary-digit-count-64bits-c-sharp/21888542#21888542
    //http://stackoverflow.com/questions/15967240/fastest-implementation-of-log2int-and-log2float
    //http://graphics.stanford.edu/~seander/bithacks.html#IntegerLogObvious
    //
    //Ideally newer versions of .NET will expose the CPU instructions to do this  Intel SSE 'lzcnt' (Leading Zero Count), or give access to the BitScanReverse VC++ functions (https://msdn.microsoft.com/en-us/library/fbxyd7zd.aspx)
    internal static class Bitwise
    {
        private static readonly int[] Lookup;

        static Bitwise()
        {
            Lookup = new int[256];
            for (int i = 1; i < 256; ++i)
            {
                Lookup[i] = (int)(Math.Log(i) / Math.Log(2));
            }
        }
        public static int NumberOfLeadingZeros(long value)
        {
            //Optimisation for 32 bit values. So for any value under 00:03:34.7 when measuring in ticks, we will hit a fast path.
            if (value < int.MaxValue)
                return 63 - Log2((int)value);
            return NumberOfLeadingZerosLong(value);
        }

        private static int NumberOfLeadingZerosLong(long value)
        {
            // Code from http://stackoverflow.com/questions/9543410/i-dont-think-numberofleadingzeroslong-i-in-long-java-is-based-floorlog2x/9543537#9543537

            //--Already checked that values here are over int.MaxValue, i.e. !=0
            // HD, Figure 5-6
            //if (value == 0)
            //    return 64;
            var n = 1;
            // >>> in Java is a "unsigned bit shift", to do the same in C# we use >> (but it HAS to be an unsigned int)
            var x = (uint)(value >> 32);
            if (x == 0) { n += 32; x = (uint)value; }
            if (x >> 16 == 0) { n += 16; x <<= 16; }
            if (x >> 24 == 0) { n += 8; x <<= 8; }
            if (x >> 28 == 0) { n += 4; x <<= 4; }
            if (x >> 30 == 0) { n += 2; x <<= 2; }
            n -= (int)(x >> 31);
            return n;
        }

        private static int Log2(int i)
        {
            if (i >= 0x1000000) { return Lookup[i >> 24] + 24; }
            if (i >= 0x10000) { return Lookup[i >> 16] + 16; }
            if (i >= 0x100) { return Lookup[i >> 8] + 8; }
            return Lookup[i];
        }
    }
}