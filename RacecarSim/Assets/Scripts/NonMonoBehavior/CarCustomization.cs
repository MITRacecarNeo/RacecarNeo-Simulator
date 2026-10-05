using System;
using UnityEngine;

/// <summary>
/// Encapsulates all of the appearance customization options for a car.
/// </summary>
[Serializable]
public class CarCustomization
{
    /// <summary>
    /// The color of the stripe around the payload shell (white on the physical car).
    /// </summary>
    public SerializableColor FrontColor;

    /// <summary>
    /// True if the stripe should be metallic.
    /// </summary>
    public bool IsFrontShiny;

    /// <summary>
    /// The color of the logos on the payload shell (red on the physical car).
    /// </summary>
    public SerializableColor BackColor;

    /// <summary>
    /// True if the logos should be metallic.
    /// </summary>
    public bool IsBackShiny;

    /// <summary>
    /// Creates a customization containing a single matte color.
    /// </summary>
    /// <param name="mainColor">The color applied to the stripe and the logos.</param>
    public CarCustomization(Color mainColor)
        : this(mainColor, mainColor)
    {
    }

    /// <summary>
    /// Creates a matte customization with separate stripe and logo colors.
    /// </summary>
    /// <param name="stripeColor">The color of the stripe.</param>
    /// <param name="logoColor">The color of the logos.</param>
    public CarCustomization(Color stripeColor, Color logoColor)
    {
        this.FrontColor = new SerializableColor(stripeColor);
        this.IsFrontShiny = false;
        this.BackColor = new SerializableColor(logoColor);
        this.IsBackShiny = false;
    }
}
