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
/// Renders the scene change loading card at 1920 x 1080 and checks that its three lines start at
/// the same left edge as the eye reads it (the italic title included), measured on drawn pixels, for
/// titles starting with an upright stem (L) and a diagonal (W), on each mode's cover tint. Writes
/// Logs/ui-preview-loading-card-*.png.
/// </summary>
public class SceneFlowCardRenderTests
{
    private const string overlayPanelPath = "Assets/UI/RacecarSimOverlayPanel.asset";
    private const string layoutPath = "Assets/UI/SceneFlow/SceneFlowOverlay.uxml";

    // Drawn left edges may differ by antialiasing and glyph side bearings
    private const int alignmentTolerance = 3;

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
    public IEnumerator LoadingCard_LinesShareLeftEdge(
        [Values("Lab I: Wall Follower", "Wall Following")] string title,
        [Values] LevelManagerMode mode)
    {
#if UNITY_EDITOR
        PanelSettings shared = AssetDatabase.LoadAssetAtPath<PanelSettings>(SceneFlowCardRenderTests.overlayPanelPath);
        VisualTreeAsset layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(SceneFlowCardRenderTests.layoutPath);
        this.target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        this.panel = Object.Instantiate(shared);
        this.panel.targetTexture = this.target;
        this.host = new GameObject("LoadingCard");
        UIDocument document = this.host.AddComponent<UIDocument>();
        document.panelSettings = this.panel;
        document.visualTreeAsset = layout;
        yield return null;

        VisualElement root = document.rootVisualElement;
        root.Q<TransitionWipe>("wipe").Progress = 1;
        if (SceneFlow.ModeClass(mode) is string modeClass)
        {
            root.Q("scene-flow").AddToClassList(modeClass);
        }
        root.Q<Label>("card-collection").text = "NEO LABS";
        root.Q<Label>("card-title").text = title;
        root.Q<Label>("card-mode").text = "Exploration";
        root.Q("card-thumbnail").style.backgroundImage = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/UI/Levels/Thumbnails/lab-i-wall-follower.jpg");
        root.Q("card").AddToClassList("sf-card--visible");

        // The card fades and slides in over 0.25 s
        float until = Time.realtimeSinceStartup + 0.6f;
        while (Time.realtimeSinceStartup < until)
        {
            yield return null;
        }

        RenderTexture.active = this.target;
        Texture2D pixels = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
        pixels.Apply();
        RenderTexture.active = null;
        try
        {
            string logs = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs");
            Directory.CreateDirectory(logs);
            File.WriteAllBytes(Path.Combine(logs, $"ui-preview-loading-card-{mode.ToString().ToLowerInvariant()}-{LaunchOptions.Slug(title)}.png"), pixels.EncodeToPNG());

            Color32 expected = root.Q<TransitionWipe>("wipe").CoverColor;
            Color32 cover = pixels.GetPixel(1900, 1060);
            Assert.IsTrue(SceneFlowCardRenderTests.Distance(cover, expected) <= 12, $"{mode} cover: expected {expected}, got {cover}");

            // A tint stays close to charcoal and leans toward the mode's color
            int shift = SceneFlowCardRenderTests.Distance(cover, ThemeTokens.Charcoal);
            Color32 toward = mode == LevelManagerMode.Autograder ? ThemeTokens.Crimson : ThemeTokens.Orange;
            if (mode == LevelManagerMode.Exploration)
            {
                Assert.LessOrEqual(shift, 12, "exploration cover is charcoal");
            }
            else
            {
                Assert.That(shift, Is.InRange(40, 250), $"{mode} cover tinted ({cover})");
                Assert.Less(SceneFlowCardRenderTests.Distance(cover, toward), SceneFlowCardRenderTests.Distance(ThemeTokens.Charcoal, toward), $"{mode} cover leans toward {toward}");
            }
            foreach (string label in new[] { "card-collection", "card-title", "card-mode" })
            {
                Color32 text = root.Q<Label>(label).resolvedStyle.color;
                Assert.GreaterOrEqual(ThemeContrast.Ratio(text, expected), 4.5f, $"{label} text {text} on the {mode} cover");
            }

            int eyebrow = SceneFlowCardRenderTests.InkLeft(pixels, root.Q("card-collection").worldBound, cover);
            int titleEdge = SceneFlowCardRenderTests.InkLeft(pixels, root.Q("card-title").worldBound, cover);
            int modeEdge = SceneFlowCardRenderTests.InkLeft(pixels, root.Q("card-mode").worldBound, cover);
            string edges = $"eyebrow {eyebrow}, title {titleEdge}, mode {modeEdge}";
            Assert.LessOrEqual(Mathf.Abs(titleEdge - eyebrow), SceneFlowCardRenderTests.alignmentTolerance, edges);
            Assert.LessOrEqual(Mathf.Abs(modeEdge - eyebrow), SceneFlowCardRenderTests.alignmentTolerance, edges);
        }
        finally
        {
            Object.Destroy(pixels);
        }
#else
        Assert.Ignore("Loads the overlay with AssetDatabase, which needs the editor.");
        yield break;
#endif
    }

    /// <summary>
    /// Left edge of the text in a panel rect, as the eye reads it: the median over pixel rows of
    /// each row's leftmost text pixel (far from the cover color). For italic text this is the
    /// slant's middle, not its lowest point. Rows whose leftmost ink lies past the first third of
    /// the rect (descenders of later letters) are skipped.
    /// </summary>
    private static int InkLeft(Texture2D pixels, Rect area, Color32 cover)
    {
        int xMin = Mathf.Max(0, Mathf.FloorToInt(area.xMin) - 40);
        float firstThird = area.xMin + area.width / 3;
        System.Collections.Generic.List<int> edges = new System.Collections.Generic.List<int>();
        for (int y = Mathf.CeilToInt(area.yMin); y < area.yMax; y++)
        {
            for (int x = xMin; x < firstThird; x++)
            {
                Color32 pixel = pixels.GetPixel(x, pixels.height - 1 - y);
                if (SceneFlowCardRenderTests.Distance(pixel, cover) > 150)
                {
                    edges.Add(x);
                    break;
                }
            }
        }
        Assert.IsNotEmpty(edges, $"no text drawn in {area}");
        edges.Sort();
        return edges[edges.Count / 2];
    }

    private static int Distance(Color32 a, Color32 b)
    {
        return Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b);
    }
}
