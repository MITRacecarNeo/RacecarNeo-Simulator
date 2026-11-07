using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

/// <summary>
/// The UI Toolkit main menu in Main.unity: Home opens Map select, which shows the collections in
/// order as tabs and the selected collection's levels as cards; a level's unsupported modes are
/// disabled; Start loads the level in the chosen mode; Settings, Best times, and Controls open and
/// fill. Writes Logs/ui-preview-*.png for each screen (3D stage and menu composited).
/// </summary>
public class MenuPlayTests
{
    private const int width = 1920;
    private const int height = 1080;

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
    public IEnumerator Menu_SelectsLevelAndModeThenStarts()
    {
        SceneManager.LoadScene(LevelCollection.MainMenuBuildIndex, LoadSceneMode.Single);
        yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == LevelCollection.MainMenuBuildIndex, "the main menu");
        yield return null;
        yield return null;

        UIDocument document = Object.FindAnyObjectByType<MenuController>().GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;
        Assert.IsTrue(root.Q("home").ClassListContains("mn-screen--open"), "Home open first");
        yield return MenuPlayTests.Capture(document, "ui-preview-home.png");

        MenuPlayTests.Submit(root.Q<Button>("play"));
        yield return null;
        Assert.IsTrue(root.Q("map-select").ClassListContains("mn-screen--open"), "Map select open");

        string[] tabs = root.Q("tabs").Children().OfType<Button>().Select(tab => tab.text).ToArray();
        CollectionAssert.AreEqual(new[] { "Sandbox", "Labs", "Grand Prix", "Community", "Legacy", WorldLevels.CollectionDisplayName }, tabs, "Mapped Worlds always last");

        LevelCollection grandPrix = LevelCollection.LevelCollections[2];
        MenuPlayTests.Submit(root.Q("tabs").Children().OfType<Button>().ElementAt(2));
        yield return null;
        Assert.AreEqual(grandPrix.Levels.Length, root.Q<ScrollView>("cards").contentContainer.childCount, "one card per Grand Prix level");
        Assert.AreEqual(grandPrix.Levels[0].DisplayName, root.Q<Label>("detail-title").text, "first level selected");
        yield return new WaitForSecondsRealtime(0.3f);
        yield return MenuPlayTests.Capture(document, "ui-preview-map-select.png");

        // Demo World supports only Exploration: the other mode tiles are disabled
        LevelInfo demo = LevelCollection.LevelCollections[0].Levels[0];
        MenuPlayTests.Submit(root.Q("tabs").Children().OfType<Button>().ElementAt(0));
        yield return null;
        foreach (Button tile in root.Q("modes").Children().OfType<Button>())
        {
            Assert.AreEqual(demo.SupportedModes.Contains((LevelManagerMode)tile.userData), tile.enabledSelf, $"{tile.userData} tile follows SupportedModes");
        }

        // The first Grand Prix level in Race
        LevelInfo level = grandPrix.Levels[0];
        Assert.Contains(LevelManagerMode.Race, level.SupportedModes);
        MenuPlayTests.Submit(root.Q("tabs").Children().OfType<Button>().ElementAt(2));
        yield return null;
        Button race = root.Q("modes").Children().OfType<Button>().First(tile => (LevelManagerMode)tile.userData == LevelManagerMode.Race);
        MenuPlayTests.Submit(race);
        yield return null;
        Assert.IsTrue(race.ClassListContains("mn-mode--selected"), "race selected");

        MenuPlayTests.Submit(root.Q<Button>("start"));
        int scene = LevelLaunch.BuildIndex(level, LevelManagerMode.Race);
        yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex != LevelCollection.MainMenuBuildIndex, "the level to load");
        Assert.AreEqual(LevelManagerMode.Race, LevelManager.LevelManagerMode);
        Assert.AreEqual(level, LevelManager.LevelInfo);
        Assert.IsTrue(level.HasRandomMaps ? level.RandomSceneBuildIndices.Contains(SceneManager.GetActiveScene().buildIndex) : SceneManager.GetActiveScene().buildIndex == scene, "the level's scene");
    }

    /// <summary>
    /// Mapped Worlds in map select: the tab with only the import card in an empty library; a map
    /// imported through the dialog becomes a selected world with its settings; a setting saves to
    /// world.json; Delete world removes it.
    /// </summary>
    [UnityTest]
    public IEnumerator Menu_MappedWorldsImportSettingsDelete()
    {
        WorldLibrary library = WorldLibrary.Default;
        // This test empties the library: only ever the scratch data folder's
        StringAssert.StartsWith(System.IO.Path.GetFullPath(LaunchOptions.Current.DataDirectory), System.IO.Path.GetFullPath(library.Root));
        foreach (CapturedWorld existing in library.List())
        {
            library.Delete(existing);
        }
        string maps = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "racecarsim-import-" + System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(maps);
        try
        {
            OccupancyGridMsg grid = new OccupancyGridMsg { Width = 20, Height = 10, Resolution = 0.05f, Data = Enumerable.Repeat((sbyte)0, 200).ToArray() };
            MapServerFile.Save(grid, System.IO.Path.Combine(maps, "shop.yaml"), "shop.pgm");

            SceneManager.LoadScene(LevelCollection.MainMenuBuildIndex, LoadSceneMode.Single);
            yield return PlayModeLevels.WaitFor(() => Object.FindAnyObjectByType<MenuController>() != null, "the main menu");
            yield return null;
            yield return null;
            UIDocument document = Object.FindAnyObjectByType<MenuController>().GetComponent<UIDocument>();
            VisualElement root = document.rootVisualElement;
            MenuPlayTests.Submit(root.Q<Button>("play"));
            yield return null;

            Button tab = root.Q("tabs").Query<Button>().Where(each => each.text == WorldLevels.CollectionDisplayName).First();
            Assert.IsNotNull(tab, "Mapped Worlds tab with no worlds");
            MenuPlayTests.Submit(tab);
            yield return null;
            Assert.AreEqual(1, root.Q("cards").Query<Button>(className: "mn-card").ToList().Count, "only the import card");
            Assert.IsFalse(root.Q<Button>("start").enabledSelf, "nothing to start");

            MenuPlayTests.Submit(root.Q("cards").Query<Button>(className: "mn-card").First());
            yield return null;
            root.Q<TextField>("dialog-input").value = "\"" + System.IO.Path.Combine(maps, "shop.yaml") + "\"";
            MenuPlayTests.Submit(root.Q<Button>("dialog-ok"));
            yield return null;
            CapturedWorld imported = library.List().Single();
            Assert.AreEqual("shop", imported.Name);
            Assert.AreEqual("shop", root.Q<Label>("detail-title").text, "imported world selected");
            Assert.AreEqual(DisplayStyle.Flex, root.Q("world-options").resolvedStyle.display);
            yield return MenuPlayTests.Capture(document, "ui-preview-mapped-worlds.png");

            MenuPlayTests.Submit(root.Q("world-walls").Query<Button>().AtIndex(0));
            MenuPlayTests.Submit(root.Q("world-close").Q<Button>());
            yield return null;
            CapturedWorld changed = WorldLibrary.Read(imported.Folder);
            Assert.AreEqual(0.3, changed.WallHeight, 1e-9);
            Assert.AreEqual(CapturedWorld.UnknownClosed, changed.Unknown);
            Assert.IsTrue(root.Q("world-walls").Query<Button>().AtIndex(0).ClassListContains("mn-segment--selected"));

            MenuPlayTests.Submit(root.Q<Button>("world-delete"));
            yield return null;
            MenuPlayTests.Submit(root.Q<Button>("dialog-ok"));
            yield return null;
            CollectionAssert.IsEmpty(library.List(), "deleted");
            Assert.AreEqual(1, root.Q("cards").Query<Button>(className: "mn-card").ToList().Count, "back to the import card");
        }
        finally
        {
            System.IO.Directory.Delete(maps, true);
            foreach (CapturedWorld left in library.List())
            {
                library.Delete(left);
            }
        }
    }

    [UnityTest]
    public IEnumerator Menu_SettingsBestTimesControls()
    {
        SceneManager.LoadScene(LevelCollection.MainMenuBuildIndex, LoadSceneMode.Single);
        yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == LevelCollection.MainMenuBuildIndex, "the main menu");
        yield return null;
        yield return null;
        UIDocument document = Object.FindAnyObjectByType<MenuController>().GetComponent<UIDocument>();
        VisualElement root = document.rootVisualElement;

        // Settings: Cancel discards a change; the shell sliders recolor the swatch
        bool realism = Settings.IsRealism;
        MenuPlayTests.Submit(root.Q<Button>("settings"));
        yield return null;
        Assert.IsTrue(root.Q("settings-screen").ClassListContains("mn-screen--open"), "Settings open");
        Button realismSwitch = root.Q("realism").Q<Button>();
        Assert.AreEqual(realism, realismSwitch.ClassListContains("mn-switch--on"), "switch shows the setting");
        MenuPlayTests.Submit(realismSwitch);
        root.Q<Slider>("shell-r").value = 1;
        yield return null;
        Assert.AreEqual(!realism, realismSwitch.ClassListContains("mn-switch--on"), "switch flips");
        Assert.AreEqual(1, root.Q("cars-swatches").Children().First().resolvedStyle.backgroundColor.r, 0.01f, "swatch follows the slider");
        CollectionAssert.AreEqual(new[] { "1", "2", "3", "4" }, root.Q("cars-swatches").Query<Label>().ToList().Select(label => label.text), "cars numbered");

        // Presets and Code set the selected car's shell; the car swatches stay car choices
        Button[] pick = root.Q("shell-pick").Query<Button>().ToList().ToArray();
        MenuPlayTests.Submit(pick[1]);
        yield return null;
        Assert.AreEqual(DisplayStyle.None, root.Q("shell-sliders").resolvedStyle.display, "sliders hidden");
        MenuPlayTests.Submit(root.Q("shell-presets").Query<Button>().AtIndex(4));
        yield return null;
        Assert.AreEqual(MenuController.ShellCode(MenuController.ShellPresets[4]), MenuController.ShellCode(root.Q("cars-swatches").Children().First().resolvedStyle.backgroundColor), "preset applied to car 1");
        MenuPlayTests.Submit(root.Q("cars-swatches").Query<Button>().AtIndex(1));
        yield return null;
        MenuPlayTests.Submit(pick[2]);
        root.Q<TextField>("shell-code").value = "#FF8000";
        yield return null;
        Assert.AreEqual("#FF8000", MenuController.ShellCode(root.Q("cars-swatches").Query<Button>().AtIndex(1).resolvedStyle.backgroundColor), "code applied to car 2");
        Assert.AreEqual(MenuController.ShellCode(MenuController.ShellPresets[4]), MenuController.ShellCode(root.Q("cars-swatches").Children().First().resolvedStyle.backgroundColor), "car 1 kept");
        MenuPlayTests.Submit(pick[0]);
        yield return new WaitForSecondsRealtime(0.2f);
        yield return MenuPlayTests.Capture(document, "ui-preview-settings.png");
        MenuPlayTests.Submit(root.Q<Button>("settings-cancel"));
        yield return null;
        Assert.AreEqual(realism, Settings.IsRealism, "Cancel keeps the saved setting");
        Assert.IsTrue(root.Q("home").ClassListContains("mn-screen--open"), "back Home");

        // Best times: one card per raceable level
        MenuPlayTests.Submit(root.Q<Button>("best-times"));
        yield return null;
        Assert.AreEqual(LevelInfo.WinableLevels.Count, root.Q<ScrollView>("best-times-cards").contentContainer.childCount);
        yield return new WaitForSecondsRealtime(0.2f);
        yield return MenuPlayTests.Capture(document, "ui-preview-best-times.png");
        MenuPlayTests.Submit(root.Q<Button>("best-times-back"));
        yield return null;

        // Controls: one section per InputBindings section
        MenuPlayTests.Submit(root.Q<Button>("controls"));
        yield return null;
        yield return null;
        Assert.AreEqual(InputBindings.Sections.Length, root.Query(className: "mn-controls-section").ToList().Count);
        yield return MenuPlayTests.Capture(document, "ui-preview-controls.png");
        foreach ((int width, int height, string name) in new[] { (2520, 1080, "21x9"), (1440, 1080, "4x3") })
        {
            yield return MenuPlayTests.Capture(document, $"ui-preview-controls-{name}.png", width, height, () =>
            {
                Rect content = root.Q("controls-columns").worldBound;
                Assert.LessOrEqual(content.xMax, root.worldBound.xMax, $"Controls fit the width at {name}");
                Assert.AreEqual(InputBindings.Sections.Length, root.Query(className: "mn-controls-section").ToList().Count, name);
            });
        }
    }

    [UnityTest]
    public IEnumerator Menu_AutograderAsksForUsername()
    {
        string username = Settings.Username;
        Settings.Username = Settings.DefaultUsername;
        try
        {
            SceneManager.LoadScene(LevelCollection.MainMenuBuildIndex, LoadSceneMode.Single);
            yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == LevelCollection.MainMenuBuildIndex, "the main menu");
            yield return null;
            yield return null;
            VisualElement root = Object.FindAnyObjectByType<MenuController>().GetComponent<UIDocument>().rootVisualElement;

            // Labs: pick the first level with an autograder, choose Autograder, Start
            MenuPlayTests.Submit(root.Q<Button>("play"));
            yield return null;
            LevelCollection labs = LevelCollection.LevelCollections[1];
            LevelInfo lab = labs.Levels.First(level => level.SupportedModes.Contains(LevelManagerMode.Autograder));
            MenuPlayTests.Submit(root.Q("tabs").Children().OfType<Button>().ElementAt(1));
            yield return null;
            root.Q<ScrollView>("cards").contentContainer.Children().OfType<Button>().ElementAt(System.Array.IndexOf(labs.Levels, lab)).Focus();
            yield return null;
            MenuPlayTests.Submit(root.Q("modes").Children().OfType<Button>().First(tile => (LevelManagerMode)tile.userData == LevelManagerMode.Autograder));
            MenuPlayTests.Submit(root.Q<Button>("start"));
            yield return null;
            Assert.IsTrue(root.Q("dialog").ClassListContains("mn-dialog-backdrop--open"), "username reminder shown");
            Assert.AreEqual(LevelCollection.MainMenuBuildIndex, SceneManager.GetActiveScene().buildIndex, "the level waits for the reminder");

            MenuPlayTests.Submit(root.Q<Button>("dialog-other"));
            yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == lab.AutograderBuildIndex, "the first autograder trial");
        }
        finally
        {
            Settings.Username = username;
        }
    }

    [UnityTest]
    public IEnumerator Menu_SplashPlaysOncePerRun()
    {
        Presentation.Override = true;
        MenuSplash.HasPlayed = false;
        try
        {
            SceneManager.LoadScene(LevelCollection.MainMenuBuildIndex, LoadSceneMode.Single);
            yield return PlayModeLevels.WaitFor(() => SceneManager.GetActiveScene().buildIndex == LevelCollection.MainMenuBuildIndex, "the main menu");
            yield return null;
            UIDocument document = Object.FindAnyObjectByType<MenuController>().GetComponent<UIDocument>();
            VisualElement splash = document.rootVisualElement.Q("splash");
            Assert.AreEqual(DisplayStyle.Flex, splash.resolvedStyle.display, "splash on the first menu load");

            yield return new WaitForSecondsRealtime(MenuSplash.BarSeconds + MenuSplash.LogoSeconds);
            yield return MenuPlayTests.Capture(document, "ui-preview-splash.png");

            float limit = MenuSplash.BarSeconds + MenuSplash.LogoSeconds + MenuSplash.HoldSeconds + MenuSplash.RevealSeconds + 1;
            float start = Time.realtimeSinceStartup;
            while (splash.resolvedStyle.display != DisplayStyle.None)
            {
                Assert.Less(Time.realtimeSinceStartup - start, limit, "splash ends");
                yield return null;
            }
            Assert.IsTrue(document.rootVisualElement.Q("home").ClassListContains("mn-screen--open"), "Home after the splash");

            // A second menu load in the same run has no splash
            SceneManager.LoadScene(LevelCollection.MainMenuBuildIndex, LoadSceneMode.Single);
            yield return null;
            yield return null;
            splash = Object.FindAnyObjectByType<MenuController>().GetComponent<UIDocument>().rootVisualElement.Q("splash");
            Assert.AreEqual(DisplayStyle.None, splash.resolvedStyle.display, "no splash on later menu loads");
        }
        finally
        {
            Presentation.Override = null;
            MenuSplash.HasPlayed = true;
        }
    }

    private static void Submit(Button button)
    {
        using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled())
        {
            submit.target = button;
            button.SendEvent(submit);
        }
    }

    /// <summary>
    /// Renders the main camera and the menu panel to separate textures and writes them
    /// composited (panel over camera) to Logs/.
    /// </summary>
    internal static IEnumerator Capture(UIDocument document, string file, int width = MenuPlayTests.width, int height = MenuPlayTests.height, System.Action check = null)
    {
        RenderTexture stage = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        RenderTexture ui = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
        PanelSettings original = document.panelSettings;
        PanelSettings capture = Object.Instantiate(original);
        capture.targetTexture = ui;
        capture.clearColor = true;
        capture.colorClearValue = Color.clear;
        Camera camera = Camera.main;
        try
        {
            document.panelSettings = capture;
            yield return null;
            yield return null;
            yield return null;
            check?.Invoke();
            camera.targetTexture = stage;
            camera.Render();
            camera.targetTexture = null;

            Texture2D back = MenuPlayTests.Read(stage);
            Texture2D front = MenuPlayTests.Read(ui);
            Color32[] b = back.GetPixels32();
            Color32[] f = front.GetPixels32();
            for (int i = 0; i < b.Length; i++)
            {
                b[i] = Color32.Lerp(b[i], new Color32(f[i].r, f[i].g, f[i].b, 255), f[i].a / 255f);
            }
            back.SetPixels32(b);
            string logs = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Logs");
            Directory.CreateDirectory(logs);
            File.WriteAllBytes(Path.Combine(logs, file), back.EncodeToPNG());
            Object.Destroy(back);
            Object.Destroy(front);
        }
        finally
        {
            document.panelSettings = original;
            Object.Destroy(capture);
            stage.Release();
            ui.Release();
        }
        yield return null;
    }

    private static Texture2D Read(RenderTexture texture)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = texture;
        Texture2D pixels = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;
        return pixels;
    }
}
