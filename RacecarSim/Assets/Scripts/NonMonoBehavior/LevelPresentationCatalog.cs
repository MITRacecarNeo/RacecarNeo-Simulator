using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every level's LevelPresentation, in Resources/LevelPresentation.asset. A level without an entry
/// gets an empty one (no images, no route), so a new level works before it is captured.
/// </summary>
[CreateAssetMenu(menuName = "RacecarSim/Level Presentation Catalog")]
public class LevelPresentationCatalog : ScriptableObject
{
    /// <summary>
    /// Resources path of the catalog asset.
    /// </summary>
    public const string ResourcePath = "LevelPresentation";

    /// <summary>
    /// One entry per level.
    /// </summary>
    public List<LevelPresentation> Entries = new List<LevelPresentation>();

    private static LevelPresentationCatalog loaded;
    private static readonly Dictionary<string, LevelPresentation> registered = new Dictionary<string, LevelPresentation>();
    private Dictionary<string, LevelPresentation> byKey;

    /// <summary>
    /// Adds or replaces a presentation made at runtime (levels outside the build, such as
    /// captured worlds); it takes precedence over the catalog asset.
    /// </summary>
    public static void Register(LevelPresentation presentation)
    {
        LevelPresentationCatalog.registered[presentation.Key] = presentation;
    }

    /// <summary>
    /// The presentation for a level; an empty entry when the catalog has none.
    /// </summary>
    public static LevelPresentation Get(LevelInfo level)
    {
        if (LevelPresentationCatalog.registered.TryGetValue(level.Key, out LevelPresentation made))
        {
            return made;
        }
        if (LevelPresentationCatalog.loaded == null)
        {
            LevelPresentationCatalog.loaded = Resources.Load<LevelPresentationCatalog>(LevelPresentationCatalog.ResourcePath);
        }
        return LevelPresentationCatalog.loaded != null && LevelPresentationCatalog.loaded.Find(level.Key) is LevelPresentation found
            ? found
            : new LevelPresentation { Key = level.Key };
    }

    /// <summary>
    /// The entry with the given key, or null.
    /// </summary>
    public LevelPresentation Find(string key)
    {
        if (this.byKey == null || this.byKey.Count != this.Entries.Count)
        {
            this.byKey = new Dictionary<string, LevelPresentation>();
            foreach (LevelPresentation entry in this.Entries)
            {
                this.byKey[entry.Key] = entry;
            }
        }
        return this.byKey.TryGetValue(key, out LevelPresentation presentation) ? presentation : null;
    }
}
