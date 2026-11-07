using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// The car's body frame (REP 103: x forward, y left, z up) as three axes from the car's center,
/// seen from a fixed world view so the figure turns, pitches, and rolls with the car. Axes are
/// red, green, and blue for x, y, and z (the convention of ROS tools such as RViz), over a faint
/// outline of the car body; nearer axes draw over farther ones.
/// </summary>
[UxmlElement]
public partial class BodyFrameView : VisualElement
{
    /// <summary>
    /// The view direction: looking down by this pitch, from behind and to the right of world +z.
    /// </summary>
    public static readonly Quaternion ViewRotation = Quaternion.Euler(25, -35, 0);

    private static readonly Color[] axisColors = { new Color(1, 0, 0), new Color(0, 1, 0), new Color(0, 0, 1) };
    private static readonly string[] axisNames = { "X", "Y", "Z" };

    /// <summary>
    /// Half size of the body outline in car units (right, up, forward), relative to the axis length.
    /// </summary>
    private static readonly Vector3 bodyExtents = new Vector3(0.3f, 0.15f, 0.5f);

    private const float axisWidth = 4;
    private const float bodyWidth = 1.5f;

    // Axis length as a fraction of half the element's smaller side, leaving room for the labels
    private const float axisScale = 0.82f;

    private Quaternion orientation = Quaternion.identity;
    private readonly Label[] labels = new Label[3];

    public BodyFrameView()
    {
        this.pickingMode = PickingMode.Ignore;
        this.generateVisualContent += this.Draw;
        for (int i = 0; i < 3; i++)
        {
            this.labels[i] = new Label(BodyFrameView.axisNames[i]);
            this.labels[i].AddToClassList("hud-axis-label");
            this.Add(this.labels[i]);
        }
        this.RegisterCallback<GeometryChangedEvent>(_ => this.PlaceLabels());
    }

    /// <summary>
    /// Sets the car's world rotation.
    /// </summary>
    public void SetOrientation(Quaternion rotation)
    {
        if (Quaternion.Angle(rotation, this.orientation) < 0.1f)
        {
            return;
        }
        this.orientation = rotation;
        this.PlaceLabels();
        this.MarkDirtyRepaint();
    }

    /// <summary>
    /// The body axes (x forward, y left, z up) as Unity world directions for a car rotation.
    /// </summary>
    public static Vector3[] Axes(Quaternion rotation)
    {
        return new[] { rotation * Vector3.forward, rotation * Vector3.left, rotation * Vector3.up };
    }

    /// <summary>
    /// A world direction projected into the view: x right and y down on screen, z away from the viewer.
    /// </summary>
    public static Vector3 Project(Vector3 world)
    {
        Vector3 view = Quaternion.Inverse(BodyFrameView.ViewRotation) * world;
        return new Vector3(view.x, -view.y, view.z);
    }

    private float Scale => Mathf.Min(this.contentRect.width, this.contentRect.height) * 0.5f * BodyFrameView.axisScale;

    private Vector2 ToElement(Vector3 world, float length)
    {
        Vector3 projected = BodyFrameView.Project(world);
        return this.contentRect.center + new Vector2(projected.x, projected.y) * length;
    }

    private void PlaceLabels()
    {
        Vector3[] axes = BodyFrameView.Axes(this.orientation);
        for (int i = 0; i < 3; i++)
        {
            Vector2 tip = this.ToElement(axes[i], this.Scale * 1.16f);
            this.labels[i].style.left = tip.x - this.labels[i].layout.width * 0.5f;
            this.labels[i].style.top = tip.y - this.labels[i].layout.height * 0.5f;
        }
    }

    private void Draw(MeshGenerationContext context)
    {
        Painter2D painter = context.painter2D;
        float scale = this.Scale;
        if (scale <= 0)
        {
            return;
        }

        // Body outline: the 12 edges of a box around the center
        painter.strokeColor = new Color(ThemeTokens.Mist.r / 255f, ThemeTokens.Mist.g / 255f, ThemeTokens.Mist.b / 255f, 0.35f);
        painter.lineWidth = BodyFrameView.bodyWidth;
        painter.lineJoin = LineJoin.Round;
        Vector2[] corners = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 local = Vector3.Scale(BodyFrameView.bodyExtents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            corners[i] = this.ToElement(this.orientation * local, scale);
        }
        painter.BeginPath();
        for (int i = 0; i < 8; i++)
        {
            foreach (int bit in new[] { 1, 2, 4 })
            {
                if ((i & bit) == 0)
                {
                    painter.MoveTo(corners[i]);
                    painter.LineTo(corners[i | bit]);
                }
            }
        }
        painter.Stroke();

        // Axes, farthest first
        Vector3[] axes = BodyFrameView.Axes(this.orientation);
        int[] order = { 0, 1, 2 };
        System.Array.Sort(order, (a, b) => BodyFrameView.Project(axes[b]).z.CompareTo(BodyFrameView.Project(axes[a]).z));
        painter.lineCap = LineCap.Round;
        painter.lineWidth = BodyFrameView.axisWidth;
        foreach (int i in order)
        {
            painter.strokeColor = BodyFrameView.axisColors[i];
            painter.BeginPath();
            painter.MoveTo(this.contentRect.center);
            painter.LineTo(this.ToElement(axes[i], scale));
            painter.Stroke();
        }
    }
}
