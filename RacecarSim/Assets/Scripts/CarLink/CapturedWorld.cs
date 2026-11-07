using System.Collections.Generic;

/// <summary>
/// A world captured from a car's map (or imported from a map_server map): the contents of a
/// world folder's world.json.
/// </summary>
/// <remarks>
/// Folder files: world.json, map.pgm and map.yaml (map_server format), and from a car
/// optionally trajectory.csv, car-session.tar, and car-meta.yaml.
/// </remarks>
public sealed class CapturedWorld
{
    #region Constants
    public const int CurrentFormat = 1;
    public const string FileName = "world.json";
    public const string MapYaml = "map.yaml";
    public const string MapImage = "map.pgm";

    /// <summary>
    /// Wall height built from occupied cells by default (in meters).
    /// </summary>
    public const double DefaultWallHeight = 1.0;

    /// <summary>
    /// Occupied groups smaller than this many cells are dropped by default.
    /// </summary>
    public const int DefaultSpeckleCells = 2;

    /// <summary>
    /// Unknown cells build nothing.
    /// </summary>
    public const string UnknownOpen = "open";

    /// <summary>
    /// Unknown cells next to free cells build walls, closing the explored area.
    /// </summary>
    public const string UnknownClosed = "closed";
    #endregion

    #region Public Interface
    public int Format = CapturedWorld.CurrentFormat;

    /// <summary>
    /// Folder name in the library, also the shown name.
    /// </summary>
    public string Name;

    /// <summary>
    /// Local time saved, ISO 8601.
    /// </summary>
    public string Created;

    /// <summary>
    /// "car" or "import".
    /// </summary>
    public string SourceKind;

    public string CarHostname;
    public string DriverVersion;
    public string LinkInterface;

    /// <summary>
    /// The map's name on the car, or the imported YAML's name.
    /// </summary>
    public string SourceMap;

    public double Resolution;
    public int Width;
    public int Height;
    public double OriginX;
    public double OriginY;
    public double OriginYaw;

    /// <summary>
    /// MapServerFile.GridHash of the map as saved; a reload matches it.
    /// </summary>
    public string GridHash;

    public double WallHeight = CapturedWorld.DefaultWallHeight;
    public int SpeckleCells = CapturedWorld.DefaultSpeckleCells;

    /// <summary>
    /// How unknown cells build: UnknownOpen or UnknownClosed.
    /// </summary>
    public string Unknown = CapturedWorld.UnknownOpen;

    /// <summary>
    /// Where the car started (map frame, meters and radians); 0 when unknown.
    /// </summary>
    public double StartX;
    public double StartY;
    public double StartYaw;

    /// <summary>
    /// Fidelity tiers built: "base" (walls from the 2D map).
    /// </summary>
    public List<string> Tiers = new List<string> { "base" };

    /// <summary>
    /// The car's info when captured (its profile snapshot), or Null.
    /// </summary>
    public CarLinkValue CarInfo = CarLinkValue.Null;

    /// <summary>
    /// The world's folder, set when listed or saved; not written.
    /// </summary>
    public string Folder;

    public CarLinkValue ToValue()
    {
        Dictionary<string, CarLinkValue> source = new Dictionary<string, CarLinkValue>
        {
            { "kind", CapturedWorld.Text(this.SourceKind) },
            { "car_hostname", CapturedWorld.Text(this.CarHostname) },
            { "driver_version", CapturedWorld.Text(this.DriverVersion) },
            { "link_interface", CapturedWorld.Text(this.LinkInterface) },
            { "map", CapturedWorld.Text(this.SourceMap) },
        };
        Dictionary<string, CarLinkValue> grid = new Dictionary<string, CarLinkValue>
        {
            { "resolution", CarLinkValue.FromDouble(this.Resolution) },
            { "width", CarLinkValue.FromLong(this.Width) },
            { "height", CarLinkValue.FromLong(this.Height) },
            { "origin", CarLinkValue.FromDoubles(new[] { this.OriginX, this.OriginY, this.OriginYaw }) },
            { "sha256", CapturedWorld.Text(this.GridHash) },
        };
        Dictionary<string, CarLinkValue> build = new Dictionary<string, CarLinkValue>
        {
            { "wall_height_m", CarLinkValue.FromDouble(this.WallHeight) },
            { "speckle_cells", CarLinkValue.FromLong(this.SpeckleCells) },
            { "unknown", CapturedWorld.Text(this.Unknown) },
        };
        List<CarLinkValue> tiers = new List<CarLinkValue>();
        foreach (string tier in this.Tiers)
        {
            tiers.Add(CarLinkValue.FromString(tier));
        }
        return CarLinkValue.FromMap(new Dictionary<string, CarLinkValue>
        {
            { "format", CarLinkValue.FromLong(this.Format) },
            { "name", CapturedWorld.Text(this.Name) },
            { "created", CapturedWorld.Text(this.Created) },
            { "source", CarLinkValue.FromMap(source) },
            { "grid", CarLinkValue.FromMap(grid) },
            { "build", CarLinkValue.FromMap(build) },
            { "start_pose", CarLinkValue.FromDoubles(new[] { this.StartX, this.StartY, this.StartYaw }) },
            { "tiers", CarLinkValue.FromList(tiers) },
            { "car_info", this.CarInfo },
        });
    }

    /// <exception cref="CarLinkFormatException">A required field is missing or the format is newer.</exception>
    public static CapturedWorld Parse(CarLinkValue value)
    {
        CapturedWorld world = new CapturedWorld();
        world.Format = CarLinkFields.Int(value, "format");
        if (world.Format > CapturedWorld.CurrentFormat)
        {
            throw new CarLinkFormatException($"world format {world.Format} is newer than this simulator");
        }
        world.Name = CarLinkFields.String(value, "name");
        world.Created = CarLinkFields.OptionalString(value, "created");
        world.SourceKind = CarLinkFields.OptionalString(value, "source.kind");
        world.CarHostname = CarLinkFields.OptionalString(value, "source.car_hostname");
        world.DriverVersion = CarLinkFields.OptionalString(value, "source.driver_version");
        world.LinkInterface = CarLinkFields.OptionalString(value, "source.link_interface");
        world.SourceMap = CarLinkFields.OptionalString(value, "source.map");
        world.Resolution = CarLinkFields.Double(value, "grid.resolution");
        world.Width = CarLinkFields.Int(value, "grid.width");
        world.Height = CarLinkFields.Int(value, "grid.height");
        double[] origin = CarLinkFields.Required(value, "grid.origin").AsDoubles();
        if (origin.Length != 3)
        {
            throw new CarLinkFormatException("field 'grid.origin': expected 3 numbers");
        }
        world.OriginX = origin[0];
        world.OriginY = origin[1];
        world.OriginYaw = origin[2];
        world.GridHash = CarLinkFields.OptionalString(value, "grid.sha256");
        world.WallHeight = CarLinkFields.OptionalDouble(value, "build.wall_height_m") ?? CapturedWorld.DefaultWallHeight;
        world.SpeckleCells = CarLinkFields.OptionalInt(value, "build.speckle_cells") ?? CapturedWorld.DefaultSpeckleCells;
        world.Unknown = CarLinkFields.OptionalString(value, "build.unknown") ?? CapturedWorld.UnknownOpen;
        CarLinkValue start = value["start_pose"];
        if (!start.IsNull)
        {
            double[] pose = start.AsDoubles();
            if (pose.Length == 3)
            {
                world.StartX = pose[0];
                world.StartY = pose[1];
                world.StartYaw = pose[2];
            }
        }
        world.Tiers.Clear();
        foreach (CarLinkValue tier in value["tiers"].Items)
        {
            world.Tiers.Add(tier.AsString());
        }
        if (world.Tiers.Count == 0)
        {
            world.Tiers.Add("base");
        }
        world.CarInfo = value["car_info"];
        return world;
    }
    #endregion

    private static CarLinkValue Text(string value)
    {
        return value == null ? CarLinkValue.Null : CarLinkValue.FromString(value);
    }
}
