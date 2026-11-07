using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// The captured worlds on this computer: one folder per world under a root folder
/// (<c>Worlds/</c> in the data directory). Saves from a car's map or imports a map_server map;
/// never overwrites a world, adding a suffix on a name clash.
/// </summary>
/// <remarks>
/// A world is written into a hidden folder and moved into place when complete, so a listed
/// world is never half written. Every call is file work only and may run off the main thread.
/// </remarks>
public sealed class WorldLibrary
{
    #region Constants
    public const string FolderName = "Worlds";

    /// <summary>
    /// Longest world name, in characters.
    /// </summary>
    public const int MaxNameLength = 48;
    #endregion

    #region Public Interface
    public string Root { get; }

    /// <summary>
    /// The library in the data directory (-racecarsim-data-dir when given).
    /// </summary>
    public static WorldLibrary Default
    {
        get { return new WorldLibrary(Path.Combine(SavedDataManager.DataDirectory, WorldLibrary.FolderName)); }
    }

    public WorldLibrary(string root)
    {
        this.Root = root;
    }

    /// <summary>
    /// The worlds that read, newest first; a folder that does not read is skipped with a warning.
    /// </summary>
    public List<CapturedWorld> List()
    {
        List<CapturedWorld> worlds = new List<CapturedWorld>();
        if (!Directory.Exists(this.Root))
        {
            return worlds;
        }
        foreach (string folder in Directory.GetDirectories(this.Root))
        {
            if (Path.GetFileName(folder).StartsWith(".", StringComparison.Ordinal))
            {
                continue;
            }
            try
            {
                worlds.Add(WorldLibrary.Read(folder));
            }
            catch (Exception exception) when (exception is IOException || exception is CarLinkFormatException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning($"Worlds: skipped {Path.GetFileName(folder)}: {exception.Message}");
            }
        }
        worlds.Sort((a, b) => string.CompareOrdinal(b.Created ?? string.Empty, a.Created ?? string.Empty));
        return worlds;
    }

    /// <summary>
    /// Reads one world folder's world.json.
    /// </summary>
    public static CapturedWorld Read(string folder)
    {
        CapturedWorld world = CapturedWorld.Parse(CarLinkJson.Parse(File.ReadAllText(Path.Combine(folder, CapturedWorld.FileName))));
        world.Folder = folder;
        world.Name = Path.GetFileName(folder);
        return world;
    }

    /// <summary>
    /// Reads a world's map.
    /// </summary>
    public static OccupancyGridMsg LoadGrid(CapturedWorld world)
    {
        return MapServerFile.Load(Path.Combine(world.Folder, CapturedWorld.MapYaml));
    }

    /// <summary>
    /// Saves a world from a car. Uses the car's saved map files when a download folder holds them
    /// (with its session record, trajectory, and meta.yaml), else writes the live map.
    /// </summary>
    /// <param name="name">Wanted name; cleaned and given a suffix when taken.</param>
    /// <param name="downloadFolder">Folder of the map's downloads, or null.</param>
    /// <param name="carMap">The map's name on the car, naming its files in downloadFolder.</param>
    /// <param name="liveMap">The car's newest map, used without a download; may be null when downloaded.</param>
    /// <param name="info">The car's info, or null.</param>
    public CapturedWorld SaveFromCar(string name, string downloadFolder, string carMap, OccupancyGridMsg liveMap, RacecarInfo info)
    {
        string carYaml = downloadFolder != null && carMap != null ? Path.Combine(downloadFolder, carMap + ".yaml") : null;
        bool hasDownload = carYaml != null && File.Exists(carYaml);
        if (!hasDownload && liveMap == null)
        {
            throw new InvalidOperationException("no map to save");
        }
        CapturedWorld world = new CapturedWorld
        {
            SourceKind = "car",
            CarHostname = info?.Hostname,
            DriverVersion = info?.DriverVersion,
            LinkInterface = info?.LinkInterface,
            SourceMap = carMap,
            CarInfo = info?.Source ?? CarLinkValue.Null,
        };
        return this.Write(name, world, staging =>
        {
            if (hasDownload)
            {
                WorldLibrary.CopyMap(carYaml, staging);
                WorldLibrary.CopyIfPresent(Path.Combine(downloadFolder, "session.tar"), Path.Combine(staging, "car-session.tar"));
                WorldLibrary.CopyIfPresent(Path.Combine(downloadFolder, SessionRecorder.FileName), Path.Combine(staging, SessionRecorder.FileName));
                string meta = Path.Combine(downloadFolder, "meta.yaml");
                if (WorldLibrary.CopyIfPresent(meta, Path.Combine(staging, "car-meta.yaml")))
                {
                    WorldLibrary.ReadStartPose(File.ReadAllText(meta), world);
                }
            }
            else
            {
                MapServerFile.Save(liveMap, Path.Combine(staging, CapturedWorld.MapYaml), CapturedWorld.MapImage);
            }
        });
    }

    /// <summary>
    /// Imports a map_server map (a YAML file and its .pgm image) as a world named after the YAML.
    /// </summary>
    public CapturedWorld Import(string yamlPath)
    {
        // Read first so a map that does not load leaves nothing behind
        MapServerFile.Load(yamlPath);
        CapturedWorld world = new CapturedWorld
        {
            SourceKind = "import",
            SourceMap = Path.GetFileNameWithoutExtension(yamlPath),
        };
        return this.Write(Path.GetFileNameWithoutExtension(yamlPath), world, staging => WorldLibrary.CopyMap(yamlPath, staging));
    }

    /// <summary>
    /// Writes a world's build settings to its world.json (replacing it whole) and removes its
    /// rendered thumbnail, which no longer shows the world as it builds.
    /// </summary>
    public static void SaveSettings(CapturedWorld world)
    {
        string path = Path.Combine(world.Folder, CapturedWorld.FileName);
        string staging = path + ".saving";
        File.WriteAllText(staging, CarLinkJson.WriteIndented(world.ToValue()), new UTF8Encoding(false));
        File.Delete(path);
        File.Move(staging, path);
        string thumbnail = Path.Combine(world.Folder, WorldThumbnail.FileName);
        if (File.Exists(thumbnail))
        {
            File.Delete(thumbnail);
        }
    }

    /// <summary>
    /// Deletes a world's folder. Refuses a folder outside the library or without world.json.
    /// </summary>
    public void Delete(CapturedWorld world)
    {
        string folder = Path.GetFullPath(world.Folder);
        string root = Path.GetFullPath(this.Root);
        if (!string.Equals(Path.GetDirectoryName(folder), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
            || !File.Exists(Path.Combine(folder, CapturedWorld.FileName)))
        {
            throw new InvalidOperationException($"{world.Folder} is not a world in {this.Root}");
        }
        Directory.Delete(folder, true);
    }

    /// <summary>
    /// A name made safe for a folder: letters, digits, '-', '_' and spaces kept, others replaced
    /// by '_', at most MaxNameLength characters; "world" when nothing is left.
    /// </summary>
    public static string CleanName(string name)
    {
        StringBuilder builder = new StringBuilder();
        foreach (char c in (name ?? string.Empty).Trim())
        {
            bool keep = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-' || c == '_' || c == ' ';
            builder.Append(keep ? c : '_');
            if (builder.Length == WorldLibrary.MaxNameLength)
            {
                break;
            }
        }
        string clean = builder.ToString().Trim();
        return clean.Trim('_').Length == 0 ? "world" : clean;
    }

    /// <summary>
    /// A clean name not yet used in the library: the name, else name_2, name_3, and so on.
    /// </summary>
    public string UniqueName(string name)
    {
        string clean = WorldLibrary.CleanName(name);
        string candidate = clean;
        for (int suffix = 2; Directory.Exists(Path.Combine(this.Root, candidate)); suffix++)
        {
            candidate = $"{clean}_{suffix}";
        }
        return candidate;
    }
    #endregion

    /// <summary>
    /// Fills a hidden staging folder, completes world.json from its map, and moves it into place.
    /// </summary>
    private CapturedWorld Write(string name, CapturedWorld world, Action<string> fill)
    {
        Directory.CreateDirectory(this.Root);
        string staging = Path.Combine(this.Root, ".saving-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try
        {
            fill(staging);
            OccupancyGridMsg grid = MapServerFile.Load(Path.Combine(staging, CapturedWorld.MapYaml));
            world.Resolution = grid.Resolution;
            world.Width = grid.Width;
            world.Height = grid.Height;
            world.OriginX = grid.OriginX;
            world.OriginY = grid.OriginY;
            world.OriginYaw = grid.OriginYaw;
            world.GridHash = MapServerFile.GridHash(grid);
            world.Created = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);

            lock (WorldLibrary.nameLock)
            {
                world.Name = this.UniqueName(name);
                File.WriteAllText(Path.Combine(staging, CapturedWorld.FileName), CarLinkJson.WriteIndented(world.ToValue()), new UTF8Encoding(false));
                world.Folder = Path.Combine(this.Root, world.Name);
                Directory.Move(staging, world.Folder);
            }
            return world;
        }
        catch
        {
            WorldLibrary.DeleteStaging(staging);
            throw;
        }
    }

    private static readonly object nameLock = new object();

    /// <summary>
    /// Copies a map's YAML and image into a folder as map.yaml and map.pgm.
    /// </summary>
    private static void CopyMap(string yamlPath, string folder)
    {
        string text = File.ReadAllText(yamlPath);
        if (!MapServerFile.ParseYaml(text).TryGetValue("image", out string image))
        {
            throw new CarLinkFormatException("map YAML has no 'image'");
        }
        image = image.Trim('"', '\'');
        string imagePath = Path.IsPathRooted(image) ? image : Path.Combine(Path.GetDirectoryName(yamlPath) ?? string.Empty, image);
        File.Copy(imagePath, Path.Combine(folder, CapturedWorld.MapImage));
        StringBuilder yaml = new StringBuilder();
        foreach (string line in text.Replace("\r\n", "\n").Split('\n'))
        {
            yaml.Append(line.StartsWith("image:", StringComparison.Ordinal) ? "image: " + CapturedWorld.MapImage : line).Append('\n');
        }
        File.WriteAllText(Path.Combine(folder, CapturedWorld.MapYaml), yaml.ToString().TrimEnd('\n') + "\n", new UTF8Encoding(false));
    }

    private static bool CopyIfPresent(string source, string target)
    {
        if (!File.Exists(source))
        {
            return false;
        }
        File.Copy(source, target);
        return true;
    }

    /// <summary>
    /// Takes the start pose from a car map's meta.yaml ("start_pose: [x, y, yaw]") when present.
    /// </summary>
    private static void ReadStartPose(string metaYaml, CapturedWorld world)
    {
        if (!MapServerFile.ParseYaml(metaYaml).TryGetValue("start_pose", out string text))
        {
            return;
        }
        try
        {
            double[] pose = MapServerFile.ParseNumbers(text);
            if (pose.Length == 3)
            {
                world.StartX = pose[0];
                world.StartY = pose[1];
                world.StartYaw = pose[2];
            }
        }
        catch (CarLinkFormatException exception)
        {
            Debug.LogWarning($"Worlds: start pose not read from meta.yaml: {exception.Message}");
        }
    }

    /// <summary>
    /// Removes a staging folder's files, then the folder; one explicit path at a time.
    /// </summary>
    private static void DeleteStaging(string staging)
    {
        try
        {
            foreach (string file in Directory.GetFiles(staging))
            {
                File.Delete(file);
            }
            Directory.Delete(staging);
        }
        catch (IOException exception)
        {
            Debug.LogWarning($"Worlds: could not remove {staging}: {exception.Message}");
        }
    }
}
