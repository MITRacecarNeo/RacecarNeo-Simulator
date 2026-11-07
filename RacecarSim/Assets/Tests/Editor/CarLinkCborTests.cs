using System;
using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// CarLinkCbor against RFC 8949 appendix A vectors and RFC 8746 typed arrays.
/// </summary>
public class CarLinkCborTests
{
    private static byte[] Hex(string hex)
    {
        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    [TestCase("00", 0L)]
    [TestCase("17", 23L)]
    [TestCase("1818", 24L)]
    [TestCase("1903e8", 1000L)]
    [TestCase("1a000f4240", 1000000L)]
    [TestCase("1b000000e8d4a51000", 1000000000000L)]
    [TestCase("20", -1L)]
    [TestCase("3863", -100L)]
    [TestCase("3903e7", -1000L)]
    public void Integers(string hex, long expected)
    {
        CarLinkValue value = CarLinkCbor.Read(CarLinkCborTests.Hex(hex));
        Assert.AreEqual(CarLinkValueKind.Integer, value.Kind);
        Assert.AreEqual(expected, value.AsLong());
    }

    [TestCase("f90000", 0.0)]
    [TestCase("f93c00", 1.0)]
    [TestCase("f93e00", 1.5)]
    [TestCase("f97bff", 65504.0)]
    [TestCase("f90001", 5.960464477539063e-8)]
    [TestCase("f9c400", -4.0)]
    [TestCase("fa47c35000", 100000.0)]
    [TestCase("fb3ff199999999999a", 1.1)]
    [TestCase("fbc010666666666666", -4.1)]
    public void Floats(string hex, double expected)
    {
        Assert.AreEqual(expected, CarLinkCbor.Read(CarLinkCborTests.Hex(hex)).AsDouble(), Math.Abs(expected) * 1e-7);
    }

    [Test]
    public void SpecialFloats()
    {
        Assert.IsTrue(double.IsPositiveInfinity(CarLinkCbor.Read(CarLinkCborTests.Hex("f97c00")).AsDouble()));
        Assert.IsTrue(double.IsNaN(CarLinkCbor.Read(CarLinkCborTests.Hex("f97e00")).AsDouble()));
        Assert.IsTrue(double.IsNegativeInfinity(CarLinkCbor.Read(CarLinkCborTests.Hex("fbfff0000000000000")).AsDouble()));
    }

    [Test]
    public void SimpleValues()
    {
        Assert.IsFalse(CarLinkCbor.Read(CarLinkCborTests.Hex("f4")).AsBool());
        Assert.IsTrue(CarLinkCbor.Read(CarLinkCborTests.Hex("f5")).AsBool());
        Assert.IsTrue(CarLinkCbor.Read(CarLinkCborTests.Hex("f6")).IsNull);
        Assert.IsTrue(CarLinkCbor.Read(CarLinkCborTests.Hex("f7")).IsNull);
    }

    [Test]
    public void Strings()
    {
        Assert.AreEqual("", CarLinkCbor.Read(CarLinkCborTests.Hex("60")).AsString());
        Assert.AreEqual("IETF", CarLinkCbor.Read(CarLinkCborTests.Hex("6449455446")).AsString());
        Assert.AreEqual("\u00fc", CarLinkCbor.Read(CarLinkCborTests.Hex("62c3bc")).AsString());
        Assert.AreEqual(new byte[] { 1, 2, 3, 4 }, CarLinkCbor.Read(CarLinkCborTests.Hex("4401020304")).AsBytes());
        Assert.AreEqual("streaming", CarLinkCbor.Read(CarLinkCborTests.Hex("7f657374726561646d696e67ff")).AsString());
        Assert.AreEqual(new byte[] { 1, 2, 3, 4, 5 }, CarLinkCbor.Read(CarLinkCborTests.Hex("5f42010243030405ff")).AsBytes());
    }

    [Test]
    public void ArraysAndMaps()
    {
        CarLinkValue nested = CarLinkCbor.Read(CarLinkCborTests.Hex("8301820203820405"));
        Assert.AreEqual(3, nested.Count);
        Assert.AreEqual(5, nested[2][1].AsLong());

        CarLinkValue map = CarLinkCbor.Read(CarLinkCborTests.Hex("a26161016162820203"));
        Assert.AreEqual(1, map["a"].AsLong());
        Assert.AreEqual(3, map["b"][1].AsLong());

        CarLinkValue indefinite = CarLinkCbor.Read(CarLinkCborTests.Hex("bf61610161629f0203ffff"));
        Assert.AreEqual(3, indefinite["b"][1].AsLong());

        Assert.AreEqual(25, CarLinkCbor.Read(CarLinkCborTests.Hex("98190102030405060708090a0b0c0d0e0f101112131415161718181819")).Count);
    }

    [Test]
    public void OtherTagsReadTheirContent()
    {
        // Tag 1 (epoch time) around an integer
        Assert.AreEqual(1363896240, CarLinkCbor.Read(CarLinkCborTests.Hex("c11a514b67b0")).AsLong());
    }

    [Test]
    public void TypedArrays()
    {
        // Tag 85: float32 little endian [1.5, -2]
        float[] floats = CarLinkCbor.Read(CarLinkCborTests.Hex("d855480000c03f000000c0")).AsFloats();
        Assert.AreEqual(new[] { 1.5f, -2f }, floats);

        // Tag 81: float32 big endian [1.5]
        Assert.AreEqual(new[] { 1.5f }, CarLinkCbor.Read(CarLinkCborTests.Hex("d851443fc00000")).AsFloats());

        // Tag 86: float64 little endian [0.25]
        Assert.AreEqual(new[] { 0.25 }, CarLinkCbor.Read(CarLinkCborTests.Hex("d85648000000000000d03f")).AsDoubles());

        // Tag 72: int8 [-1, 0, 100]
        CarLinkValue int8 = CarLinkCbor.Read(CarLinkCborTests.Hex("d84843ff0064"));
        Assert.AreEqual(CarLinkValueKind.SBytes, int8.Kind);
        Assert.AreEqual(new sbyte[] { -1, 0, 100 }, int8.AsSBytes());

        // Tag 64: uint8
        Assert.AreEqual(CarLinkValueKind.Bytes, CarLinkCbor.Read(CarLinkCborTests.Hex("d8404201ff")).Kind);

        // Tag 77: int16 little endian [-2, 300]
        Assert.AreEqual(new[] { -2.0, 300.0 }, CarLinkCbor.Read(CarLinkCborTests.Hex("d84d44feff2c01")).AsDoubles());

        // Tag 70: uint32 little endian [4000000000]
        Assert.AreEqual(4000000000.0, CarLinkCbor.Read(CarLinkCborTests.Hex("d8464400286bee")).AsDoubles()[0]);
    }

    [Test]
    public void TypedArray_WrongLengthThrows()
    {
        Assert.Throws<CarLinkFormatException>(() => CarLinkCbor.Read(CarLinkCborTests.Hex("d855430000c0")));
    }

    [Test]
    public void LargeOccupancyGrid()
    {
        // A 1000 x 1000 int8 grid as rosbridge sends it: map with a tag 72 byte string
        int cells = 1000000;
        List<byte> data = new List<byte>(cells + 32);
        // Map of one key "data", then tag 72 around a byte string with a 4-byte length (1000000)
        data.AddRange(CarLinkCborTests.Hex("a16464617461"));
        data.AddRange(CarLinkCborTests.Hex("d8485a000f4240"));
        for (int i = 0; i < cells; i++)
        {
            data.Add((byte)(i % 3 == 0 ? 0xff : i % 3 == 1 ? 0 : 100));
        }
        sbyte[] grid = CarLinkCbor.Read(data.ToArray())["data"].AsSBytes();
        Assert.AreEqual(cells, grid.Length);
        Assert.AreEqual(-1, grid[0]);
        Assert.AreEqual(0, grid[1]);
        Assert.AreEqual(100, grid[999998]);
    }

    [TestCase("")]
    [TestCase("18")]
    [TestCase("62c3")]
    [TestCase("8201")]
    [TestCase("9f01")]
    [TestCase("ff")]
    [TestCase("a10102")]
    [TestCase("1b8000000000000000")]
    [TestCase("5bffffffffffffffff")]
    [TestCase("0000")]
    public void RejectsMalformed(string hex)
    {
        Assert.Throws<CarLinkFormatException>(() => CarLinkCbor.Read(CarLinkCborTests.Hex(hex)));
    }

    [Test]
    public void RejectsDeepNesting()
    {
        byte[] deep = new byte[CarLinkCbor.MaxDepth + 2];
        for (int i = 0; i < deep.Length - 1; i++)
        {
            deep[i] = 0x81;
        }
        deep[deep.Length - 1] = 0x00;
        Assert.Throws<CarLinkFormatException>(() => CarLinkCbor.Read(deep));
    }
}
