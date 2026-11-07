using System;
using UnityEngine;

/// <summary>
/// Draws 84 LED colors onto a light band mesh (LedBar) through an emission texture: LED 0 at the
/// band's UV x = 0 (front-left), around the rear, to LED 83 at the front-right. Each LED lights its
/// own segment; the outer pixel of each segment is dimmed so neighboring LEDs read as dots.
/// </summary>
public class LedBandRenderer : IDisposable
{
    /// <summary>
    /// Texture pixels per LED along the band.
    /// </summary>
    private const int pixelsPerLed = 4;

    /// <summary>
    /// Emission strength of a fully lit LED, so lit segments stand out in daylight scenes.
    /// </summary>
    private static readonly Color emissionColor = new Color(1.6f, 1.6f, 1.6f);

    private static readonly int emissionMapId = Shader.PropertyToID("_EmissionMap");
    private static readonly int emissionColorId = Shader.PropertyToID("_EmissionColor");

    private readonly Texture2D texture;
    private readonly Color32[] pixels;
    private readonly Material material;

    /// <summary>
    /// Takes a per-band copy of the band's material, released by Dispose.
    /// </summary>
    public LedBandRenderer(Renderer band)
    {
        this.texture = new Texture2D(ActuatorCommands.LedCount * LedBandRenderer.pixelsPerLed, 1, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        this.pixels = new Color32[this.texture.width];
        this.material = band.material;
        this.material.SetTexture(LedBandRenderer.emissionMapId, this.texture);
        this.material.SetColor(LedBandRenderer.emissionColorId, LedBandRenderer.emissionColor);
    }

    /// <summary>
    /// The colors last drawn, LED 0 first.
    /// </summary>
    public Color32[] Shown { get; private set; }

    /// <summary>
    /// Draws the colors (LedCount entries, LED 0 first); does nothing when they did not change.
    /// </summary>
    public void Show(Color32[] colors)
    {
        if (this.Shown != null && LedBandRenderer.Same(colors, this.Shown))
        {
            return;
        }

        this.Shown = (Color32[])colors.Clone();
        for (int i = 0; i < colors.Length; i++)
        {
            Color32 dim = Color32.Lerp(new Color32(0, 0, 0, 255), colors[i], 0.35f);
            int x = i * LedBandRenderer.pixelsPerLed;
            this.pixels[x] = dim;
            this.pixels[x + 1] = colors[i];
            this.pixels[x + 2] = colors[i];
            this.pixels[x + 3] = dim;
        }
        this.texture.SetPixels32(this.pixels);
        this.texture.Apply(false);
    }

    /// <summary>
    /// Releases the texture and the material copy.
    /// </summary>
    public void Dispose()
    {
        UnityEngine.Object.Destroy(this.texture);
        UnityEngine.Object.Destroy(this.material);
    }

    private static bool Same(Color32[] a, Color32[] b)
    {
        for (int i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
            {
                return false;
            }
        }
        return true;
    }
}
