using System.Collections;
using UnityEngine;

/// <summary>
/// Moves a gate back and forth along a world direction: out by moveDistance, hold, back to the
/// start, hold, repeating.
/// </summary>
public class MovingGate : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// How far the gate moves from its start along direction (in dm; negative moves the other way).
    /// </summary>
    [SerializeField]
    private float moveDistance = 1.0f;

    /// <summary>
    /// The speed of the gate while moving (in dm/s).
    /// </summary>
    [SerializeField]
    private float moveSpeed = 1.0f;

    /// <summary>
    /// The time the gate holds at each end (in seconds).
    /// </summary>
    [SerializeField]
    private float holdTime = 5.0f;

    /// <summary>
    /// The world direction of travel: up for a lifting gate, forward for a sliding one.
    /// </summary>
    [SerializeField]
    private Vector3 direction = Vector3.up;
    #endregion

    /// <summary>
    /// The gate's position when the level started.
    /// </summary>
    private Vector3 startPosition;

    private void Start()
    {
        this.startPosition = this.transform.position;
        this.StartCoroutine(this.MoveCoroutine());
    }

    private IEnumerator MoveCoroutine()
    {
        while (true)
        {
            yield return this.StartCoroutine(this.MoveToPosition(this.startPosition + this.direction * this.moveDistance));
            yield return new WaitForSeconds(this.holdTime);
            yield return this.StartCoroutine(this.MoveToPosition(this.startPosition));
            yield return new WaitForSeconds(this.holdTime);
        }
    }

    private IEnumerator MoveToPosition(Vector3 target)
    {
        while (Vector3.Distance(this.transform.position, target) > 0.01f)
        {
            this.transform.position = Vector3.MoveTowards(this.transform.position, target, this.moveSpeed * Time.deltaTime);
            yield return null;
        }
        this.transform.position = target;
    }
}
