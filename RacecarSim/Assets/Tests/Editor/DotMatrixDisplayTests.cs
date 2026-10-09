using System.Text;
using NUnit.Framework;

/// <summary>
/// DotMatrixDisplay frames against references rendered with luma.core's legacy text() and the
/// car driver's compositing (dotmatrix_node): mode glyphs and labels, text, scrolling, splash.
/// </summary>
public class DotMatrixDisplayTests
{
    /// <summary>
    /// Time after the splash ends (in seconds).
    /// </summary>
    private const float afterSplash = 100;

    [Test]
    public void Manual_GlyphAndLabel()
    {
        DotMatrixDisplayTests.AssertFrame(new[]
        {
            "..XXXX..................",
            ".X....X.................",
            "X..XX..X..X.X..X..X..X..",
            "X.X..X.X..XXX.X.X.XX.X..",
            "X.X..X.X..XXX.XXX.X.XX..",
            "X..XX..X..X.X.X.X.X..X..",
            ".X....X...X.X.X.X.X..X..",
            "..XXXX..................",
        }, new DotMatrixDisplay(0).Frame(new ActuatorCommands(), DotMatrixDisplay.DriveMode.Manual, DotMatrixDisplayTests.afterSplash));
    }

    [Test]
    public void Idle_GlyphAndLabel()
    {
        DotMatrixDisplayTests.AssertFrame(new[]
        {
            "........................",
            ".XX..XX.................",
            ".XX..XX.XXX.XX..X...XXX.",
            ".XX..XX..X..X.X.X...X...",
            ".XX..XX..X..X.X.X...XX..",
            ".XX..XX..X..X.X.X...X...",
            ".XX..XX.XXX.XX..XXX.XXX.",
            "........................",
        }, new DotMatrixDisplay(0).Frame(new ActuatorCommands(), DotMatrixDisplay.DriveMode.Idle, DotMatrixDisplayTests.afterSplash));
    }

    [Test]
    public void Auto_GlyphAndLabel()
    {
        DotMatrixDisplayTests.AssertFrame(new[]
        {
            ".X......................",
            ".XX.....................",
            ".XXX.....X..X.X.XXX.XXX.",
            ".XXXX...X.X.X.X..X..X.X.",
            ".XXXXX..XXX.X.X..X..X.X.",
            ".XXXX...X.X.X.X..X..X.X.",
            ".XXX....X.X.XXX..X..XXX.",
            ".XX.....................",
        }, new DotMatrixDisplay(0).Frame(new ActuatorCommands(), DotMatrixDisplay.DriveMode.Auto, DotMatrixDisplayTests.afterSplash));
    }

    [Test]
    public void Text_ThatFits_DoesNotScroll()
    {
        ActuatorCommands commands = new ActuatorCommands();
        commands.SetText("HI 42");
        DotMatrixDisplay display = new DotMatrixDisplay(0);
        display.Frame(commands, DotMatrixDisplay.DriveMode.Auto, 10);
        DotMatrixDisplayTests.AssertFrame(new[]
        {
            "........................",
            "........................",
            "X.X.XXX.....X.X.XX......",
            "X.X..X......X.X...X.....",
            "XXX..X......XXX..X......",
            "X.X..X........X.X.......",
            "X.X.XXX.......X.XXX.....",
            "........................",
        }, display.Frame(commands, DotMatrixDisplay.DriveMode.Auto, 11));
    }

    [Test]
    public void Text_ScrollsFromTheRightEdge()
    {
        ActuatorCommands commands = new ActuatorCommands();
        commands.SetText("Hello, RACECAR!");
        DotMatrixDisplay display = new DotMatrixDisplay(0);

        bool[,] start = display.Frame(commands, DotMatrixDisplay.DriveMode.Auto, 10);
        foreach (bool pixel in start)
        {
            Assert.IsFalse(pixel, "the message starts just past the right edge");
        }

        DotMatrixDisplayTests.AssertFrame(new[]
        {
            "........................",
            "........................",
            "...............XXX..X...",
            ".X.X..X........X.X.X.X.X",
            ".X.X.X.X.......XX..XXX.X",
            ".X.X.X.X.X.....X.X.X.X.X",
            ".X.X..X..X.....X.X.X.X..",
            "........................",
        }, display.Frame(commands, DotMatrixDisplay.DriveMode.Auto, 11.7f));
    }

    [Test]
    public void SameText_KeepsScrolling()
    {
        ActuatorCommands commands = new ActuatorCommands();
        commands.SetText("Hello, RACECAR!");
        DotMatrixDisplay display = new DotMatrixDisplay(0);
        display.Frame(commands, DotMatrixDisplay.DriveMode.Auto, 10);
        commands.SetText("Hello, RACECAR!");
        bool[,] resent = display.Frame(commands, DotMatrixDisplay.DriveMode.Auto, 11.7f);
        Assert.IsTrue(resent[2, 15], "resending the same text does not restart the scroll");
    }

    [Test]
    public void Splash_ScrollsOnceAfterLoad()
    {
        DotMatrixDisplay display = new DotMatrixDisplay(0);
        DotMatrixDisplayTests.AssertFrame(new[]
        {
            "........................",
            "........................",
            ".........X..............",
            ".X..X.X.X.X......X...X..",
            "X.X.XXX.XXX.....XXX.X.X.",
            "X.X.X.X.X........X..X.X.",
            ".X..X.X..XX......XX..X..",
            "........................",
        }, display.Frame(new ActuatorCommands(), DotMatrixDisplay.DriveMode.Manual, 3));
    }

    [Test]
    public void Pixels_OverrideTextAndIdle()
    {
        ActuatorCommands commands = new ActuatorCommands();
        bool[,] pixels = new bool[ActuatorCommands.MatrixRows, ActuatorCommands.MatrixColumns];
        pixels[0, 0] = true;
        pixels[7, 23] = true;
        commands.SetMatrix(pixels);
        bool[,] frame = new DotMatrixDisplay(0).Frame(commands, DotMatrixDisplay.DriveMode.Manual, 3);
        Assert.AreEqual(pixels, frame);
    }

    [Test]
    public void RenderedWidth_MatchesLuma()
    {
        Assert.AreEqual(51, DotMatrixFont.RenderedWidth("Hello, RACECAR!"));
        Assert.AreEqual(12, DotMatrixFont.RenderedWidth("MAN"));
        Assert.AreEqual(120, DotMatrixFont.RenderedWidth(DotMatrixDisplay.SplashMessage));
    }

    private static void AssertFrame(string[] expected, bool[,] frame)
    {
        StringBuilder actual = new StringBuilder();
        for (int row = 0; row < frame.GetLength(0); row++)
        {
            for (int column = 0; column < frame.GetLength(1); column++)
            {
                actual.Append(frame[row, column] ? 'X' : '.');
            }
            actual.Append('\n');
        }
        Assert.AreEqual(string.Join("\n", expected) + "\n", actual.ToString());
    }
}
