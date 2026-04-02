using System.Collections;
using UnityEngine;

public class TriggerGate : MonoBehaviour
{
    public float moveDistance = 5.0f; // How far the gate raises
    public float moveSpeed = 1.0f;   // Speed of the raise
    public float holdTime = 5.0f;    // Time the gate stays raised before lowering
    public float raiseDelay = 0.0f;  // Delay before the gate starts raising

    private Vector3 startPos;
    private bool isActive = false;

    void Start()
    {
        startPos = transform.position; // Record the starting position
    }

    /// <summary>
    /// Raises the gate upward by moveDistance. Ignored if a cycle is already in progress.
    /// </summary>
    public void Raise()
    {
        if (!isActive)
        {
            StartCoroutine(RaiseAndLower());
        }
    }

    IEnumerator RaiseAndLower()
    {
        isActive = true;
        // Wait before raising
        if (raiseDelay > 0)
        {
            yield return new WaitForSeconds(raiseDelay);
        }
        // Raise the gate
        yield return StartCoroutine(MoveToPosition(startPos + Vector3.up * moveDistance));
        // Hold at the top
        yield return new WaitForSeconds(holdTime);
        // Lower back down
        yield return StartCoroutine(MoveToPosition(startPos));
        isActive = false;
    }

    IEnumerator MoveToPosition(Vector3 target)
    {
        while (Vector3.Distance(transform.position, target) > 0.01f)
        {
            transform.position = Vector3.MoveTowards(transform.position, target, moveSpeed * Time.deltaTime);
            yield return null;
        }
        transform.position = target; // Ensure the target position is reached
    }
}
