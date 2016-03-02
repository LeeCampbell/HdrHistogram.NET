using System;
using HdrHistogram.Utilities;

namespace HdrHistogram
{


    //TODO: .NET/C#-ify this class.
    public static class ZigZagEncoding
    {
        /**
         * Writes a long value to the given buffer in LEB128 ZigZag encoded format
         * @param buffer the buffer to write to
         * @param value  the value to write to the buffer
         */
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

        /**
         * Writes an int value to the given buffer in LEB128-64b9B ZigZag encoded format
         * @param buffer the buffer to write to
         * @param value  the value to write to the buffer
         */
        public static void PutInt(ByteBuffer buffer, int value)
        {
            value = (value << 1) ^ (value >> 31);
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
                            buffer.Put((byte)(value >> 28));
                        }
                    }
                }
            }
        }

        /**
         * Read an LEB128-64b9B ZigZag encoded long value from the given buffer
         * @param buffer the buffer to read from
         * @return the value read from the buffer
         */

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

        /**
         * Read an LEB128-64b9B ZigZag encoded int value from the given buffer
         * @param buffer the buffer to read from
         * @return the value read from the buffer
         */
        public static int getInt(ByteBuffer buffer)
        {
            int v = buffer.Get();
            int value = v & 0x7F;
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
                        }
                    }
                }
            }
            value = (value >> 1) ^ (-(value & 1));
            return value;
        }
    }
}
