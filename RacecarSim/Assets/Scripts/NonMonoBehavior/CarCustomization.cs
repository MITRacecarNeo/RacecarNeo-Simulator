using System;
using UnityEngine;

/// <summary>
/// Encapsulates all of the appearance customization options for a car.
/// </summary>
[Serializable]
public class CarCustomization
{
    /// <summary>
    /// The CAD color of the payload shell (near black on the physical car).
    /// </summary>
    public static readonly Color DefaultShellColor = new Color(0x19 / 255f, 0x19 / 255f, 0x19 / 255f);

    /// <summary>
    /// The color of the payload shell (top cap and main body). The R logo, the beaver, and the LED
    /// bar keep their own colors.
    /// </summary>
    public SerializableColor ShellColor;

    /// <summary>
    /// True if the shell should be metallic.
    /// </summary>
    public bool IsShellShiny;

    /// <summary>
    /// Creates a matte customization.
    /// </summary>
    /// <param name="shellColor">The color of the payload shell.</param>
    public CarCustomization(Color shellColor)
    {
        this.ShellColor = new SerializableColor(shellColor);
        this.IsShellShiny = false;
    }
}
