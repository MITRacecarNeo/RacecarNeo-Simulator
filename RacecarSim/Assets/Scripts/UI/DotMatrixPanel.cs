using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows the car's 8x24 dot matrix on the HUD as round red LEDs, when Settings.ShowDotMatrix is on.
/// </summary>
public class DotMatrixPanel : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The image the matrix is drawn into.
    /// </summary>
    [SerializeField]
    private RawImage image;
    #endregion

    #region Constants
    /// <summary>
    /// Texture pixels per LED along each axis.
    /// </summary>
    private const int cellSize = 20;

    /// <summary>
    /// Radius of an LED within its cell (in texture pixels).
    /// </summary>
    private const float dotRadius = 7.5f;

    private static readonly Color32 litColor = new Color32(255, 48, 32, 255);
    private static readonly Color32 unlitColor = new Color32(58, 16, 14, 255);
    private static readonly Color32 backgroundColor = new Color32(14, 12, 12, 255);

    private static readonly Color32[] litCell = DotMatrixPanel.Cell(DotMatrixPanel.litColor);
    private static readonly Color32[] unlitCell = DotMatrixPanel.Cell(DotMatrixPanel.unlitColor);
    #endregion

    #region Public Interface
    /// <summary>
    /// Sets the drive mode shown when the program sends nothing: AUTO while a program runs, IDLE
    /// while one is connected and waiting, MAN for keyboard or controller driving.
    /// </summary>
    public void SetDriveMode(SimulationMode mode, bool isProgramConnected)
    {
        this.driveMode = mode == SimulationMode.UserProgram ? DotMatrixDisplay.DriveMode.Auto
            : mode == SimulationMode.DefaultDrive && !isProgramConnected ? DotMatrixDisplay.DriveMode.Manual
            : DotMatrixDisplay.DriveMode.Idle;
    }

    /// <summary>
    /// Creates a texture sized for the matrix (MatrixColumns x MatrixRows LED cells).
    /// </summary>
    public static Texture2D CreateTexture()
    {
        return new Texture2D(ActuatorCommands.MatrixColumns * DotMatrixPanel.cellSize, ActuatorCommands.MatrixRows * DotMatrixPanel.cellSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    /// <summary>
    /// Draws a frame into a texture from CreateTexture as round lit and unlit LEDs.
    /// </summary>
    /// <param name="texture">The target texture.</param>
    /// <param name="buffer">Scratch pixels, texture width times height.</param>
    /// <param name="frame">Rows by columns, true for a lit LED.</param>
    public static void DrawFrame(Texture2D texture, Color32[] buffer, bool[,] frame)
    {
        int width = texture.width;
        for (int row = 0; row < ActuatorCommands.MatrixRows; row++)
        {
            // Texture rows start at the bottom; matrix row 0 is the top
            int y0 = (ActuatorCommands.MatrixRows - 1 - row) * DotMatrixPanel.cellSize;
            for (int column = 0; column < ActuatorCommands.MatrixColumns; column++)
            {
                Color32[] cell = frame[row, column] ? DotMatrixPanel.litCell : DotMatrixPanel.unlitCell;
                int x0 = column * DotMatrixPanel.cellSize;
                for (int y = 0; y < DotMatrixPanel.cellSize; y++)
                {
                    System.Array.Copy(cell, y * DotMatrixPanel.cellSize, buffer, (y0 + y) * width + x0, DotMatrixPanel.cellSize);
                }
            }
        }
        texture.SetPixels32(buffer);
        texture.Apply(false);
    }
    #endregion

    private DotMatrixDisplay display;

    private DotMatrixDisplay.DriveMode driveMode = DotMatrixDisplay.DriveMode.Manual;

    private Texture2D texture;

    private Color32[] texturePixels;

    /// <summary>
    /// The frame currently in the texture, or null before the first draw.
    /// </summary>
    private bool[,] shownFrame;

    private void Awake()
    {
        this.display = new DotMatrixDisplay(Time.unscaledTime);
        this.texture = DotMatrixPanel.CreateTexture();
        this.texturePixels = new Color32[this.texture.width * this.texture.height];
        this.image.texture = this.texture;
    }

    private void Update()
    {
        this.image.enabled = Settings.ShowDotMatrix;
        Racecar car = LevelManager.GetCar(0);
        if (!this.image.enabled || car == null)
        {
            return;
        }

        bool[,] frame = this.display.Frame(car.Actuators, this.driveMode, Time.unscaledTime);
        if (!DotMatrixPanel.SameFrame(frame, this.shownFrame))
        {
            DotMatrixPanel.DrawFrame(this.texture, this.texturePixels, frame);
            this.shownFrame = frame;
        }
    }

    private void OnDestroy()
    {
        Destroy(this.texture);
    }

    /// <summary>
    /// One LED cell: a round dot on the dark background, its edge softened over one pixel.
    /// </summary>
    private static Color32[] Cell(Color32 dot)
    {
        Color32[] cell = new Color32[DotMatrixPanel.cellSize * DotMatrixPanel.cellSize];
        float center = (DotMatrixPanel.cellSize - 1) / 2f;
        for (int y = 0; y < DotMatrixPanel.cellSize; y++)
        {
            for (int x = 0; x < DotMatrixPanel.cellSize; x++)
            {
                float distance = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
                float coverage = Mathf.Clamp01(DotMatrixPanel.dotRadius + 0.5f - distance);
                cell[y * DotMatrixPanel.cellSize + x] = Color32.Lerp(DotMatrixPanel.backgroundColor, dot, coverage);
            }
        }
        return cell;
    }

    private static bool SameFrame(bool[,] a, bool[,] b)
    {
        if (b == null)
        {
            return false;
        }
        for (int row = 0; row < ActuatorCommands.MatrixRows; row++)
        {
            for (int column = 0; column < ActuatorCommands.MatrixColumns; column++)
            {
                if (a[row, column] != b[row, column])
                {
                    return false;
                }
            }
        }
        return true;
    }
}
