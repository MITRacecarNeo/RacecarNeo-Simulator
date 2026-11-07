using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// CBOR reader (RFC 8949) for rosbridge binary frames sent with "cbor" compression.
/// </summary>
/// <remarks>
/// Typed arrays (RFC 8746, tags 64 to 87) decode to typed values: uint8 to Bytes, int8 to
/// SBytes, float16 and float32 to Floats, float64 to Doubles, other integer widths to Longs.
/// Other tags decode to their content. Map keys must be text.
/// </remarks>
public static class CarLinkCbor
{
    #region Constants
    /// <summary>
    /// Deepest nesting of arrays, maps, and tags accepted.
    /// </summary>
    public const int MaxDepth = 64;
    #endregion

    #region Public Interface
    /// <summary>
    /// Decodes one CBOR data item that fills the whole buffer.
    /// </summary>
    /// <exception cref="CarLinkFormatException">The bytes are not valid CBOR, or use a feature
    /// this reader does not support.</exception>
    public static CarLinkValue Read(byte[] data)
    {
        if (data == null)
        {
            throw new CarLinkFormatException("CBOR: no data");
        }
        Reader reader = new Reader(data);
        CarLinkValue value = reader.ReadItem(0);
        if (reader.Position != data.Length)
        {
            throw new CarLinkFormatException($"CBOR: {data.Length - reader.Position} bytes after the item");
        }
        return value;
    }
    #endregion

    private const int breakMarker = 0xff;

    private sealed class Reader
    {
        private readonly byte[] data;

        public Reader(byte[] data)
        {
            this.data = data;
        }

        public int Position { get; private set; }

        public CarLinkValue ReadItem(int depth)
        {
            if (depth > CarLinkCbor.MaxDepth)
            {
                throw this.Error("nesting too deep");
            }
            int initial = this.ReadByte();
            int major = initial >> 5;
            int info = initial & 0x1f;
            switch (major)
            {
                case 0:
                    return CarLinkValue.FromLong(this.ToLong(this.ReadArgument(info)));
                case 1:
                    // -1 - n; an argument above long.MaxValue does not fit a long
                    return CarLinkValue.FromLong(-1 - this.ToLong(this.ReadArgument(info)));
                case 2:
                    return CarLinkValue.FromBytes(this.ReadByteString(info, 2));
                case 3:
                    return CarLinkValue.FromString(Encoding.UTF8.GetString(this.ReadByteString(info, 3)));
                case 4:
                    return this.ReadArray(info, depth);
                case 5:
                    return this.ReadMap(info, depth);
                case 6:
                    return this.ReadTag(this.ReadArgument(info), depth);
                default:
                    return this.ReadSimple(info);
            }
        }

        private CarLinkValue ReadArray(int info, int depth)
        {
            List<CarLinkValue> list = new List<CarLinkValue>();
            if (info == 31)
            {
                while (this.PeekByte() != CarLinkCbor.breakMarker)
                {
                    list.Add(this.ReadItem(depth + 1));
                }
                this.Position++;
            }
            else
            {
                long count = this.ReadLength(info, 1);
                for (long i = 0; i < count; i++)
                {
                    list.Add(this.ReadItem(depth + 1));
                }
            }
            return CarLinkValue.FromList(list);
        }

        private CarLinkValue ReadMap(int info, int depth)
        {
            Dictionary<string, CarLinkValue> map = new Dictionary<string, CarLinkValue>();
            if (info == 31)
            {
                while (this.PeekByte() != CarLinkCbor.breakMarker)
                {
                    this.ReadEntry(map, depth);
                }
                this.Position++;
            }
            else
            {
                long count = this.ReadLength(info, 2);
                for (long i = 0; i < count; i++)
                {
                    this.ReadEntry(map, depth);
                }
            }
            return CarLinkValue.FromMap(map);
        }

        private void ReadEntry(Dictionary<string, CarLinkValue> map, int depth)
        {
            CarLinkValue key = this.ReadItem(depth + 1);
            if (key.Kind != CarLinkValueKind.String)
            {
                throw this.Error($"map key is {key.Describe()}, not text");
            }
            map[key.AsString()] = this.ReadItem(depth + 1);
        }

        private CarLinkValue ReadTag(ulong tag, int depth)
        {
            if (tag < 64 || tag > 87)
            {
                return this.ReadItem(depth + 1);
            }
            int initial = this.ReadByte();
            if (initial >> 5 != 2)
            {
                throw this.Error($"typed array tag {tag} without a byte string");
            }
            byte[] raw = this.ReadByteString(initial & 0x1f, 2);
            return CarLinkCbor.DecodeTypedArray((int)tag, raw);
        }

        private CarLinkValue ReadSimple(int info)
        {
            switch (info)
            {
                case 20:
                    return CarLinkValue.FromBool(false);
                case 21:
                    return CarLinkValue.FromBool(true);
                case 22:
                case 23:
                    return CarLinkValue.Null;
                case 25:
                    return CarLinkValue.FromDouble(CarLinkCbor.HalfToSingle((ushort)this.ReadBigEndian(2)));
                case 26:
                    return CarLinkValue.FromDouble(BitConverter.Int32BitsToSingle((int)this.ReadBigEndian(4)));
                case 27:
                    return CarLinkValue.FromDouble(BitConverter.Int64BitsToDouble((long)this.ReadBigEndian(8)));
                case 31:
                    throw this.Error("unexpected break");
                default:
                    if (info < 20)
                    {
                        // Unassigned simple values carry no data for a car link message
                        return CarLinkValue.Null;
                    }
                    if (info == 24)
                    {
                        this.ReadByte();
                        return CarLinkValue.Null;
                    }
                    throw this.Error($"reserved simple value {info}");
            }
        }

        private byte[] ReadByteString(int info, int major)
        {
            if (info != 31)
            {
                long length = this.ReadLength(info, 1);
                byte[] bytes = new byte[length];
                Buffer.BlockCopy(this.data, this.Position, bytes, 0, (int)length);
                this.Position += (int)length;
                return bytes;
            }
            List<byte> chunks = new List<byte>();
            while (this.PeekByte() != CarLinkCbor.breakMarker)
            {
                int initial = this.ReadByte();
                if (initial >> 5 != major || (initial & 0x1f) == 31)
                {
                    throw this.Error("bad chunk in an indefinite string");
                }
                chunks.AddRange(this.ReadByteString(initial & 0x1f, major));
            }
            this.Position++;
            return chunks.ToArray();
        }

        /// <summary>
        /// Reads a length argument and checks that at least minBytesPerElement times as many bytes
        /// remain, so a corrupt length cannot allocate a huge buffer.
        /// </summary>
        private long ReadLength(int info, int minBytesPerElement)
        {
            ulong length = this.ReadArgument(info);
            if (length > (ulong)((this.data.Length - this.Position) / minBytesPerElement))
            {
                throw this.Error($"length {length} runs past the end");
            }
            return (long)length;
        }

        private ulong ReadArgument(int info)
        {
            if (info < 24)
            {
                return (ulong)info;
            }
            switch (info)
            {
                case 24:
                    return this.ReadBigEndian(1);
                case 25:
                    return this.ReadBigEndian(2);
                case 26:
                    return this.ReadBigEndian(4);
                case 27:
                    return this.ReadBigEndian(8);
                default:
                    throw this.Error($"bad additional information {info}");
            }
        }

        private ulong ReadBigEndian(int count)
        {
            if (this.Position + count > this.data.Length)
            {
                throw this.Error("unexpected end");
            }
            ulong value = 0;
            for (int i = 0; i < count; i++)
            {
                value = (value << 8) | this.data[this.Position++];
            }
            return value;
        }

        private long ToLong(ulong value)
        {
            if (value > long.MaxValue)
            {
                throw this.Error("integer too large");
            }
            return (long)value;
        }

        private int ReadByte()
        {
            if (this.Position >= this.data.Length)
            {
                throw this.Error("unexpected end");
            }
            return this.data[this.Position++];
        }

        private int PeekByte()
        {
            if (this.Position >= this.data.Length)
            {
                throw this.Error("unexpected end");
            }
            return this.data[this.Position];
        }

        private CarLinkFormatException Error(string message)
        {
            return new CarLinkFormatException($"CBOR: {message} at byte {this.Position}");
        }
    }

    private static CarLinkValue DecodeTypedArray(int tag, byte[] raw)
    {
        // Tags 64 to 87: bit 4 float, bit 3 signed (integers), bit 2 little endian, bits 0-1 size
        bool isFloat = (tag & 0x10) != 0;
        bool isLittleEndian = (tag & 0x04) != 0;
        int sizeCode = tag & 0x03;
        if (tag == 64 || tag == 68)
        {
            return CarLinkValue.FromBytes(raw);
        }
        if (tag == 72)
        {
            sbyte[] signed = new sbyte[raw.Length];
            Buffer.BlockCopy(raw, 0, signed, 0, raw.Length);
            return CarLinkValue.FromSBytes(signed);
        }
        if (tag == 76 || (isFloat && sizeCode == 3))
        {
            throw new CarLinkFormatException($"CBOR: typed array tag {tag} is not supported");
        }
        if (isFloat)
        {
            int width = 2 << sizeCode;
            CarLinkCbor.CheckWidth(raw, width, tag);
            int count = raw.Length / width;
            if (width == 8)
            {
                double[] doubles = new double[count];
                for (int i = 0; i < count; i++)
                {
                    doubles[i] = BitConverter.Int64BitsToDouble((long)CarLinkCbor.ReadUnsigned(raw, i * 8, 8, isLittleEndian));
                }
                return CarLinkValue.FromDoubles(doubles);
            }
            float[] floats = new float[count];
            for (int i = 0; i < count; i++)
            {
                ulong bits = CarLinkCbor.ReadUnsigned(raw, i * width, width, isLittleEndian);
                floats[i] = width == 4 ? BitConverter.Int32BitsToSingle((int)bits) : CarLinkCbor.HalfToSingle((ushort)bits);
            }
            return CarLinkValue.FromFloats(floats);
        }
        bool isSigned = (tag & 0x08) != 0;
        int size = 1 << sizeCode;
        CarLinkCbor.CheckWidth(raw, size, tag);
        long[] longs = new long[raw.Length / size];
        for (int i = 0; i < longs.Length; i++)
        {
            ulong value = CarLinkCbor.ReadUnsigned(raw, i * size, size, isLittleEndian);
            if (isSigned && size < 8)
            {
                int shift = 64 - 8 * size;
                longs[i] = ((long)(value << shift)) >> shift;
            }
            else if (!isSigned && value > long.MaxValue)
            {
                throw new CarLinkFormatException("CBOR: uint64 element too large");
            }
            else
            {
                longs[i] = (long)value;
            }
        }
        return CarLinkValue.FromLongs(longs);
    }

    private static void CheckWidth(byte[] raw, int width, int tag)
    {
        if (raw.Length % width != 0)
        {
            throw new CarLinkFormatException($"CBOR: typed array tag {tag} has {raw.Length} bytes, not a multiple of {width}");
        }
    }

    private static ulong ReadUnsigned(byte[] raw, int offset, int width, bool isLittleEndian)
    {
        ulong value = 0;
        for (int i = 0; i < width; i++)
        {
            int index = isLittleEndian ? offset + width - 1 - i : offset + i;
            value = (value << 8) | raw[index];
        }
        return value;
    }

    private static float HalfToSingle(ushort half)
    {
        int sign = (half >> 15) & 1;
        int exponent = (half >> 10) & 0x1f;
        int mantissa = half & 0x3ff;
        double value;
        if (exponent == 0)
        {
            value = mantissa * Math.Pow(2, -24);
        }
        else if (exponent == 31)
        {
            value = mantissa == 0 ? double.PositiveInfinity : double.NaN;
        }
        else
        {
            value = (1 + mantissa / 1024.0) * Math.Pow(2, exponent - 15);
        }
        return (float)(sign == 1 ? -value : value);
    }
}
