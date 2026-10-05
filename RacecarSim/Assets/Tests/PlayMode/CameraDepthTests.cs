using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// The depth image holds the distance along each pixel's ray to the first collider.
/// </summary>
public class CameraDepthTests
{
    private bool realism;

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();
        this.realism = Settings.IsRealism;
        Settings.IsRealism = false;
    }

    [UnityTearDown]
    public IEnumerator Unload()
    {
        Settings.IsRealism = this.realism;
        yield return PlayModeLevels.Unload();
    }

    [UnityTest]
    public IEnumerator DepthImage_MatchesOneRaycastPerPixel()
    {
#if UNITY_EDITOR
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        Racecar car = LevelManager.GetCar();
        PlayModeLevels.PlaceOnPad(car, new Vector3(0, 0.5f, 0));
        GameObject cone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacles/Cone.prefab");
        Object.Instantiate(cone, PlayModeLevels.PadCenter + new Vector3(0.5f, 0, 8), Quaternion.identity);
        yield return new WaitForSeconds(1);
        yield return null;

        float[][] image = car.Camera.DepthImage;
        Camera depthCamera = car.GetComponentsInChildren<Camera>(true)[1];
        int width = CameraModule.DepthWidth;
        int height = CameraModule.DepthHeight;
        int hits = 0;
        for (int r = 0; r < height; r++)
        {
            for (int c = 0; c < width; c++)
            {
                Ray ray = depthCamera.ViewportPointToRay(new Vector3((float)c / (width - 1), (height - r - 1.0f) / (height - 1), 0));
                float expected = Physics.Raycast(ray, out RaycastHit hit, 100, Constants.IgnoreUIMask) ? hit.distance * 10 : 0;
                if (expected > 0 && expected < 10.5f)
                {
                    expected = 0;
                }
                hits += expected > 0 ? 1 : 0;
                Assert.AreEqual(expected, image[r][c], 1e-3f, $"pixel ({r}, {c})");
            }
        }
        Assert.Greater(hits, width * height / 4, "rays hit the pad and the cone");
#else
        Assert.Ignore("Loads the cone prefab with AssetDatabase, which needs the editor.");
        yield break;
#endif
    }
}
