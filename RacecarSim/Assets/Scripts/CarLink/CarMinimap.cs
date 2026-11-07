using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// A top-down view of the car's 2D map (map frame, +x right, +y up): free cells light, occupied
/// dark, unknown gray, with the car's path and an arrow at the car. The scroll wheel zooms; at
/// zoom 1 the whole map fits, above 1 the view follows the car.
/// </summary>
[UxmlElement]
public partial class CarMinimap : VisualElement
{
    #region Constants
    public const float MinZoom = 1;
    public const float MaxZoom = 16;

    /// <summary>
    /// Zoom change per scroll notch.
    /// </summary>
    public const float ZoomStep = 1.25f;

    /// <summary>
    /// The path gains a point after the car moves this far (in meters).
    /// </summary>
    public const double PathSpacing = 0.05;

    private const int maxPathPoints = 4000;

    /// <summary>
    /// Most path points drawn; a longer path is drawn through every Nth point.
    /// </summary>
    private const int maxDrawnPathPoints = 1000;

    /// <summary>
    /// Shortest time between redraws for car movement (in seconds); each redraw rebuilds the path,
    /// and redrawing every frame made frame times uneven.
    /// </summary>
    private const float carRedrawInterval = 1 / 30f;

    /// <summary>
    /// Space between the panel edge and the map (in panel pixels), so walls on the map's edge
    /// stand apart from the panel background.
    /// </summary>
    private const float inset = 10;

    private static readonly Color32 freeColor = new Color32(235, 235, 240, 255);
    private static readonly Color32 occupiedColor = new Color32(40, 40, 48, 255);
    private static readonly Color32 unknownColor = new Color32(128, 128, 136, 255);
    private static readonly Color32 pathColor = new Color32(25, 104, 108, 255);
    private static readonly Color32 edgeColor = new Color32(200, 200, 210, 160);
    #endregion

    #region Public Interface
    public float Zoom { get; private set; } = CarMinimap.MinZoom;

    /// <summary>
    /// True while the pointer is over the view (the scroll wheel zooms it, not the camera).
    /// </summary>
    public bool IsPointerOver { get; private set; }

    public bool HasMap { get { return this.grid != null; } }

    public int PathCount { get { return this.path.Count; } }

    public CarMinimap()
    {
        this.pickingMode = PickingMode.Position;
        this.generateVisualContent += this.Draw;
        this.RegisterCallback<WheelEvent>(evt =>
        {
            this.ZoomBy(-Mathf.Sign(evt.delta.y));
            evt.StopPropagation();
        });
        this.RegisterCallback<PointerEnterEvent>(evt => this.IsPointerOver = true);
        this.RegisterCallback<PointerLeaveEvent>(evt => this.IsPointerOver = false);

        this.style.display = DisplayStyle.None;
    }

    /// <summary>
    /// Shows a map (rebuilds the image); null hides the view.
    /// </summary>
    public void SetMap(OccupancyGridMsg map)
    {
        this.grid = map;
        this.style.display = map != null ? DisplayStyle.Flex : DisplayStyle.None;
        if (map == null)
        {
            return;
        }
        if (this.texture == null || this.texture.width != map.Width || this.texture.height != map.Height)
        {
            if (this.texture != null)
            {
                Object.Destroy(this.texture);
            }
            this.texture = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        }
        Color32[] pixels = new Color32[map.Width * map.Height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = CarMinimap.CellColor(map.Data[i]);
        }
        this.texture.SetPixels32(pixels);
        this.texture.Apply(false);
        this.MarkDirtyRepaint();
    }

    /// <summary>
    /// Moves the car arrow and extends the path (map frame).
    /// </summary>
    public void SetCar(CarPose pose)
    {
        this.car = pose;
        this.hasCar = true;
        CarPose last = this.path.Count > 0 ? this.path[this.path.Count - 1] : default;
        if (this.path.Count == 0 || System.Math.Sqrt((pose.X - last.X) * (pose.X - last.X) + (pose.Y - last.Y) * (pose.Y - last.Y)) >= CarMinimap.PathSpacing)
        {
            this.path.Add(pose);
            if (this.path.Count > CarMinimap.maxPathPoints)
            {
                this.path.RemoveAt(0);
            }
        }
        if (Time.unscaledTime >= this.nextCarRedraw)
        {
            this.nextCarRedraw = Time.unscaledTime + CarMinimap.carRedrawInterval;
            this.MarkDirtyRepaint();
        }
    }

    /// <summary>
    /// Forgets the map and the path.
    /// </summary>
    public void ClearMap()
    {
        this.path.Clear();
        this.SetMap(null);
    }

    /// <summary>
    /// Frees the map image (when the owning scene object goes away; a panel can detach and
    /// reattach the view, so detaching keeps it).
    /// </summary>
    public void Release()
    {
        if (this.texture != null)
        {
            Object.Destroy(this.texture);
            this.texture = null;
        }
    }

    /// <summary>
    /// Zooms by scroll notches (positive zooms in).
    /// </summary>
    public void ZoomBy(float notches)
    {
        this.Zoom = Mathf.Clamp(this.Zoom * Mathf.Pow(CarMinimap.ZoomStep, notches), CarMinimap.MinZoom, CarMinimap.MaxZoom);
        this.MarkDirtyRepaint();
    }

    /// <summary>
    /// The square window of cells shown: its lower-left cell coordinates and side (in cells). At
    /// zoom 1 it fits the whole map, centered; zoomed in, it centers on the car and stays inside
    /// the map along each axis the map is longer than the window.
    /// </summary>
    /// <param name="carX">The car's cell coordinate (x), or NaN without a car.</param>
    public static (float x0, float y0, float side) View(int width, int height, float zoom, float carX, float carY)
    {
        float side = Mathf.Max(width, height) / Mathf.Max(zoom, CarMinimap.MinZoom);
        bool follow = zoom > CarMinimap.MinZoom && !float.IsNaN(carX);
        float cx = follow ? carX : width / 2f;
        float cy = follow ? carY : height / 2f;
        return (CarMinimap.Fit(cx - side / 2, side, width), CarMinimap.Fit(cy - side / 2, side, height), side);
    }

    /// <summary>
    /// A map point (meters) as cell coordinates, for a grid without origin rotation.
    /// </summary>
    public static Vector2 ToCell(OccupancyGridMsg map, double x, double y)
    {
        return new Vector2((float)((x - map.OriginX) / map.Resolution), (float)((y - map.OriginY) / map.Resolution));
    }

    public static Color32 CellColor(sbyte value)
    {
        if (value < 0)
        {
            return CarMinimap.unknownColor;
        }
        if (value <= OccupancyTiles.FreeThreshold)
        {
            return CarMinimap.freeColor;
        }
        return value >= OccupancyTiles.OccupiedThreshold ? CarMinimap.occupiedColor : CarMinimap.unknownColor;
    }
    #endregion

    private readonly List<CarPose> path = new List<CarPose>();
    private OccupancyGridMsg grid;
    private Texture2D texture;
    private CarPose car;
    private bool hasCar;
    private float nextCarRedraw;

    /// <summary>
    /// A window start along one axis: inside [0, length - side] when the map is longer than the
    /// window, else centered on the map.
    /// </summary>
    private static float Fit(float start, float side, float length)
    {
        return side >= length ? (length - side) / 2 : Mathf.Clamp(start, 0, length - side);
    }

    private void Draw(MeshGenerationContext context)
    {
        float size = Mathf.Min(this.contentRect.width, this.contentRect.height) - 2 * CarMinimap.inset;
        if (this.grid == null || this.texture == null || size <= 0)
        {
            return;
        }
        Vector2 carCell = this.hasCar ? CarMinimap.ToCell(this.grid, this.car.X, this.car.Y) : new Vector2(float.NaN, float.NaN);
        (float x0, float y0, float side) = CarMinimap.View(this.grid.Width, this.grid.Height, this.Zoom, carCell.x, carCell.y);
        float scale = size / side;
        Vector2 Panel(Vector2 cell) => new Vector2(CarMinimap.inset + (cell.x - x0) * scale, CarMinimap.inset + size - (cell.y - y0) * scale);

        // The whole map image, placed by the window; the element clips what falls outside
        Vector2 a = Panel(Vector2.zero);
        Vector2 b = Panel(new Vector2(this.grid.Width, this.grid.Height));
        MeshWriteData mesh = context.Allocate(4, 6, this.texture);
        mesh.SetAllVertices(new[]
        {
            new Vertex { position = new Vector3(a.x, a.y, Vertex.nearZ), tint = Color.white, uv = new Vector2(0, 0) },
            new Vertex { position = new Vector3(a.x, b.y, Vertex.nearZ), tint = Color.white, uv = new Vector2(0, 1) },
            new Vertex { position = new Vector3(b.x, b.y, Vertex.nearZ), tint = Color.white, uv = new Vector2(1, 1) },
            new Vertex { position = new Vector3(b.x, a.y, Vertex.nearZ), tint = Color.white, uv = new Vector2(1, 0) },
        });
        mesh.SetAllIndices(new ushort[] { 0, 1, 2, 0, 2, 3 });

        Painter2D painter = context.painter2D;
        painter.strokeColor = CarMinimap.edgeColor;
        painter.lineWidth = 1;
        painter.BeginPath();
        painter.MoveTo(a);
        painter.LineTo(new Vector2(a.x, b.y));
        painter.LineTo(b);
        painter.LineTo(new Vector2(b.x, a.y));
        painter.ClosePath();
        painter.Stroke();
        if (this.path.Count > 1)
        {
            painter.strokeColor = CarMinimap.pathColor;
            painter.lineWidth = 2;
            painter.lineJoin = LineJoin.Round;
            painter.BeginPath();
            int stride = (this.path.Count + CarMinimap.maxDrawnPathPoints - 1) / CarMinimap.maxDrawnPathPoints;
            painter.MoveTo(Panel(CarMinimap.ToCell(this.grid, this.path[0].X, this.path[0].Y)));
            for (int i = stride; i < this.path.Count; i += stride)
            {
                painter.LineTo(Panel(CarMinimap.ToCell(this.grid, this.path[i].X, this.path[i].Y)));
            }
            CarPose newest = this.path[this.path.Count - 1];
            painter.LineTo(Panel(CarMinimap.ToCell(this.grid, newest.X, newest.Y)));
            painter.Stroke();
        }
        if (this.hasCar)
        {
            // Screen y runs down, so a counterclockwise map heading turns the other way on screen
            Vector2 forward = new Vector2(Mathf.Cos((float)this.car.Yaw), -Mathf.Sin((float)this.car.Yaw));
            MiniMapView.DrawCarArrow(painter, Panel(carCell), forward);
        }
    }
}
