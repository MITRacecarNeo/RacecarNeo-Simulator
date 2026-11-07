using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// The kind of data a CarLinkValue holds.
/// </summary>
public enum CarLinkValueKind
{
    Null,
    Bool,
    Integer,
    Float,
    String,
    Bytes,
    SBytes,
    Floats,
    Doubles,
    Longs,
    List,
    Map
}

/// <summary>
/// The format of a car link message did not match what the reader expected.
/// </summary>
public class CarLinkFormatException : Exception
{
    public CarLinkFormatException(string message) : base(message) { }
}

/// <summary>
/// An immutable value decoded from a car link message (JSON or CBOR): null, bool, number, string,
/// byte string, typed numeric array, list, or map.
/// </summary>
/// <remarks>
/// Typed arrays keep large numeric fields (laser ranges, occupancy grids) without boxing each
/// element. Missing map keys read as Null, so optional fields chain without checks; the typed
/// accessors throw CarLinkFormatException on a kind mismatch.
/// </remarks>
public sealed class CarLinkValue
{
    #region Constants
    /// <summary>
    /// The null value; also returned for missing keys and indices.
    /// </summary>
    public static readonly CarLinkValue Null = new CarLinkValue(CarLinkValueKind.Null);
    #endregion

    #region Public Interface
    /// <summary>
    /// The kind of data held.
    /// </summary>
    public CarLinkValueKind Kind { get; }

    /// <summary>
    /// True for the null value.
    /// </summary>
    public bool IsNull { get { return this.Kind == CarLinkValueKind.Null; } }

    /// <summary>
    /// True for an integer or floating point number.
    /// </summary>
    public bool IsNumber { get { return this.Kind == CarLinkValueKind.Integer || this.Kind == CarLinkValueKind.Float; } }

    /// <summary>
    /// Number of elements of a list, map, byte string, or typed array; 0 for other kinds.
    /// </summary>
    public int Count
    {
        get
        {
            switch (this.Kind)
            {
                case CarLinkValueKind.List:
                    return this.list.Count;
                case CarLinkValueKind.Map:
                    return this.map.Count;
                case CarLinkValueKind.Bytes:
                case CarLinkValueKind.SBytes:
                case CarLinkValueKind.Floats:
                case CarLinkValueKind.Doubles:
                case CarLinkValueKind.Longs:
                    return this.array.Length;
                default:
                    return 0;
            }
        }
    }

    /// <summary>
    /// The value under a map key, or Null when this is not a map or the key is missing.
    /// </summary>
    public CarLinkValue this[string key]
    {
        get
        {
            if (this.Kind == CarLinkValueKind.Map && this.map.TryGetValue(key, out CarLinkValue value))
            {
                return value;
            }
            return CarLinkValue.Null;
        }
    }

    /// <summary>
    /// The list element at an index, or Null when this is not a list or the index is out of range.
    /// </summary>
    public CarLinkValue this[int index]
    {
        get
        {
            if (this.Kind == CarLinkValueKind.List && index >= 0 && index < this.list.Count)
            {
                return this.list[index];
            }
            return CarLinkValue.Null;
        }
    }

    /// <summary>
    /// The map entries; empty for other kinds.
    /// </summary>
    public IReadOnlyDictionary<string, CarLinkValue> Entries
    {
        get { return this.Kind == CarLinkValueKind.Map ? this.map : CarLinkValue.emptyMap; }
    }

    /// <summary>
    /// The list elements; empty for other kinds.
    /// </summary>
    public IReadOnlyList<CarLinkValue> Items
    {
        get { return this.Kind == CarLinkValueKind.List ? this.list : CarLinkValue.emptyList; }
    }

    public static CarLinkValue FromBool(bool value)
    {
        return new CarLinkValue(CarLinkValueKind.Bool) { boolValue = value };
    }

    public static CarLinkValue FromLong(long value)
    {
        return new CarLinkValue(CarLinkValueKind.Integer) { longValue = value, doubleValue = value };
    }

    public static CarLinkValue FromDouble(double value)
    {
        return new CarLinkValue(CarLinkValueKind.Float) { doubleValue = value };
    }

    public static CarLinkValue FromString(string value)
    {
        return value == null ? CarLinkValue.Null : new CarLinkValue(CarLinkValueKind.String) { stringValue = value };
    }

    public static CarLinkValue FromBytes(byte[] value)
    {
        return new CarLinkValue(CarLinkValueKind.Bytes) { array = value };
    }

    public static CarLinkValue FromSBytes(sbyte[] value)
    {
        return new CarLinkValue(CarLinkValueKind.SBytes) { array = value };
    }

    public static CarLinkValue FromFloats(float[] value)
    {
        return new CarLinkValue(CarLinkValueKind.Floats) { array = value };
    }

    public static CarLinkValue FromDoubles(double[] value)
    {
        return new CarLinkValue(CarLinkValueKind.Doubles) { array = value };
    }

    public static CarLinkValue FromLongs(long[] value)
    {
        return new CarLinkValue(CarLinkValueKind.Longs) { array = value };
    }

    public static CarLinkValue FromList(List<CarLinkValue> value)
    {
        return new CarLinkValue(CarLinkValueKind.List) { list = value };
    }

    public static CarLinkValue FromMap(Dictionary<string, CarLinkValue> value)
    {
        return new CarLinkValue(CarLinkValueKind.Map) { map = value };
    }

    /// <summary>
    /// The value at a dotted path of map keys (for example "header.stamp.sec"), or Null when any
    /// step is missing.
    /// </summary>
    public CarLinkValue At(string path)
    {
        CarLinkValue current = this;
        foreach (string key in path.Split('.'))
        {
            current = current[key];
            if (current.IsNull)
            {
                break;
            }
        }
        return current;
    }

    public bool AsBool()
    {
        this.Expect(CarLinkValueKind.Bool, "a boolean");
        return this.boolValue;
    }

    /// <summary>
    /// The number as a long; a float must be integral.
    /// </summary>
    public long AsLong()
    {
        if (this.Kind == CarLinkValueKind.Integer)
        {
            return this.longValue;
        }
        if (this.Kind == CarLinkValueKind.Float && Math.Floor(this.doubleValue) == this.doubleValue
            && Math.Abs(this.doubleValue) < 9.2e18)
        {
            return (long)this.doubleValue;
        }
        throw new CarLinkFormatException($"expected an integer, got {this.Describe()}");
    }

    public int AsInt()
    {
        long value = this.AsLong();
        if (value < int.MinValue || value > int.MaxValue)
        {
            throw new CarLinkFormatException($"integer {value} is out of range");
        }
        return (int)value;
    }

    public double AsDouble()
    {
        if (!this.IsNumber)
        {
            throw new CarLinkFormatException($"expected a number, got {this.Describe()}");
        }
        return this.doubleValue;
    }

    public float AsFloat()
    {
        return (float)this.AsDouble();
    }

    public string AsString()
    {
        this.Expect(CarLinkValueKind.String, "a string");
        return this.stringValue;
    }

    /// <summary>
    /// A byte string; also accepts base64 text (rosbridge JSON for uint8 arrays) and a list of
    /// integers from 0 to 255.
    /// </summary>
    public byte[] AsBytes()
    {
        switch (this.Kind)
        {
            case CarLinkValueKind.Bytes:
                return (byte[])this.array;
            case CarLinkValueKind.String:
                try
                {
                    return Convert.FromBase64String(this.stringValue);
                }
                catch (FormatException)
                {
                    throw new CarLinkFormatException("expected base64 bytes");
                }
            case CarLinkValueKind.List:
                byte[] bytes = new byte[this.list.Count];
                for (int i = 0; i < bytes.Length; i++)
                {
                    long value = this.list[i].AsLong();
                    if (value < 0 || value > 255)
                    {
                        throw new CarLinkFormatException($"byte {value} is out of range");
                    }
                    bytes[i] = (byte)value;
                }
                return bytes;
            default:
                throw new CarLinkFormatException($"expected bytes, got {this.Describe()}");
        }
    }

    /// <summary>
    /// Signed bytes (int8 arrays such as occupancy grid data) from a typed array or a list of
    /// integers from -128 to 127.
    /// </summary>
    public sbyte[] AsSBytes()
    {
        switch (this.Kind)
        {
            case CarLinkValueKind.SBytes:
                return (sbyte[])this.array;
            case CarLinkValueKind.Bytes:
                byte[] raw = (byte[])this.array;
                sbyte[] converted = new sbyte[raw.Length];
                Buffer.BlockCopy(raw, 0, converted, 0, raw.Length);
                return converted;
            case CarLinkValueKind.List:
                sbyte[] values = new sbyte[this.list.Count];
                for (int i = 0; i < values.Length; i++)
                {
                    long value = this.list[i].AsLong();
                    if (value < sbyte.MinValue || value > sbyte.MaxValue)
                    {
                        throw new CarLinkFormatException($"int8 {value} is out of range");
                    }
                    values[i] = (sbyte)value;
                }
                return values;
            default:
                throw new CarLinkFormatException($"expected int8 values, got {this.Describe()}");
        }
    }

    /// <summary>
    /// Single precision values from any numeric typed array or a list of numbers. A null element
    /// reads as NaN: rosbridge's JSON writes infinity and NaN (laser ranges) as null.
    /// </summary>
    public float[] AsFloats()
    {
        switch (this.Kind)
        {
            case CarLinkValueKind.Floats:
                return (float[])this.array;
            case CarLinkValueKind.Doubles:
                double[] doubles = (double[])this.array;
                float[] fromDoubles = new float[doubles.Length];
                for (int i = 0; i < doubles.Length; i++)
                {
                    fromDoubles[i] = (float)doubles[i];
                }
                return fromDoubles;
            case CarLinkValueKind.Longs:
                long[] longs = (long[])this.array;
                float[] fromLongs = new float[longs.Length];
                for (int i = 0; i < longs.Length; i++)
                {
                    fromLongs[i] = longs[i];
                }
                return fromLongs;
            case CarLinkValueKind.List:
                float[] values = new float[this.list.Count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = this.list[i].IsNull ? float.NaN : this.list[i].AsFloat();
                }
                return values;
            default:
                throw new CarLinkFormatException($"expected numbers, got {this.Describe()}");
        }
    }

    /// <summary>
    /// Double precision values from any numeric typed array or a list of numbers; a null element
    /// reads as NaN.
    /// </summary>
    public double[] AsDoubles()
    {
        switch (this.Kind)
        {
            case CarLinkValueKind.Doubles:
                return (double[])this.array;
            case CarLinkValueKind.Floats:
                float[] floats = (float[])this.array;
                double[] fromFloats = new double[floats.Length];
                for (int i = 0; i < floats.Length; i++)
                {
                    fromFloats[i] = floats[i];
                }
                return fromFloats;
            case CarLinkValueKind.Longs:
                long[] longs = (long[])this.array;
                double[] fromLongs = new double[longs.Length];
                for (int i = 0; i < longs.Length; i++)
                {
                    fromLongs[i] = longs[i];
                }
                return fromLongs;
            case CarLinkValueKind.SBytes:
                sbyte[] sbytes = (sbyte[])this.array;
                double[] fromSBytes = new double[sbytes.Length];
                for (int i = 0; i < sbytes.Length; i++)
                {
                    fromSBytes[i] = sbytes[i];
                }
                return fromSBytes;
            case CarLinkValueKind.List:
                double[] values = new double[this.list.Count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = this.list[i].IsNull ? double.NaN : this.list[i].AsDouble();
                }
                return values;
            default:
                throw new CarLinkFormatException($"expected numbers, got {this.Describe()}");
        }
    }

    /// <summary>
    /// A short description of the kind for error messages.
    /// </summary>
    public string Describe()
    {
        switch (this.Kind)
        {
            case CarLinkValueKind.Null:
                return "null";
            case CarLinkValueKind.Bool:
                return this.boolValue ? "true" : "false";
            case CarLinkValueKind.Integer:
                return this.longValue.ToString(CultureInfo.InvariantCulture);
            case CarLinkValueKind.Float:
                return this.doubleValue.ToString("R", CultureInfo.InvariantCulture);
            case CarLinkValueKind.String:
                return "a string";
            case CarLinkValueKind.List:
                return $"a list of {this.list.Count}";
            case CarLinkValueKind.Map:
                return "an object";
            default:
                return $"an array of {this.array.Length}";
        }
    }
    #endregion

    private static readonly IReadOnlyDictionary<string, CarLinkValue> emptyMap = new Dictionary<string, CarLinkValue>();
    private static readonly IReadOnlyList<CarLinkValue> emptyList = new List<CarLinkValue>();

    private bool boolValue;
    private long longValue;
    private double doubleValue;
    private string stringValue;
    private Array array;
    private List<CarLinkValue> list;
    private Dictionary<string, CarLinkValue> map;

    private CarLinkValue(CarLinkValueKind kind)
    {
        this.Kind = kind;
    }

    private void Expect(CarLinkValueKind kind, string description)
    {
        if (this.Kind != kind)
        {
            throw new CarLinkFormatException($"expected {description}, got {this.Describe()}");
        }
    }
}
