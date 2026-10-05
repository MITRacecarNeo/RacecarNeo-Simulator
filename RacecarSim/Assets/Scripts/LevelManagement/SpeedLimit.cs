using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages a zone in which cars are not allowed to surpass a certain maximum speed.
/// </summary>
public class SpeedLimit : MonoBehaviour
{
    #region Set in Unity Editor
    /// <summary>
    /// The maximum speed (in meters/second) which cars are allowed to travel within this speed limit zone.
    /// </summary>
    [SerializeField]
    private float maxSpeed = 0.5f;
    #endregion

    /// <summary>
    /// The message shown when a car breaks the speed limit.
    /// </summary>
    private string FailureMessage
    {
        get
        {
            return $"You traveled above {this.maxSpeed} m/s within the speed limit zone";
        }
    }            

    /// <summary>
    /// The number of each car's colliders inside the zone. A car counts as inside until its last
    /// collider leaves.
    /// </summary>
    private readonly Dictionary<Racecar, int> collidersInside = new Dictionary<Racecar, int>();

    /// <summary>
    /// Cars that already failed in this zone; each car fails once.
    /// </summary>
    private readonly HashSet<Racecar> failedCars = new HashSet<Racecar>();

    private void Update()
    {
        foreach (Racecar car in this.collidersInside.Keys)
        {
            if (car.Physics.LinearVelocity.magnitude > this.maxSpeed && this.failedCars.Add(car))
            {
                LevelManager.HandleFailure(car.Index, this.FailureMessage);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        Racecar car = other.GetComponentInParent<Racecar>();
        if (car != null)
        {
            this.collidersInside.TryGetValue(car, out int count);
            this.collidersInside[car] = count + 1;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Racecar car = other.GetComponentInParent<Racecar>();
        if (car != null && this.collidersInside.TryGetValue(car, out int count))
        {
            if (count > 1)
            {
                this.collidersInside[car] = count - 1;
            }
            else
            {
                this.collidersInside.Remove(car);
            }
        }
    }
}
