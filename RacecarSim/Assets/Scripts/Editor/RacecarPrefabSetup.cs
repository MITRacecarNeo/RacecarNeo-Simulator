using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies the RACECAR Neo V2 model geometry to the car prefab: the model as the visual, wheel
/// colliders at the model's wheel centers, box colliders fitted to the model, and the cameras and
/// LIDAR at the model's sensor anchors, and the physical car's mass, center of mass, inertia, and
/// suspension and tire grip. The friction curves' shapes are left as is.
/// Safe to run again after the model is re-exported. Batch use:
/// <c>Unity.exe -batchmode -quit -projectPath . -executeMethod RacecarPrefabSetup.Apply</c>
/// </summary>
public static class RacecarPrefabSetup
{
    /// <summary>
    /// Path of the car prefab.
    /// </summary>
    public const string PrefabPath = "Assets/Prefabs/Racecar.prefab";

    /// <summary>
    /// Name of the model instance under the car.
    /// </summary>
    public const string ModelName = "Model";

    /// <summary>
    /// Name of the object holding the body colliders.
    /// </summary>
    public const string CollidersName = "Colliders";

    /// <summary>
    /// The LED band material: a light diffuser color, with emission for the LED texture that
    /// LedStrip sets per car. Emission is black in the asset (the band shows no light outside
    /// play mode) but enabled, so builds keep the emissive shader variant.
    /// </summary>
    public const string LedBandMaterialPath = "Assets/Models/RacecarNeoV2/LedBand.mat";

    /// <summary>
    /// Wheel collider names in Drive.WheelColliders order, with the matching model wheels.
    /// </summary>
    public static readonly (string Collider, string Model)[] Wheels =
    {
        ("FrontLeft", "Wheel_FL"),
        ("FrontRight", "Wheel_FR"),
        ("BackLeft", "Wheel_RL"),
        ("BackRight", "Wheel_RR"),
    };

    /// <summary>
    /// Body collider boxes (center, size) in car root coordinates, fitted to the model: chassis
    /// across the wheels up to the payload floor and out to the front impact rail, rear bumper,
    /// mudflaps, and full-width rail, payload shell (rear
    /// section, front sensor deck), LIDAR stack, LIDAR head, and the two antenna masts (the LIDAR
    /// sees them behind it, as on the physical car).
    /// </summary>
    public static readonly (string Name, Vector3 Center, Vector3 Size)[] BodyBoxes =
    {
        ("Chassis", new Vector3(0, 0.6125f, 0.161f), new Vector3(2.09f, 0.815f, 3.292f)),
        ("RearBumper", new Vector3(0, 0.438f, -1.464f), new Vector3(1.85f, 0.544f, 0.548f)),
        ("ShellRear", new Vector3(0, 1.445f, -0.3125f), new Vector3(2.0f, 0.85f, 2.025f)),
        ("ShellFront", new Vector3(0, 1.295f, 1.1875f), new Vector3(1.05f, 0.55f, 0.975f)),
        ("LidarStack", new Vector3(0, 1.74f, 1.26f), new Vector3(0.7f, 0.34f, 0.7f)),
        ("LidarHead", new Vector3(0, 2.0075f, 1.2575f), new Vector3(0.7f, 0.195f, 0.7f)),
        ("AntennaRight", new Vector3(0.449f, 1.985f, -1.425f), new Vector3(0.124f, 1.37f, 0.124f)),
        ("AntennaLeft", new Vector3(-0.449f, 1.985f, -1.425f), new Vector3(0.124f, 1.37f, 0.124f)),
    };

    /// <summary>
    /// Size of the LIDAR's collision trigger, slightly larger than the LIDAR head.
    /// </summary>
    public static readonly Vector3 LidarTriggerSize = new Vector3(0.76f, 0.26f, 0.76f);

    /// <summary>
    /// Height of the camera lens center above the ground on the physical car (112.5 mm: measured
    /// underside 100 mm plus half the 25 mm body). The model places it 7.0 mm higher; the
    /// fore-aft position comes from the model.
    /// </summary>
    public const float MeasuredCameraHeight = 1.125f;

    /// <summary>
    /// Height of the LIDAR scan plane above the ground on the physical car (195 mm). The model
    /// places it 6.6 mm higher; the rotation axis position comes from the model.
    /// </summary>
    public const float MeasuredLidarHeight = 1.95f;

    /// <summary>
    /// Mass of the car with its battery, in kilograms.
    /// </summary>
    public const float Mass = 2.36f;

    /// <summary>
    /// Center of mass in car root coordinates: centered, 95 mm above the ground (measured: the car
    /// balances on two wheels at 47 degrees), at 52.2 percent of the wheelbase from the rear axle
    /// (measured axle loads 1160 g front, 1060 g rear).
    /// </summary>
    public static readonly Vector3 CenterOfMassPosition = new Vector3(0, 0.95f, 0.05f);

    /// <summary>
    /// Principal moments of inertia about the center of mass (pitch about x, yaw about y, roll
    /// about z), in kg units^2; 0.0216, 0.0200, and 0.0098 kg m^2.
    /// </summary>
    public static readonly Vector3 InertiaTensor = new Vector3(2.16f, 2.00f, 0.98f);

    /// <summary>
    /// Mass of one wheel and tire, in kilograms.
    /// </summary>
    public const float WheelMass = 0.04f;

    /// <summary>
    /// Wheel rate and damper of the front and rear suspension, per wheel, in N/m and N s/m (both
    /// independent of the 1 unit = 0.1 m world scale): coilover springs of 1.3 and 1.1 mm wire at an
    /// assumed 0.6 motion ratio, damped at 0.6 of critical.
    /// </summary>
    public static readonly (float Spring, float Damper) FrontSuspension = (770, 26);

    /// <inheritdoc cref="FrontSuspension"/>
    public static readonly (float Spring, float Damper) RearSuspension = (400, 18);

    /// <summary>
    /// Suspension travel of each wheel (20 mm).
    /// </summary>
    public const float SuspensionTravel = 0.2f;

    /// <summary>
    /// Fraction of the suspension travel at which the spring holds the car at rest.
    /// </summary>
    public const float SuspensionTarget = 0.5f;

    /// <summary>
    /// Tire friction stiffness, the peak friction coefficient on a smooth floor (estimated). The
    /// friction curves' shapes are unchanged.
    /// </summary>
    public const float TireGrip = 0.8f;

    /// <summary>
    /// Wheel damping rate. The speed controller's feedforward (<c>Drive</c>) is matched to it.
    /// </summary>
    public const float WheelDampingRate = 0.07867f;

    private const int PlayerLayer = 9;

    /// <summary>
    /// Rebuilds the car prefab geometry from the model and saves the prefab.
    /// </summary>
    public static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(RacecarPrefabSetup.PrefabPath);
        try
        {
            RacecarPrefabSetup.ApplyTo(root);
            PrefabUtility.SaveAsPrefabAsset(root, RacecarPrefabSetup.PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        Debug.Log($"RacecarPrefabSetup: {RacecarPrefabSetup.PrefabPath} rebuilt from {RacecarModelImport.ModelPath}");
    }

    private static void ApplyTo(GameObject root)
    {
        Transform body = root.transform.Find("TransformShift");
        body.localPosition = Vector3.zero;

        // Visual model
        RacecarPrefabSetup.DestroyChild(body, RacecarPrefabSetup.ModelName);
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RacecarModelImport.ModelPath);
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset, body);
        model.name = RacecarPrefabSetup.ModelName;
        model.transform.SetSiblingIndex(0);

        // Body colliders replace the old chassis boxes and primitive visuals
        RacecarPrefabSetup.DestroyChild(body, "Chasis");
        RacecarPrefabSetup.DestroyChild(body, "Wheels");
        RacecarPrefabSetup.DestroyChild(body, RacecarPrefabSetup.CollidersName);
        GameObject colliders = new GameObject(RacecarPrefabSetup.CollidersName);
        colliders.transform.SetParent(body, false);
        colliders.transform.SetSiblingIndex(1);
        foreach ((string _, Vector3 center, Vector3 size) in RacecarPrefabSetup.BodyBoxes)
        {
            BoxCollider box = colliders.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        // Wheel colliders at the model wheel centers, raised so the wheels rest there; rest
        // rotations and friction unchanged
        Transform wheelColliders = body.Find("WheelColliders");
        Drive drive = root.GetComponent<Drive>();
        SerializedObject driveObject = new SerializedObject(drive);
        SerializedProperty wheelModels = driveObject.FindProperty("Wheels");
        wheelModels.arraySize = RacecarPrefabSetup.Wheels.Length;
        for (int i = 0; i < RacecarPrefabSetup.Wheels.Length; i++)
        {
            Transform wheelModel = model.transform.Find(RacecarPrefabSetup.Wheels[i].Model);
            WheelCollider wheelCollider = wheelColliders.Find(RacecarPrefabSetup.Wheels[i].Collider).GetComponent<WheelCollider>();
            wheelCollider.transform.localPosition = wheelModel.localPosition + Vector3.up * RacecarPrefabSetup.RestExtension;
            wheelCollider.radius = wheelModel.GetComponent<MeshFilter>().sharedMesh.bounds.extents.y;
            wheelCollider.center = Vector3.zero;
            wheelCollider.mass = RacecarPrefabSetup.WheelMass;
            wheelCollider.wheelDampingRate = RacecarPrefabSetup.WheelDampingRate;
            WheelFrictionCurve forward = wheelCollider.forwardFriction;
            forward.stiffness = RacecarPrefabSetup.TireGrip;
            wheelCollider.forwardFriction = forward;
            WheelFrictionCurve sideways = wheelCollider.sidewaysFriction;
            sideways.stiffness = RacecarPrefabSetup.TireGrip;
            wheelCollider.sidewaysFriction = sideways;
            (float spring, float damper) = i < 2 ? RacecarPrefabSetup.FrontSuspension : RacecarPrefabSetup.RearSuspension;
            wheelCollider.suspensionSpring = new JointSpring
            {
                spring = spring,
                damper = damper,
                targetPosition = RacecarPrefabSetup.SuspensionTarget,
            };
            wheelCollider.suspensionDistance = RacecarPrefabSetup.SuspensionTravel;
            drive.WheelColliders[i] = wheelCollider;
            wheelModels.GetArrayElementAtIndex(i).objectReferenceValue = wheelModel.gameObject;
        }
        SerializedProperty knuckles = driveObject.FindProperty("SteeringKnuckles");
        knuckles.arraySize = 2;
        knuckles.GetArrayElementAtIndex(0).objectReferenceValue = model.transform.Find("Knuckle_FL");
        knuckles.GetArrayElementAtIndex(1).objectReferenceValue = model.transform.Find("Knuckle_FR");
        driveObject.ApplyModifiedPropertiesWithoutUndo();

        // Mass properties; CenterOfMass applies the center of mass at Start
        Rigidbody rigidbody = root.GetComponent<Rigidbody>();
        rigidbody.mass = RacecarPrefabSetup.Mass;
        rigidbody.automaticCenterOfMass = false;
        rigidbody.centerOfMass = RacecarPrefabSetup.CenterOfMassPosition;
        rigidbody.automaticInertiaTensor = false;
        rigidbody.inertiaTensor = RacecarPrefabSetup.InertiaTensor;
        rigidbody.inertiaTensorRotation = Quaternion.identity;
        SerializedObject centerOfMass = new SerializedObject(root.GetComponent<CenterOfMass>());
        centerOfMass.FindProperty("Com").vector3Value = RacecarPrefabSetup.CenterOfMassPosition;
        centerOfMass.ApplyModifiedPropertiesWithoutUndo();

        // Recolor target
        SerializedObject racecarObject = new SerializedObject(root.GetComponent<Racecar>());
        racecarObject.FindProperty("shell").objectReferenceValue = model.transform.Find("Shell").gameObject;
        racecarObject.ApplyModifiedPropertiesWithoutUndo();

        // LED strip in the light band
        Renderer band = model.transform.Find("LedBar").GetComponent<Renderer>();
        band.sharedMaterial = RacecarPrefabSetup.LedBandMaterial();
        LedStrip strip = root.GetComponent<LedStrip>();
        if (strip == null)
        {
            strip = root.AddComponent<LedStrip>();
        }
        SerializedObject stripObject = new SerializedObject(strip);
        stripObject.FindProperty("band").objectReferenceValue = band;
        stripObject.ApplyModifiedPropertiesWithoutUndo();

        // Sensors at the model anchors, at the heights measured on the physical car
        Vector3 cameraPosition = model.transform.Find("CameraAnchor").localPosition;
        cameraPosition.y = RacecarPrefabSetup.MeasuredCameraHeight;
        body.Find("ColorCamera").localPosition = cameraPosition;
        body.Find("DepthCamera").localPosition = cameraPosition;
        Transform lidar = body.Find("Lidar");
        Vector3 lidarPosition = model.transform.Find("LidarAnchor").localPosition;
        lidarPosition.y = RacecarPrefabSetup.MeasuredLidarHeight;
        lidar.localPosition = lidarPosition;
        foreach (Transform child in lidar.Cast<Transform>().ToArray())
        {
            Object.DestroyImmediate(child.gameObject);
        }
        foreach (BoxCollider box in lidar.GetComponents<BoxCollider>().Where(b => !b.isTrigger))
        {
            Object.DestroyImmediate(box);
        }
        BoxCollider trigger = lidar.GetComponent<BoxCollider>();
        trigger.center = Vector3.zero;
        trigger.size = RacecarPrefabSetup.LidarTriggerSize;

        foreach (Transform part in root.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = RacecarPrefabSetup.PlayerLayer;
        }
    }

    /// <summary>
    /// Distance from a wheel collider's anchor down to the wheel center at rest: the suspension
    /// extension the spring holds under the car's weight.
    /// </summary>
    private const float RestExtension = RacecarPrefabSetup.SuspensionTravel * RacecarPrefabSetup.SuspensionTarget;

    /// <summary>
    /// Loads the LED band material, creating it on first use.
    /// </summary>
    private static Material LedBandMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(RacecarPrefabSetup.LedBandMaterialPath);
        if (material != null)
        {
            return material;
        }
        material = new Material(Shader.Find("Standard"))
        {
            name = "LedBand",
            color = new Color(0.55f, 0.55f, 0.55f),
            globalIlluminationFlags = MaterialGlobalIlluminationFlags.None
        };
        material.SetFloat("_Glossiness", 0.4f);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", Color.black);
        AssetDatabase.CreateAsset(material, RacecarPrefabSetup.LedBandMaterialPath);
        return material;
    }

    private static void DestroyChild(Transform parent, string name)
    {
        Transform child = parent.Find(name);
        if (child != null)
        {
            Object.DestroyImmediate(child.gameObject);
        }
    }
}
