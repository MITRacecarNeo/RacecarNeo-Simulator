/// <summary>
/// The dot matrix text font of the RACECAR Neo V2 driver: luma.core TINY_FONT with proportional
/// spacing, and the driver's diagonal-stroke 'N' (racecar_neo_ros2_driver dotmatrix_node).
/// </summary>
/// <remarks>
/// Glyph data from luma.core (luma/core/legacy/font.py, TINY_FONT from dafont.com "Tiny",
/// transposed by Richard Hull), MIT License, Copyright (c) 2017-2026 Richard Hull and
/// contributors. Each glyph is its columns left to right, bit 0 the top row, already trimmed and
/// followed by one blank column as luma.core's proportional wrapper returns them; space is four
/// blank columns.
/// </remarks>
public static class DotMatrixFont
{
    #region Public Interface
    /// <summary>
    /// The columns of a character, a single blank column for characters outside 0x20 to 0x7E.
    /// </summary>
    public static byte[] Columns(char character)
    {
        return character >= DotMatrixFont.first && character <= DotMatrixFont.last
            ? DotMatrixFont.glyphs[character - DotMatrixFont.first]
            : DotMatrixFont.blank;
    }

    /// <summary>
    /// Width of a message as drawn (in pixels): up to and including its last lit column, as the
    /// driver measures it.
    /// </summary>
    public static int RenderedWidth(string message)
    {
        int x = 0;
        int width = 0;
        foreach (char character in message)
        {
            foreach (byte column in DotMatrixFont.Columns(character))
            {
                x++;
                if (column != 0)
                {
                    width = x;
                }
            }
        }
        return width;
    }

    /// <summary>
    /// Draws a message onto a matrix frame with its top-left corner at (x, y); pixels outside the
    /// frame are skipped.
    /// </summary>
    /// <param name="frame">Rows by columns, true for a lit pixel.</param>
    public static void Draw(bool[,] frame, string message, int x, int y)
    {
        int rows = frame.GetLength(0);
        int columns = frame.GetLength(1);
        foreach (char character in message)
        {
            foreach (byte column in DotMatrixFont.Columns(character))
            {
                for (int bit = 0; bit < 8; bit++)
                {
                    int row = y + bit;
                    if ((column & (1 << bit)) != 0 && x >= 0 && x < columns && row >= 0 && row < rows)
                    {
                        frame[row, x] = true;
                    }
                }
                x++;
            }
        }
    }
    #endregion

    private const char first = (char)0x20;

    private const char last = (char)0x7E;

    private static readonly byte[] blank = { 0x00 };

    private static readonly byte[][] glyphs =
    {
        new byte[] { 0x00, 0x00, 0x00, 0x00 }, // space
        new byte[] { 0x2E, 0x00 }, // !
        new byte[] { 0x06, 0x00, 0x06, 0x00 }, // "
        new byte[] { 0x3E, 0x14, 0x3E, 0x00 }, // #
        new byte[] { 0x14, 0x3E, 0x14, 0x00 }, // $
        new byte[] { 0x34, 0x08, 0x16, 0x00 }, // %
        new byte[] { 0x34, 0x2A, 0x3A, 0x00 }, // &
        new byte[] { 0x06, 0x00 }, // '
        new byte[] { 0x1C, 0x22, 0x00 }, // (
        new byte[] { 0x22, 0x1C, 0x00 }, // )
        new byte[] { 0x14, 0x08, 0x14, 0x00 }, // *
        new byte[] { 0x08, 0x1C, 0x08, 0x00 }, // +
        new byte[] { 0x30, 0x00 }, // ,
        new byte[] { 0x08, 0x08, 0x08, 0x00 }, // -
        new byte[] { 0x20, 0x00 }, // .
        new byte[] { 0x30, 0x08, 0x06, 0x00 }, // /
        new byte[] { 0x1C, 0x22, 0x1C, 0x00 }, // 0
        new byte[] { 0x24, 0x3E, 0x20, 0x00 }, // 1
        new byte[] { 0x32, 0x2A, 0x24, 0x00 }, // 2
        new byte[] { 0x22, 0x2A, 0x14, 0x00 }, // 3
        new byte[] { 0x0E, 0x08, 0x3E, 0x00 }, // 4
        new byte[] { 0x2E, 0x2A, 0x12, 0x00 }, // 5
        new byte[] { 0x3E, 0x2A, 0x3A, 0x00 }, // 6
        new byte[] { 0x02, 0x3A, 0x06, 0x00 }, // 7
        new byte[] { 0x3E, 0x2A, 0x3E, 0x00 }, // 8
        new byte[] { 0x2E, 0x2A, 0x3E, 0x00 }, // 9
        new byte[] { 0x14, 0x00 }, // :
        new byte[] { 0x34, 0x00 }, // ;
        new byte[] { 0x08, 0x14, 0x22, 0x00 }, // <
        new byte[] { 0x14, 0x14, 0x14, 0x00 }, // =
        new byte[] { 0x22, 0x14, 0x08, 0x00 }, // >
        new byte[] { 0x02, 0x2A, 0x06, 0x00 }, // ?
        new byte[] { 0x1C, 0x2A, 0x1C, 0x00 }, // @
        new byte[] { 0x3C, 0x0A, 0x3C, 0x00 }, // A
        new byte[] { 0x3E, 0x2A, 0x14, 0x00 }, // B
        new byte[] { 0x1C, 0x22, 0x22, 0x00 }, // C
        new byte[] { 0x3E, 0x22, 0x1C, 0x00 }, // D
        new byte[] { 0x3E, 0x2A, 0x22, 0x00 }, // E
        new byte[] { 0x3E, 0x0A, 0x02, 0x00 }, // F
        new byte[] { 0x1C, 0x22, 0x3A, 0x00 }, // G
        new byte[] { 0x3E, 0x08, 0x3E, 0x00 }, // H
        new byte[] { 0x22, 0x3E, 0x22, 0x00 }, // I
        new byte[] { 0x32, 0x22, 0x3E, 0x00 }, // J
        new byte[] { 0x3E, 0x08, 0x36, 0x00 }, // K
        new byte[] { 0x3E, 0x20, 0x20, 0x00 }, // L
        new byte[] { 0x3E, 0x0C, 0x3E, 0x00 }, // M
        new byte[] { 0x3E, 0x04, 0x08, 0x3E, 0x00 }, // N
        new byte[] { 0x3E, 0x22, 0x3E, 0x00 }, // O
        new byte[] { 0x3E, 0x0A, 0x0E, 0x00 }, // P
        new byte[] { 0x1E, 0x12, 0x3E, 0x00 }, // Q
        new byte[] { 0x3E, 0x0A, 0x36, 0x00 }, // R
        new byte[] { 0x2E, 0x2A, 0x3A, 0x00 }, // S
        new byte[] { 0x02, 0x3E, 0x02, 0x00 }, // T
        new byte[] { 0x3E, 0x20, 0x3E, 0x00 }, // U
        new byte[] { 0x1E, 0x20, 0x1E, 0x00 }, // V
        new byte[] { 0x3E, 0x18, 0x3E, 0x00 }, // W
        new byte[] { 0x36, 0x08, 0x36, 0x00 }, // X
        new byte[] { 0x0E, 0x38, 0x0E, 0x00 }, // Y
        new byte[] { 0x32, 0x2A, 0x26, 0x00 }, // Z
        new byte[] { 0x3E, 0x22, 0x00 }, // [
        new byte[] { 0x06, 0x08, 0x30, 0x00 }, // backslash
        new byte[] { 0x22, 0x3E, 0x00 }, // ]
        new byte[] { 0x04, 0x02, 0x04, 0x00 }, // ^
        new byte[] { 0x20, 0x20, 0x20, 0x00 }, // _
        new byte[] { 0x02, 0x04, 0x00 }, // `
        new byte[] { 0x10, 0x2A, 0x3C, 0x00 }, // a
        new byte[] { 0x3E, 0x28, 0x10, 0x00 }, // b
        new byte[] { 0x18, 0x24, 0x24, 0x00 }, // c
        new byte[] { 0x10, 0x28, 0x3E, 0x00 }, // d
        new byte[] { 0x1C, 0x2A, 0x2C, 0x00 }, // e
        new byte[] { 0x3C, 0x0A, 0x00 }, // f
        new byte[] { 0x04, 0x2A, 0x3E, 0x00 }, // g
        new byte[] { 0x3E, 0x08, 0x38, 0x00 }, // h
        new byte[] { 0x3A, 0x00 }, // i
        new byte[] { 0x20, 0x3A, 0x00 }, // j
        new byte[] { 0x3C, 0x10, 0x28, 0x00 }, // k
        new byte[] { 0x3C, 0x00 }, // l
        new byte[] { 0x3C, 0x08, 0x3C, 0x00 }, // m
        new byte[] { 0x38, 0x04, 0x38, 0x00 }, // n
        new byte[] { 0x18, 0x24, 0x18, 0x00 }, // o
        new byte[] { 0x3C, 0x14, 0x08, 0x00 }, // p
        new byte[] { 0x08, 0x14, 0x3C, 0x00 }, // q
        new byte[] { 0x3C, 0x08, 0x04, 0x00 }, // r
        new byte[] { 0x28, 0x3C, 0x14, 0x00 }, // s
        new byte[] { 0x08, 0x3C, 0x28, 0x00 }, // t
        new byte[] { 0x3C, 0x20, 0x3C, 0x00 }, // u
        new byte[] { 0x1C, 0x20, 0x1C, 0x00 }, // v
        new byte[] { 0x3C, 0x10, 0x3C, 0x00 }, // w
        new byte[] { 0x24, 0x18, 0x24, 0x00 }, // x
        new byte[] { 0x0C, 0x28, 0x3C, 0x00 }, // y
        new byte[] { 0x24, 0x34, 0x2C, 0x00 }, // z
        new byte[] { 0x14, 0x2A, 0x00 }, // {
        new byte[] { 0x3E, 0x00 }, // |
        new byte[] { 0x2A, 0x14, 0x00 }, // }
        new byte[] { 0x04, 0x04, 0x0C, 0x00 }, // ~
    };
}
