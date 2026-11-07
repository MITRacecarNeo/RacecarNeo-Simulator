using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Full-screen cover that slides in from the left and out to the right, its edges leaning
/// ThemeTokens.MarkSkewDegrees from vertical. The moving edge carries a gradient.mark band. The
/// cover is charcoal mixed with the --tint style color by --tint-strength (0 to 1; plain
/// charcoal when unset).
/// Progress 0 is uncovered (cover off the left), 1 is fully covered, 2 is uncovered again
/// (cover off the right).
/// </summary>
[UxmlElement]
public partial class TransitionWipe : VisualElement
{
    /// <summary>
    /// Width of the gradient band at the moving edge, in panel pixels.
    /// </summary>
    public const float BandWidth = 36;

    private static readonly CustomStyleProperty<Color> tintProperty = new CustomStyleProperty<Color>("--tint");
    private static readonly CustomStyleProperty<float> tintStrengthProperty = new CustomStyleProperty<float>("--tint-strength");

    private float progress;
    private Color cover = ThemeTokens.Charcoal;

    public TransitionWipe()
    {
        this.pickingMode = PickingMode.Ignore;
        this.generateVisualContent += this.Draw;
        this.RegisterCallback<CustomStyleResolvedEvent>(this.OnStyleResolved);
    }

    /// <summary>
    /// The cover color: charcoal mixed with --tint by --tint-strength.
    /// </summary>
    public Color CoverColor => this.cover;

    /// <summary>
    /// Wipe position from 0 to 2; see the class summary.
    /// </summary>
    [UxmlAttribute]
    public float Progress
    {
        get => this.progress;
        set
        {
            this.progress = Mathf.Clamp(value, 0, 2);
            this.MarkDirtyRepaint();
        }
    }

    /// <summary>
    /// Cubic ease in and out.
    /// </summary>
    public static float Ease(float t)
    {
        t = Mathf.Clamp01(t);
        return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
    }

    /// <summary>
    /// The cover's left and right edges where they cross the vertical middle of the screen.
    /// </summary>
    public static (float Left, float Right) Edges(float progress, float width, float height)
    {
        float lean = TransitionWipe.Lean(height);
        float start = -lean;
        float end = width + lean;
        return progress <= 1
            ? (start, Mathf.Lerp(start, end, TransitionWipe.Ease(progress)))
            : (Mathf.Lerp(start, end, TransitionWipe.Ease(progress - 1)), end);
    }

    /// <summary>
    /// The cover as a quad (top-left, top-right, bottom-right, bottom-left) in panel pixels.
    /// </summary>
    public static Vector2[] Cover(float progress, float width, float height)
    {
        (float left, float right) = TransitionWipe.Edges(progress, width, height);
        return TransitionWipe.Band(left, right, height);
    }

    private void OnStyleResolved(CustomStyleResolvedEvent evt)
    {
        bool tinted = evt.customStyle.TryGetValue(TransitionWipe.tintProperty, out Color tint);
        bool strong = evt.customStyle.TryGetValue(TransitionWipe.tintStrengthProperty, out float strength);
        this.cover = tinted && strong ? Color.Lerp(ThemeTokens.Charcoal, tint, strength) : (Color)ThemeTokens.Charcoal;
        this.MarkDirtyRepaint();
    }

    private static float Lean(float height)
    {
        return height * Mathf.Tan(ThemeTokens.MarkSkewDegrees * Mathf.Deg2Rad);
    }

    private static Vector2[] Band(float left, float right, float height)
    {
        float half = TransitionWipe.Lean(height) / 2;
        return new[]
        {
            new Vector2(left + half, 0),
            new Vector2(right + half, 0),
            new Vector2(right - half, height),
            new Vector2(left - half, height),
        };
    }

    private void Draw(MeshGenerationContext context)
    {
        float width = this.layout.width;
        float height = this.layout.height;
        if (width <= 0 || height <= 0 || this.progress <= 0 || this.progress >= 2)
        {
            return;
        }

        (float left, float right) = TransitionWipe.Edges(this.progress, width, height);
        Painter2D painter = context.painter2D;
        TransitionWipe.Fill(painter, TransitionWipe.Band(left, right, height), this.cover);

        // The band rides on the moving edge: the right edge while covering, the left while revealing
        bool covering = this.progress <= 1;
        float bandLeft = covering ? right - TransitionWipe.BandWidth : left;
        float bandRight = covering ? right : left + TransitionWipe.BandWidth;
        if (bandRight - bandLeft > 0)
        {
            painter.fillGradient = FillGradient.MakeLinearGradient(GradientBar.MarkGradient, new Vector2(bandLeft, 0), new Vector2(bandRight, 0), AddressMode.Clamp);
            TransitionWipe.Path(painter, TransitionWipe.Band(bandLeft, bandRight, height));
            painter.Fill();
        }
    }

    private static void Fill(Painter2D painter, Vector2[] quad, Color color)
    {
        // A one-color gradient: a gradient set by an earlier fill would otherwise override fillColor
        Gradient solid = new Gradient();
        solid.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
        painter.fillGradient = FillGradient.MakeLinearGradient(solid, quad[3], quad[2], AddressMode.Clamp);
        TransitionWipe.Path(painter, quad);
        painter.Fill();
    }

    private static void Path(Painter2D painter, Vector2[] quad)
    {
        painter.BeginPath();
        painter.MoveTo(quad[0]);
        for (int i = 1; i < quad.Length; i++)
        {
            painter.LineTo(quad[i]);
        }
        painter.ClosePath();
    }
}
