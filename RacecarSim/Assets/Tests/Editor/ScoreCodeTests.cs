using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NUnit.Framework;

/// <summary>
/// Score-code payload formatting and the version 2 score-code format (Utilities.Encrypt, DecryptScoreCode).
/// </summary>
public class ScoreCodeTests
{
    /// <summary>
    /// FIPS-197 appendix C.1 key (00 01 ... 0F). Public test vector, not an autograder key.
    /// </summary>
    private static readonly byte[] testKey = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();

    [TestCase("en-US")]
    [TestCase("de-DE")]
    [TestCase("fr-FR")]
    public void FormatScorePayload_UsesInvariantDecimalPoint(string cultureName)
    {
        CultureInfo previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo(cultureName);
            string payload = AutograderSummary.FormatScorePayload("lab1", 12.5f, 25.5f, "student");
            Assert.AreEqual("lab1|12.5|25.5|student", payload);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    /// <summary>
    /// IV used with testKey for the cross-language vector (10 11 ... 1F).
    /// </summary>
    private static readonly byte[] testIv = Enumerable.Range(16, 16).Select(i => (byte)i).ToArray();

    /// <summary>
    /// Produced by tools/score_code.py (Python cryptography) for testKey, testIv, and the payload
    /// below, so the C# encoder and the reference decoder agree byte for byte.
    /// </summary>
    private const string crossLanguageVector =
        "R2101112131415161718191A1B1C1D1E1F5EB617B680B85ADDA1D5B9B360F83A27493308C1C2140D25166249BBDE6F8B2044063D8A22D673762F45EAB76BD515BE";

    [Test]
    public void Encrypt_MatchesPythonReferenceVector()
    {
        Assert.AreEqual(crossLanguageVector, Utilities.Encrypt("gp2026|23.5|25|étudiant", ScoreCodeTests.testKey, ScoreCodeTests.testIv));
    }

    [TestCase("lab1|12.5|25|student")]
    [TestCase("gp2026|23.5|25|étudiant")]
    [TestCase("")]
    public void DecryptScoreCode_RoundTrips(string payload)
    {
        string code = Utilities.Encrypt(payload, ScoreCodeTests.testKey);

        StringAssert.StartsWith(Utilities.ScoreCodePrefix, code);
        StringAssert.IsMatch("^R2[0-9A-F]+$", code);
        Assert.AreEqual(payload, Utilities.DecryptScoreCode(code, ScoreCodeTests.testKey));
    }

    [Test]
    public void Encrypt_UsesFreshIvEachTime()
    {
        Assert.AreNotEqual(
            Utilities.Encrypt("lab1|12.5|25|student", ScoreCodeTests.testKey),
            Utilities.Encrypt("lab1|12.5|25|student", ScoreCodeTests.testKey));
    }

    [TestCase(2)]
    [TestCase(40)]
    [TestCase(-1)]
    public void DecryptScoreCode_RejectsTamperedCode(int position)
    {
        string code = Utilities.Encrypt("lab1|12.5|25|student", ScoreCodeTests.testKey);
        int index = position < 0 ? code.Length - 1 : position;
        char flipped = code[index] == '0' ? '1' : '0';
        string tampered = code.Substring(0, index) + flipped + code.Substring(index + 1);

        Assert.IsNull(Utilities.DecryptScoreCode(tampered, ScoreCodeTests.testKey));
    }

    [Test]
    public void DecryptScoreCode_RejectsWrongKey()
    {
        string code = Utilities.Encrypt("lab1|12.5|25|student", ScoreCodeTests.testKey);
        byte[] otherKey = Enumerable.Range(100, 16).Select(i => (byte)i).ToArray();

        Assert.IsNull(Utilities.DecryptScoreCode(code, otherKey));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("69C4E0D86A7B0430D8CDB78070B4C55A")]
    [TestCase("R2ZZ")]
    [TestCase("R200")]
    public void DecryptScoreCode_RejectsMalformedCodes(string code)
    {
        Assert.IsNull(Utilities.DecryptScoreCode(code, ScoreCodeTests.testKey));
    }

    [Test]
    public void ParseKeyHex_ReadsSixteenBytesIgnoringWhitespace()
    {
        CollectionAssert.AreEqual(ScoreCodeTests.testKey, Utilities.ParseKeyHex("00010203 04050607\n08090a0b0C0D0E0F\n"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("000102030405060708090A0B0C0D0E")]
    [TestCase("000102030405060708090A0B0C0D0E0F10")]
    [TestCase("000102030405060708090A0B0C0D0EZZ")]
    public void ParseKeyHex_RejectsMalformedText(string text)
    {
        Assert.IsNull(Utilities.ParseKeyHex(text));
    }

    [Test]
    public void Encrypt_ThrowsWhenKeyMissing()
    {
        Assert.Throws<InvalidOperationException>(() => Utilities.Encrypt("lab1", null));
    }

    [Test]
    public void UtilitiesSource_HasNoKeyLiteral()
    {
        string utilities = File.ReadAllText("Assets/Scripts/Static/Utilities.cs");
        Assert.IsFalse(Regex.IsMatch(utilities, @"0x[0-9A-Fa-f]{2}\s*,"), "Utilities.cs contains a byte-array literal.");
    }
}
