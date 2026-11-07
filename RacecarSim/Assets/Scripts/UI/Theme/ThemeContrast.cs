using UnityEngine;

/// <summary>
/// WCAG 2.1 contrast between two colors, for checking text on theme colors and tints.
/// </summary>
public static class ThemeContrast
{
    /// <summary>
    /// Contrast ratio from 1 (same luminance) to 21 (black on white).
    /// </summary>
    public static float Ratio(Color32 a, Color32 b)
    {
        float la = ThemeContrast.Luminance(a);
        float lb = ThemeContrast.Luminance(b);
        return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
    }

    private static float Luminance(Color32 color)
    {
        float Channel(byte value)
        {
            float c = value / 255f;
            return c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }
        return 0.2126f * Channel(color.r) + 0.7152f * Channel(color.g) + 0.0722f * Channel(color.b);
    }
}
