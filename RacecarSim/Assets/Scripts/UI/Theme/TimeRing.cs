using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A trial's time limit as a ring: the arc fills clockwise from the top as time passes, in the
/// --fill color (styles switch it for the warning and overrun states), over a --track ring. Ticks
/// mark the time-bonus brackets. A child label in the middle shows the limit.
/// </summary>
[UxmlElement]
public partial class TimeRing : VisualElement
{
    private static readonly CustomStyleProperty<Color> fillProperty = new CustomStyleProperty<Color>("--fill");
    private static readonly CustomStyleProperty<Color> trackProperty = new CustomStyleProperty<Color>("--track");
    private static readonly CustomStyleProperty<Color> tickProperty = new CustomStyleProperty<Color>("--tick");

    private const float ringWidth = 8;
    private const float tickLength = 14;
    private const float tickWidth = 3;

    private Color fill = ThemeTokens.Orange;
    private Color track = ThemeTokens.Steel;
    private Color tick = ThemeTokens.Paper;
    private float elapsed;
    private float[] ticks = new float[0];

    public TimeRing()
    {
        this.pickingMode = PickingMode.Ignore;
        this.generateVisualContent += this.Draw;
        this.RegisterCallback<CustomStyleResolvedEvent>(this.OnStyleResolved);
    }

    /// <summary>
    /// Elapsed fraction of the limit, 0 to 1.
    /// </summary>
    public float Elapsed
    {
        get => this.elapsed;
        set
        {
            float clamped = Mathf.Clamp01(value);
            if (!Mathf.Approximately(clamped, this.elapsed))
            {
                this.elapsed = clamped;
                this.MarkDirtyRepaint();
            }
        }
    }

    /// <summary>
    /// Bracket positions as fractions of the limit; values outside 0 to 1 are left out.
    /// </summary>
    public float[] Ticks
    {
        get => this.ticks;
        set
        {
            this.ticks = value ?? new float[0];
            this.MarkDirtyRepaint();
        }
    }

    /// <summary>
    /// The angle in degrees for a fraction of the limit: 0 at the top, clockwise, in the
    /// Painter2D convention (0 = right, positive = clockwise on screen).
    /// </summary>
    public static float AngleFor(float fraction)
    {
        return -90 + 360 * fraction;
    }

    private void OnStyleResolved(CustomStyleResolvedEvent evt)
    {
        evt.customStyle.TryGetValue(TimeRing.fillProperty, out this.fill);
        evt.customStyle.TryGetValue(TimeRing.trackProperty, out this.track);
        evt.customStyle.TryGetValue(TimeRing.tickProperty, out this.tick);
        this.MarkDirtyRepaint();
    }

    private void Draw(MeshGenerationContext context)
    {
        Rect rect = this.contentRect;
        float radius = Mathf.Min(rect.width, rect.height) * 0.5f - TimeRing.ringWidth * 0.5f;
        if (radius <= 0)
        {
            return;
        }
        Vector2 center = rect.center;
        Painter2D painter = context.painter2D;
        painter.lineWidth = TimeRing.ringWidth;

        painter.strokeColor = this.track;
        painter.BeginPath();
        painter.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
        painter.Stroke();

        if (this.elapsed > 0)
        {
            painter.strokeColor = this.fill;
            painter.lineCap = LineCap.Butt;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(TimeRing.AngleFor(0)), Angle.Degrees(TimeRing.AngleFor(this.elapsed)));
            painter.Stroke();
        }

        painter.strokeColor = this.tick;
        painter.lineWidth = TimeRing.tickWidth;
        painter.lineCap = LineCap.Round;
        foreach (float fraction in this.ticks)
        {
            if (fraction <= 0 || fraction >= 1)
            {
                continue;
            }
            float angle = TimeRing.AngleFor(fraction) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            painter.BeginPath();
            painter.MoveTo(center + direction * (radius - TimeRing.tickLength * 0.5f));
            painter.LineTo(center + direction * (radius + TimeRing.tickLength * 0.5f));
            painter.Stroke();
        }
    }
}
