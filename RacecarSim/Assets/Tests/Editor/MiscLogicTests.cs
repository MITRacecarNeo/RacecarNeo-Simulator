using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Small pure-logic helpers: Konami Code matching and sensor noise sampling.
/// </summary>
public class MiscLogicTests
{
    [Test]
    public void NextKonamiIndex_FullCodeMatches()
    {
        int matched = 0;
        foreach (KeyCode key in MainMenu.KonamiCodes)
        {
            matched = MainMenu.NextKonamiIndex(matched, key);
        }
        Assert.AreEqual(MainMenu.KonamiCodes.Length, matched);
    }

    [Test]
    public void NextKonamiIndex_ExtraLeadingKeyStillMatches()
    {
        KeyCode[] typed = new[] { MainMenu.KonamiCodes[0] }.Concat(MainMenu.KonamiCodes).ToArray();
        int matched = 0;
        foreach (KeyCode key in typed)
        {
            matched = MainMenu.NextKonamiIndex(matched, key);
        }
        Assert.AreEqual(MainMenu.KonamiCodes.Length, matched);
    }

    [Test]
    public void NextKonamiIndex_UnrelatedKeyResets()
    {
        Assert.AreEqual(0, MainMenu.NextKonamiIndex(3, KeyCode.Z));
    }

    [Test]
    public void NormalDist_SamplesAreFiniteWithExpectedMoments()
    {
        const int count = 20000;
        float[] samples = Enumerable.Range(0, count).Select(_ => NormalDist.Random(2, 0.5f)).ToArray();

        Assert.IsTrue(samples.All(x => !float.IsNaN(x) && !float.IsInfinity(x)));
        double mean = samples.Average();
        double sdev = System.Math.Sqrt(samples.Select(x => (x - mean) * (x - mean)).Average());
        Assert.AreEqual(2.0, mean, 0.02);
        Assert.AreEqual(0.5, sdev, 0.02);
    }
}
