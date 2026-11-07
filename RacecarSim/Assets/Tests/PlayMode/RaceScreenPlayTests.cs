using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// The race screen with 2 to 4 cars: one tile per car with its badge and readouts, the race camera
/// in a free tile, and no badge or readout over a car in any camera view. Writes previews to
/// Logs/ui-preview-race-*.png.
/// </summary>
public class RaceScreenPlayTests
{
    /// <summary>
    /// Tile elements on screen for the whole race.
    /// </summary>
    private static readonly string[] tileElements = { "badge", "readouts" };

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        yield return PlayModeLevels.Unload();
    }

    [UnityTest]
    public IEnumerator Tiles_OnePerCarAndTheRaceCameraInAFreeTile([Values(2, 3, 4)] int cars)
    {
        (RaceScreen screen, VisualElement root) = default((RaceScreen, VisualElement));
        yield return RaceScreenPlayTests.LoadRace(cars, loaded => (screen, root) = loaded);

        Assert.AreEqual(cars, screen.CarTiles.Count, "car tiles");
        Assert.AreEqual(cars < RaceScreen.MaxTiles, screen.OverviewTile != null, "race camera tile");
        for (int i = 0; i < cars; i++)
        {
            VisualElement tile = screen.CarTiles[i];
            Assert.AreEqual($"Car {i + 1}", tile.Q<Label>("car-label").text);
            Assert.AreEqual(SavedDataManager.Data.CarCustomizations[i].ShellColor.Color, tile.Q("swatch").resolvedStyle.backgroundColor, $"car {i + 1} swatch");
            Assert.IsTrue(tile.ClassListContains("race-tile--waiting"), $"car {i + 1} waits for a program");
            Assert.AreEqual(DisplayStyle.Flex, tile.Q("waiting").resolvedStyle.display, $"car {i + 1} waiting text");
            Assert.IsNotNull(tile.Q("view").resolvedStyle.backgroundImage.renderTexture, $"car {i + 1} view bound");
        }

        screen.UpdateConnectedPrograms(new[] { true, false });
        yield return null;
        Assert.IsFalse(screen.CarTiles[0].ClassListContains("race-tile--waiting"), "car 1 connected");
        Assert.IsTrue(screen.CarTiles[0].Q("connection").ClassListContains("hud-connection--on"), "car 1 connection dot");
        Assert.AreEqual(DisplayStyle.None, screen.CarTiles[0].Q("waiting").resolvedStyle.display, "car 1 waiting text hidden");
        Assert.IsTrue(screen.CarTiles[1].ClassListContains("race-tile--waiting"), "car 2 still waiting");

        // Tiles fill a 2 x 2 grid below the header, inside the screen and without overlapping, at
        // window sizes that do not divide evenly (a 1894 x 1016 window wrapped every tile into
        // one column before the grid used rows)
        List<VisualElement> tiles = screen.CarTiles.ToList();
        if (screen.OverviewTile != null)
        {
            tiles.Add(screen.OverviewTile);
        }
        UIDocument document = screen.GetComponent<UIDocument>();
        PanelSettings original = document.panelSettings;
        foreach ((int width, int height) in new[] { (1920, 1080), (1894, 1016), (1366, 768) })
        {
            RenderTexture target = new RenderTexture(width, height, 0);
            PanelSettings sized = Object.Instantiate(original);
            sized.targetTexture = target;
            document.panelSettings = sized;
            try
            {
                yield return null;
                yield return null;
                Rect screenRect = root.worldBound;
                Rect header = root.Q("header").worldBound;
                for (int i = 0; i < tiles.Count; i++)
                {
                    Rect a = tiles[i].worldBound;
                    string size = $"{width} x {height} tile {i}";
                    Assert.GreaterOrEqual(a.yMin, header.yMax - 0.5f, $"{size} below the header");
                    Assert.LessOrEqual(a.yMax, screenRect.yMax + 0.5f, $"{size} inside the screen");
                    Assert.LessOrEqual(a.xMax, screenRect.xMax + 0.5f, $"{size} inside the screen");
                    Assert.Greater(a.width, screenRect.width * 0.45f, $"{size} width");
                    Assert.Greater(a.height, (screenRect.height - header.height) * 0.4f, $"{size} height");
                    for (int j = i + 1; j < tiles.Count; j++)
                    {
                        Assert.IsFalse(a.Overlaps(tiles[j].worldBound), $"{width} x {height} tiles {i} and {j} overlap");
                    }
                }
            }
            finally
            {
                document.panelSettings = original;
                Object.Destroy(sized);
                target.Release();
            }
        }

        // One selected view at a time takes the camera keys and scroll wheel
        for (int i = 0; i < cars; i++)
        {
            Assert.IsFalse(LevelManager.GetCar(i).TakesViewInput, $"car {i + 1} takes no view input before a click");
        }
        screen.Select(1);
        yield return null;
        for (int i = 0; i < cars; i++)
        {
            Assert.AreEqual(i == 1, LevelManager.GetCar(i).TakesViewInput, $"car {i + 1} view input with car 2 selected");
            Assert.AreEqual(i == 1, screen.CarTiles[i].ClassListContains("race-tile--selected"), $"car {i + 1} outline");
        }
        screen.Select(-1);
        Assert.IsFalse(LevelManager.GetCar(1).TakesViewInput, "car 2 released");

        screen.UpdateTime(83.456f, new float[0]);
        yield return RaceScreenPlayTests.Capture(screen.GetComponent<UIDocument>(), $"ui-preview-race-{cars}.png");
        Assert.AreEqual("01:23.46", root.Q<ParallelogramElement>("timer").Text);
    }

    [UnityTest]
    public IEnumerator SafeZone_TileOverlaysClearOfTheCarInEveryView()
    {
        (RaceScreen screen, VisualElement root) = default((RaceScreen, VisualElement));
        yield return RaceScreenPlayTests.LoadRace(RaceScreen.MaxTiles, loaded => (screen, root) = loaded);
        UIDocument document = screen.GetComponent<UIDocument>();
        PanelSettings original = document.panelSettings;
        List<string> overlaps = new List<string>();
        foreach ((int width, string name) in new[] { (1920, "16:9"), (1728, "16:10") })
        {
            RenderTexture target = new RenderTexture(width, 1080, 0);
            PanelSettings sized = Object.Instantiate(original);
            sized.targetTexture = target;
            document.panelSettings = sized;
            try
            {
                for (int view = 0; view < PlayerCameraViews.Count; view++)
                {
                    for (int i = 0; i < screen.CarTiles.Count; i++)
                    {
                        LevelManager.GetCar(i).SetCamera(view);
                    }
                    yield return null;
                    yield return null;
                    for (int i = 0; i < screen.CarTiles.Count; i++)
                    {
                        Racecar car = LevelManager.GetCar(i);
                        VisualElement tile = screen.CarTiles[i];
                        Texture texture = tile.Q("view").resolvedStyle.backgroundImage.renderTexture;
                        Camera camera = car.transform.root.GetComponentsInChildren<Camera>().First(each => each.enabled && each.targetTexture == texture);
                        Rect carRect = RaceScreenPlayTests.CarRectInView(camera, car, tile.Q("view").worldBound, texture);
                        foreach (string element in RaceScreenPlayTests.tileElements)
                        {
                            Rect overlay = tile.Q(element).worldBound;
                            if (overlay.Overlaps(carRect))
                            {
                                overlaps.Add($"{name} view {view} car {i + 1}: {element} {overlay} over car {carRect}");
                            }
                        }
                    }
                }
            }
            finally
            {
                document.panelSettings = original;
                Object.Destroy(sized);
                target.Release();
            }
        }
        Assert.IsEmpty(overlaps, string.Join("\n", overlaps));
    }

    private static IEnumerator LoadRace(int cars, System.Action<(RaceScreen, VisualElement)> loaded)
    {
        LevelInfo level = PlayModeLevels.Find("Grand Prix 2026");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Race, LevelLaunch.BuildIndex(level, LevelManagerMode.Race), cars);
        RaceScreen screen = Object.FindAnyObjectByType<RaceScreen>();
        Assert.IsNotNull(screen, "race screen");
        Assert.IsNull(Object.FindAnyObjectByType<Hud>(), "no single-car HUD in a race");
        yield return null;
        loaded((screen, screen.GetComponent<UIDocument>().rootVisualElement));
    }

    /// <summary>
    /// The car's screen rectangle in a tile's view, which shows the camera's texture scaled to
    /// cover the view and centered (scale-and-crop).
    /// </summary>
    private static Rect CarRectInView(Camera camera, Racecar car, Rect view, Texture texture)
    {
        Bounds bounds = new Bounds(car.Center, Vector3.zero);
        foreach (Renderer renderer in car.GetComponentsInChildren<Renderer>())
        {
            if (renderer.enabled && renderer is MeshRenderer)
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        float scale = Mathf.Max(view.width / texture.width, view.height / texture.height);
        Vector2 shown = new Vector2(texture.width * scale, texture.height * scale);
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 viewport = camera.WorldToViewportPoint(corner);
            Vector2 point = view.center + new Vector2((viewport.x - 0.5f) * shown.x, (0.5f - viewport.y) * shown.y);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        Rect rect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return Rect.MinMaxRect(Mathf.Max(rect.xMin, view.xMin), Mathf.Max(rect.yMin, view.yMin), Mathf.Min(rect.xMax, view.xMax), Mathf.Min(rect.yMax, view.yMax));
    }

    /// <summary>
    /// Renders the panel alone (the race screen covers the whole display) to Logs/.
    /// </summary>
    private static IEnumerator Capture(UIDocument document, string file)
    {
        RenderTexture ui = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
        PanelSettings original = document.panelSettings;
        PanelSettings capture = Object.Instantiate(original);
        capture.targetTexture = ui;
        capture.clearColor = true;
        capture.colorClearValue = Color.black;
        try
        {
            document.panelSettings = capture;
            yield return null;
            yield return null;
            yield return null;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = ui;
            Texture2D pixels = new Texture2D(ui.width, ui.height, TextureFormat.RGBA32, false);
            pixels.ReadPixels(new Rect(0, 0, ui.width, ui.height), 0, 0);
            pixels.Apply();
            RenderTexture.active = previous;
            string logs = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs");
            Directory.CreateDirectory(logs);
            File.WriteAllBytes(Path.Combine(logs, file), pixels.EncodeToPNG());
            Object.Destroy(pixels);
        }
        finally
        {
            document.panelSettings = original;
            Object.Destroy(capture);
            ui.Release();
        }
        yield return null;
    }
}
