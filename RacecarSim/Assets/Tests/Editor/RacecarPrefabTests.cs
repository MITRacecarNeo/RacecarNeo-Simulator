using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The car prefab matches the V2 model: wheel colliders at the model wheels, sensors at the model
/// anchors, body colliders inside the model extents, recolor targets, and the Player layer.
/// </summary>
public class RacecarPrefabTests
{
    private const float Tolerance = 0.001f;

    private GameObject prefab;
    private Transform model;

    [OneTimeSetUp]
    public void LoadPrefab()
    {
        this.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RacecarPrefabSetup.PrefabPath);
        Assert.IsNotNull(this.prefab, RacecarPrefabSetup.PrefabPath);
        this.model = this.prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == RacecarPrefabSetup.ModelName);
    }

    [Test]
    public void WheelColliders_SitAtModelWheelsWithModelRadius()
    {
        Drive drive = this.prefab.GetComponent<Drive>();
        GameObject[] wheelModels = RacecarPrefabTests.WheelModels(drive);
        for (int i = 0; i < RacecarPrefabSetup.Wheels.Length; i++)
        {
            WheelCollider wheelCollider = drive.WheelColliders[i];
            Transform wheelModel = this.model.Find(RacecarPrefabSetup.Wheels[i].Model);
            Assert.AreEqual(RacecarPrefabSetup.Wheels[i].Collider, wheelCollider.name);
            Assert.AreSame(wheelModel.gameObject, wheelModels[i], $"Drive.Wheels[{i}]");
            // The anchor sits above the model wheel by the suspension's rest extension
            Vector3 anchor = this.RootPosition(wheelModel) + Vector3.up * RacecarPrefabSetup.SuspensionTravel * RacecarPrefabSetup.SuspensionTarget;
            Assert.Less(Vector3.Distance(anchor, this.RootPosition(wheelCollider.transform)), Tolerance, wheelCollider.name);
            Assert.AreEqual(0.36f, wheelCollider.radius, 0.005f, wheelCollider.name);
            Assert.AreEqual(RacecarPrefabSetup.SuspensionTravel, wheelCollider.suspensionDistance, 1e-5f, wheelCollider.name);
            Assert.AreEqual(i < 2 ? 770 : 400, wheelCollider.suspensionSpring.spring, 1e-3f, wheelCollider.name);
        }
    }

    [Test]
    public void Rigidbody_HasThePhysicalCarsMassProperties()
    {
        // A prefab asset is outside the physics scene, so read the serialized values
        SerializedObject body = new SerializedObject(this.prefab.GetComponent<Rigidbody>());
        Vector3 center = new Vector3(0, 0.95f, 0.05f);
        Assert.AreEqual(2.36f, body.FindProperty("m_Mass").floatValue, 1e-4f, "mass");
        Assert.IsFalse(body.FindProperty("m_ImplicitCom").boolValue, "explicit center of mass");
        Assert.IsFalse(body.FindProperty("m_ImplicitTensor").boolValue, "explicit inertia tensor");
        Assert.Less(Vector3.Distance(center, body.FindProperty("m_CenterOfMass").vector3Value), 1e-4f, "center of mass");
        Assert.Less(Vector3.Distance(new Vector3(2.16f, 2.00f, 0.98f), body.FindProperty("m_InertiaTensor").vector3Value), 1e-4f, "inertia tensor");

        // CenterOfMass applies its value at Start and must agree with the Rigidbody
        SerializedObject centerOfMass = new SerializedObject(this.prefab.GetComponent<CenterOfMass>());
        Assert.Less(Vector3.Distance(center, centerOfMass.FindProperty("Com").vector3Value), 1e-4f, "CenterOfMass.Com");
    }

    [Test]
    public void Wheelbase_AndTrackMatchSpec()
    {
        WheelCollider[] wheels = this.prefab.GetComponent<Drive>().WheelColliders;
        Vector3 frontLeft = this.RootPosition(wheels[0].transform);
        Vector3 frontRight = this.RootPosition(wheels[1].transform);
        Vector3 backLeft = this.RootPosition(wheels[2].transform);
        Assert.AreEqual(2.25f, frontLeft.z - backLeft.z, Tolerance, "wheelbase");
        Assert.AreEqual(1.78f, frontRight.x - frontLeft.x, Tolerance, "track");
    }

    [Test]
    public void Sensors_SitAtModelAnchorsAndMeasuredHeights()
    {
        Camera[] cameras = this.prefab.GetComponentsInChildren<Camera>(true);
        Assert.AreEqual("ColorCamera", cameras[0].name, "CameraModule reads the color camera first");
        Assert.AreEqual("DepthCamera", cameras[1].name, "CameraModule reads the depth camera second");
        Vector3 cameraAnchor = this.RootPosition(this.model.Find("CameraAnchor"));
        cameraAnchor.y = 1.125f;
        Assert.Less(Vector3.Distance(cameraAnchor, this.RootPosition(cameras[0].transform)), Tolerance);
        Assert.Less(Vector3.Distance(cameraAnchor, this.RootPosition(cameras[1].transform)), Tolerance);

        Lidar lidar = this.prefab.GetComponentInChildren<Lidar>(true);
        Vector3 lidarAnchor = this.RootPosition(this.model.Find("LidarAnchor"));
        lidarAnchor.y = 1.95f;
        Assert.Less(Vector3.Distance(lidarAnchor, this.RootPosition(lidar.transform)), Tolerance);
        Assert.IsTrue(lidar.GetComponents<Collider>().All(c => c.isTrigger), "LIDAR keeps only its collision trigger");
    }

    [Test]
    public void SensorOrigins_LieInsideTheirBodyColliders()
    {
        // Raycasts starting inside a collider do not hit it, so the sensors never see the car
        GameObject instance = Object.Instantiate(this.prefab);
        try
        {
            Physics.SyncTransforms();
            BoxCollider[] boxes = instance.GetComponentsInChildren<BoxCollider>().Where(b => !b.isTrigger).ToArray();
            Vector3 camera = instance.GetComponentInChildren<Camera>(true).transform.position;
            Vector3 lidar = instance.GetComponentInChildren<Lidar>(true).transform.position;
            Assert.IsTrue(boxes.Any(b => b.bounds.Contains(camera)), "camera");
            Assert.IsTrue(boxes.Any(b => b.bounds.Contains(lidar)), "LIDAR");
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void BodyColliders_StayInsideModelExtents()
    {
        Bounds modelBounds = new Bounds(this.RootPosition(this.model), Vector3.zero);
        foreach (Renderer renderer in this.model.GetComponentsInChildren<Renderer>())
        {
            Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            modelBounds.Encapsulate(new Bounds(this.RootPosition(renderer.transform) + mesh.bounds.center, mesh.bounds.size));
        }
        // 1 mm per side: decimation moves the model's outermost vertices by up to 0.6 mm
        modelBounds.Expand(0.02f);

        BoxCollider[] boxes = this.prefab.GetComponentsInChildren<BoxCollider>(true).Where(b => !b.isTrigger).ToArray();
        Assert.AreEqual(RacecarPrefabSetup.BodyBoxes.Length, boxes.Length);
        foreach (BoxCollider box in boxes)
        {
            Vector3 center = this.RootPosition(box.transform) + box.center;
            Assert.IsTrue(modelBounds.Contains(center - box.size / 2) && modelBounds.Contains(center + box.size / 2), $"{box.name} {center} {box.size} outside model {modelBounds.min} {modelBounds.max}");
        }
    }

    [Test]
    public void Center_LiesInsideABodyCollider()
    {
        GameObject instance = Object.Instantiate(this.prefab);
        try
        {
            Physics.SyncTransforms();
            Vector3 center = instance.GetComponent<Racecar>().Center;
            Assert.IsTrue(instance.GetComponentsInChildren<BoxCollider>().Any(b => !b.isTrigger && b.bounds.Contains(center)));
        }
        finally
        {
            Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void RecolorTargets_AreModelAccents()
    {
        SerializedObject racecar = new SerializedObject(this.prefab.GetComponent<Racecar>());
        Assert.AreEqual("AccentStripe", racecar.FindProperty("chassisFront").objectReferenceValue.name);
        Assert.AreEqual("AccentLogo", racecar.FindProperty("chassisBack").objectReferenceValue.name);
    }

    [Test]
    public void AllParts_AreOnPlayerLayer()
    {
        int player = LayerMask.NameToLayer("Player");
        string[] others = this.prefab.GetComponentsInChildren<Transform>(true).Where(t => t.gameObject.layer != player).Select(t => t.name).ToArray();
        Assert.IsEmpty(others);
    }

    private static GameObject[] WheelModels(Drive drive)
    {
        SerializedProperty wheels = new SerializedObject(drive).FindProperty("Wheels");
        return Enumerable.Range(0, wheels.arraySize).Select(i => (GameObject)wheels.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
    }

    private Vector3 RootPosition(Transform part)
    {
        Vector3 position = Vector3.zero;
        for (Transform t = part; t != this.prefab.transform; t = t.parent)
        {
            position = t.localRotation * position + t.localPosition;
        }
        return position;
    }
}
