/// <summary>
/// Composes the 8x24 dot matrix frame as the RACECAR Neo V2 driver does (dotmatrix_node): the
/// program's pixels, else its scrolling text, else a one-time welcome splash, else the drive
/// mode glyph and label.
/// </summary>
/// <remarks>
/// Unlike the driver, pixels and text stay until the program replaces them or disconnects
/// rather than expiring after 5 s and 6 s.
/// </remarks>
public class DotMatrixDisplay
{
    #region Constants
    /// <summary>
    /// Time for scrolling text to pass fully across the display (in seconds).
    /// </summary>
    public const float ScrollPeriod = 4.0f;

    /// <summary>
    /// The welcome message scrolled once when the level loads.
    /// </summary>
    public const string SplashMessage = ">>> Welcome to RACECAR Neo! >>>";

    /// <summary>
    /// Time for the splash to pass across the display (in seconds).
    /// </summary>
    public const float SplashPeriod = 8.0f;

    private const int rows = ActuatorCommands.MatrixRows;

    private const int columns = ActuatorCommands.MatrixColumns;

    /// <summary>
    /// Text and labels are drawn from this row, as the driver does.
    /// </summary>
    private const int textRow = 1;

    /// <summary>
    /// Width of the mode glyph; the label is centered in the columns to its right.
    /// </summary>
    private const int glyphWidth = 8;

    private static readonly string[] idleGlyph =
    {
        "........",
        ".XX..XX.",
        ".XX..XX.",
        ".XX..XX.",
        ".XX..XX.",
        ".XX..XX.",
        ".XX..XX.",
        "........",
    };

    private static readonly string[] manualGlyph =
    {
        "..XXXX..",
        ".X....X.",
        "X..XX..X",
        "X.X..X.X",
        "X.X..X.X",
        "X..XX..X",
        ".X....X.",
        "..XXXX..",
    };

    private static readonly string[] autoGlyph =
    {
        ".X......",
        ".XX.....",
        ".XXX....",
        ".XXXX...",
        ".XXXXX..",
        ".XXXX...",
        ".XXX....",
        ".XX.....",
    };
    #endregion

    #region Public Interface
    /// <summary>
    /// The drive modes the idle display shows, as on the car's gamepad mux.
    /// </summary>
    public enum DriveMode
    {
        /// <summary>
        /// A program is connected but not running.
        /// </summary>
        Idle,

        /// <summary>
        /// Keyboard or controller driving (no program).
        /// </summary>
        Manual,

        /// <summary>
        /// A program is running.
        /// </summary>
        Auto
    }

    /// <summary>
    /// Creates a display whose splash starts at the given time.
    /// </summary>
    /// <param name="startTime">Time the level loaded (in seconds).</param>
    public DotMatrixDisplay(float startTime)
    {
        this.splashStart = startTime;
    }

    /// <summary>
    /// Pixel offset of a left-scrolling message: it enters from the right edge and leaves past the
    /// left edge once per period; 0 when it fits the display.
    /// </summary>
    public static int ScrollOffset(float elapsed, int totalWidth, float period)
    {
        if (totalWidth <= DotMatrixDisplay.columns || period <= 0)
        {
            return 0;
        }
        int travel = totalWidth + DotMatrixDisplay.columns;
        float phase = (elapsed % period) / period;
        return (int)(phase * travel) - DotMatrixDisplay.columns;
    }

    /// <summary>
    /// Composes the frame shown at a time.
    /// </summary>
    /// <param name="commands">The program's matrix contents.</param>
    /// <param name="mode">The drive mode for the idle display.</param>
    /// <param name="time">Current time (in seconds), on the clock passed to the constructor.</param>
    /// <returns>Rows by columns, true for a lit pixel.</returns>
    public bool[,] Frame(ActuatorCommands commands, DriveMode mode, float time)
    {
        bool[,] pixels = commands.GetMatrixPixels();
        if (pixels != null)
        {
            return pixels;
        }

        bool[,] frame = new bool[DotMatrixDisplay.rows, DotMatrixDisplay.columns];
        string text = commands.GetMatrixText();
        if (text != null)
        {
            // The scroll restarts only when the text changes, as on the car
            if (text != this.text)
            {
                this.text = text;
                this.textStart = time;
            }
            DotMatrixDisplay.DrawScrolling(frame, text, time - this.textStart, DotMatrixDisplay.ScrollPeriod);
            return frame;
        }
        this.text = null;

        float splashElapsed = time - this.splashStart;
        if (splashElapsed < DotMatrixDisplay.SplashPeriod)
        {
            DotMatrixDisplay.DrawScrolling(frame, DotMatrixDisplay.SplashMessage, splashElapsed, DotMatrixDisplay.SplashPeriod);
            return frame;
        }

        DotMatrixDisplay.DrawModeGlyph(frame, mode);
        return frame;
    }
    #endregion

    private readonly float splashStart;

    private string text;

    private float textStart;

    private static void DrawScrolling(bool[,] frame, string message, float elapsed, float period)
    {
        int offset = DotMatrixDisplay.ScrollOffset(elapsed, DotMatrixFont.RenderedWidth(message), period);
        DotMatrixFont.Draw(frame, message, -offset, DotMatrixDisplay.textRow);
    }

    private static void DrawModeGlyph(bool[,] frame, DriveMode mode)
    {
        string[] glyph = mode == DriveMode.Manual ? DotMatrixDisplay.manualGlyph
            : mode == DriveMode.Auto ? DotMatrixDisplay.autoGlyph
            : DotMatrixDisplay.idleGlyph;
        for (int row = 0; row < glyph.Length; row++)
        {
            for (int column = 0; column < glyph[row].Length; column++)
            {
                frame[row, column] = glyph[row][column] == 'X';
            }
        }

        string label = mode == DriveMode.Manual ? "MAN" : mode == DriveMode.Auto ? "AUTO" : "IDLE";
        int region = DotMatrixDisplay.columns - DotMatrixDisplay.glyphWidth;
        int x = DotMatrixDisplay.glyphWidth + System.Math.Max(0, (region - DotMatrixFont.RenderedWidth(label)) / 2);
        DotMatrixFont.Draw(frame, label, x, DotMatrixDisplay.textRow);
    }
}
