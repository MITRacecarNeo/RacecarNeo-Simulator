using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Occupancy tiles, rectangle merging, the speckle filter, wall meshes, floor colors, and scan
/// scoring for the car world.
/// </summary>
public class CarWorldBuildTests
{
    /// <summary>
    /// A grid with every cell set to a value.
    /// </summary>
    private static OccupancyGridMsg Grid(int width, int height, double originX, double originY, sbyte fill = 0)
    {
        sbyte[] data = new sbyte[width * height];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = fill;
        }
        return new OccupancyGridMsg { Width = width, Height = height, Resolution = 0.05f, OriginX = originX, OriginY = originY, Data = data };
    }

    private static void Set(OccupancyGridMsg grid, int column, int row, sbyte value)
    {
        grid.Data[row * grid.Width + column] = value;
    }

    [Test]
    public void Merge_CoversExactlyTheOccupiedCells()
    {
        System.Random random = new System.Random(7);
        for (int trial = 0; trial < 20; trial++)
        {
            bool[,] mask = new bool[24, 24];
            for (int c = 0; c < 24; c++)
            {
                for (int r = 0; r < 24; r++)
                {
                    mask[c, r] = random.NextDouble() < 0.4;
                }
            }
            int[,] covered = new int[24, 24];
            foreach (CellRect rect in OccupancyTiles.Merge(mask))
            {
                for (int c = rect.MinColumn; c <= rect.MaxColumn; c++)
                {
                    for (int r = rect.MinRow; r <= rect.MaxRow; r++)
                    {
                        covered[c, r]++;
                    }
                }
            }
            for (int c = 0; c < 24; c++)
            {
                for (int r = 0; r < 24; r++)
                {
                    Assert.AreEqual(mask[c, r] ? 1 : 0, covered[c, r], $"cell {c},{r} in trial {trial}");
                }
            }
        }
    }

    [Test]
    public void Merge_AWallIsOneRectangle()
    {
        bool[,] mask = new bool[10, 10];
        for (int c = 0; c < 10; c++)
        {
            mask[c, 3] = true;
            mask[c, 4] = true;
        }
        List<CellRect> rects = OccupancyTiles.Merge(mask);
        Assert.AreEqual(1, rects.Count);
        Assert.AreEqual(new CellRect(0, 3, 9, 4), rects[0]);
    }

    [Test]
    public void DropSmallComponents_RemovesSpecks()
    {
        bool[,] mask = new bool[8, 8];
        mask[1, 1] = true;
        mask[5, 5] = true;
        mask[5, 6] = true;
        mask[3, 3] = true;
        mask[4, 4] = true;
        OccupancyTiles.DropSmallComponents(mask, 2);
        Assert.IsFalse(mask[1, 1], "single cell dropped");
        Assert.IsFalse(mask[3, 3], "diagonal neighbors are separate components");
        Assert.IsTrue(mask[5, 5] && mask[5, 6], "two-cell component kept");
    }

    [Test]
    public void Fingerprints_OneChangedCellRebuildsOneTile()
    {
        OccupancyGridMsg before = CarWorldBuildTests.Grid(200, 200, -5, -5);
        for (int c = 0; c < 200; c++)
        {
            CarWorldBuildTests.Set(before, c, 10, 100);
        }
        OccupancyGridMsg after = CarWorldBuildTests.Grid(200, 200, -5, -5);
        Array.Copy(before.Data, after.Data, before.Data.Length);
        CarWorldBuildTests.Set(after, 150, 150, 100);
        HashSet<(int x, int y)> changed = OccupancyTiles.Changed(OccupancyTiles.Fingerprints(before), OccupancyTiles.Fingerprints(after));
        Assert.AreEqual(1, changed.Count);
        // Absolute cell (origin -100 + 150 = 50) is in tile 0
        Assert.IsTrue(changed.Contains((0, 0)));
    }

    [Test]
    public void Fingerprints_GrowingMapKeepsItsTiles()
    {
        OccupancyGridMsg first = CarWorldBuildTests.Grid(100, 100, 0, 0);
        CarWorldBuildTests.Set(first, 20, 30, 100);
        CarWorldBuildTests.Set(first, 21, 30, 100);
        // The same map grown 40 cells to the left and 10 down: origin moves, cells stay put
        OccupancyGridMsg grown = CarWorldBuildTests.Grid(140, 110, -2.0, -0.5);
        CarWorldBuildTests.Set(grown, 60, 40, 100);
        CarWorldBuildTests.Set(grown, 61, 40, 100);
        Assert.AreEqual(0, OccupancyTiles.Changed(OccupancyTiles.Fingerprints(first), OccupancyTiles.Fingerprints(grown)).Count);
    }

    [Test]
    public void Fingerprints_EmptiedTileIsChanged()
    {
        OccupancyGridMsg before = CarWorldBuildTests.Grid(64, 64, 0, 0);
        CarWorldBuildTests.Set(before, 5, 5, 100);
        OccupancyGridMsg after = CarWorldBuildTests.Grid(64, 64, 0, 0);
        Assert.IsTrue(OccupancyTiles.Changed(OccupancyTiles.Fingerprints(before), OccupancyTiles.Fingerprints(after)).Contains((0, 0)));
    }

    [Test]
    public void TileRects_UseAbsoluteCells()
    {
        OccupancyGridMsg grid = CarWorldBuildTests.Grid(20, 20, -0.5, -0.5);
        for (int c = 2; c < 6; c++)
        {
            CarWorldBuildTests.Set(grid, c, 7, 100);
        }
        CarWorldBuildTests.Set(grid, 15, 15, 64);
        List<CellRect> rects = OccupancyTiles.TileRects(grid, (-1, -1), 1);
        Assert.AreEqual(1, rects.Count, "64 is below the occupied threshold");
        // Origin cell -10: columns 2..5 are absolute -8..-5, row 7 is -3
        Assert.AreEqual(new CellRect(-8, -3, -5, -3), rects[0]);
    }

    [Test]
    public void WallMesh_PlacesTheBoxInSimUnits()
    {
        Mesh mesh = CarWorld_WallMesh(new CellRect(0, 0, 1, 0));
        Bounds bounds = mesh.bounds;
        // Cells x 0..0.1 m, y 0..0.05 m: sim Z 0..1, X -0.5..0; ground 2, 1 m high
        Assert.AreEqual(0, bounds.min.z, 1e-4);
        Assert.AreEqual(1, bounds.max.z, 1e-4);
        Assert.AreEqual(-0.5f, bounds.min.x, 1e-4);
        Assert.AreEqual(0, bounds.max.x, 1e-4);
        Assert.AreEqual(2, bounds.min.y, 1e-4);
        Assert.AreEqual(12, bounds.max.y, 1e-4);
        UnityEngine.Object.DestroyImmediate(mesh);
    }

    private static Mesh CarWorld_WallMesh(CellRect rect)
    {
        return CarWorldMap.WallMesh(new List<CellRect> { rect }, 0.05f, 2, 10);
    }

    [Test]
    public void FloorColor_ByOccupancy()
    {
        Assert.AreEqual(0, CarWorldMap.FloorColor(-1).a, "unknown is clear");
        Assert.Greater(CarWorldMap.FloorColor(0).r, CarWorldMap.FloorColor(100).r, "free lighter than occupied");
        Assert.Greater(CarWorldMap.FloorColor(100).a, CarWorldMap.FloorColor(0).a);
    }

    [Test]
    public void ScanScore_CountsReturnsWithinTolerance()
    {
        LaserScanMsg scan = new LaserScanMsg { RangeMin = 0.15f, RangeMax = 12, AngleMin = 0, AngleIncrement = 0.01f, Ranges = new float[16] };
        float[] sim = new float[16];
        for (int i = 0; i < 16; i++)
        {
            scan.Ranges[i] = 2;
            sim[i] = 2;
        }
        // Compared beams: 0, 4, 8, 12
        sim[4] = 2.09f;
        sim[8] = 2.2f;
        scan.Ranges[12] = float.PositiveInfinity;
        float score = ScanMatch.Score(scan, sim, out int compared);
        Assert.AreEqual(3, compared, "infinite return not compared");
        Assert.AreEqual(2f / 3, score, 1e-6);

        scan.Ranges = new[] { float.NaN };
        Assert.IsTrue(float.IsNaN(ScanMatch.Score(scan, new[] { 1f }, out compared)));
    }

    [Test]
    public void LaserPose_AppliesTheMount()
    {
        (double x, double y, double yaw) laser = ScanMatch.LaserPose(new CarPose(1, 2, Math.PI / 2), 0.2383, 0, Math.PI);
        Assert.AreEqual(1, laser.x, 1e-9);
        Assert.AreEqual(2.2383, laser.y, 1e-9);
        Assert.AreEqual(1.5 * Math.PI, laser.yaw, 1e-9);
    }

    [Test]
    public void WorldText()
    {
        Assert.AreEqual("-", CarWorld.WorldText(null));
    }
}
