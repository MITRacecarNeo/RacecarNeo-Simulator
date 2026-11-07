using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Motion lines trailing behind something moving left to right: thin rounded strokes of varied
/// length, parallel to travel, in the --line-color style color (steel when unset). Strength (0 to 1)
/// sets how far they reach; at 0 nothing is drawn.
/// </summary>
[UxmlElement]
public partial class SpeedLines : VisualElement
{
    /// <summary>
    /// Each line as (vertical position 0 to 1, length as a fraction of the width, width in px).
    /// </summary>
    private static readonly (float Y, float Length, float Width)[] lines =
    {
        (0.18f, 0.55f, 6),
        (0.36f, 0.85f, 8),
        (0.52f, 0.65f, 6),
        (0.68f, 1.00f, 8),
        (0.84f, 0.45f, 6),
    };

    private static readonly CustomStyleProperty<Color> lineColorProperty = new CustomStyleProperty<Color>("--line-color");

    private float strength;
    private Color lineColor = ThemeTokens.Steel;

    public SpeedLines()
    {
        this.pickingMode = PickingMode.Ignore;
        this.generateVisualContent += this.Draw;
        this.RegisterCallback<CustomStyleResolvedEvent>(evt =>
        {
            this.lineColor = evt.customStyle.TryGetValue(SpeedLines.lineColorProperty, out Color color) ? color : (Color)ThemeTokens.Steel;
            this.MarkDirtyRepaint();
        });
    }

    /// <summary>
    /// How far the lines reach, from 0 (none) to 1 (full length).
    /// </summary>
    [UxmlAttribute]
    public float Strength
    {
        get => this.strength;
        set
        {
            this.strength = Mathf.Clamp01(value);
            this.MarkDirtyRepaint();
        }
    }

    private void Draw(MeshGenerationContext context)
    {
        float width = this.layout.width;
        float height = this.layout.height;
        if (width <= 0 || height <= 0 || this.strength <= 0)
        {
            return;
        }

        Painter2D painter = context.painter2D;
        painter.strokeColor = this.lineColor;
        painter.lineCap = LineCap.Round;
        foreach ((float y, float length, float lineWidth) in SpeedLines.lines)
        {
            // Lines end at the right edge (the moving object) and reach back to the left
            float start = width * (1 - length * this.strength);
            painter.lineWidth = lineWidth;
            painter.BeginPath();
            painter.MoveTo(new Vector2(start, y * height));
            painter.LineTo(new Vector2(width - lineWidth, y * height));
            painter.Stroke();
        }
    }
}
