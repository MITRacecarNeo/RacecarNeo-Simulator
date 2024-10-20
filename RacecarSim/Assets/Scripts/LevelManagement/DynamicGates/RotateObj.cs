using UnityEngine;
using System.Collections;

public class RotateObj : MonoBehaviour
{
    public float rotationAngle = 180.0f;  // The angle to rotate
    public float rotationSpeed = 1.0f;    // The speed of the rotation
    public float holdTime = 5.0f;         // The time to hold at the rotated positions

    private Quaternion startRotation;     // The starting rotation of the object
    private Quaternion endRotation;       // The target rotation of the object

    void Start()
    {
        startRotation = transform.rotation;  // Record the starting rotation
        endRotation = startRotation * Quaternion.Euler(0, rotationAngle, 0);  // Calculate the target rotation
        StartCoroutine(RotateCoroutine());  // Start the rotation coroutine
    }

    IEnumerator RotateCoroutine()
    {
        while (true)
        {
            // Rotate to 180 degrees
            yield return StartCoroutine(RotateToRotation(endRotation));
            // Hold at 180 degrees
            yield return new WaitForSeconds(holdTime);
            // Rotate back to the starting position
            yield return StartCoroutine(RotateToRotation(startRotation));
            // Hold at the starting position
            yield return new WaitForSeconds(holdTime);
        }
    }

    IEnumerator RotateToRotation(Quaternion targetRotation)
    {
        while (Quaternion.Angle(transform.rotation, targetRotation) > 0.01f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = targetRotation;  // Ensure the target rotation is reached
    }
}