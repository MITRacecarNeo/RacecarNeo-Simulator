using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// CarLinkJson parsing, writing, and the CarLinkValue accessors it feeds.
/// </summary>
public class CarLinkJsonTests
{
    [Test]
    public void Parse_ReadsEveryKind()
    {
        CarLinkValue value = CarLinkJson.Parse(
            "{\"n\": null, \"t\": true, \"f\": false, \"i\": -42, \"d\": 2.5e-3, \"s\": \"a\\\"b\\u00e9\\n\", \"l\": [1, 2.0, \"x\"], \"m\": {\"k\": {}}}");
        Assert.AreEqual(CarLinkValueKind.Map, value.Kind);
        Assert.IsTrue(value["n"].IsNull);
        Assert.IsTrue(value["t"].AsBool());
        Assert.IsFalse(value["f"].AsBool());
        Assert.AreEqual(CarLinkValueKind.Integer, value["i"].Kind);
        Assert.AreEqual(-42, value["i"].AsLong());
        Assert.AreEqual(CarLinkValueKind.Float, value["d"].Kind);
        Assert.AreEqual(0.0025, value["d"].AsDouble(), 1e-12);
        Assert.AreEqual("a\"b\u00e9\n", value["s"].AsString());
        Assert.AreEqual(3, value["l"].Count);
        Assert.AreEqual(2, value["l"][1].AsLong());
        Assert.AreEqual(CarLinkValueKind.Map, value.At("m.k").Kind);
    }

    [Test]
    public void Parse_AcceptsNanAndInfinity()
    {
        // Python's json module writes these for laser ranges with no return
        float[] ranges = CarLinkJson.Parse("[Infinity, -Infinity, NaN, 1.5]").AsFloats();
        Assert.IsTrue(float.IsPositiveInfinity(ranges[0]));
        Assert.IsTrue(float.IsNegativeInfinity(ranges[1]));
        Assert.IsTrue(float.IsNaN(ranges[2]));
        Assert.AreEqual(1.5f, ranges[3]);
    }

    [Test]
    public void Parse_LargeIntegerBecomesFloat()
    {
        CarLinkValue value = CarLinkJson.Parse("123456789012345678901234");
        Assert.AreEqual(CarLinkValueKind.Float, value.Kind);
        Assert.AreEqual(1.2345678901234568e23, value.AsDouble(), 1e9);
    }

    [TestCase("")]
    [TestCase("{")]
    [TestCase("[1,]")]
    [TestCase("{\"a\" 1}")]
    [TestCase("tru")]
    [TestCase("\"open")]
    [TestCase("1 2")]
    [TestCase("\"bad \\q escape\"")]
    [TestCase("{'a': 1}")]
    public void Parse_RejectsMalformed(string text)
    {
        Assert.Throws<CarLinkFormatException>(() => CarLinkJson.Parse(text));
    }

    [Test]
    public void Parse_RejectsDeepNesting()
    {
        string deep = new string('[', CarLinkJson.MaxDepth + 2) + new string(']', CarLinkJson.MaxDepth + 2);
        Assert.Throws<CarLinkFormatException>(() => CarLinkJson.Parse(deep));
        string allowed = new string('[', CarLinkJson.MaxDepth) + new string(']', CarLinkJson.MaxDepth);
        Assert.DoesNotThrow(() => CarLinkJson.Parse(allowed));
    }

    [Test]
    public void Write_RoundTrips()
    {
        string text = "{\"op\":\"publish\",\"topic\":\"/sim_drive\",\"msg\":{\"a\":[1,-2,3],\"b\":0.25,\"c\":1.0,\"d\":\"q\\\"\\\\\",\"e\":null,\"f\":true}}";
        CarLinkValue value = CarLinkJson.Parse(text);
        CarLinkValue again = CarLinkJson.Parse(CarLinkJson.Write(value));
        Assert.AreEqual("/sim_drive", again["topic"].AsString());
        Assert.AreEqual(-2, again.At("msg.a")[1].AsLong());
        Assert.AreEqual(0.25, again.At("msg.b").AsDouble());
        Assert.AreEqual(CarLinkValueKind.Float, again.At("msg.c").Kind);
        Assert.AreEqual("q\"\\", again.At("msg.d").AsString());
        Assert.IsTrue(again.At("msg.e").IsNull);
        Assert.IsTrue(again.At("msg.f").AsBool());
    }

    [Test]
    public void Write_UsesInvariantNumbers()
    {
        System.Globalization.CultureInfo previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            Assert.AreEqual("[0.5,-1.25]", CarLinkJson.Write(CarLinkValue.FromList(new List<CarLinkValue>
            {
                CarLinkValue.FromDouble(0.5),
                CarLinkValue.FromDouble(-1.25),
            })));
            Assert.AreEqual(0.5, CarLinkJson.Parse("0.5").AsDouble());
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }

    [Test]
    public void Write_TypedArraysAsLists()
    {
        Assert.AreEqual("[-1,0,100]", CarLinkJson.Write(CarLinkValue.FromSBytes(new sbyte[] { -1, 0, 100 })));
        Assert.AreEqual("[0.5,1.0]", CarLinkJson.Write(CarLinkValue.FromFloats(new[] { 0.5f, 1f })));
        Assert.AreEqual("\"AQL/\"", CarLinkJson.Write(CarLinkValue.FromBytes(new byte[] { 1, 2, 255 })));
    }

    [Test]
    public void Accessors_ConvertBetweenArrayForms()
    {
        Assert.AreEqual(new sbyte[] { -1, 0, 100 }, CarLinkJson.Parse("[-1, 0, 100]").AsSBytes());
        Assert.AreEqual(new byte[] { 1, 2, 255 }, CarLinkJson.Parse("\"AQL/\"").AsBytes());
        Assert.AreEqual(new byte[] { 1, 2, 255 }, CarLinkJson.Parse("[1, 2, 255]").AsBytes());
        Assert.Throws<CarLinkFormatException>(() => CarLinkJson.Parse("[128]").AsSBytes());
        Assert.Throws<CarLinkFormatException>(() => CarLinkJson.Parse("[256]").AsBytes());
    }

    [Test]
    public void Accessors_MissingKeysReadNull()
    {
        CarLinkValue value = CarLinkJson.Parse("{\"a\": {\"b\": 1}}");
        Assert.IsTrue(value["missing"].IsNull);
        Assert.IsTrue(value.At("a.b.c").IsNull);
        Assert.IsTrue(value["a"][3].IsNull);
        Assert.AreEqual(1, value.At("a.b").AsInt());
    }

    [Test]
    public void Accessors_KindMismatchThrows()
    {
        CarLinkValue value = CarLinkJson.Parse("{\"s\": \"x\", \"d\": 1.5}");
        Assert.Throws<CarLinkFormatException>(() => value["s"].AsDouble());
        Assert.Throws<CarLinkFormatException>(() => value["d"].AsLong());
        Assert.Throws<CarLinkFormatException>(() => value["d"].AsBool());
        Assert.AreEqual(1, CarLinkJson.Parse("1.0").AsLong());
    }
}
