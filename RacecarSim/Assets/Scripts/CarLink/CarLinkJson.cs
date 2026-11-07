using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

/// <summary>
/// JSON reader and writer for car link messages (rosbridge text frames and the car's HTTP API).
/// </summary>
/// <remarks>
/// Accepts NaN, Infinity, and -Infinity as numbers, as Python's json module writes them (laser
/// ranges carry infinity for no return). Integers that fit a long read as Integer, all other
/// numbers as Float. Numbers use the invariant culture in both directions.
/// </remarks>
public static class CarLinkJson
{
    #region Constants
    /// <summary>
    /// Deepest nesting of lists and objects accepted.
    /// </summary>
    public const int MaxDepth = 64;
    #endregion

    #region Public Interface
    /// <summary>
    /// Parses one JSON document.
    /// </summary>
    /// <exception cref="CarLinkFormatException">The text is not valid JSON.</exception>
    public static CarLinkValue Parse(string text)
    {
        Reader reader = new Reader(text);
        reader.SkipWhitespace();
        CarLinkValue value = reader.ReadValue(0);
        reader.SkipWhitespace();
        if (!reader.AtEnd)
        {
            throw reader.Error("unexpected text after the value");
        }
        return value;
    }

    /// <summary>
    /// Writes a value as compact JSON. Typed arrays are written as lists; byte strings as base64.
    /// </summary>
    public static string Write(CarLinkValue value)
    {
        StringBuilder builder = new StringBuilder();
        CarLinkJson.WriteValue(builder, value, -1);
        return builder.ToString();
    }

    /// <summary>
    /// Writes a value as JSON with each map entry on its own line, indented two spaces per level;
    /// lists stay on one line.
    /// </summary>
    public static string WriteIndented(CarLinkValue value)
    {
        StringBuilder builder = new StringBuilder();
        CarLinkJson.WriteValue(builder, value, 0);
        return builder.Append('\n').ToString();
    }
    #endregion

    /// <param name="depth">Nesting level for indented output; negative for compact.</param>
    private static void WriteValue(StringBuilder builder, CarLinkValue value, int depth)
    {
        switch (value.Kind)
        {
            case CarLinkValueKind.Null:
                builder.Append("null");
                break;
            case CarLinkValueKind.Bool:
                builder.Append(value.AsBool() ? "true" : "false");
                break;
            case CarLinkValueKind.Integer:
                builder.Append(value.AsLong().ToString(CultureInfo.InvariantCulture));
                break;
            case CarLinkValueKind.Float:
                CarLinkJson.WriteNumber(builder, value.AsDouble());
                break;
            case CarLinkValueKind.String:
                CarLinkJson.WriteString(builder, value.AsString());
                break;
            case CarLinkValueKind.Bytes:
                CarLinkJson.WriteString(builder, Convert.ToBase64String(value.AsBytes()));
                break;
            case CarLinkValueKind.SBytes:
            case CarLinkValueKind.Floats:
            case CarLinkValueKind.Doubles:
            case CarLinkValueKind.Longs:
                bool isInteger = value.Kind == CarLinkValueKind.SBytes || value.Kind == CarLinkValueKind.Longs;
                double[] numbers = value.AsDoubles();
                builder.Append('[');
                for (int i = 0; i < numbers.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }
                    if (isInteger)
                    {
                        builder.Append(((long)numbers[i]).ToString(CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        CarLinkJson.WriteNumber(builder, numbers[i]);
                    }
                }
                builder.Append(']');
                break;
            case CarLinkValueKind.List:
                builder.Append('[');
                for (int i = 0; i < value.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(',');
                    }
                    CarLinkJson.WriteValue(builder, value[i], depth);
                }
                builder.Append(']');
                break;
            case CarLinkValueKind.Map:
                builder.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, CarLinkValue> entry in value.Entries)
                {
                    if (!first)
                    {
                        builder.Append(',');
                    }
                    first = false;
                    if (depth >= 0)
                    {
                        builder.Append('\n').Append(' ', 2 * (depth + 1));
                    }
                    CarLinkJson.WriteString(builder, entry.Key);
                    builder.Append(depth >= 0 ? ": " : ":");
                    CarLinkJson.WriteValue(builder, entry.Value, depth >= 0 ? depth + 1 : -1);
                }
                if (depth >= 0 && !first)
                {
                    builder.Append('\n').Append(' ', 2 * depth);
                }
                builder.Append('}');
                break;
        }
    }

    private static void WriteNumber(StringBuilder builder, double number)
    {
        if (double.IsNaN(number))
        {
            builder.Append("NaN");
        }
        else if (double.IsPositiveInfinity(number))
        {
            builder.Append("Infinity");
        }
        else if (double.IsNegativeInfinity(number))
        {
            builder.Append("-Infinity");
        }
        else if (Math.Floor(number) == number && Math.Abs(number) < 1e15)
        {
            // Keep a decimal point so an integral float reads back as a float
            builder.Append(number.ToString("F1", CultureInfo.InvariantCulture));
        }
        else
        {
            builder.Append(number.ToString("R", CultureInfo.InvariantCulture));
        }
    }

    private static void WriteString(StringBuilder builder, string text)
    {
        builder.Append('"');
        foreach (char c in text)
        {
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                default:
                    if (c < 0x20)
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }
        builder.Append('"');
    }

    private sealed class Reader
    {
        private readonly string text;
        private int position;

        public Reader(string text)
        {
            this.text = text ?? throw new CarLinkFormatException("no JSON text");
        }

        public bool AtEnd { get { return this.position >= this.text.Length; } }

        public CarLinkFormatException Error(string message)
        {
            return new CarLinkFormatException($"JSON: {message} at character {this.position}");
        }

        public void SkipWhitespace()
        {
            while (!this.AtEnd)
            {
                char c = this.text[this.position];
                if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                {
                    break;
                }
                this.position++;
            }
        }

        public CarLinkValue ReadValue(int depth)
        {
            if (depth > CarLinkJson.MaxDepth)
            {
                throw this.Error("nesting too deep");
            }
            if (this.AtEnd)
            {
                throw this.Error("unexpected end");
            }
            char c = this.text[this.position];
            switch (c)
            {
                case '{':
                    return this.ReadObject(depth);
                case '[':
                    return this.ReadArray(depth);
                case '"':
                    return CarLinkValue.FromString(this.ReadString());
                case 't':
                    this.ExpectWord("true");
                    return CarLinkValue.FromBool(true);
                case 'f':
                    this.ExpectWord("false");
                    return CarLinkValue.FromBool(false);
                case 'n':
                    this.ExpectWord("null");
                    return CarLinkValue.Null;
                case 'N':
                    this.ExpectWord("NaN");
                    return CarLinkValue.FromDouble(double.NaN);
                case 'I':
                    this.ExpectWord("Infinity");
                    return CarLinkValue.FromDouble(double.PositiveInfinity);
                default:
                    if (c == '-' || (c >= '0' && c <= '9'))
                    {
                        return this.ReadNumber();
                    }
                    throw this.Error($"unexpected character '{c}'");
            }
        }

        private CarLinkValue ReadObject(int depth)
        {
            this.position++;
            Dictionary<string, CarLinkValue> map = new Dictionary<string, CarLinkValue>();
            this.SkipWhitespace();
            if (this.Peek() == '}')
            {
                this.position++;
                return CarLinkValue.FromMap(map);
            }
            while (true)
            {
                this.SkipWhitespace();
                if (this.Peek() != '"')
                {
                    throw this.Error("expected a key");
                }
                string key = this.ReadString();
                this.SkipWhitespace();
                this.ExpectChar(':');
                this.SkipWhitespace();
                map[key] = this.ReadValue(depth + 1);
                this.SkipWhitespace();
                char next = this.Next();
                if (next == '}')
                {
                    return CarLinkValue.FromMap(map);
                }
                if (next != ',')
                {
                    this.position--;
                    throw this.Error("expected ',' or '}'");
                }
            }
        }

        private CarLinkValue ReadArray(int depth)
        {
            this.position++;
            List<CarLinkValue> list = new List<CarLinkValue>();
            this.SkipWhitespace();
            if (this.Peek() == ']')
            {
                this.position++;
                return CarLinkValue.FromList(list);
            }
            while (true)
            {
                this.SkipWhitespace();
                list.Add(this.ReadValue(depth + 1));
                this.SkipWhitespace();
                char next = this.Next();
                if (next == ']')
                {
                    return CarLinkValue.FromList(list);
                }
                if (next != ',')
                {
                    this.position--;
                    throw this.Error("expected ',' or ']'");
                }
            }
        }

        private string ReadString()
        {
            this.position++;
            StringBuilder builder = new StringBuilder();
            while (true)
            {
                if (this.AtEnd)
                {
                    throw this.Error("unterminated string");
                }
                char c = this.text[this.position++];
                if (c == '"')
                {
                    return builder.ToString();
                }
                if (c != '\\')
                {
                    if (c < 0x20)
                    {
                        this.position--;
                        throw this.Error("control character in a string");
                    }
                    builder.Append(c);
                    continue;
                }
                char escape = this.Next();
                switch (escape)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escape);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        if (this.position + 4 > this.text.Length
                            || !int.TryParse(this.text.Substring(this.position, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                        {
                            throw this.Error("bad \\u escape");
                        }
                        this.position += 4;
                        builder.Append((char)code);
                        break;
                    default:
                        throw this.Error($"bad escape '\\{escape}'");
                }
            }
        }

        private CarLinkValue ReadNumber()
        {
            int start = this.position;
            if (this.Peek() == '-')
            {
                this.position++;
                if (this.Peek() == 'I')
                {
                    this.ExpectWord("Infinity");
                    return CarLinkValue.FromDouble(double.NegativeInfinity);
                }
            }
            bool isFloat = false;
            while (!this.AtEnd)
            {
                char c = this.text[this.position];
                if (c >= '0' && c <= '9')
                {
                    this.position++;
                }
                else if (c == '.' || c == 'e' || c == 'E' || c == '+' || c == '-')
                {
                    isFloat = true;
                    this.position++;
                }
                else
                {
                    break;
                }
            }
            string token = this.text.Substring(start, this.position - start);
            if (!isFloat && long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long integer))
            {
                return CarLinkValue.FromLong(integer);
            }
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                return CarLinkValue.FromDouble(number);
            }
            this.position = start;
            throw this.Error($"bad number '{token}'");
        }

        private void ExpectWord(string word)
        {
            if (string.CompareOrdinal(this.text, this.position, word, 0, word.Length) != 0)
            {
                throw this.Error($"expected '{word}'");
            }
            this.position += word.Length;
        }

        private void ExpectChar(char expected)
        {
            if (this.Peek() != expected)
            {
                throw this.Error($"expected '{expected}'");
            }
            this.position++;
        }

        private char Peek()
        {
            return this.AtEnd ? '\0' : this.text[this.position];
        }

        private char Next()
        {
            if (this.AtEnd)
            {
                throw this.Error("unexpected end");
            }
            return this.text[this.position++];
        }
    }
}
