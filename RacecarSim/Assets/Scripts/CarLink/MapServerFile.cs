using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// A 2D map in the ROS map_server format: a YAML file naming a PGM image, with the cell size,
/// origin, and the thresholds that turn pixels into occupancy.
/// </summary>
/// <remarks>
/// Reads P5 (binary) and P2 (text) PGM images in trinary, scale, and raw modes as map_server
/// does, with one exception: in trinary mode the pixel 205, which map_saver writes for unknown
/// cells, always reads as unknown. map_saver writes free_thresh 0.25, under which 205 would
/// otherwise read as free. Writes trinary maps as map_saver does (occupied 0, free 254,
/// unknown 205).
/// </remarks>
public static class MapServerFile
{
    #region Constants
    /// <summary>
    /// The pixel map_saver writes for unknown cells.
    /// </summary>
    public const byte UnknownPixel = 205;

    public const byte FreePixel = 254;
    public const byte OccupiedPixel = 0;

    /// <summary>
    /// Thresholds written with a map (fractions of full occupancy).
    /// </summary>
    public const double OccupiedThreshold = 0.65;
    public const double FreeThreshold = 0.25;
    #endregion

    #region Public Interface
    /// <summary>
    /// Reads a map from its YAML file; the image path is relative to the YAML's folder.
    /// </summary>
    /// <exception cref="CarLinkFormatException">The YAML or image cannot be read as a map.</exception>
    public static OccupancyGridMsg Load(string yamlPath)
    {
        Dictionary<string, string> yaml = MapServerFile.ParseYaml(File.ReadAllText(yamlPath));
        string image = MapServerFile.Required(yaml, "image").Trim('"', '\'');
        string imagePath = Path.IsPathRooted(image) ? image : Path.Combine(Path.GetDirectoryName(yamlPath) ?? string.Empty, image);
        if (!string.Equals(Path.GetExtension(imagePath), ".pgm", StringComparison.OrdinalIgnoreCase))
        {
            throw new CarLinkFormatException($"'{image}' is not a .pgm image");
        }
        if (!File.Exists(imagePath))
        {
            throw new CarLinkFormatException($"'{image}' not found beside the YAML file");
        }
        double[] origin = MapServerFile.ParseNumbers(MapServerFile.Required(yaml, "origin"));
        if (origin.Length != 3)
        {
            throw new CarLinkFormatException("origin must hold x, y, and yaw");
        }
        Settings settings = new Settings
        {
            Mode = yaml.TryGetValue("mode", out string mode) ? mode.Trim('"', '\'') : "trinary",
            Negate = yaml.TryGetValue("negate", out string negate) && MapServerFile.ParseNumber(negate) != 0,
            OccupiedThreshold = yaml.TryGetValue("occupied_thresh", out string occupied) ? MapServerFile.ParseNumber(occupied) : MapServerFile.OccupiedThreshold,
            FreeThreshold = yaml.TryGetValue("free_thresh", out string free) ? MapServerFile.ParseNumber(free) : MapServerFile.FreeThreshold,
        };
        if (settings.Mode != "trinary" && settings.Mode != "scale" && settings.Mode != "raw")
        {
            throw new CarLinkFormatException($"unknown map mode '{settings.Mode}'");
        }
        float resolution = (float)MapServerFile.ParseNumber(MapServerFile.Required(yaml, "resolution"));
        if (!(resolution > 0))
        {
            throw new CarLinkFormatException($"resolution {resolution} is not positive");
        }

        (int width, int height, int[] pixels, int maxValue) = MapServerFile.ReadPgm(File.ReadAllBytes(imagePath));
        sbyte[] data = new sbyte[width * height];
        for (int row = 0; row < height; row++)
        {
            // Image rows run top down; grid rows run from the origin up
            int gridRow = height - 1 - row;
            for (int column = 0; column < width; column++)
            {
                data[gridRow * width + column] = MapServerFile.Cell(pixels[row * width + column], maxValue, settings);
            }
        }
        return new OccupancyGridMsg
        {
            FrameId = "map",
            Resolution = resolution,
            Width = width,
            Height = height,
            OriginX = origin[0],
            OriginY = origin[1],
            OriginYaw = origin[2],
            Data = data,
        };
    }

    /// <summary>
    /// Writes a grid as a trinary map: imageName beside the YAML file.
    /// </summary>
    public static void Save(OccupancyGridMsg grid, string yamlPath, string imageName)
    {
        string folder = Path.GetDirectoryName(yamlPath) ?? string.Empty;
        byte[] header = Encoding.ASCII.GetBytes($"P5\n{grid.Width} {grid.Height}\n255\n");
        byte[] image = new byte[header.Length + grid.Width * grid.Height];
        header.CopyTo(image, 0);
        int occupied = (int)Math.Round(MapServerFile.OccupiedThreshold * 100);
        int free = (int)Math.Round(MapServerFile.FreeThreshold * 100);
        for (int row = 0; row < grid.Height; row++)
        {
            int gridRow = grid.Height - 1 - row;
            for (int column = 0; column < grid.Width; column++)
            {
                int value = grid.Data[gridRow * grid.Width + column];
                byte pixel = value < 0 || value > 100 ? MapServerFile.UnknownPixel
                    : value >= occupied ? MapServerFile.OccupiedPixel
                    : value <= free ? MapServerFile.FreePixel
                    : MapServerFile.UnknownPixel;
                image[header.Length + row * grid.Width + column] = pixel;
            }
        }
        File.WriteAllBytes(Path.Combine(folder, imageName), image);
        File.WriteAllText(yamlPath, MapServerFile.Yaml(grid, imageName), new UTF8Encoding(false));
    }

    /// <summary>
    /// The YAML for a trinary map written by Save.
    /// </summary>
    public static string Yaml(OccupancyGridMsg grid, string imageName)
    {
        return $"image: {imageName}\n"
            + "mode: trinary\n"
            + $"resolution: {MapServerFile.Number(grid.Resolution)}\n"
            + $"origin: [{MapServerFile.Number(grid.OriginX)}, {MapServerFile.Number(grid.OriginY)}, {MapServerFile.Number(grid.OriginYaw)}]\n"
            + "negate: 0\n"
            + $"occupied_thresh: {MapServerFile.Number(MapServerFile.OccupiedThreshold)}\n"
            + $"free_thresh: {MapServerFile.Number(MapServerFile.FreeThreshold)}\n";
    }

    /// <summary>
    /// A YAML file's top-level "key: value" pairs, values as written (comments and nested
    /// blocks skipped).
    /// </summary>
    public static Dictionary<string, string> ParseYaml(string text)
    {
        Dictionary<string, string> values = new Dictionary<string, string>();
        foreach (string rawLine in text.Split('\n'))
        {
            string line = rawLine.TrimEnd('\r');
            if (line.Length == 0 || char.IsWhiteSpace(line[0]) || line[0] == '#' || line[0] == '-')
            {
                continue;
            }
            int colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }
            string value = line.Substring(colon + 1);
            int comment = value.IndexOf(" #", StringComparison.Ordinal);
            if (comment >= 0)
            {
                value = value.Substring(0, comment);
            }
            values[line.Substring(0, colon).Trim()] = value.Trim();
        }
        return values;
    }

    /// <summary>
    /// The numbers of a YAML flow list such as "[1.0, -2, 0.5]".
    /// </summary>
    public static double[] ParseNumbers(string text)
    {
        string inner = text.Trim();
        if (inner.Length < 2 || inner[0] != '[' || inner[inner.Length - 1] != ']')
        {
            throw new CarLinkFormatException($"'{text}' is not a list");
        }
        inner = inner.Substring(1, inner.Length - 2);
        if (inner.Trim().Length == 0)
        {
            return new double[0];
        }
        string[] parts = inner.Split(',');
        double[] numbers = new double[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            numbers[i] = MapServerFile.ParseNumber(parts[i]);
        }
        return numbers;
    }

    /// <summary>
    /// SHA-256 of a grid's size, cell size, origin, and cells, as lowercase hex; equal grids
    /// give equal hashes.
    /// </summary>
    public static string GridHash(OccupancyGridMsg grid)
    {
        using (SHA256 sha = SHA256.Create())
        {
            byte[] header = Encoding.ASCII.GetBytes(string.Join(",",
                grid.Width.ToString(CultureInfo.InvariantCulture),
                grid.Height.ToString(CultureInfo.InvariantCulture),
                MapServerFile.Number(grid.Resolution),
                MapServerFile.Number(grid.OriginX),
                MapServerFile.Number(grid.OriginY),
                MapServerFile.Number(grid.OriginYaw)) + "\n");
            sha.TransformBlock(header, 0, header.Length, null, 0);
            byte[] cells = new byte[grid.Data.Length];
            Buffer.BlockCopy(grid.Data, 0, cells, 0, cells.Length);
            sha.TransformFinalBlock(cells, 0, cells.Length);
            StringBuilder builder = new StringBuilder(64);
            foreach (byte b in sha.Hash)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }

    /// <summary>
    /// A PGM image's size, pixels (row by row from the top), and maximum value.
    /// </summary>
    public static (int width, int height, int[] pixels, int maxValue) ReadPgm(byte[] file)
    {
        int position = 0;
        string magic = MapServerFile.Token(file, ref position);
        if (magic != "P5" && magic != "P2")
        {
            throw new CarLinkFormatException("not a PGM image");
        }
        int width = MapServerFile.IntToken(file, ref position);
        int height = MapServerFile.IntToken(file, ref position);
        int maxValue = MapServerFile.IntToken(file, ref position);
        if (width <= 0 || height <= 0 || maxValue <= 0 || maxValue > 65535 || (long)width * height > int.MaxValue / 2)
        {
            throw new CarLinkFormatException($"PGM size {width} x {height} or maximum {maxValue} is out of range");
        }
        int[] pixels = new int[width * height];
        if (magic == "P2")
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = MapServerFile.IntToken(file, ref position);
            }
            return (width, height, pixels, maxValue);
        }
        // One whitespace byte separates the header from the binary pixels
        position++;
        int bytesPerPixel = maxValue > 255 ? 2 : 1;
        if (file.Length - position < (long)pixels.Length * bytesPerPixel)
        {
            throw new CarLinkFormatException("PGM image is shorter than its size");
        }
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = bytesPerPixel == 1 ? file[position + i] : (file[position + 2 * i] << 8) | file[position + 2 * i + 1];
        }
        return (width, height, pixels, maxValue);
    }
    #endregion

    private struct Settings
    {
        public string Mode;
        public bool Negate;
        public double OccupiedThreshold;
        public double FreeThreshold;
    }

    private static sbyte Cell(int pixel, int maxValue, Settings settings)
    {
        if (settings.Mode == "raw")
        {
            return pixel <= 100 ? (sbyte)pixel : (sbyte)-1;
        }
        if (settings.Mode == "trinary" && maxValue == 255 && pixel == MapServerFile.UnknownPixel && !settings.Negate)
        {
            return -1;
        }
        double shade = (double)pixel / maxValue;
        double occupancy = settings.Negate ? shade : 1 - shade;
        if (occupancy > settings.OccupiedThreshold)
        {
            return 100;
        }
        if (occupancy < settings.FreeThreshold)
        {
            return 0;
        }
        if (settings.Mode == "trinary")
        {
            return -1;
        }
        return (sbyte)Math.Round(99 * (occupancy - settings.FreeThreshold) / (settings.OccupiedThreshold - settings.FreeThreshold));
    }

    private static string Required(Dictionary<string, string> yaml, string key)
    {
        if (!yaml.TryGetValue(key, out string value) || value.Length == 0)
        {
            throw new CarLinkFormatException($"map YAML has no '{key}'");
        }
        return value;
    }

    private static double ParseNumber(string text)
    {
        if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            throw new CarLinkFormatException($"'{text.Trim()}' is not a number");
        }
        return value;
    }

    private static string Number(double value)
    {
        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// The next whitespace-separated header token, skipping '#' comments.
    /// </summary>
    private static string Token(byte[] file, ref int position)
    {
        while (position < file.Length)
        {
            if (file[position] == '#')
            {
                while (position < file.Length && file[position] != '\n')
                {
                    position++;
                }
            }
            else if (char.IsWhiteSpace((char)file[position]))
            {
                position++;
            }
            else
            {
                break;
            }
        }
        int start = position;
        while (position < file.Length && !char.IsWhiteSpace((char)file[position]) && file[position] != '#')
        {
            position++;
        }
        if (start == position)
        {
            throw new CarLinkFormatException("PGM header ends early");
        }
        return Encoding.ASCII.GetString(file, start, position - start);
    }

    private static int IntToken(byte[] file, ref int position)
    {
        string token = MapServerFile.Token(file, ref position);
        if (!int.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out int value))
        {
            throw new CarLinkFormatException($"'{token}' is not a PGM number");
        }
        return value;
    }
}
