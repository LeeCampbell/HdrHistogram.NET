using System;
using HdrHistogram.Utilities;

namespace HdrHistogram
{
    /// <summary>
    /// Exposes methods to write values to a <see cref="ByteBuffer"/> with ZigZag LEB-128 encoding.
    /// </summary>
    public static class ZigZagEncoding
    {
        /// <summary>
        /// Writes a 64 bit integer (<see cref="long"/>) value to the given buffer in LEB128 ZigZag encoded format.
        /// </summary>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="value">The value to write to the buffer.</param>
        public static void PutLong(ByteBuffer buffer, long value)
        {
            value = (value << 1) ^ (value >> 63);
            if (value >> 7 == 0)
            {
                buffer.Put((byte)value);
            }
            else {
                buffer.Put((byte)((value & 0x7F) | 0x80));
                if (value >> 14 == 0)
                {
                    buffer.Put((byte)(value >> 7));
                }
                else {
                    buffer.Put((byte)(value >> 7 | 0x80));
                    if (value >> 21 == 0)
                    {
                        buffer.Put((byte)(value >> 14));
                    }
                    else {
                        buffer.Put((byte)(value >> 14 | 0x80));
                        if (value >> 28 == 0)
                        {
                            buffer.Put((byte)(value >> 21));
                        }
                        else {
                            buffer.Put((byte)(value >> 21 | 0x80));
                            if (value >> 35 == 0)
                            {
                                buffer.Put((byte)(value >> 28));
                            }
                            else {
                                buffer.Put((byte)(value >> 28 | 0x80));
                                if (value >> 42 == 0)
                                {
                                    buffer.Put((byte)(value >> 35));
                                }
                                else {
                                    buffer.Put((byte)(value >> 35 | 0x80));
                                    if (value >> 49 == 0)
                                    {
                                        buffer.Put((byte)(value >> 42));
                                    }
                                    else {
                                        buffer.Put((byte)(value >> 42 | 0x80));
                                        if (value >> 56 == 0)
                                        {
                                            buffer.Put((byte)(value >> 49));
                                        }
                                        else {
                                            buffer.Put((byte)(value >> 49 | 0x80));
                                            buffer.Put((byte)(value >> 56));
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Reads an LEB128-64b9B ZigZag encoded 64 bit integer (<see cref="long"/>) value from the given buffer.
        /// </summary>
        /// <param name="buffer">The buffer to read from.</param>
        /// <returns>The value read from the buffer.</returns>
        public static long GetLong(ByteBuffer buffer)
        {
            long v = buffer.Get();
            long value = v & 0x7F;
            if ((v & 0x80) != 0)
            {
                v = buffer.Get();
                value |= (v & 0x7F) << 7;
                if ((v & 0x80) != 0)
                {
                    v = buffer.Get();
                    value |= (v & 0x7F) << 14;
                    if ((v & 0x80) != 0)
                    {
                        v = buffer.Get();
                        value |= (v & 0x7F) << 21;
                        if ((v & 0x80) != 0)
                        {
                            v = buffer.Get();
                            value |= (v & 0x7F) << 28;
                            if ((v & 0x80) != 0)
                            {
                                v = buffer.Get();
                                value |= (v & 0x7F) << 35;
                                if ((v & 0x80) != 0)
                                {
                                    v = buffer.Get();
                                    value |= (v & 0x7F) << 42;
                                    if ((v & 0x80) != 0)
                                    {
                                        v = buffer.Get();
                                        value |= (v & 0x7F) << 49;
                                        if ((v & 0x80) != 0)
                                        {
                                            v = buffer.Get();
                                            value |= v << 56;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            value = (value >> 1) ^ (-(value & 1));
            return value;
        }
    }
}
