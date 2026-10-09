using System;
using System.Text;
using UnityEngine;

/// <summary>
/// The dot matrix and LED strip contents most recently sent by a car's Python program.
/// PythonInterface writes on the main thread (sync calls) and on the async thread (Jupyter
/// calls); renderers read on the main thread, so every access takes the lock.
/// </summary>
/// <remarks>
/// The latest dot matrix call wins: set_matrix replaces scrolling text and show_text replaces
/// pixels. Contents stay until replaced or until the program disconnects (Clear).
/// </remarks>
public class ActuatorCommands
{
    #region Constants
    /// <summary>
    /// Rows of the dot matrix (three MAX7219 modules side by side).
    /// </summary>
    public const int MatrixRows = 8;

    /// <summary>
    /// Columns of the dot matrix; column 0 is at the left as seen.
    /// </summary>
    public const int MatrixColumns = 24;

    /// <summary>
    /// Pixels on the WS2812B strip; index 0 is at the left end of the bar.
    /// </summary>
    public const int LedCount = 84;

    /// <summary>
    /// Bytes of a packed matrix frame: one bit per pixel, row-major, most significant bit first.
    /// </summary>
    public const int MatrixFrameBytes = MatrixRows * MatrixColumns / 8;

    /// <summary>
    /// Bytes of an LED frame: R, G, B per pixel.
    /// </summary>
    public const int LedFrameBytes = LedCount * 3;
    #endregion

    #region Public Interface
    /// <summary>
    /// What the dot matrix shows.
    /// </summary>
    public enum MatrixContent
    {
        /// <summary>
        /// Nothing sent; the car shows its idle display.
        /// </summary>
        None,
        Pixels,
        Text
    }

    /// <summary>
    /// Whether the matrix shows pixels, text, or nothing from the program.
    /// </summary>
    public MatrixContent Matrix
    {
        get { lock (this.sync) { return this.matrixContent; } }
    }

    /// <summary>
    /// True when the program has set the LED strip.
    /// </summary>
    public bool HasLeds
    {
        get { lock (this.sync) { return this.leds != null; } }
    }

    /// <summary>
    /// Shows a pixel frame on the dot matrix, replacing any text.
    /// </summary>
    /// <param name="pixels">MatrixRows x MatrixColumns, true for a lit pixel.</param>
    public void SetMatrix(bool[,] pixels)
    {
        lock (this.sync)
        {
            this.matrixPixels = (bool[,])pixels.Clone();
            this.matrixText = null;
            this.matrixContent = MatrixContent.Pixels;
        }
    }

    /// <summary>
    /// Scrolls text on the dot matrix, replacing any pixel frame. Empty text clears the matrix
    /// back to the idle display.
    /// </summary>
    public void SetText(string text)
    {
        lock (this.sync)
        {
            this.matrixPixels = null;
            this.matrixText = string.IsNullOrEmpty(text) ? null : text;
            this.matrixContent = this.matrixText == null ? MatrixContent.None : MatrixContent.Text;
        }
    }

    /// <summary>
    /// Sets every pixel of the LED strip.
    /// </summary>
    /// <param name="colors">LedCount colors.</param>
    public void SetLeds(Color32[] colors)
    {
        lock (this.sync)
        {
            this.leds = (Color32[])colors.Clone();
        }
    }

    /// <summary>
    /// Returns a copy of the matrix pixel frame, or null if the matrix does not show pixels.
    /// </summary>
    public bool[,] GetMatrixPixels()
    {
        lock (this.sync)
        {
            return (bool[,])this.matrixPixels?.Clone();
        }
    }

    /// <summary>
    /// Returns the scrolling text, or null if the matrix does not show text.
    /// </summary>
    public string GetMatrixText()
    {
        lock (this.sync)
        {
            return this.matrixText;
        }
    }

    /// <summary>
    /// Returns a copy of the LED colors, or null if the program has not set the strip.
    /// </summary>
    public Color32[] GetLeds()
    {
        lock (this.sync)
        {
            return (Color32[])this.leds?.Clone();
        }
    }

    /// <summary>
    /// Returns the matrix and LED strip to the idle display. Called when the program disconnects.
    /// </summary>
    public void Clear()
    {
        lock (this.sync)
        {
            this.matrixPixels = null;
            this.matrixText = null;
            this.matrixContent = MatrixContent.None;
            this.leds = null;
        }
    }

    /// <summary>
    /// Unpacks a matrix frame: MatrixFrameBytes bytes, row-major, most significant bit first.
    /// </summary>
    /// <param name="data">The packet.</param>
    /// <param name="offset">Index of the first frame byte in data.</param>
    public static bool[,] DecodeMatrix(byte[] data, int offset)
    {
        bool[,] pixels = new bool[MatrixRows, MatrixColumns];
        for (int i = 0; i < MatrixRows * MatrixColumns; i++)
        {
            pixels[i / MatrixColumns, i % MatrixColumns] = (data[offset + i / 8] & (0x80 >> (i % 8))) != 0;
        }
        return pixels;
    }

    /// <summary>
    /// Unpacks an LED frame: LedFrameBytes bytes, R, G, B per pixel.
    /// </summary>
    /// <param name="data">The packet.</param>
    /// <param name="offset">Index of the first frame byte in data.</param>
    public static Color32[] DecodeLeds(byte[] data, int offset)
    {
        Color32[] colors = new Color32[LedCount];
        for (int i = 0; i < LedCount; i++)
        {
            int p = offset + 3 * i;
            colors[i] = new Color32(data[p], data[p + 1], data[p + 2], 255);
        }
        return colors;
    }

    /// <summary>
    /// Reads length-prefixed ASCII text: one length byte, then that many characters.
    /// </summary>
    /// <param name="data">The packet.</param>
    /// <param name="offset">Index of the length byte in data.</param>
    public static string DecodeText(byte[] data, int offset)
    {
        int length = Math.Min(data[offset], data.Length - offset - 1);
        return Encoding.ASCII.GetString(data, offset + 1, length);
    }
    #endregion

    private readonly object sync = new object();
    private MatrixContent matrixContent = MatrixContent.None;
    private bool[,] matrixPixels;
    private string matrixText;
    private Color32[] leds;
}
