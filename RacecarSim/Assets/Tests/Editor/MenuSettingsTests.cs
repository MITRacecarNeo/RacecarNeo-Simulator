using System.Linq;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Settings screen draft, the Controls screen bindings, and the menu layout's element names. Drafts are not applied here, since
/// applying writes PlayerPrefs and the saved data file.
/// </summary>
public class MenuSettingsTests
{
    [Test]
    public void RestoreDefaults_SetsEveryDefault()
    {
        SettingsDraft draft = SettingsDraft.From(SavedData.Default.CarCustomizations);
        draft.IsRealism = !Settings.DefaultIsRealism;
        draft.IsRealCarResponse = !Settings.DefaultIsRealCarResponse;
        draft.IsAntennaColliderRemoved = !Settings.DefaultIsAntennaColliderRemoved;
        draft.CarDetail = Settings.CarDetailLevel.High;
        draft.Username = "someone";
        draft.ShellColors[0] = Color.magenta;
        draft.RestoreDefaults();

        Assert.AreEqual(Settings.DefaultIsRealism, draft.IsRealism);
        Assert.AreEqual(Settings.DefaultIsRealCarResponse, draft.IsRealCarResponse);
        Assert.AreEqual(Settings.DefaultIsAntennaColliderRemoved, draft.IsAntennaColliderRemoved);
        Assert.IsTrue(Settings.DefaultIsAntennaColliderRemoved, "antenna colliders off by default");
        Assert.AreEqual(Settings.DefaultIsBatteryMode, draft.IsBatteryMode);
        Assert.AreEqual(Settings.DefaultHideCarsInColorCamera, draft.HideCarsInColorCamera);
        Assert.AreEqual(Settings.DefaultShowDotMatrix, draft.ShowDotMatrix);
        Assert.AreEqual(Settings.DefaultDepthRes, draft.DepthRes);
        Assert.AreEqual(Settings.DefaultCarDetail, draft.CarDetail);
        Assert.AreEqual(Settings.DefaultUsername, draft.Username);
        CarCustomization[] defaults = SavedData.Default.CarCustomizations;
        CollectionAssert.AreEqual(defaults.Select(each => each.ShellColor.Color), draft.ShellColors);
        CollectionAssert.AreEqual(defaults.Select(each => each.IsShellShiny), draft.IsShellShiny);
    }

    [Test]
    public void From_CopiesCustomizations()
    {
        CarCustomization[] saved = SavedData.Default.CarCustomizations;
        SettingsDraft draft = SettingsDraft.From(saved);
        Assert.AreEqual(saved.Length, draft.ShellColors.Length);
        draft.ShellColors[0] = Color.magenta;
        Assert.AreNotEqual(Color.magenta, saved[0].ShellColor.Color, "the draft is a copy");
    }

    [TestCase("  team42 ", "team42")]
    [TestCase("", Settings.DefaultUsername)]
    [TestCase("   ", Settings.DefaultUsername)]
    [TestCase(null, Settings.DefaultUsername)]
    public void CleanUsername_TrimsOrDefaults(string typed, string saved)
    {
        Assert.AreEqual(saved, SettingsDraft.CleanUsername(typed));
    }

    [Test]
    public void Hotkeys_DoNotDriveTheControllerEmulation()
    {
        KeyCode[] emulated = System.Enum.GetValues(typeof(Controller.Button)).Cast<Controller.Button>().Select(Controller.KeyFor)
            .Concat(System.Enum.GetValues(typeof(Controller.Trigger)).Cast<Controller.Trigger>().Select(Controller.KeyFor))
            .Concat(System.Enum.GetValues(typeof(Controller.Joystick)).Cast<Controller.Joystick>().SelectMany(Controller.KeysFor))
            .ToArray();
        KeyCode[] hotkeys =
        {
            Hotkeys.Exit, Hotkeys.SlowDown, Hotkeys.SpeedUp, Hotkeys.SpeedUpAlternate, Hotkeys.SkipCheckpoint,
            Hotkeys.ResetToCheckpoint, Hotkeys.ResetCar, Hotkeys.NextView, Hotkeys.ResetView, Hotkeys.ToggleSensors, Hotkeys.ToggleMap,
        };
        CollectionAssert.IsEmpty(hotkeys.Intersect(emulated), "a hotkey would also press a controller input");
    }

    [Test]
    public void Bindings_EveryRowHasKeysOrController()
    {
        foreach (InputBindings.Section section in InputBindings.Sections)
        {
            Assert.IsNotEmpty(section.Bindings, section.Title);
            foreach (InputBindings.Binding binding in section.Bindings)
            {
                Assert.IsTrue(binding.Keys.Length > 0 || binding.Controller != null, binding.Action);
                Assert.IsTrue(binding.Keys.SelectMany(chord => chord).All(key => !string.IsNullOrEmpty(InputBindings.KeyLabel(key))), binding.Action);
            }
        }
    }

    [Test]
    public void MenuLayout_ElementNamesAreUnique()
    {
        // MenuController finds elements by name; a repeated name returns the first match
        string layout = System.IO.File.ReadAllText(System.IO.Path.Combine(Application.dataPath, "UI", "Menu", "MainMenu.uxml"));
        string[] names = System.Text.RegularExpressions.Regex.Matches(layout, "name=\"([^\"]+)\"").Cast<System.Text.RegularExpressions.Match>().Select(match => match.Groups[1].Value).ToArray();
        CollectionAssert.IsNotEmpty(names);
        CollectionAssert.AllItemsAreUnique(names);
    }

    [Test]
    public void ShellCode_ReadsAndWritesHex()
    {
        Assert.IsTrue(MenuController.TryParseShellCode("#FF8000", out Color color));
        Assert.AreEqual("#FF8000", MenuController.ShellCode(color));
        Assert.IsTrue(MenuController.TryParseShellCode(" 1f59d9 ", out color));
        Assert.AreEqual("#1F59D9", MenuController.ShellCode(color));
        foreach (string bad in new[] { null, "", "#FF80", "#FF80000", "#GG8000", "red" })
        {
            Assert.IsFalse(MenuController.TryParseShellCode(bad, out _), bad ?? "null");
        }
        CollectionAssert.AllItemsAreUnique(MenuController.ShellPresets.Select(MenuController.ShellCode));
        Assert.AreEqual(MenuController.ShellCode(CarCustomization.DefaultShellColor), MenuController.ShellCode(MenuController.ShellPresets[0]));
    }

    [TestCase(KeyCode.Alpha1, "1")]
    [TestCase(KeyCode.Escape, "Esc")]
    [TestCase(KeyCode.RightShift, "R Shift")]
    [TestCase(KeyCode.Slash, "/")]
    [TestCase(KeyCode.Tab, "Tab")]
    public void KeyLabel_ShortNames(KeyCode key, string label)
    {
        Assert.AreEqual(label, InputBindings.KeyLabel(key));
    }
}
