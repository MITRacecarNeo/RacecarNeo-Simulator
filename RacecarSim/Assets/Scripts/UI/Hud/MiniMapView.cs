using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Draws the overhead map overlay over the MiniMap image: the checkpoint route (a line in the
/// mist tint, closed when the level is a loop), a diamond per key point (the next one in the
/// accent color), and the car as an LED red arrow at the center, pointing along its heading.
/// North (world +z) is up; points outside the view are left out.
/// </summary>
[UxmlElement]
public partial class MiniMapView : VisualElement
{
    private Vector3 center;
    private float heading;
    private float halfExtent = MiniMap.MinHalfExtent;
    private Vector3[] keyPoints = new Vector3[0];
    private bool isLoop;
    private int nextKeyPoint = -1;

    public MiniMapView()
    {
        this.pickingMode = PickingMode.Ignore;
        this.generateVisualContent += this.Draw;
    }

    /// <summary>
    /// The route's key points in race order (start, checkpoints, finish), world positions.
    /// </summary>
    public void SetRoute(Vector3[] points, bool loop)
    {
        this.keyPoints = points ?? new Vector3[0];
        this.isLoop = loop;
        this.MarkDirtyRepaint();
    }

    /// <summary>
    /// Moves the view: the car's position and heading (degrees, 0 = +z, clockwise), the visible
    /// half width in world units, and the index of the next key point (-1 for none).
    /// </summary>
    public void SetView(Vector3 carPosition, float carHeading, float viewHalfExtent, int next)
    {
        this.center = carPosition;
        this.heading = carHeading;
        this.halfExtent = viewHalfExtent;
        this.nextKeyPoint = next;
        this.MarkDirtyRepaint();
    }

    /// <summary>
    /// A world point in panel coordinates of a map of the given size centered on center.
    /// </summary>
    public static Vector2 ToMap(Vector3 world, Vector3 center, float halfExtent, Vector2 size)
    {
        float u = 0.5f + (world.x - center.x) / (2 * halfExtent);
        float v = 0.5f - (world.z - center.z) / (2 * halfExtent);
        return new Vector2(u * size.x, v * size.y);
    }

    private void Draw(MeshGenerationContext context)
    {
        Vector2 size = new Vector2(this.layout.width, this.layout.height);
        if (size.x <= 0 || size.y <= 0)
        {
            return;
        }

        Painter2D p = context.painter2D;
        p.lineCap = LineCap.Round;
        p.lineJoin = LineJoin.Round;

        if (this.keyPoints.Length >= 2)
        {
            p.strokeColor = new Color(ThemeTokens.Mist.r / 255f, ThemeTokens.Mist.g / 255f, ThemeTokens.Mist.b / 255f, 0.55f);
            p.lineWidth = 3;
            p.BeginPath();
            p.MoveTo(MiniMapView.ToMap(this.keyPoints[0], this.center, this.halfExtent, size));
            for (int i = 1; i < this.keyPoints.Length; i++)
            {
                p.LineTo(MiniMapView.ToMap(this.keyPoints[i], this.center, this.halfExtent, size));
            }
            if (this.isLoop)
            {
                p.ClosePath();
            }
            p.Stroke();
        }

        for (int i = 0; i < this.keyPoints.Length; i++)
        {
            Vector2 at = MiniMapView.ToMap(this.keyPoints[i], this.center, this.halfExtent, size);
            if (at.x < 0 || at.y < 0 || at.x > size.x || at.y > size.y)
            {
                continue;
            }
            p.fillColor = i == this.nextKeyPoint ? (Color)ThemeTokens.Orange : (Color)ThemeTokens.Mist;
            float r = i == this.nextKeyPoint ? 9 : 6;
            p.BeginPath();
            p.MoveTo(at + new Vector2(0, -r));
            p.LineTo(at + new Vector2(r, 0));
            p.LineTo(at + new Vector2(0, r));
            p.LineTo(at + new Vector2(-r, 0));
            p.ClosePath();
            p.Fill();
        }

        // Car arrow at the center, rotated to the heading
        float radians = this.heading * Mathf.Deg2Rad;
        MiniMapView.DrawCarArrow(p, size / 2, new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)));
    }

    /// <summary>
    /// The car marker shared by the overhead maps: an LED red arrowhead with a charcoal outline.
    /// </summary>
    /// <param name="at">Arrow center, in panel pixels.</param>
    /// <param name="forward">Unit heading on the panel (y down).</param>
    public static void DrawCarArrow(Painter2D p, Vector2 at, Vector2 forward)
    {
        // Shape points: x to the car's right, y toward its rear
        Vector2 right = new Vector2(-forward.y, forward.x);
        Vector2 Point(float x, float y) => at + right * x - forward * y;
        p.fillColor = ThemeTokens.LedRed;
        p.strokeColor = ThemeTokens.Charcoal;
        p.lineWidth = 2;
        p.BeginPath();
        p.MoveTo(Point(0, -14));
        p.LineTo(Point(10, 11));
        p.LineTo(Point(0, 6));
        p.LineTo(Point(-10, 11));
        p.ClosePath();
        p.Fill();
        p.Stroke();
    }
}
