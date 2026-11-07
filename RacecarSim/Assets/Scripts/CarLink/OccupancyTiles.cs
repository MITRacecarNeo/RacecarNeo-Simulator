using System;
using System.Collections.Generic;

/// <summary>
/// A run of occupied cells merged into one rectangle, in absolute cell indices (inclusive).
/// </summary>
public struct CellRect
{
    public int MinColumn;
    public int MinRow;
    public int MaxColumn;
    public int MaxRow;

    public CellRect(int minColumn, int minRow, int maxColumn, int maxRow)
    {
        this.MinColumn = minColumn;
        this.MinRow = minRow;
        this.MaxColumn = maxColumn;
        this.MaxRow = maxRow;
    }
}

/// <summary>
/// Splits an occupancy grid into world-aligned tiles of cells and turns each tile's occupied
/// cells into a few merged rectangles, for wall meshes and colliders.
/// </summary>
/// <remarks>
/// Cells are addressed by absolute index (grid origin / resolution + column), so a map that grows
/// and moves its origin by whole cells keeps its tiles; only tiles whose cells changed are rebuilt.
/// Thresholds follow the map server: occupied at 65 and above, free at 25 and below.
/// </remarks>
public static class OccupancyTiles
{
    #region Constants
    /// <summary>
    /// Tile edge in cells (3.2 m at 5 cm).
    /// </summary>
    public const int TileSize = 64;

    public const int OccupiedThreshold = 65;

    public const int FreeThreshold = 25;
    #endregion

    #region Public Interface
    /// <summary>
    /// Absolute index of cell 0 along an axis: the origin in cells, rounded.
    /// </summary>
    public static int OriginCell(double origin, float resolution)
    {
        return (int)Math.Round(origin / resolution);
    }

    /// <summary>
    /// The tile holding an absolute cell index.
    /// </summary>
    public static int TileOf(int cell)
    {
        return (int)Math.Floor(cell / (double)OccupancyTiles.TileSize);
    }

    /// <summary>
    /// True for an occupied cell value.
    /// </summary>
    public static bool IsOccupied(sbyte value)
    {
        return value >= OccupancyTiles.OccupiedThreshold;
    }

    /// <summary>
    /// A fingerprint of every tile's occupied cells, keyed by tile; compare two to find changed tiles.
    /// </summary>
    public static Dictionary<(int x, int y), long> Fingerprints(OccupancyGridMsg grid)
    {
        int originColumn = OccupancyTiles.OriginCell(grid.OriginX, grid.Resolution);
        int originRow = OccupancyTiles.OriginCell(grid.OriginY, grid.Resolution);
        Dictionary<(int x, int y), long> prints = new Dictionary<(int x, int y), long>();
        for (int row = 0; row < grid.Height; row++)
        {
            int absoluteRow = originRow + row;
            for (int column = 0; column < grid.Width; column++)
            {
                if (!OccupancyTiles.IsOccupied(grid.Data[row * grid.Width + column]))
                {
                    continue;
                }
                int absoluteColumn = originColumn + column;
                (int x, int y) key = (OccupancyTiles.TileOf(absoluteColumn), OccupancyTiles.TileOf(absoluteRow));
                // Order-independent mix of the cell's position
                long cell = ((long)absoluteColumn * 73856093) ^ ((long)absoluteRow * 19349663);
                prints.TryGetValue(key, out long print);
                prints[key] = print + (cell * 2654435761L) + 1;
            }
        }
        return prints;
    }

    /// <summary>
    /// Tiles whose occupied cells differ between two fingerprint sets, including tiles that emptied.
    /// </summary>
    public static HashSet<(int x, int y)> Changed(Dictionary<(int x, int y), long> before, Dictionary<(int x, int y), long> after)
    {
        HashSet<(int x, int y)> changed = new HashSet<(int x, int y)>();
        foreach (KeyValuePair<(int x, int y), long> entry in after)
        {
            if (!before.TryGetValue(entry.Key, out long old) || old != entry.Value)
            {
                changed.Add(entry.Key);
            }
        }
        foreach ((int x, int y) key in before.Keys)
        {
            if (!after.ContainsKey(key))
            {
                changed.Add(key);
            }
        }
        return changed;
    }

    /// <summary>
    /// The occupied cells of one tile as merged rectangles (absolute indices), after dropping
    /// occupied components smaller than minComponent cells (single-cell noise).
    /// </summary>
    public static List<CellRect> TileRects(OccupancyGridMsg grid, (int x, int y) tile, int minComponent)
    {
        int size = OccupancyTiles.TileSize;
        bool[,] occupied = new bool[size, size];
        int originColumn = OccupancyTiles.OriginCell(grid.OriginX, grid.Resolution);
        int originRow = OccupancyTiles.OriginCell(grid.OriginY, grid.Resolution);
        int firstColumn = tile.x * size;
        int firstRow = tile.y * size;
        bool any = false;
        for (int r = 0; r < size; r++)
        {
            int row = firstRow + r - originRow;
            if (row < 0 || row >= grid.Height)
            {
                continue;
            }
            for (int c = 0; c < size; c++)
            {
                int column = firstColumn + c - originColumn;
                if (column >= 0 && column < grid.Width && OccupancyTiles.IsOccupied(grid.Data[row * grid.Width + column]))
                {
                    occupied[c, r] = true;
                    any = true;
                }
            }
        }
        if (!any)
        {
            return new List<CellRect>();
        }
        if (minComponent > 1)
        {
            OccupancyTiles.DropSmallComponents(occupied, minComponent);
        }
        List<CellRect> rects = OccupancyTiles.Merge(occupied);
        for (int i = 0; i < rects.Count; i++)
        {
            CellRect rect = rects[i];
            rects[i] = new CellRect(rect.MinColumn + firstColumn, rect.MinRow + firstRow, rect.MaxColumn + firstColumn, rect.MaxRow + firstRow);
        }
        return rects;
    }

    /// <summary>
    /// Greedy rectangle cover of a cell mask (indices within the mask): each rectangle grows along
    /// columns, then rows, over cells not yet covered. Rectangles do not overlap.
    /// </summary>
    public static List<CellRect> Merge(bool[,] occupied)
    {
        int columns = occupied.GetLength(0);
        int rows = occupied.GetLength(1);
        bool[,] used = new bool[columns, rows];
        List<CellRect> rects = new List<CellRect>();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (!occupied[column, row] || used[column, row])
                {
                    continue;
                }
                int maxColumn = column;
                while (maxColumn + 1 < columns && occupied[maxColumn + 1, row] && !used[maxColumn + 1, row])
                {
                    maxColumn++;
                }
                int maxRow = row;
                while (maxRow + 1 < rows && OccupancyTiles.RowFree(occupied, used, column, maxColumn, maxRow + 1))
                {
                    maxRow++;
                }
                for (int r = row; r <= maxRow; r++)
                {
                    for (int c = column; c <= maxColumn; c++)
                    {
                        used[c, r] = true;
                    }
                }
                rects.Add(new CellRect(column, row, maxColumn, maxRow));
            }
        }
        return rects;
    }

    /// <summary>
    /// Clears 4-connected occupied components with fewer than minimum cells.
    /// </summary>
    public static void DropSmallComponents(bool[,] occupied, int minimum)
    {
        int columns = occupied.GetLength(0);
        int rows = occupied.GetLength(1);
        bool[,] seen = new bool[columns, rows];
        List<(int c, int r)> component = new List<(int c, int r)>();
        Stack<(int c, int r)> stack = new Stack<(int c, int r)>();
        for (int row = 0; row < rows; row++)
        {
            for (int column = 0; column < columns; column++)
            {
                if (!occupied[column, row] || seen[column, row])
                {
                    continue;
                }
                component.Clear();
                stack.Push((column, row));
                seen[column, row] = true;
                while (stack.Count > 0)
                {
                    (int c, int r) cell = stack.Pop();
                    component.Add(cell);
                    foreach ((int dc, int dr) in OccupancyTiles.neighbors)
                    {
                        int nc = cell.c + dc;
                        int nr = cell.r + dr;
                        if (nc >= 0 && nr >= 0 && nc < columns && nr < rows && occupied[nc, nr] && !seen[nc, nr])
                        {
                            seen[nc, nr] = true;
                            stack.Push((nc, nr));
                        }
                    }
                }
                if (component.Count < minimum)
                {
                    foreach ((int c, int r) in component)
                    {
                        occupied[c, r] = false;
                    }
                }
            }
        }
    }
    #endregion

    private static readonly (int dc, int dr)[] neighbors = { (1, 0), (-1, 0), (0, 1), (0, -1) };

    private static bool RowFree(bool[,] occupied, bool[,] used, int minColumn, int maxColumn, int row)
    {
        for (int column = minColumn; column <= maxColumn; column++)
        {
            if (!occupied[column, row] || used[column, row])
            {
                return false;
            }
        }
        return true;
    }
}
