using UnityEngine;

/// <summary>
/// Keyboard keys for simulator functions outside the controller emulation (Controller): the level
/// and camera hotkeys. LevelManager and Racecar read these; InputBindings lists them on the
/// Controls screen.
/// </summary>
public static class Hotkeys
{
    /// <summary>
    /// Return to the main menu.
    /// </summary>
    public const KeyCode Exit = KeyCode.Escape;

    /// <summary>
    /// Halve the time scale.
    /// </summary>
    public const KeyCode SlowDown = KeyCode.LeftAlt;

    /// <summary>
    /// Double the time scale (up to 1x); AltGr on keyboards without a right Alt.
    /// </summary>
    public const KeyCode SpeedUp = KeyCode.RightAlt;

    /// <summary>
    /// Second key for SpeedUp.
    /// </summary>
    public const KeyCode SpeedUpAlternate = KeyCode.AltGr;

    /// <summary>
    /// Skip to the next checkpoint (Exploration).
    /// </summary>
    public const KeyCode SkipCheckpoint = KeyCode.Tab;

    /// <summary>
    /// Return the car to the current checkpoint (Exploration).
    /// </summary>
    public const KeyCode ResetToCheckpoint = KeyCode.CapsLock;

    /// <summary>
    /// Held with a car number to reset that car (races with several cars).
    /// </summary>
    public const KeyCode ResetCar = KeyCode.R;

    /// <summary>
    /// Next player camera view.
    /// </summary>
    public const KeyCode NextView = KeyCode.Space;

    /// <summary>
    /// Back to the default camera view and zoom.
    /// </summary>
    public const KeyCode ResetView = KeyCode.C;

    /// <summary>
    /// Show or hide the HUD sensor panels (camera, depth, LIDAR, IMU).
    /// </summary>
    public const KeyCode ToggleSensors = KeyCode.H;

    /// <summary>
    /// Show or hide the HUD map and speed gauge.
    /// </summary>
    public const KeyCode ToggleMap = KeyCode.M;
}
