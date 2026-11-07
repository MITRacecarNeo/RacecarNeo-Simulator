using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The brand gradient (gradient.mark: crimson to 22 percent, then to orange) across the element,
/// left to right. Used for dividers, progress bars, and transition edges; one per view. Fill sets
/// the drawn fraction of the width, for progress.
/// </summary>
[UxmlElement]
public partial class GradientBar : VisualElement
{
    private static Gradient markGradient;

    private float fill = 1;

    public GradientBar()
    {
        this.generateVisualContent += this.Draw;
    }

    /// <summary>
    /// Drawn fraction of the width, 0 to 1. The gradient spans the full width either way.
    /// </summary>
    [UxmlAttribute]
    public float Fill
    {
        get => this.fill;
        set
        {
            this.fill = Mathf.Clamp01(value);
            this.MarkDirtyRepaint();
        }
    }

    /// <summary>
    /// gradient.mark as a Unity gradient, from ThemeTokens.MarkGradient.
    /// </summary>
    public static Gradient MarkGradient
    {
        get
        {
            if (GradientBar.markGradient == null)
            {
                (float Position, Color32 Color)[] stops = ThemeTokens.MarkGradient;
                GradientColorKey[] keys = new GradientColorKey[stops.Length];
                for (int i = 0; i < stops.Length; i++)
                {
                    keys[i] = new GradientColorKey(stops[i].Color, stops[i].Position);
                }
                GradientBar.markGradient = new Gradient { mode = GradientMode.Blend };
                GradientBar.markGradient.SetKeys(keys, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            }
            return GradientBar.markGradient;
        }
    }

    private void Draw(MeshGenerationContext context)
    {
        float width = this.layout.width;
        float height = this.layout.height;
        if (width <= 0 || height <= 0 || this.fill <= 0)
        {
            return;
        }

        Painter2D painter = context.painter2D;
        painter.fillGradient = FillGradient.MakeLinearGradient(GradientBar.MarkGradient, Vector2.zero, new Vector2(width, 0), AddressMode.Clamp);
        painter.BeginPath();
        painter.MoveTo(Vector2.zero);
        painter.LineTo(new Vector2(width * this.fill, 0));
        painter.LineTo(new Vector2(width * this.fill, height));
        painter.LineTo(new Vector2(0, height));
        painter.ClosePath();
        painter.Fill();
    }
}
