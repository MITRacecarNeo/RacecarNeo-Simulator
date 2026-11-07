using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Renders the theme sheet (every text style and component) through the shared panel settings
/// at 1920 x 1080, checks sampled colors against the tokens, and writes Logs/ui-preview-theme.png.
/// </summary>
public class ThemeRenderTests
{
    private const string panelSettingsPath = "Assets/UI/RacecarSimPanel.asset";
    private const string sheetPath = "Assets/Tests/PlayMode/ThemeSheet.uxml";
    private const int width = 1920;
    private const int height = 1080;

    // Per channel; antialiasing and gamma rounding stay well inside it
    private const int tolerance = 6;

    private GameObject host;
    private PanelSettings panel;
    private RenderTexture target;

    [UnityTearDown]
    public IEnumerator Release()
    {
        Object.Destroy(this.host);
        Object.Destroy(this.panel);
        if (this.target != null)
        {
            this.target.Release();
            Object.Destroy(this.target);
        }
        yield return null;
    }

    [UnityTest]
    public IEnumerator ThemeSheet_RendersTokenColors()
    {
#if UNITY_EDITOR
        PanelSettings shared = AssetDatabase.LoadAssetAtPath<PanelSettings>(ThemeRenderTests.panelSettingsPath);
        VisualTreeAsset sheet = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(ThemeRenderTests.sheetPath);
        Assert.IsNotNull(shared, ThemeRenderTests.panelSettingsPath);
        Assert.IsNotNull(sheet, ThemeRenderTests.sheetPath);

        this.target = new RenderTexture(ThemeRenderTests.width, ThemeRenderTests.height, 24, RenderTextureFormat.ARGB32);
        this.panel = Object.Instantiate(shared);
        this.panel.targetTexture = this.target;
        this.host = new GameObject("ThemeSheet");
        UIDocument document = this.host.AddComponent<UIDocument>();
        document.panelSettings = this.panel;
        document.visualTreeAsset = sheet;

        for (int i = 0; i < 5; i++)
        {
            yield return null;
        }

        VisualElement root = document.rootVisualElement;
        Texture2D pixels = ThemeRenderTests.ReadTarget(this.target);
        try
        {
            string logs = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs");
            Directory.CreateDirectory(logs);
            File.WriteAllBytes(Path.Combine(logs, "ui-preview-theme.png"), pixels.EncodeToPNG());

            Rect screen = root.Q("screen").worldBound;
            ThemeRenderTests.AssertColor(pixels, new Vector2(screen.xMax - 4, screen.yMax - 4), ThemeTokens.Charcoal, "background");

            Rect primary = root.Q("primary").worldBound;
            ThemeRenderTests.AssertColor(pixels, new Vector2(primary.xMin + 8, primary.center.y), ThemeTokens.Orange, "primary button");

            Rect timer = root.Q("timer").worldBound;
            ThemeRenderTests.AssertColor(pixels, new Vector2(timer.xMin + 6, timer.yMax - 4), ThemeTokens.Charcoal, "timer badge, bottom-left");

            Assert.IsTrue(ThemeRenderTests.HasLightPixels(pixels, timer), "timer digits drawn over the badge fill");

            Rect badge = root.Q("badge").worldBound;
            ThemeRenderTests.AssertColor(pixels, new Vector2(badge.xMin + 4, badge.yMax - 3), ThemeTokens.Gold, "badge");

            Rect divider = root.Q("divider").worldBound;
            ThemeRenderTests.AssertColor(pixels, new Vector2(divider.xMin + divider.width * 0.1f, divider.center.y), ThemeTokens.Crimson, "gradient start");
            ThemeRenderTests.AssertColor(pixels, new Vector2(divider.xMax - 1, divider.center.y), ThemeTokens.Orange, "gradient end");

            Rect progress = root.Q("progress").worldBound;
            ThemeRenderTests.AssertColor(pixels, new Vector2(progress.xMin + progress.width * 0.8f, progress.center.y), ThemeTokens.Charcoal, "progress beyond fill");

            Assert.AreEqual("Barlow-Regular", root.Q<Label>("body").resolvedStyle.unityFontDefinition.font?.name, "body font");
            Assert.AreEqual("BarlowSemiCondensed-ExtraBold", root.Q<Label>("display").resolvedStyle.unityFontDefinition.font?.name, "display font");
            Assert.AreEqual("JetBrainsMono-SemiBold", root.Q("timer").Q<Label>().resolvedStyle.unityFontDefinition.font?.name, "timer font");
        }
        finally
        {
            Object.Destroy(pixels);
        }
#else
        Assert.Ignore("Loads the theme sheet with AssetDatabase, which needs the editor.");
        yield break;
#endif
    }

    private static Texture2D ReadTarget(RenderTexture texture)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        Texture2D pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;
        return pixels;
    }

    /// <summary>
    /// True when the area holds near-white pixels, such as paper text.
    /// </summary>
    private static bool HasLightPixels(Texture2D pixels, Rect panelArea)
    {
        for (int y = Mathf.CeilToInt(panelArea.yMin); y < panelArea.yMax; y++)
        {
            for (int x = Mathf.CeilToInt(panelArea.xMin); x < panelArea.xMax; x++)
            {
                Color32 pixel = pixels.GetPixel(x, pixels.height - 1 - y);
                if (pixel.r > 230 && pixel.g > 230 && pixel.b > 230)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// Compares the pixel at a panel position (origin top-left) with the expected color.
    /// </summary>
    private static void AssertColor(Texture2D pixels, Vector2 panelPosition, Color32 expected, string what)
    {
        Color32 actual = pixels.GetPixel(Mathf.RoundToInt(panelPosition.x), pixels.height - 1 - Mathf.RoundToInt(panelPosition.y));
        bool close = Mathf.Abs(actual.r - expected.r) <= ThemeRenderTests.tolerance
            && Mathf.Abs(actual.g - expected.g) <= ThemeRenderTests.tolerance
            && Mathf.Abs(actual.b - expected.b) <= ThemeRenderTests.tolerance;
        Assert.IsTrue(close, $"{what} at {panelPosition}: expected {expected}, got {actual}");
    }
}
