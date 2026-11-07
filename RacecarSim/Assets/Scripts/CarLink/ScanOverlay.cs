using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The car's real lidar returns drawn as small squares at lidar height, on the Arena layer (10)
/// so the car's own cameras and the map view skip them; no collider, so no sensor hits them.
/// </summary>
public class ScanOverlay : MonoBehaviour
{
    #region Constants
    /// <summary>
    /// Square edge (units).
    /// </summary>
    public const float PointSize = 0.4f;

    private const int arenaLayer = 10;
    #endregion

    #region Public Interface
    /// <summary>
    /// Points shown.
    /// </summary>
    public int PointCount { get; private set; }

    public void Initialize(Material material)
    {
        this.gameObject.layer = ScanOverlay.arenaLayer;
        this.filter = this.gameObject.AddComponent<MeshFilter>();
        this.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        this.mesh = new Mesh { name = "Scan" };
        this.filter.sharedMesh = this.mesh;
    }

    /// <summary>
    /// Replaces the shown points (sim positions).
    /// </summary>
    public void Show(List<Vector3> points)
    {
        this.vertices.Clear();
        this.triangles.Clear();
        float h = ScanOverlay.PointSize / 2;
        foreach (Vector3 point in points)
        {
            int start = this.vertices.Count;
            this.vertices.Add(point + new Vector3(-h, 0, -h));
            this.vertices.Add(point + new Vector3(h, 0, -h));
            this.vertices.Add(point + new Vector3(h, 0, h));
            this.vertices.Add(point + new Vector3(-h, 0, h));
            this.triangles.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
        }
        this.mesh.Clear();
        this.mesh.SetVertices(this.vertices);
        this.mesh.SetTriangles(this.triangles, 0);
        this.mesh.RecalculateBounds();
        this.PointCount = points.Count;
    }
    #endregion

    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<int> triangles = new List<int>();
    private MeshFilter filter;
    private Mesh mesh;

    private void OnDestroy()
    {
        if (this.mesh != null)
        {
            GameObject.Destroy(this.mesh);
        }
    }
}
