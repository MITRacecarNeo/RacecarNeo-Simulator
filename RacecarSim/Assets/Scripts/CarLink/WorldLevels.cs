using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Captured worlds as levels: one runtime LevelInfo per world (Exploration, one car, the CarWorld
/// scene), the Mapped Worlds collection for map select, a thumbnail per world, and the world being
/// driven.
/// </summary>
public static class WorldLevels
{
    #region Constants
    public const string CollectionDisplayName = "Mapped Worlds";

    /// <summary>
    /// Prefix of a world level's Id, before the world's name as a slug.
    /// </summary>
    public const string IdPrefix = "world-";

    private const string scenePath = "Assets/Scenes/CarWorld.unity";

    /// <summary>
    /// Longest thumbnail side, in pixels; larger maps are sampled.
    /// </summary>
    private const int thumbnailMaxSide = 256;

    /// <summary>
    /// Cells of margin kept around the known part of a map in its thumbnail.
    /// </summary>
    private const int thumbnailMargin = 4;
    #endregion

    #region Public Interface
    /// <summary>
    /// The world loaded in the CarWorld scene, or null. Kept across reloads of the scene (reset in
    /// exploration reloads it).
    /// </summary>
    public static CapturedWorld Current { get; set; }

    /// <summary>
    /// The CarWorld scene's build index.
    /// </summary>
    public static int BuildIndex { get { return SceneUtility.GetBuildIndexByScenePath(WorldLevels.scenePath); } }

    /// <summary>
    /// The level for a world; the same object for the same folder, so the menu can return to it.
    /// </summary>
    public static LevelInfo LevelFor(CapturedWorld world)
    {
        string key = WorldLevels.Key(world);
        if (!WorldLevels.levels.TryGetValue(key, out LevelInfo level))
        {
            level = new LevelInfo
            {
                DisplayName = world.Name,
                Id = WorldLevels.IdPrefix + LaunchOptions.Slug(world.Name),
                BuildIndex = WorldLevels.BuildIndex,
                CollectionName = WorldLevels.CollectionDisplayName,
                CollectionDisplayName = WorldLevels.CollectionDisplayName,
                MaxCars = 1,
            };
            WorldLevels.levels[key] = level;
        }
        WorldLevels.worlds[level] = world;
        return level;
    }

    /// <summary>
    /// The world behind a level from LevelFor, or null for any other level.
    /// </summary>
    public static CapturedWorld WorldOf(LevelInfo level)
    {
        return level != null && WorldLevels.worlds.TryGetValue(level, out CapturedWorld world) ? world : null;
    }

    /// <summary>
    /// Mapped Worlds with every world in a library, newest first (possibly none). Registers each
    /// world's thumbnail with the level presentation catalog.
    /// </summary>
    public static LevelCollection Collection(WorldLibrary library)
    {
        List<CapturedWorld> worlds = library.List();
        LevelInfo[] levels = new LevelInfo[worlds.Count];
        for (int i = 0; i < worlds.Count; i++)
        {
            levels[i] = WorldLevels.LevelFor(worlds[i]);
            LevelPresentationCatalog.Register(new LevelPresentation { Key = levels[i].Key, Thumbnail = WorldLevels.Thumbnail(worlds[i]) });
        }
        return new LevelCollection { DisplayName = WorldLevels.CollectionDisplayName, ShortName = WorldLevels.CollectionDisplayName, Levels = levels };
    }

    /// <summary>
    /// Sets the world to drive and prepares its level (Exploration, one car); returns the build
    /// index to load.
    /// </summary>
    public static int Prepare(CapturedWorld world)
    {
        WorldLevels.Current = world;
        return LevelLaunch.Prepare(WorldLevels.LevelFor(world), LevelManagerMode.Exploration, 1);
    }

    /// <summary>
    /// A world by its folder (holding world.json) or by its name in a library; null when neither
    /// finds one.
    /// </summary>
    public static CapturedWorld Find(string nameOrFolder, WorldLibrary library)
    {
        if (string.IsNullOrEmpty(nameOrFolder))
        {
            return null;
        }
        if (File.Exists(Path.Combine(nameOrFolder, CapturedWorld.FileName)))
        {
            return WorldLibrary.Read(nameOrFolder);
        }
        return library.List().Find(world => world.Name == nameOrFolder || LaunchOptions.Slug(world.Name) == LaunchOptions.Slug(nameOrFolder));
    }

    /// <summary>
    /// A world's thumbnail: the rendered view in its folder (WorldThumbnail) when there is one,
    /// else its map from above in the top-down map's colors, cropped to the known cells. Cached;
    /// null when neither reads.
    /// </summary>
    public static Texture2D Thumbnail(CapturedWorld world)
    {
        bool isRendered = WorldThumbnail.Exists(world);
        string key = WorldLevels.Key(world) + (isRendered ? "|rendered" : "|map");
        if (WorldLevels.thumbnails.TryGetValue(key, out Texture2D cached) && cached != null)
        {
            return cached;
        }
        if (isRendered)
        {
            Texture2D image = new Texture2D(2, 2);
            if (image.LoadImage(File.ReadAllBytes(Path.Combine(world.Folder, WorldThumbnail.FileName))))
            {
                WorldLevels.thumbnails[key] = image;
                return image;
            }
            Object.Destroy(image);
        }
        OccupancyGridMsg grid;
        try
        {
            grid = WorldLibrary.LoadGrid(world);
        }
        catch (System.Exception exception) when (exception is IOException || exception is CarLinkFormatException)
        {
            Debug.LogWarning($"Worlds: no thumbnail for {world.Name}: {exception.Message}");
            return null;
        }
        Texture2D texture = WorldLevels.Render(grid);
        WorldLevels.thumbnails[key] = texture;
        return texture;
    }

    /// <summary>
    /// A copy of a grid with every unknown cell that touches a free cell (4-neighbors) made
    /// occupied, so walls close the explored area.
    /// </summary>
    public static OccupancyGridMsg CloseUnknownEdges(OccupancyGridMsg grid)
    {
        sbyte[] data = (sbyte[])grid.Data.Clone();
        bool IsFree(int x, int y) => x >= 0 && y >= 0 && x < grid.Width && y < grid.Height
            && grid.Data[y * grid.Width + x] >= 0 && grid.Data[y * grid.Width + x] <= OccupancyTiles.FreeThreshold;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.Data[y * grid.Width + x] < 0 && (IsFree(x - 1, y) || IsFree(x + 1, y) || IsFree(x, y - 1) || IsFree(x, y + 1)))
                {
                    data[y * grid.Width + x] = 100;
                }
            }
        }
        return new OccupancyGridMsg
        {
            Stamp = grid.Stamp,
            FrameId = grid.FrameId,
            Resolution = grid.Resolution,
            Width = grid.Width,
            Height = grid.Height,
            OriginX = grid.OriginX,
            OriginY = grid.OriginY,
            OriginYaw = grid.OriginYaw,
            Data = data,
        };
    }

    /// <summary>
    /// The known cells' bounds (with a margin, inside the grid) as x0, y0, width, height; the whole
    /// grid when nothing is known.
    /// </summary>
    public static RectInt KnownBounds(OccupancyGridMsg grid, int margin)
    {
        int minX = grid.Width;
        int minY = grid.Height;
        int maxX = -1;
        int maxY = -1;
        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (grid.Data[y * grid.Width + x] >= 0)
                {
                    minX = Mathf.Min(minX, x);
                    maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }
        if (maxX < 0)
        {
            return new RectInt(0, 0, grid.Width, grid.Height);
        }
        minX = Mathf.Max(0, minX - margin);
        minY = Mathf.Max(0, minY - margin);
        maxX = Mathf.Min(grid.Width - 1, maxX + margin);
        maxY = Mathf.Min(grid.Height - 1, maxY + margin);
        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
    #endregion

    private static readonly Dictionary<string, LevelInfo> levels = new Dictionary<string, LevelInfo>();
    private static readonly Dictionary<LevelInfo, CapturedWorld> worlds = new Dictionary<LevelInfo, CapturedWorld>();
    private static readonly Dictionary<string, Texture2D> thumbnails = new Dictionary<string, Texture2D>();

    private static string Key(CapturedWorld world)
    {
        return Path.GetFullPath(world.Folder ?? world.Name) + "|" + world.GridHash;
    }

    private static Texture2D Render(OccupancyGridMsg grid)
    {
        RectInt bounds = WorldLevels.KnownBounds(grid, WorldLevels.thumbnailMargin);
        int step = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(bounds.width, bounds.height) / (float)WorldLevels.thumbnailMaxSide));
        int width = Mathf.Max(1, bounds.width / step);
        int height = Mathf.Max(1, bounds.height / step);
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // A sampled block shows occupied if any cell in it is (thin walls survive), else
                // its first known cell, else unknown
                sbyte value = -1;
                for (int dy = 0; dy < step && value < OccupancyTiles.OccupiedThreshold; dy++)
                {
                    for (int dx = 0; dx < step && value < OccupancyTiles.OccupiedThreshold; dx++)
                    {
                        sbyte cell = grid.Data[(bounds.y + y * step + dy) * grid.Width + bounds.x + x * step + dx];
                        if (cell >= OccupancyTiles.OccupiedThreshold || value < 0)
                        {
                            value = cell;
                        }
                    }
                }
                pixels[y * width + x] = CarMinimap.CellColor(value);
            }
        }
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels32(pixels);
        texture.Apply(false);
        return texture;
    }
}
