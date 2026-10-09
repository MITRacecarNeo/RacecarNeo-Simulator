using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The third-person views of the car cycled with the space bar, and the scroll wheel zoom.
/// </summary>
public static class PlayerCameraViews
{
    #region Constants
    /// <summary>
    /// Each view's camera offset from the car's center at zoom 1 (x right, y up, z forward, in car units).
    /// </summary>
    /// <remarks>Levels select views by index (AutograderLevelInfo.DefaultCameraIndex); append new views.</remarks>
    private static readonly Vector3[] offsets =
    {
        new Vector3(0, 4, -8),          // chase
        new Vector3(0, 20, -2),         // overhead
        new Vector3(0, 4, 8),           // front
        new Vector3(0, 6.4f, -6.4f),    // 45 degree overhead, from behind
        new Vector3(0, 6.4f, 6.4f),     // 45 degree front
        new Vector3(8, 3, 0),           // right side
        new Vector3(-8, 3, 0),          // left side
        new Vector3(5.7f, 4, -5.7f),    // 45 degree right, from behind
        new Vector3(-5.7f, 4, -5.7f)    // 45 degree left, from behind
    };

    /// <summary>
    /// Zoom factor change per scroll wheel notch.
    /// </summary>
    private const float zoomStep = 1.1f;
    #endregion

    #region Public Interface
    /// <summary>
    /// The number of views.
    /// </summary>
    public static int Count { get { return PlayerCameraViews.offsets.Length; } }

    /// <summary>
    /// The smallest zoom factor (closest to the car).
    /// </summary>
    public const float MinZoom = 0.25f;

    /// <summary>
    /// The largest zoom factor (farthest from the car).
    /// </summary>
    public const float MaxZoom = 4;

    /// <summary>
    /// Objects which use the scroll wheel themselves (a resizable cone, a selected AR marker); zoom is off while any is registered.
    /// </summary>
    public static readonly HashSet<Object> ScrollCaptures = new HashSet<Object>();

    /// <summary>
    /// The camera position for a view of the car; zoom moves the camera along the line to the target.
    /// </summary>
    /// <param name="car">The car's transform, which sets the view directions.</param>
    /// <param name="target">The point the camera looks at (Racecar.Center).</param>
    /// <param name="view">The view index.</param>
    /// <param name="zoom">The zoom factor; 1 is the view's default distance.</param>
    public static Vector3 Position(Transform car, Vector3 target, int view, float zoom)
    {
        Vector3 offset = PlayerCameraViews.offsets[view] * zoom;
        Vector3 horizontal = car.forward * offset.z + car.right * offset.x;
        return target + new Vector3(horizontal.x, offset.y, horizontal.z);
    }

    /// <summary>
    /// The zoom factor after a scroll; scrolling up moves the camera closer.
    /// </summary>
    /// <param name="zoom">The current zoom factor.</param>
    /// <param name="scroll">The scroll amount in notches (Input.mouseScrollDelta.y).</param>
    public static float Zoom(float zoom, float scroll)
    {
        return Mathf.Clamp(zoom * Mathf.Pow(PlayerCameraViews.zoomStep, -scroll), PlayerCameraViews.MinZoom, PlayerCameraViews.MaxZoom);
    }
    #endregion
}
