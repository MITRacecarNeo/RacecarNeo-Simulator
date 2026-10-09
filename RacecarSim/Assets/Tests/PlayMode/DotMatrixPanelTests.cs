using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// The HUD dot matrix panel: hidden unless Show dot matrix is on; a frame sent to the car lights
/// the matching LEDs, column 0 at the left and row 0 at the top.
/// </summary>
public class DotMatrixPanelTests
{
    private bool showDotMatrix;

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
        this.showDotMatrix = Settings.ShowDotMatrix;
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        Settings.ShowDotMatrix = this.showDotMatrix;
        yield return PlayModeLevels.Unload();
    }

    [UnitySetUp]
    public IEnumerator LoadLevel()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
    }

    [UnityTest]
    public IEnumerator HiddenUnlessEnabled()
    {
        RawImage image = Object.FindAnyObjectByType<DotMatrixPanel>().GetComponentInChildren<RawImage>();
        Settings.ShowDotMatrix = false;
        yield return null;
        Assert.IsFalse(image.enabled);
        Settings.ShowDotMatrix = true;
        yield return null;
        Assert.IsTrue(image.enabled);
    }

    [UnityTest]
    public IEnumerator Frame_LightsMatchingLeds()
    {
        Settings.ShowDotMatrix = true;
        bool[,] pixels = new bool[ActuatorCommands.MatrixRows, ActuatorCommands.MatrixColumns];
        pixels[0, 0] = true;
        pixels[7, 22] = true;
        LevelManager.GetCar().Actuators.SetMatrix(pixels);
        yield return null;
        yield return null;

        Texture2D texture = (Texture2D)Object.FindAnyObjectByType<DotMatrixPanel>().GetComponentInChildren<RawImage>().texture;
        int cell = texture.width / ActuatorCommands.MatrixColumns;
        Color32 LedCenter(int row, int column) => texture.GetPixel(column * cell + cell / 2, (ActuatorCommands.MatrixRows - 1 - row) * cell + cell / 2);

        Debug.Log($"Matrix LEDs: (0,0) {LedCenter(0, 0)}, (7,22) {LedCenter(7, 22)}, (7,23) {LedCenter(7, 23)}, (0,1) {LedCenter(0, 1)}");
        Assert.Greater(LedCenter(0, 0).r, 200, "row 0, column 0 lit (top left)");
        Assert.Greater(LedCenter(7, 22).r, 200, "row 7, column 22 lit");
        Assert.Less(LedCenter(7, 23).r, 100, "row 7, column 23 unlit");
        Assert.Less(LedCenter(0, 1).r, 100, "row 0, column 1 unlit");
    }
}
