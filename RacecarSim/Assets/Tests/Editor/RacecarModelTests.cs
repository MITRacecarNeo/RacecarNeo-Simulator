using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Imported RACECAR Neo V2 model geometry at the world scale (1 unit = 1 dm): wheel and kingpin
/// positions, sensor anchors, overall extents, and the triangle budget.
/// </summary>
public class RacecarModelTests
{
    private const float Tolerance = 0.005f;

    // Decimation moves outermost vertices by up to 0.5 mm; extents are held to 1 mm.
    private const float ExtentTolerance = 0.01f;

    private GameObject model;

    [OneTimeSetUp]
    public void LoadModel()
    {
        this.model = AssetDatabase.LoadAssetAtPath<GameObject>(RacecarModelImport.ModelPath);
        Assert.IsNotNull(this.model, RacecarModelImport.ModelPath);
    }

    [TestCase("Wheel_FL", -0.89f, 0.36f, 1.125f)]
    [TestCase("Wheel_FR", 0.89f, 0.36f, 1.125f)]
    [TestCase("Wheel_RL", -0.89f, 0.36f, -1.125f)]
    [TestCase("Wheel_RR", 0.89f, 0.36f, -1.125f)]
    [TestCase("Knuckle_FL", -0.73f, 0.29f, 1.125f)]
    [TestCase("Knuckle_FR", 0.73f, 0.29f, 1.125f)]
    [TestCase("CameraAnchor", 0f, 1.195f, 1.654f)]
    [TestCase("LidarAnchor", 0f, 2.016f, 1.258f)]
    [TestCase("ImuAnchor", 0f, 1.135f, 0.774f)]
    public void Part_IsAtSpecPositionWithoutRotation(string name, float x, float y, float z)
    {
        Transform part = this.Part(name);
        Assert.AreEqual(x, part.localPosition.x, Tolerance, "x");
        Assert.AreEqual(y, part.localPosition.y, Tolerance, "y");
        Assert.AreEqual(z, part.localPosition.z, Tolerance, "z");
        Assert.AreEqual(0, Quaternion.Angle(Quaternion.identity, part.localRotation), 0.01f, "rotation");
        Assert.AreEqual(Vector3.one, part.localScale, "scale");
    }

    [TestCase("Wheel_FL")]
    [TestCase("Wheel_RR")]
    public void Wheel_MeshIsCenteredWithSpecRadius(string name)
    {
        Bounds bounds = this.Part(name).GetComponent<MeshFilter>().sharedMesh.bounds;
        Assert.AreEqual(0.36f, bounds.extents.y, Tolerance, "radius (vertical)");
        Assert.AreEqual(0.36f, bounds.extents.z, Tolerance, "radius (longitudinal)");
        Assert.AreEqual(0, bounds.center.y, Tolerance, "center y");
        Assert.AreEqual(0, bounds.center.z, Tolerance, "center z");
    }

    [Test]
    public void Model_ExtentsMatchSpec()
    {
        Bounds bounds = this.ModelBounds();
        Assert.AreEqual(0, bounds.min.y, ExtentTolerance, "tire contact at root");
        Assert.AreEqual(2.670f, bounds.max.y, ExtentTolerance, "antenna top");
        Assert.AreEqual(-1.738f, bounds.min.z, ExtentTolerance, "rear (full-width rail)");
        Assert.AreEqual(1.807f, bounds.max.z, ExtentTolerance, "front (impact rail)");
        Assert.AreEqual(2.115f, bounds.size.x, ExtentTolerance, "width");
    }

    [Test]
    public void Model_FitsTriangleBudget()
    {
        long triangles = this.model.GetComponentsInChildren<MeshFilter>()
            .Select(f => f.sharedMesh)
            .Sum(m => Enumerable.Range(0, m.subMeshCount).Sum(s => (long)m.GetIndexCount(s)) / 3);
        Assert.LessOrEqual(triangles, 110000);
    }

    [TestCase("AccentStripe")]
    [TestCase("AccentLogo")]
    public void RecolorTarget_HasRenderer(string name)
    {
        Assert.IsNotNull(this.Part(name).GetComponent<MeshRenderer>());
    }

    private Transform Part(string name)
    {
        Transform part = this.model.transform.Find(name);
        Assert.IsNotNull(part, name);
        return part;
    }

    private Bounds ModelBounds()
    {
        Bounds? total = null;
        foreach (MeshFilter filter in this.model.GetComponentsInChildren<MeshFilter>())
        {
            Bounds local = filter.sharedMesh.bounds;
            Bounds shifted = new Bounds(local.center + filter.transform.localPosition, local.size);
            if (total.HasValue)
            {
                Bounds grown = total.Value;
                grown.Encapsulate(shifted);
                total = grown;
            }
            else
            {
                total = shifted;
            }
        }
        return total.Value;
    }
}
