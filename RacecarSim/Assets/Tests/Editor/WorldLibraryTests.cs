using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Captured worlds: map_server files read as map_server reads them, written as map_saver writes
/// them, and saved, imported, listed, and reloaded with the same grid hash.
/// </summary>
public class WorldLibraryTests
{
    private string folder;
    private WorldLibrary library;

    [SetUp]
    public void SetUp()
    {
        this.folder = Path.Combine(Path.GetTempPath(), "racecarsim-worlds-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.folder);
        this.library = new WorldLibrary(Path.Combine(this.folder, "Worlds"));
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(this.folder, true);
    }

    /// <summary>
    /// A 6 x 4 grid: occupied border, free inside, one unknown cell at (1, 2).
    /// </summary>
    private static OccupancyGridMsg Grid()
    {
        int width = 6;
        int height = 4;
        sbyte[] data = new sbyte[width * height];
        for (int row = 0; row < height; row++)
        {
            for (int column = 0; column < width; column++)
            {
                bool border = row == 0 || column == 0 || row == height - 1 || column == width - 1;
                data[row * width + column] = border ? (sbyte)100 : (sbyte)0;
            }
        }
        data[2 * width + 1] = -1;
        return new OccupancyGridMsg { Width = width, Height = height, Resolution = 0.05f, OriginX = -1.25, OriginY = 0.5, OriginYaw = 0, Data = data, FrameId = "map" };
    }

    private string WriteFile(string name, string text)
    {
        string path = Path.Combine(this.folder, name);
        File.WriteAllText(path, text);
        return path;
    }

    private string WriteFile(string name, byte[] bytes)
    {
        string path = Path.Combine(this.folder, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    [Test]
    public void SavedMap_ReadsBackIdentical()
    {
        OccupancyGridMsg grid = WorldLibraryTests.Grid();
        string yaml = Path.Combine(this.folder, "map.yaml");
        MapServerFile.Save(grid, yaml, "map.pgm");
        OccupancyGridMsg loaded = MapServerFile.Load(yaml);
        Assert.AreEqual(grid.Width, loaded.Width);
        Assert.AreEqual(grid.Height, loaded.Height);
        Assert.AreEqual(grid.Resolution, loaded.Resolution);
        Assert.AreEqual(grid.OriginX, loaded.OriginX);
        Assert.AreEqual(grid.OriginY, loaded.OriginY);
        CollectionAssert.AreEqual(grid.Data, loaded.Data);
        Assert.AreEqual(MapServerFile.GridHash(grid), MapServerFile.GridHash(loaded));

        // Image rows run top down: the first pixel row is the grid's top row (all occupied)
        (int width, int height, int[] pixels, int maxValue) = MapServerFile.ReadPgm(File.ReadAllBytes(Path.Combine(this.folder, "map.pgm")));
        Assert.AreEqual(255, maxValue);
        Assert.IsTrue(pixels.Take(width).All(p => p == MapServerFile.OccupiedPixel));
        Assert.AreEqual(MapServerFile.UnknownPixel, pixels[(height - 1 - 2) * width + 1], "unknown cell at grid row 2");
        StringAssert.Contains("origin: [-1.25, 0.5, 0]", File.ReadAllText(yaml));

        sbyte[] changed = (sbyte[])grid.Data.Clone();
        changed[8] = 100;
        OccupancyGridMsg other = WorldLibraryTests.Grid();
        other.Data = changed;
        Assert.AreNotEqual(MapServerFile.GridHash(grid), MapServerFile.GridHash(other));
    }

    [Test]
    public void Pixels_ReadAsMapServerReadsThem()
    {
        // Text PGM with a comment: free, the map_saver unknown, a mid gray, occupied
        this.WriteFile("t.pgm", "P2\n# from a test\n4 1\n255\n254 205 128 0\n");
        string trinary = this.WriteFile("t.yaml", "image: t.pgm\nresolution: 0.05\norigin: [0, 0, 0]\noccupied_thresh: 0.65\nfree_thresh: 0.25\n");
        CollectionAssert.AreEqual(new sbyte[] { 0, -1, -1, 100 }, MapServerFile.Load(trinary).Data);

        string lowFree = this.WriteFile("t2.yaml", "image: t.pgm\nresolution: 0.05\norigin: [0, 0, 0]\noccupied_thresh: 0.65\nfree_thresh: 0.196\n");
        CollectionAssert.AreEqual(new sbyte[] { 0, -1, -1, 100 }, MapServerFile.Load(lowFree).Data);

        // Scale: 128 is occupancy 0.498, 99 * (0.498 - 0.25) / 0.4 = 61
        string scale = this.WriteFile("s.yaml", "image: t.pgm\nmode: scale\nresolution: 0.05\norigin: [0, 0, 0]\noccupied_thresh: 0.65\nfree_thresh: 0.25\n");
        CollectionAssert.AreEqual(new sbyte[] { 0, 0, 61, 100 }, MapServerFile.Load(scale).Data);

        string negate = this.WriteFile("n.yaml", "image: t.pgm\nresolution: 0.05\norigin: [0, 0, 0]\nnegate: 1\noccupied_thresh: 0.65\nfree_thresh: 0.25\n");
        CollectionAssert.AreEqual(new sbyte[] { 100, 100, -1, 0 }, MapServerFile.Load(negate).Data);

        this.WriteFile("r.pgm", "P2 3 1 255 0 100 101");
        string raw = this.WriteFile("r.yaml", "image: \"r.pgm\"  # quoted\nmode: raw\nresolution: 0.1\norigin: [0, 0, 0]\n");
        CollectionAssert.AreEqual(new sbyte[] { 0, 100, -1 }, MapServerFile.Load(raw).Data);

        // 16-bit binary
        byte[] header = Encoding.ASCII.GetBytes("P5\n2 1\n65535\n");
        this.WriteFile("w.pgm", header.Concat(new byte[] { 0xFF, 0xFF, 0x00, 0x00 }).ToArray());
        string wide = this.WriteFile("w.yaml", "image: w.pgm\nresolution: 0.05\norigin: [0, 0, 0]\n");
        CollectionAssert.AreEqual(new sbyte[] { 0, 100 }, MapServerFile.Load(wide).Data);
    }

    [Test]
    public void BadMaps_Refused()
    {
        this.WriteFile("p.png", new byte[] { 0x89, 0x50 });
        Assert.Throws<CarLinkFormatException>(() => MapServerFile.Load(this.WriteFile("png.yaml", "image: p.png\nresolution: 0.05\norigin: [0, 0, 0]\n")));
        Assert.Throws<CarLinkFormatException>(() => MapServerFile.Load(this.WriteFile("missing.yaml", "image: none.pgm\nresolution: 0.05\norigin: [0, 0, 0]\n")));
        this.WriteFile("short.pgm", Encoding.ASCII.GetBytes("P5\n4 4\n255\n").Concat(new byte[3]).ToArray());
        Assert.Throws<CarLinkFormatException>(() => MapServerFile.Load(this.WriteFile("short.yaml", "image: short.pgm\nresolution: 0.05\norigin: [0, 0, 0]\n")));
        this.WriteFile("ok.pgm", "P2 1 1 255 254");
        Assert.Throws<CarLinkFormatException>(() => MapServerFile.Load(this.WriteFile("noorigin.yaml", "image: ok.pgm\nresolution: 0.05\n")));
        Assert.Throws<CarLinkFormatException>(() => MapServerFile.Load(this.WriteFile("mode.yaml", "image: ok.pgm\nmode: weird\nresolution: 0.05\norigin: [0, 0, 0]\n")));

        Assert.Throws<CarLinkFormatException>(() => this.library.Import(Path.Combine(this.folder, "png.yaml")));
        Assert.IsFalse(Directory.Exists(this.library.Root) && Directory.GetDirectories(this.library.Root).Length > 0, "nothing left behind");
    }

    [Test]
    public void Import_NamesKeepsAndNeverOverwrites()
    {
        Directory.CreateDirectory(Path.Combine(this.folder, "maps"));
        string yaml = Path.Combine(this.folder, "maps", "lab room.yaml");
        MapServerFile.Save(WorldLibraryTests.Grid(), yaml, "lab room.pgm");

        CapturedWorld first = this.library.Import(yaml);
        Assert.AreEqual("lab room", first.Name);
        Assert.AreEqual("import", first.SourceKind);
        Assert.IsTrue(File.Exists(Path.Combine(first.Folder, CapturedWorld.MapImage)));
        StringAssert.StartsWith("image: map.pgm\n", File.ReadAllText(Path.Combine(first.Folder, CapturedWorld.MapYaml)));
        Assert.AreEqual(MapServerFile.GridHash(WorldLibraryTests.Grid()), first.GridHash);

        CapturedWorld second = this.library.Import(yaml);
        Assert.AreEqual("lab room_2", second.Name);
        CollectionAssert.AreEquivalent(new[] { "lab room", "lab room_2" }, this.library.List().Select(w => w.Name));
    }

    [Test]
    public void SaveFromCar_KeepsTheCarsFilesAndReloadsIdentical()
    {
        string download = Path.Combine(this.folder, "download");
        Directory.CreateDirectory(download);
        OccupancyGridMsg grid = WorldLibraryTests.Grid();
        MapServerFile.Save(grid, Path.Combine(download, "room_1.yaml"), "room_1.pgm");
        File.WriteAllText(Path.Combine(download, "meta.yaml"), "name: room_1\nstart_pose: [0.5, -0.25, 1.5]\nsession: {record: session}\n");
        File.WriteAllBytes(Path.Combine(download, "session.tar"), new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(download, SessionRecorder.FileName), SessionRecorder.Header + "\n");
        RacecarInfo info = RacecarInfo.Parse(CarLinkJson.Parse(
            "{\"link_interface\": \"1.0\", \"driver_version\": \"0.9.1\", \"hostname\": \"racecar-neo\", \"frames\": {\"laser_x_mm\": 238.3}}"));

        CapturedWorld world = this.library.SaveFromCar("room_1", download, "room_1", null, info);
        Assert.AreEqual("room_1", world.Name);
        CollectionAssert.AreEquivalent(
            new[] { "world.json", "map.pgm", "map.yaml", "car-meta.yaml", "car-session.tar", SessionRecorder.FileName },
            Directory.GetFiles(world.Folder).Select(Path.GetFileName));
        CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(download, "room_1.pgm")), File.ReadAllBytes(Path.Combine(world.Folder, "map.pgm")), "car's image kept");

        CapturedWorld listed = this.library.List().Single();
        Assert.AreEqual("car", listed.SourceKind);
        Assert.AreEqual("racecar-neo", listed.CarHostname);
        Assert.AreEqual("0.9.1", listed.DriverVersion);
        Assert.AreEqual("1.0", listed.LinkInterface);
        Assert.AreEqual("room_1", listed.SourceMap);
        Assert.AreEqual(0.5, listed.StartX);
        Assert.AreEqual(-0.25, listed.StartY);
        Assert.AreEqual(1.5, listed.StartYaw);
        Assert.AreEqual(grid.Width, listed.Width);
        Assert.AreEqual(238.3, listed.CarInfo["frames"]["laser_x_mm"].AsDouble(), 1e-9);
        CollectionAssert.AreEqual(new[] { "base" }, listed.Tiers);
        Assert.AreEqual(listed.GridHash, MapServerFile.GridHash(WorldLibrary.LoadGrid(listed)), "reload identical");
        Assert.AreEqual(MapServerFile.GridHash(grid), listed.GridHash);

        // Without a download, the live map
        CapturedWorld live = this.library.SaveFromCar("room_1", null, "room_1", grid, null);
        Assert.AreEqual("room_1_2", live.Name);
        Assert.AreEqual(MapServerFile.GridHash(grid), live.GridHash);
        Assert.Throws<InvalidOperationException>(() => this.library.SaveFromCar("none", null, null, null, null));
    }

    [Test]
    public void List_SkipsBrokenAndUnfinishedFolders()
    {
        this.library.SaveFromCar("good", null, null, WorldLibraryTests.Grid(), null);
        Directory.CreateDirectory(Path.Combine(this.library.Root, "broken"));
        File.WriteAllText(Path.Combine(this.library.Root, "broken", CapturedWorld.FileName), "{ not json");
        Directory.CreateDirectory(Path.Combine(this.library.Root, ".saving-x"));
        Directory.CreateDirectory(Path.Combine(this.library.Root, "newer"));
        File.WriteAllText(Path.Combine(this.library.Root, "newer", CapturedWorld.FileName), "{\"format\": 99, \"name\": \"newer\"}");

        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Worlds: skipped broken"));
        LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Worlds: skipped newer: world format 99"));
        CollectionAssert.AreEqual(new[] { "good" }, this.library.List().Select(w => w.Name));
    }

    [Test]
    public void WorldLevels_OneLevelPerWorldWithThumbnails()
    {
        Assert.AreEqual(0, WorldLevels.Collection(this.library).Levels.Length, "no worlds: an empty tab (import still offered)");
        OccupancyGridMsg grid = WorldLibraryTests.Grid();
        CapturedWorld lab = this.library.SaveFromCar("Lab Room", null, null, grid, null);
        CapturedWorld hall = this.library.SaveFromCar("hall", null, null, grid, null);

        LevelCollection worlds = WorldLevels.Collection(this.library);
        Assert.AreEqual(WorldLevels.CollectionDisplayName, worlds.DisplayName);
        Assert.AreEqual(2, worlds.Levels.Length);
        LevelInfo level = worlds.Levels.Single(each => each.DisplayName == "Lab Room");
        Assert.AreEqual("world-lab-room", level.Id);
        Assert.AreEqual(WorldLevels.BuildIndex, level.BuildIndex);
        Assert.Greater(level.BuildIndex, 0, "CarWorld is in the build");
        CollectionAssert.AreEqual(new[] { LevelManagerMode.Exploration }, level.SupportedModes);
        Assert.AreEqual(1, level.MaxCars);
        Assert.AreSame(level, WorldLevels.LevelFor(lab), "same level for the same world");
        Assert.AreEqual(lab.Folder, WorldLevels.WorldOf(level).Folder);
        Assert.IsNull(WorldLevels.WorldOf(LevelCollection.LevelCollections[0].Levels[0]));
        Assert.IsNotNull(LevelPresentationCatalog.Get(level).Thumbnail, "thumbnail registered");

        Assert.AreEqual(hall.Folder, WorldLevels.Find("hall", this.library).Folder);
        Assert.AreEqual(lab.Folder, WorldLevels.Find("lab-room", this.library).Folder, "by slug");
        Assert.AreEqual(lab.Folder, WorldLevels.Find(lab.Folder, this.library).Folder, "by folder");
        Assert.IsNull(WorldLevels.Find("nowhere", this.library));
        Assert.AreEqual("Lab Room", LaunchOptions.Parse(new[] { "RacecarSim", "-racecarsim-world", "Lab Room" }).World);
    }

    [Test]
    public void Settings_SaveDeleteAndClosedEdges()
    {
        CapturedWorld world = this.library.SaveFromCar("lab", null, null, WorldLibraryTests.Grid(), null);
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(world.Folder, WorldThumbnail.FileName), new byte[] { 1 });
        world.WallHeight = 0.3;
        world.SpeckleCells = 5;
        world.Unknown = CapturedWorld.UnknownClosed;
        WorldLibrary.SaveSettings(world);
        CapturedWorld read = WorldLibrary.Read(world.Folder);
        Assert.AreEqual(0.3, read.WallHeight);
        Assert.AreEqual(5, read.SpeckleCells);
        Assert.AreEqual(CapturedWorld.UnknownClosed, read.Unknown);
        Assert.AreEqual(world.GridHash, read.GridHash, "map untouched");
        Assert.IsFalse(WorldThumbnail.Exists(read), "stale thumbnail removed");
        Assert.IsFalse(System.IO.File.Exists(System.IO.Path.Combine(world.Folder, CapturedWorld.FileName + ".saving")));

        CapturedWorld outside = new CapturedWorld { Name = "x", Folder = this.folder };
        Assert.Throws<InvalidOperationException>(() => this.library.Delete(outside), "outside the library");
        this.library.Delete(world);
        Assert.IsFalse(System.IO.Directory.Exists(world.Folder));
        CollectionAssert.IsEmpty(this.library.List());

        // A free 2 x 1 strip in unknown: its 4-neighbors become walls, diagonals stay unknown
        sbyte[] data = Enumerable.Repeat((sbyte)-1, 5 * 3).ToArray();
        data[1 * 5 + 1] = 0;
        data[1 * 5 + 2] = 0;
        OccupancyGridMsg closed = WorldLevels.CloseUnknownEdges(new OccupancyGridMsg { Width = 5, Height = 3, Resolution = 0.05f, Data = data });
        CollectionAssert.AreEqual(new sbyte[]
        {
            -1, 100, 100, -1, -1,
            100, 0, 0, 100, -1,
            -1, 100, 100, -1, -1,
        }, closed.Data);
        Assert.AreEqual(-1, data[0], "input untouched");
    }

    [Test]
    public void WorldLevels_ThumbnailCropsToKnownCells()
    {
        // 40 x 30 unknown with a known 6 x 4 block at (10, 5): the crop keeps 4 cells of margin
        sbyte[] data = Enumerable.Repeat((sbyte)-1, 40 * 30).ToArray();
        for (int y = 5; y < 9; y++)
        {
            for (int x = 10; x < 16; x++)
            {
                data[y * 40 + x] = (sbyte)(x == 10 ? 100 : 0);
            }
        }
        OccupancyGridMsg grid = new OccupancyGridMsg { Width = 40, Height = 30, Resolution = 0.05f, Data = data };
        Assert.AreEqual(new RectInt(6, 1, 14, 12), WorldLevels.KnownBounds(grid, 4));
        OccupancyGridMsg empty = new OccupancyGridMsg { Width = 5, Height = 3, Resolution = 0.05f, Data = Enumerable.Repeat((sbyte)-1, 15).ToArray() };
        Assert.AreEqual(new RectInt(0, 0, 5, 3), WorldLevels.KnownBounds(empty, 4), "nothing known: whole grid");
    }

    [Test]
    public void Names_CleanedAndIndentedJsonReadsTheSame()
    {
        Assert.AreEqual("lab_1", WorldLibrary.CleanName("lab/1"));
        Assert.AreEqual("world", WorldLibrary.CleanName(" ../ "));
        Assert.AreEqual("world", WorldLibrary.CleanName(null));
        Assert.AreEqual(WorldLibrary.MaxNameLength, WorldLibrary.CleanName(new string('a', 80)).Length);

        CapturedWorld world = new CapturedWorld { Name = "w", Created = "2026-10-23T12:00:00", Resolution = 0.05, Width = 2, Height = 3, GridHash = "ab" };
        string indented = CarLinkJson.WriteIndented(world.ToValue());
        StringAssert.Contains("\n  \"grid\": {\n    \"resolution\": 0.05,", indented);
        Assert.AreEqual(CarLinkJson.Write(world.ToValue()), CarLinkJson.Write(CarLinkJson.Parse(indented)));
        CapturedWorld parsed = CapturedWorld.Parse(CarLinkJson.Parse(indented));
        Assert.AreEqual(3, parsed.Height);
        Assert.AreEqual(CapturedWorld.DefaultWallHeight, parsed.WallHeight);
    }
}
