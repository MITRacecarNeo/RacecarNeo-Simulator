using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// The arena nested in Terrain.prefab cannot change what the car senses: every part is on the
/// Arena layer, has no collider, casts no shadow, and is not static (so baked lighting is
/// unchanged). Every role has a color in every palette.
/// </summary>
public class ArenaTests
{
    private const string terrainPrefabPath = "Assets/Prefabs/Terrain.prefab";

    /// <summary>
    /// Scenes that place the shared terrain unmodified, and so show the arena.
    /// </summary>
    private const int sceneCount = 100;

    private static GameObject Arena()
    {
        GameObject terrain = AssetDatabase.LoadAssetAtPath<GameObject>(ArenaTests.terrainPrefabPath);
        Transform arena = terrain.transform.Find("Arena");
        Assert.IsNotNull(arena, "Arena nested in Terrain.prefab");
        return arena.gameObject;
    }

    [Test]
    public void Arena_PartsAreInvisibleToSensorsAndLighting()
    {
        GameObject arena = ArenaTests.Arena();
        int layer = LayerMask.NameToLayer(global::Arena.LayerName);
        Assert.GreaterOrEqual(layer, 0, "Arena layer defined");
        CollectionAssert.IsEmpty(arena.GetComponentsInChildren<Collider>(true), "no colliders (LIDAR and depth raycasts)");
        foreach (Transform part in arena.GetComponentsInChildren<Transform>(true))
        {
            Assert.AreEqual(layer, part.gameObject.layer, $"{part.name} layer");
            Assert.AreEqual((StaticEditorFlags)0, GameObjectUtility.GetStaticEditorFlags(part.gameObject), $"{part.name} static flags");
        }
        foreach (Renderer renderer in arena.GetComponentsInChildren<Renderer>(true))
        {
            Assert.AreEqual(ShadowCastingMode.Off, renderer.shadowCastingMode, $"{renderer.name} shadows");
        }
    }

    [Test]
    public void Arena_EveryRoleHasAColorInEveryPalette()
    {
        foreach (ArenaPalette palette in Enum.GetValues(typeof(ArenaPalette)))
        {
            foreach (global::Arena.Role role in Enum.GetValues(typeof(global::Arena.Role)))
            {
                Assert.DoesNotThrow(() => global::Arena.ColorFor(role, palette), $"{role} in {palette}");
            }
            var tokens = global::Arena.SceneryTokens(palette);
            Assert.IsTrue(ThemeTokens.Scenery.ContainsKey(tokens.Horizon), $"{palette} horizon");
        }
    }

    [Test]
    public void Arena_ReachesTheSharedTerrainScenes()
    {
        string guid = AssetDatabase.AssetPathToGUID(ArenaTests.terrainPrefabPath);
        int scenes = Directory.GetFiles(Path.Combine(Application.dataPath, "Scenes"), "*.unity", SearchOption.AllDirectories)
            .Count(scene => File.ReadAllText(scene).Contains(guid));
        Assert.AreEqual(ArenaTests.sceneCount, scenes, "scenes placing Terrain.prefab");
    }
}
