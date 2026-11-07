using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A speed dial after the brand HUD motif: a light ring, tick marks along a 240 degree sweep from
/// lower left to lower right, a red needle on a charcoal hub, and the speed in m/s at the center
/// (child label). Range 0 to MaxSpeed.
/// </summary>
[UxmlElement]
public partial class SpeedGauge : VisualElement
{
    /// <summary>
    /// Sweep of the dial in degrees, centered on straight up.
    /// </summary>
    private const float sweepDegrees = 240;

    private readonly Label readout = new Label();
    private float speed;

    public SpeedGauge()
    {
        this.pickingMode = PickingMode.Ignore;
        this.readout.AddToClassList("hud-gauge-readout");
        this.Add(this.readout);
        this.generateVisualContent += this.Draw;
        this.Speed = 0;
    }

    /// <summary>
    /// Speed at full deflection, in m/s.
    /// </summary>
    [UxmlAttribute]
    public float MaxSpeed { get; set; } = 4;

    /// <summary>
    /// The shown speed in m/s.
    /// </summary>
    public float Speed
    {
        get => this.speed;
        set
        {
            this.speed = Mathf.Max(0, value);
            this.readout.text = this.speed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            this.MarkDirtyRepaint();
        }
    }

    /// <summary>
    /// Angle in degrees (0 = right, counterclockwise positive) for a speed fraction 0 to 1.
    /// </summary>
    public static float AngleFor(float fraction)
    {
        float start = 90 + SpeedGauge.sweepDegrees / 2;
        return start - Mathf.Clamp01(fraction) * SpeedGauge.sweepDegrees;
    }

    private void Draw(MeshGenerationContext context)
    {
        float size = Mathf.Min(this.layout.width, this.layout.height);
        if (size <= 0)
        {
            return;
        }

        Painter2D p = context.painter2D;
        Vector2 center = new Vector2(this.layout.width / 2, this.layout.height / 2);
        float radius = size / 2 - 6;
        Vector2 Point(float degrees, float r) => center + new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), -Mathf.Sin(degrees * Mathf.Deg2Rad)) * r;

        // Face and ring
        p.fillColor = new Color(ThemeTokens.Charcoal.r / 255f, ThemeTokens.Charcoal.g / 255f, ThemeTokens.Charcoal.b / 255f, 0.88f);
        p.BeginPath();
        p.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
        p.Fill();
        p.strokeColor = ThemeTokens.Paper;
        p.lineWidth = 4;
        p.BeginPath();
        p.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
        p.Stroke();

        // Ticks: every 0.5 m/s, long at whole numbers; those below the speed in the accent color
        p.lineCap = LineCap.Round;
        int steps = Mathf.RoundToInt(this.MaxSpeed * 2);
        for (int i = 0; i <= steps; i++)
        {
            float fraction = (float)i / steps;
            float angle = SpeedGauge.AngleFor(fraction);
            bool major = i % 2 == 0;
            p.strokeColor = fraction * this.MaxSpeed <= this.speed + 1e-3f && this.speed > 0 ? (Color)ThemeTokens.Orange : (Color)ThemeTokens.Mist;
            p.lineWidth = major ? 4 : 2;
            p.BeginPath();
            p.MoveTo(Point(angle, radius - (major ? 18 : 12)));
            p.LineTo(Point(angle, radius - 6));
            p.Stroke();
        }

        // Needle and hub
        float needle = SpeedGauge.AngleFor(this.speed / this.MaxSpeed);
        p.strokeColor = ThemeTokens.LedRed;
        p.lineWidth = 5;
        p.BeginPath();
        p.MoveTo(center);
        p.LineTo(Point(needle, radius - 22));
        p.Stroke();
        p.fillColor = ThemeTokens.Charcoal;
        p.BeginPath();
        p.Arc(center, 9, Angle.Degrees(0), Angle.Degrees(360));
        p.Fill();
    }
}
