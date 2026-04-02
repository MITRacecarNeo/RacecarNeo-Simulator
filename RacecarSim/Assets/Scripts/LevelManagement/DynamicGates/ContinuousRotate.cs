using UnityEngine;

public class ContinuousRotate : MonoBehaviour
{
    public float rotationSpeed = 45.0f; // Degrees per second
    public Transform pivot; // The point to rotate around (if unset, rotates around own origin)

    void Update()
    {
        // Rotate clockwise around the Y-axis (negative Y in Unity = clockwise from top-down)
        if (pivot != null)
        {
            transform.RotateAround(pivot.position, Vector3.down, rotationSpeed * Time.deltaTime);
        }
        else
        {
            transform.Rotate(0, -rotationSpeed * Time.deltaTime, 0);
        }
    }
}
