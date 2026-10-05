using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Level obstacles reach above the LIDAR scan plane, so the car's LIDAR sees them.
/// </summary>
public class LidarObstacleTests
{
    private bool realism;

    [SetUp]
    public void RequireDataDirectory()
    {
        PlayModeLevels.RequireDataDirectory();

        // Realism adds about 2 percent noise to each sample
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
    public IEnumerator Antennas_AppearBehindTheLidar()
    {
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        Racecar car = LevelManager.GetCar();
        PlayModeLevels.PlaceOnPad(car, new Vector3(0, 0.5f, 0));
        yield return new WaitForSeconds(1);

        // The masts stand 2.68 units behind the LIDAR and 0.449 to each side: bearings 170.5 and
        // 189.5 deg clockwise from forward, about 26.6 cm to their near faces (the physical car
        // measures 270 mm at about 170 deg)
        foreach (float bearing in new[] { 170.5f, 189.5f })
        {
            int center = Mathf.RoundToInt(bearing / 360 * Lidar.NumSamples);
            float nearest = float.MaxValue;
            for (int i = center - 6; i <= center + 6; i++)
            {
                float sample = car.Lidar.Samples[i];
                if (sample > 0)
                {
                    nearest = Mathf.Min(nearest, sample);
                }
            }
            Assert.AreEqual(26.6f, nearest, 1.5f, $"antenna at {bearing} deg (cm)");
        }
    }

    [UnityTest]
    public IEnumerator Lab3aBlocks_BridgedBlockStaysAboveTheScanPlane()
    {
        yield return LidarObstacleTests.AssertBridgedBlock("Lab 3a: Safety Stop", 45);
    }

    [UnityTest]
    public IEnumerator Lab4aBlocks_BridgedBlockStaysAboveTheScanPlane()
    {
        yield return LidarObstacleTests.AssertBridgedBlock("Lab 4a: Safety Stop (Revisited)", 71);
    }

    [UnityTest]
    public IEnumerator Cone_AheadOfTheCarAppearsInTheScan()
    {
#if UNITY_EDITOR
        LevelInfo level = PlayModeLevels.Find("Demo World");
        yield return PlayModeLevels.Load(level, LevelManagerMode.Exploration, level.BuildIndex);
        Object.FindAnyObjectByType<LevelManager>().enabled = false;
        Racecar car = LevelManager.GetCar();
        PlayModeLevels.PlaceOnPad(car, new Vector3(0, 0.5f, 0));

        GameObject cone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Obstacles/Cone.prefab");
        Object.Instantiate(cone, PlayModeLevels.PadCenter + new Vector3(0, 0, 10), Quaternion.identity);
        yield return new WaitForSeconds(1);

        // Sample 0 points straight ahead. The LIDAR sits 1.26 units ahead of the car root, and at
        // the scan plane the cone's radius is about 0.19 units (1 unit = 10 cm)
        float expected = (10 - 1.26f - 0.19f) * 10;
        Assert.AreEqual(expected, car.Lidar.Samples[0], 3, "distance to the cone (cm)");
#else
        Assert.Ignore("Loads the cone prefab with AssetDatabase, which needs the editor.");
        yield break;
#endif
    }

    /// <summary>
    /// The Blocks trials bridge a MondoBloxx across two upright ones; only the bridged block
    /// reaches the scan plane, so it must stay up at 1 g.
    /// </summary>
    private static IEnumerator AssertBridgedBlock(string labName, int buildIndex)
    {
        LevelInfo lab = PlayModeLevels.Find(labName);
        yield return PlayModeLevels.Load(lab, LevelManagerMode.Autograder, buildIndex);
        yield return new WaitForSeconds(3);

        float ground = LevelManager.GetCar().transform.position.y;
        float top = 0;
        int blocks = 0;
        foreach (Rigidbody body in Object.FindObjectsByType<Rigidbody>())
        {
            if (body.name.StartsWith("MondoBloxx"))
            {
                blocks++;
                foreach (Collider collider in body.GetComponentsInChildren<Collider>())
                {
                    top = Mathf.Max(top, collider.bounds.max.y - ground);
                }
            }
        }
        Debug.Log($"{labName} Blocks: {blocks} blocks, highest top {top:F3} units above the car");
        Assert.Greater(blocks, 0, "blocks in the trial");
        Assert.Greater(top, 2.5f, "a block reaches above the scan plane");
    }
}
