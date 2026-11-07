using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// The arena in a loaded level: the player's view renders it, the car's color camera does not,
/// and the car's image is pixel for pixel the same with the arena on and off. The level's
/// palette applies.
/// </summary>
public class ArenaPlayTests
{
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
    public IEnumerator Arena_HiddenFromTheCarCamera()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        Arena arena = Object.FindAnyObjectByType<Arena>();
        Assert.IsNotNull(arena, "Demo World places the shared terrain");
        Assert.AreEqual(LevelPresentationCatalog.Get(level).ArenaPalette, arena.Palette, "level palette applied");

        Racecar car = LevelManager.GetCar();
        RenderTexture image = car.GetComponent<CameraModule>().ColorImage;
        Camera colorCamera = car.GetComponentsInChildren<Camera>(true).First(camera => camera.targetTexture == image);
        Assert.AreEqual(0, colorCamera.cullingMask & Arena.LayerMaskBit, "car color camera skips the Arena layer");
        Assert.AreNotEqual(0, Camera.main.cullingMask & Arena.LayerMaskBit, "player view renders the arena");

        // Look toward the stands so the arena would fill part of the frame if rendered
        Time.timeScale = 0;
        car.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        Color32[] withArena = ArenaPlayTests.Render(colorCamera, image);
        arena.gameObject.SetActive(false);
        Color32[] withoutArena = ArenaPlayTests.Render(colorCamera, image);
        arena.gameObject.SetActive(true);
        CollectionAssert.AreEqual(withoutArena, withArena, "car color image unchanged by the arena");
    }

    private static Color32[] Render(Camera camera, RenderTexture target)
    {
        camera.Render();
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        Texture2D pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        pixels.Apply();
        RenderTexture.active = previous;
        Color32[] result = pixels.GetPixels32();
        Object.Destroy(pixels);
        return result;
    }
}
