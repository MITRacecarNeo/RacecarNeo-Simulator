using NUnit.Framework;

/// <summary>
/// Command-line launch option parsing.
/// </summary>
public class LaunchOptionsTests
{
    [Test]
    public void Parse_ReadsAllOptions()
    {
        LaunchOptions.Options options = LaunchOptions.Parse(new[]
        {
            "RacecarSim.exe", "-racecarsim-level", "grand-prix-2026", "-racecarsim-mode", "Exploration",
            "-racecarsim-cars", "3", "-racecarsim-autostart", "-racecarsim-data-dir", "C:/temp/sim",
            "-racecarsim-frametime", "C:/temp/frames.csv"
        });

        Assert.AreEqual("grand-prix-2026", options.LevelId);
        Assert.AreEqual(LevelManagerMode.Exploration, options.Mode);
        Assert.AreEqual(3, options.NumCars);
        Assert.IsTrue(options.AutoStart);
        Assert.AreEqual("C:/temp/sim", options.DataDirectory);
        Assert.AreEqual("C:/temp/frames.csv", options.FrameTimePath);
    }

    [Test]
    public void Parse_DefaultsWithoutOptions()
    {
        LaunchOptions.Options options = LaunchOptions.Parse(new[] { "RacecarSim.exe", "-screen-width", "640" });

        Assert.IsNull(options.LevelId);
        Assert.AreEqual(LevelManagerMode.Race, options.Mode);
        Assert.AreEqual(1, options.NumCars);
        Assert.IsFalse(options.AutoStart);
    }

    [TestCase("-racecarsim-cars", "0")]
    [TestCase("-racecarsim-cars", "many")]
    [TestCase("-racecarsim-mode", "sprint")]
    public void Parse_IgnoresInvalidValues(string option, string value)
    {
        LaunchOptions.Options options = LaunchOptions.Parse(new[] { option, value });

        Assert.AreEqual(1, options.NumCars);
        Assert.AreEqual(LevelManagerMode.Race, options.Mode);
    }

    [Test]
    public void Parse_ToleratesTrailingOption()
    {
        Assert.IsNull(LaunchOptions.Parse(new[] { "-racecarsim-level" }).LevelId);
    }
}
