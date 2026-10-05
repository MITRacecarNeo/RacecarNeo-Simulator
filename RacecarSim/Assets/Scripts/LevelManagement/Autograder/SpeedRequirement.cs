using UnityEngine;

/// <summary>
/// An autograder task in which the user must reach a particular speed.
/// </summary>
public class SpeedRequirement : AutograderTask
{
    #region Set in Unity Editor
    /// <summary>
    /// The speed in m/s which the player must reach to complete this task.
    /// </summary>
    [SerializeField]
    private float speed;
    #endregion

    private void Update()
    {
        // Ground speed only, so the car dropping onto the track at spawn does not count
        Vector3 velocity = LevelManager.GetCar().Physics.LinearVelocity;
        if (new Vector2(velocity.x, velocity.z).magnitude > this.speed)
        {
            AutograderManager.CompleteTask(this);
        }
    }
}
