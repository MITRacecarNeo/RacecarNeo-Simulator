using UnityEngine;

/// <summary>
/// Swaps the car model's parts for the same parts from a model with fewer or more triangles
/// (Settings.CarDetail). Visual only: colliders, wheel colliders, mass, and sensor anchors stay
/// as the prefab has them, and the LED band keeps its own material.
/// </summary>
public class CarModelDetail : MonoBehaviour
{
    /// <summary>
    /// The model parts swapped, by name; every detail model has them at the same positions.
    /// </summary>
    public static readonly string[] Parts =
    {
        "Body", "Shell", "Knuckle_FL", "Knuckle_FR", "Wheel_FL", "Wheel_FR", "Wheel_RL", "Wheel_RR",
    };

    /// <summary>
    /// The car's model instance, whose parts are swapped.
    /// </summary>
    [SerializeField]
    private Transform model;

    /// <summary>
    /// The detail models (model assets), in Settings.CarDetailLevel order.
    /// </summary>
    [SerializeField]
    private GameObject[] models = new GameObject[3];

    /// <summary>
    /// The detail level the parts were last set to.
    /// </summary>
    public Settings.CarDetailLevel Level { get; private set; } = Settings.DefaultCarDetail;

    private void Awake()
    {
        // Before Racecar.SetIndex copies the shell material for the car's color
        this.Apply(Presentation.IsInteractive ? Settings.CarDetail : Settings.DefaultCarDetail);
    }

    /// <summary>
    /// Sets every part's mesh and materials from the detail model. Call before Racecar.SetIndex,
    /// which recolors the shell's material.
    /// </summary>
    /// <param name="level">The detail level to show.</param>
    public void Apply(Settings.CarDetailLevel level)
    {
        Transform source = this.models[(int)level].transform;
        foreach (string part in CarModelDetail.Parts)
        {
            Transform from = source.Find(part);
            Transform to = this.model.Find(part);
            to.GetComponent<MeshFilter>().sharedMesh = from.GetComponent<MeshFilter>().sharedMesh;
            to.GetComponent<Renderer>().sharedMaterials = from.GetComponent<Renderer>().sharedMaterials;
        }
        this.Level = level;
    }
}
