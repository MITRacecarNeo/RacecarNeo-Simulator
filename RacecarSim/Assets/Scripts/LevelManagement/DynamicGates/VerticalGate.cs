using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VerticalGate : MonoBehaviour
{
    public float moveDistance = 1.0f;
    public float moveSpeed = 1.0f;
    public float holdTime = 5.0f;

    private Vector3 startPos;

    // Start is called before the first frame update

    void Start()
    {
        startPos = transform.position; // Record the starting position
        StartCoroutine(MoveCoroutine()); // Start the movement coroutine
    }

    IEnumerator MoveCoroutine()
    {
        while (true)
        {
            // Move up
            yield return StartCoroutine(MoveToPosition(startPos + Vector3.up * moveDistance));
            // Hold at the top
            yield return new WaitForSeconds(holdTime);
            // Move down
            yield return StartCoroutine(MoveToPosition(startPos));
            // Hold at the bottom
            yield return new WaitForSeconds(holdTime);
        }
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
