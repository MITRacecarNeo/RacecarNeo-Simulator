using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// The race HUD in a loaded level: timer, checkpoint pips, map, sensor trays, autograder panel,
/// and result card; the HUD stays clear of the car in every camera view. Writes composited
/// previews to Logs/ui-preview-hud-*.png.
/// </summary>
public class HudPlayTests
{
    /// <summary>
    /// HUD elements on screen for the whole race; toasts, pause, and the result card are transient.
    /// </summary>
    private static readonly string[] persistentElements = { "map", "gauge-box", "chip", "timer", "pips", "tray-left", "tray-right" };

    private Hud hud;
    private VisualElement root;

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
    }

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        this.hud = Object.FindAnyObjectByType<Hud>();
        this.root = this.hud.GetComponent<UIDocument>().rootVisualElement;
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        yield return PlayModeLevels.Unload();
    }

    [UnityTest]
    public IEnumerator Race_ShowsTimerPipsAndMap()
    {
        this.hud.UpdateTime(83.456f, new float[] { 83.456f, 20.1f, 30.2f, 0, 0 });
        // The car's physics module sets the gauge every frame, so read it back before yielding
        this.hud.UpdatePhysics(2.4f, new Vector3(0.1f, -9.8f, 0), new Vector3(0, 0.5f, 0));
        Assert.AreEqual(2.4f, this.root.Q<SpeedGauge>("gauge").Speed);
        yield return null;

        Assert.AreEqual("01:23.46", this.root.Q<ParallelogramElement>("timer").Text);
        VisualElement pips = this.root.Q("pips");
        Assert.AreEqual(3, pips.childCount);
        Assert.IsTrue(pips[0].ClassListContains("hud-pip--done"));
        Assert.IsFalse(pips[1].ClassListContains("hud-pip--done"));
        Assert.IsNotNull(this.root.Q("map-image").resolvedStyle.backgroundImage.renderTexture, "map texture bound");
        Assert.IsNotNull(this.root.Q("color-feed").resolvedStyle.backgroundImage.renderTexture, "color feed bound");

        yield return MenuPlayTests.Capture(this.hud.GetComponent<UIDocument>(), "ui-preview-hud.png");
    }

    [UnityTest]
    public IEnumerator SafeZone_HudClearOfCarInEveryView()
    {
        Racecar car = LevelManager.GetCar();
        UIDocument document = this.hud.GetComponent<UIDocument>();
        PanelSettings original = document.panelSettings;
        Camera camera = Camera.main;
        List<string> overlaps = new List<string>();
        foreach ((int width, string name) in new[] { (1920, "16:9"), (1728, "16:10") })
        {
            RenderTexture target = new RenderTexture(width, 1080, 0);
            PanelSettings sized = Object.Instantiate(original);
            sized.targetTexture = target;
            document.panelSettings = sized;
            camera.aspect = width / 1080f;
            try
            {
                for (int view = 0; view < PlayerCameraViews.Count; view++)
                {
                    car.SetCamera(view);
                    yield return null;
                    yield return null;
                    Rect carRect = HudPlayTests.ScreenRect(camera, car, this.root.worldBound.size);
                    foreach (string element in HudPlayTests.persistentElements)
                    {
                        Rect hudRect = this.root.Q(element).worldBound;
                        if (hudRect.Overlaps(carRect))
                        {
                            overlaps.Add($"{name} view {view}: {element} {hudRect} over car {carRect}");
                        }
                    }
                }
            }
            finally
            {
                document.panelSettings = original;
                camera.ResetAspect();
                Object.Destroy(sized);
                target.Release();
            }
        }
        car.SetCamera(0);
        Assert.IsEmpty(overlaps, string.Join("\n", overlaps));
    }

    [UnityTest]
    public IEnumerator SensorTrays_Toggle()
    {
        Assert.IsTrue(this.hud.AreSensorsShown);
        this.hud.SetSensorsShown(false);
        yield return null;
        Assert.AreEqual(DisplayStyle.None, this.root.Q("tray-left").resolvedStyle.display);
        Assert.AreEqual(DisplayStyle.None, this.root.Q("tray-right").resolvedStyle.display);
        this.hud.SetSensorsShown(true);
        yield return null;
        Assert.AreEqual(DisplayStyle.Flex, this.root.Q("tray-left").resolvedStyle.display);
    }

    [UnityTest]
    public IEnumerator Map_Toggle()
    {
        Assert.IsTrue(this.hud.IsMapShown);
        this.hud.SetMapShown(false);
        yield return null;
        Assert.AreEqual(DisplayStyle.None, this.root.Q("map").resolvedStyle.display);
        Assert.AreEqual(DisplayStyle.None, this.root.Q("gauge-box").resolvedStyle.display);
        this.hud.SetMapShown(true);
        yield return null;
        Assert.AreEqual(DisplayStyle.Flex, this.root.Q("map").resolvedStyle.display);
    }

    [UnityTest]
    public IEnumerator Layout_GaugeUnderBodyFrameAndLidarOverCamera()
    {
        yield return null;
        Rect tray = this.root.Q("tray-right").worldBound;
        Rect frame = this.root.Q("body-frame").worldBound;
        Rect gauge = this.root.Q("gauge").worldBound;
        Assert.IsTrue(tray.Contains(gauge.min) && tray.Contains(gauge.max), "gauge inside the right tray");
        Assert.GreaterOrEqual(gauge.yMin, frame.yMax, "gauge under the body frame");
        float accelHeading = this.root.Q("accel").parent[0].worldBound.yMin;
        float frameHeading = this.root.Q("body-frame").parent[0].worldBound.yMin;
        Assert.AreEqual(accelHeading, frameHeading, 2, "body frame heading level with the accel heading");

        Rect lidar = this.root.Q("lidar-feed").worldBound;
        Rect color = this.root.Q("color-feed").worldBound;
        Rect depth = this.root.Q("depth-feed").worldBound;
        Assert.LessOrEqual(lidar.yMax, color.yMin, "LIDAR over the camera");
        Assert.AreEqual(lidar.xMin, color.xMin, 1, "LIDAR and camera share a column");
        Assert.AreEqual(color.yMin, depth.yMin, 1, "depth beside the camera");
    }

    [UnityTest]
    public IEnumerator Autograder_ShowsPanelAndTimerWarning()
    {
        IAutograderHud grader = this.hud;
        grader.SetLevelInfo(1, "Wall follow", "Drive the loop without touching the walls.");
        grader.UpdateScore(3, 4);
        grader.SetMaxTime(30);
        grader.SetTimeBrackets(30, new[] { 10f, 20f });
        grader.UpdateTime(27, 30);
        yield return null;

        TimeRing ring = this.root.Q<TimeRing>("ag-ring");
        Assert.AreEqual(0.9f, ring.Elapsed, 1e-4f, "ring elapsed");
        CollectionAssert.AreEqual(new[] { 1 / 3f, 2 / 3f }, ring.Ticks, "bracket ticks");
        Assert.IsTrue(ring.ClassListContains("hud-ag-ring--warning"));
        Assert.AreEqual("30 s", this.root.Q<Label>("ag-ring-text").text);

        Assert.AreEqual(DisplayStyle.Flex, this.root.Q("autograder").resolvedStyle.display);
        Assert.AreEqual("TRIAL 2", this.root.Q<Label>("ag-trial").text);
        Assert.AreEqual("3 / 4", this.root.Q<Label>("ag-score").text);
        Assert.IsTrue(this.root.Q("timer").ClassListContains("hud-timer--warning"));

        yield return MenuPlayTests.Capture(this.hud.GetComponent<UIDocument>(), "ui-preview-hud-autograder.png");

        grader.UpdateTime(31, 30);
        yield return null;
        Assert.IsTrue(this.root.Q("timer").ClassListContains("hud-timer--over"));
        Assert.IsFalse(this.root.Q("timer").ClassListContains("hud-timer--warning"));
    }

    [UnityTest]
    public IEnumerator TrialResult_ToastByCredit()
    {
        IAutograderHud grader = this.hud;
        VisualElement toast = this.root.Q("toast");
        grader.ShowTrialResult(1, 1, 1);
        yield return null;
        Assert.AreEqual("TRIAL 2   1 / 1", this.root.Q<Label>("toast-text").text);
        Assert.IsTrue(toast.ClassListContains("hud-toast--success"));
        grader.ShowTrialResult(1, 0.5f, 1);
        Assert.IsTrue(toast.ClassListContains("hud-toast--warning"));
        Assert.IsFalse(toast.ClassListContains("hud-toast--success"));
        grader.ShowTrialResult(1, 0, 1);
        Assert.IsTrue(toast.ClassListContains("hud-toast--error"));
    }

    [UnityTest]
    public IEnumerator Messages_UseToastVariants()
    {
        this.hud.ShowWarning("Battery low");
        yield return null;
        VisualElement toast = this.root.Q("toast");
        Assert.AreEqual(DisplayStyle.Flex, toast.resolvedStyle.display);
        Assert.IsTrue(toast.ClassListContains("hud-toast--warning"));

        this.hud.ShowError("Lost connection");
        yield return null;
        Assert.IsTrue(toast.ClassListContains("hud-toast--error"));
        Assert.IsFalse(toast.ClassListContains("hud-toast--warning"));
    }

    [UnityTest]
    public IEnumerator Win_ShowsResultCard()
    {
        this.hud.HandleWin(61.5f, true);
        yield return null;

        Assert.AreEqual(DisplayStyle.Flex, this.root.Q("result").resolvedStyle.display);
        Assert.IsFalse(this.root.Q("result-card").ClassListContains("hud-result--failed"));
        Assert.AreEqual("01:01.50", this.root.Q<Label>("result-detail").text);
        Assert.AreEqual(DisplayStyle.Flex, this.root.Q("result-best").resolvedStyle.display);
        Assert.AreEqual(2, this.root.Q("result-hints").childCount);

        yield return MenuPlayTests.Capture(this.hud.GetComponent<UIDocument>(), "ui-preview-hud-result.png");
    }

    [UnityTest]
    public IEnumerator Failure_ShowsRedResultCard()
    {
        this.hud.HandleFailure(0, "The LIDAR is expensive and fragile, please do not hit it!");
        yield return null;

        Assert.IsTrue(this.root.Q("result-card").ClassListContains("hud-result--failed"));
        Assert.AreEqual((Color)ThemeTokens.LedRed, this.root.Q<Label>("result-title").resolvedStyle.color);
        Assert.AreEqual((Color)ThemeTokens.LedRed, this.root.Q("result-icon").resolvedStyle.color);

        yield return MenuPlayTests.Capture(this.hud.GetComponent<UIDocument>(), "ui-preview-hud-failure.png");
    }

    /// <summary>
    /// The car's mesh bounds projected through the camera, in panel coordinates (y down).
    /// </summary>
    private static Rect ScreenRect(Camera camera, Racecar car, Vector2 panelSize)
    {
        Bounds bounds = new Bounds(car.Center, Vector3.zero);
        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>())
        {
            if (renderer.enabled && (renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 viewport = camera.WorldToViewportPoint(corner);
            Vector2 point = new Vector2(viewport.x * panelSize.x, (1 - viewport.y) * panelSize.y);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
}
