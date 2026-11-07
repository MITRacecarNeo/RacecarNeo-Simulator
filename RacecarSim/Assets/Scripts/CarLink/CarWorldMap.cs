using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The car's occupancy map as sim geometry: occupied cells extruded into walls (one mesh and one
/// collider per 64 x 64 cell tile) and a floor texture of free, unknown, and occupied cells.
/// </summary>
/// <remarks>
/// Only tiles whose occupied cells changed are rebuilt, within a time budget per frame. Walls are on
/// the Default layer, so the sim lidar, depth, cameras, and car see them; the floor texture is on
/// the Arena layer (10), which the car's cameras skip, and has no collider.
/// </remarks>
public class CarWorldMap : MonoBehaviour
{
    #region Constants
    /// <summary>
    /// Wall height (in meters).
    /// </summary>
    public const float DefaultWallHeight = 1.0f;

    /// <summary>
    /// Occupied components smaller than this many cells are dropped as noise.
    /// </summary>
    public const int DefaultMinComponent = 2;

    /// <summary>
    /// Main-thread time spent on tile rebuilds per frame (in milliseconds).
    /// </summary>
    public const double FrameBudgetMs = 5;

    /// <summary>
    /// Largest grid shown, in cells (50 x 50 m at 5 cm).
    /// </summary>
    public const int MaxCells = 1000000;

    private const int arenaLayer = 10;
    #endregion

    #region Public Interface
    /// <summary>
    /// Sim height of the ground the walls stand on (units).
    /// </summary>
    public float GroundHeight { get; set; }

    public float WallHeight { get; set; } = CarWorldMap.DefaultWallHeight;

    public int MinComponent { get; set; } = CarWorldMap.DefaultMinComponent;

    /// <summary>
    /// Tiles with walls.
    /// </summary>
    public int TileCount { get { return this.tiles.Count; } }

    /// <summary>
    /// Tiles waiting to be rebuilt.
    /// </summary>
    public int PendingCount { get { return this.pending.Count; } }

    /// <summary>
    /// The map shown, or null.
    /// </summary>
    public OccupancyGridMsg Grid { get; private set; }

    /// <summary>
    /// True when the last map was refused for its size.
    /// </summary>
    public bool IsTooLarge { get; private set; }

    /// <summary>
    /// Sets the materials; call before the first map.
    /// </summary>
    public void Initialize(Material wall, Material floor)
    {
        this.wallMaterial = wall;
        this.floorMaterial = floor;
    }

    /// <summary>
    /// Shows a new map: queues the changed tiles and redraws the floor.
    /// </summary>
    public void Show(OccupancyGridMsg grid)
    {
        this.IsTooLarge = (long)grid.Width * grid.Height > CarWorldMap.MaxCells;
        if (this.IsTooLarge)
        {
            return;
        }
        this.Grid = grid;
        Dictionary<(int x, int y), long> prints = OccupancyTiles.Fingerprints(grid);
        foreach ((int x, int y) tile in OccupancyTiles.Changed(this.prints, prints))
        {
            if (this.queued.Add(tile))
            {
                this.pending.Enqueue(tile);
            }
        }
        this.prints = prints;
        this.DrawFloor(grid);
    }

    /// <summary>
    /// Removes every wall and the floor; the next Show builds from nothing.
    /// </summary>
    public void Clear()
    {
        foreach (GameObject tile in this.tiles.Values)
        {
            if (tile != null)
            {
                GameObject.Destroy(tile.GetComponent<MeshFilter>().sharedMesh);
                GameObject.Destroy(tile);
            }
        }
        this.tiles.Clear();
        this.pending.Clear();
        this.queued.Clear();
        this.prints = new Dictionary<(int x, int y), long>();
        this.Grid = null;
        this.IsTooLarge = false;
        if (this.floor != null)
        {
            this.floor.SetActive(false);
        }
    }

    /// <summary>
    /// Rebuilds every queued tile now (tests and saved worlds).
    /// </summary>
    public void Flush()
    {
        while (this.pending.Count > 0)
        {
            this.BuildNext();
        }
    }
    #endregion

    private readonly Dictionary<(int x, int y), GameObject> tiles = new Dictionary<(int x, int y), GameObject>();
    private readonly Queue<(int x, int y)> pending = new Queue<(int x, int y)>();
    private readonly HashSet<(int x, int y)> queued = new HashSet<(int x, int y)>();
    private Dictionary<(int x, int y), long> prints = new Dictionary<(int x, int y), long>();
    private Material wallMaterial;
    private Material floorMaterial;
    private GameObject floor;
    private Texture2D floorTexture;

    private void Update()
    {
        Stopwatch watch = Stopwatch.StartNew();
        while (this.pending.Count > 0 && watch.Elapsed.TotalMilliseconds < CarWorldMap.FrameBudgetMs)
        {
            this.BuildNext();
        }
    }

    private void OnDestroy()
    {
        foreach (GameObject tile in this.tiles.Values)
        {
            if (tile != null)
            {
                GameObject.Destroy(tile.GetComponent<MeshFilter>().sharedMesh);
            }
        }
        if (this.floorTexture != null)
        {
            GameObject.Destroy(this.floorTexture);
        }
    }

    private void BuildNext()
    {
        (int x, int y) key = this.pending.Dequeue();
        this.queued.Remove(key);
        List<CellRect> rects = OccupancyTiles.TileRects(this.Grid, key, this.MinComponent);
        this.tiles.TryGetValue(key, out GameObject tile);
        if (rects.Count == 0)
        {
            if (tile != null)
            {
                GameObject.Destroy(tile.GetComponent<MeshFilter>().sharedMesh);
                GameObject.Destroy(tile);
            }
            this.tiles.Remove(key);
            return;
        }
        if (tile == null)
        {
            tile = new GameObject($"Tile {key.x} {key.y}");
            tile.transform.SetParent(this.transform, false);
            tile.AddComponent<MeshFilter>();
            tile.AddComponent<MeshRenderer>().sharedMaterial = this.wallMaterial;
            tile.AddComponent<MeshCollider>();
            this.tiles[key] = tile;
        }
        Mesh mesh = CarWorldMap.WallMesh(rects, this.Grid.Resolution, this.GroundHeight, this.WallHeight * CarFrames.UnitsPerMeter);
        MeshFilter filter = tile.GetComponent<MeshFilter>();
        if (filter.sharedMesh != null)
        {
            GameObject.Destroy(filter.sharedMesh);
        }
        filter.sharedMesh = mesh;
        tile.GetComponent<MeshCollider>().sharedMesh = mesh;
    }

    /// <summary>
    /// A mesh of boxes, one per rectangle (absolute cells), from the ground up: top and four sides.
    /// </summary>
    public static Mesh WallMesh(List<CellRect> rects, float resolution, float ground, float height)
    {
        List<Vector3> vertices = new List<Vector3>(rects.Count * 20);
        List<Vector3> normals = new List<Vector3>(rects.Count * 20);
        List<int> triangles = new List<int>(rects.Count * 30);
        foreach (CellRect rect in rects)
        {
            // Map frame corners (meters), then sim positions
            double x0 = rect.MinColumn * (double)resolution;
            double x1 = (rect.MaxColumn + 1) * (double)resolution;
            double y0 = rect.MinRow * (double)resolution;
            double y1 = (rect.MaxRow + 1) * (double)resolution;
            Vector3 a = CarFrames.ToUnity(x0, y0, 0);
            Vector3 b = CarFrames.ToUnity(x1, y0, 0);
            Vector3 c = CarFrames.ToUnity(x1, y1, 0);
            Vector3 d = CarFrames.ToUnity(x0, y1, 0);
            Vector3 low = Vector3.up * ground;
            Vector3 high = Vector3.up * (ground + height);
            CarWorldMap.Quad(vertices, normals, triangles, a + high, b + high, c + high, d + high);
            CarWorldMap.Quad(vertices, normals, triangles, a + low, b + low, b + high, a + high);
            CarWorldMap.Quad(vertices, normals, triangles, b + low, c + low, c + high, b + high);
            CarWorldMap.Quad(vertices, normals, triangles, c + low, d + low, d + high, c + high);
            CarWorldMap.Quad(vertices, normals, triangles, d + low, a + low, a + high, d + high);
        }
        Mesh mesh = new Mesh { name = "Walls" };
        if (vertices.Count > 65000)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Adds a quad facing outward from the box (its normal from the corner order), both
    /// windings so it renders from either side.
    /// </summary>
    private static void Quad(List<Vector3> vertices, List<Vector3> normals, List<int> triangles, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0).normalized;
        int start = vertices.Count;
        vertices.Add(p0);
        vertices.Add(p1);
        vertices.Add(p2);
        vertices.Add(p3);
        for (int i = 0; i < 4; i++)
        {
            normals.Add(normal);
        }
        triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
    }

    private void DrawFloor(OccupancyGridMsg grid)
    {
        if (this.floor == null)
        {
            this.floor = new GameObject("Floor") { layer = CarWorldMap.arenaLayer };
            this.floor.transform.SetParent(this.transform, false);
            this.floor.AddComponent<MeshFilter>();
            this.floor.AddComponent<MeshRenderer>().sharedMaterial = new Material(this.floorMaterial);
        }
        this.floor.SetActive(true);
        if (this.floorTexture == null || this.floorTexture.width != grid.Width || this.floorTexture.height != grid.Height)
        {
            if (this.floorTexture != null)
            {
                GameObject.Destroy(this.floorTexture);
            }
            this.floorTexture = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            this.floor.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = this.floorTexture;
        }
        Color32[] pixels = new Color32[grid.Width * grid.Height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = CarWorldMap.FloorColor(grid.Data[i]);
        }
        this.floorTexture.SetPixels32(pixels);
        this.floorTexture.Apply(false);

        double x0 = grid.OriginX;
        double y0 = grid.OriginY;
        double x1 = x0 + grid.Width * (double)grid.Resolution;
        double y1 = y0 + grid.Height * (double)grid.Resolution;
        Vector3 lift = Vector3.up * (this.GroundHeight + 0.05f);
        Mesh mesh = this.floor.GetComponent<MeshFilter>().sharedMesh;
        if (mesh == null)
        {
            mesh = new Mesh { name = "Floor" };
            this.floor.GetComponent<MeshFilter>().sharedMesh = mesh;
        }
        mesh.SetVertices(new[] { CarFrames.ToUnity(x0, y0, 0) + lift, CarFrames.ToUnity(x1, y0, 0) + lift, CarFrames.ToUnity(x1, y1, 0) + lift, CarFrames.ToUnity(x0, y1, 0) + lift });
        mesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
        mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 }, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// Floor color for a cell: clear for unknown, light for free, dark for occupied.
    /// </summary>
    public static Color32 FloorColor(sbyte value)
    {
        if (value < 0)
        {
            return new Color32(0, 0, 0, 0);
        }
        if (value <= OccupancyTiles.FreeThreshold)
        {
            return new Color32(232, 232, 238, 140);
        }
        if (value >= OccupancyTiles.OccupiedThreshold)
        {
            return new Color32(47, 45, 56, 220);
        }
        return new Color32(160, 160, 170, 100);
    }
}
