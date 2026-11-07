using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// The level presentation catalog covers every level in LevelCollection exactly once, with a
/// 960 x 540 thumbnail, a 512 x 512 top view, and a route inside the top view.
/// </summary>
public class LevelPresentationTests
{
    private const string catalogPath = "Assets/Resources/LevelPresentation.asset";

    private static IEnumerable<LevelInfo> Levels => LevelCollection.LevelCollections.SelectMany(collection => collection.Levels);

    [Test]
    public void Keys_AreUnique()
    {
        List<string> duplicates = LevelPresentationTests.Levels.GroupBy(level => level.Key).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        Assert.IsEmpty(duplicates, string.Join(", ", duplicates));
    }

    [Test]
    public void Catalog_CoversEveryLevelOnce()
    {
        LevelPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelPresentationCatalog>(LevelPresentationTests.catalogPath);
        Assert.IsNotNull(catalog, LevelPresentationTests.catalogPath);

        HashSet<string> keys = new HashSet<string>(LevelPresentationTests.Levels.Select(level => level.Key));
        List<string> missing = keys.Where(key => catalog.Find(key) == null).ToList();
        List<string> unknown = catalog.Entries.Select(entry => entry.Key).Where(key => !keys.Contains(key)).ToList();
        Assert.IsEmpty(missing, "levels without an entry (run LevelCaptureTool.CaptureAll): " + string.Join(", ", missing));
        Assert.IsEmpty(unknown, "entries for no level: " + string.Join(", ", unknown));
        Assert.AreEqual(keys.Count, catalog.Entries.Count, "one entry per level");
    }

    [Test]
    public void Entries_HaveImagesAndRouteInside()
    {
        LevelPresentationCatalog catalog = AssetDatabase.LoadAssetAtPath<LevelPresentationCatalog>(LevelPresentationTests.catalogPath);
        List<string> problems = new List<string>();
        foreach (LevelPresentation entry in catalog.Entries)
        {
            if (entry.Thumbnail == null || entry.Thumbnail.width != 960 || entry.Thumbnail.height != 540)
            {
                problems.Add($"{entry.Key}: thumbnail missing or not 960 x 540");
            }
            if (entry.TopView == null || entry.TopView.width != 512 || entry.TopView.height != 512)
            {
                problems.Add($"{entry.Key}: top view missing or not 512 x 512");
            }
            if (entry.TopViewArea.width <= 0 || entry.TopViewArea.height <= 0)
            {
                problems.Add($"{entry.Key}: empty top view area");
            }
            if (entry.Route.Any(point => point.x < 0 || point.x > 1 || point.y < 0 || point.y > 1))
            {
                problems.Add($"{entry.Key}: route outside the top view");
            }
        }
        Assert.IsEmpty(problems, string.Join("\n", problems));
    }

    [Test]
    public void Get_UnknownLevelGivesEmptyEntry()
    {
        LevelPresentation presentation = LevelPresentationCatalog.Get(new LevelInfo { DisplayName = "Not A Level" });
        Assert.AreEqual("not-a-level", presentation.Key);
        Assert.IsNull(presentation.Thumbnail);
        Assert.IsEmpty(presentation.Route);
    }

    [Test]
    public void ToTopView_MapsAreaCorners()
    {
        LevelPresentation presentation = new LevelPresentation { TopViewArea = new Rect(-50, 20, 100, 100) };
        Assert.AreEqual(Vector2.zero, presentation.ToTopView(new Vector3(-50, 3, 20)));
        Assert.AreEqual(Vector2.one, presentation.ToTopView(new Vector3(50, 0, 120)));
        Assert.AreEqual(new Vector2(0.5f, 0.25f), presentation.ToTopView(new Vector3(0, 0, 45)));
    }
}
