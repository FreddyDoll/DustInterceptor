using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace DustInterceptor
{
    /// <summary>
    /// Spatial hash grid for efficient broad-phase collision detection.
    /// Avoids O(N) checks by bucketing objects into cells.
    /// </summary>
    public sealed class SpatialHashGrid
    {
        private readonly Dictionary<long, List<int>> _cells = new();
        private readonly List<List<int>> _listPool = new();
        private float _cellSize = 500f;

        public void Clear(float cellSize)
        {
            _cellSize = cellSize;

            // Return lists to pool
            foreach (var kvp in _cells)
            {
                kvp.Value.Clear();
                _listPool.Add(kvp.Value);
            }
            _cells.Clear();
        }

        public void Insert(int index, Vector2 position, float radius)
        {
            // Insert into all cells that the object's bounding box touches
            int minCellX = (int)MathF.Floor((position.X - radius) / _cellSize);
            int maxCellX = (int)MathF.Floor((position.X + radius) / _cellSize);
            int minCellY = (int)MathF.Floor((position.Y - radius) / _cellSize);
            int maxCellY = (int)MathF.Floor((position.Y + radius) / _cellSize);

            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                for (int cy = minCellY; cy <= maxCellY; cy++)
                {
                    long key = GetKey(cx, cy);
                    if (!_cells.TryGetValue(key, out var list))
                    {
                        list = GetListFromPool();
                        _cells[key] = list;
                    }
                    list.Add(index);
                }
            }
        }

        public IEnumerable<int> Query(Vector2 position, float radius)
        {
            // Query all cells that the query circle touches
            int minCellX = (int)MathF.Floor((position.X - radius) / _cellSize);
            int maxCellX = (int)MathF.Floor((position.X + radius) / _cellSize);
            int minCellY = (int)MathF.Floor((position.Y - radius) / _cellSize);
            int maxCellY = (int)MathF.Floor((position.Y + radius) / _cellSize);

            // Track returned indices to avoid duplicates
            HashSet<int>? seen = null;

            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                for (int cy = minCellY; cy <= maxCellY; cy++)
                {
                    long key = GetKey(cx, cy);
                    if (_cells.TryGetValue(key, out var list))
                    {
                        foreach (int idx in list)
                        {
                            // Only use seen set if we're checking multiple cells
                            if (minCellX == maxCellX && minCellY == maxCellY)
                            {
                                yield return idx;
                            }
                            else
                            {
                                seen ??= new HashSet<int>();
                                if (seen.Add(idx))
                                    yield return idx;
                            }
                        }
                    }
                }
            }
        }

        private static long GetKey(int cellX, int cellY)
        {
            // Combine cell coordinates into a single key
            return ((long)cellX << 32) | (uint)cellY;
        }

        private List<int> GetListFromPool()
        {
            if (_listPool.Count > 0)
            {
                var list = _listPool[_listPool.Count - 1];
                _listPool.RemoveAt(_listPool.Count - 1);
                return list;
            }
            return new List<int>();
        }
    }

    /// <summary>
    /// LOD-based spatial hash that maintains multiple levels for different asteroid sizes.
    /// When zoomed out, only larger asteroids are queried from a coarser grid.
    /// </summary>
    public sealed class LodSpatialHash
    {
        /// <summary>
        /// Defines an LOD level with a minimum radius threshold and cell size.
        /// </summary>
        private readonly struct LodLevel
        {
            public readonly float MinRadius;
            public readonly float CellSize;
            public readonly Dictionary<long, List<int>> Cells;
            public readonly List<List<int>> ListPool;

            public LodLevel(float minRadius, float cellSize)
            {
                MinRadius = minRadius;
                CellSize = cellSize;
                Cells = new Dictionary<long, List<int>>();
                ListPool = new List<List<int>>();
            }
        }

        private readonly LodLevel[] _levels;

        /// <summary>
        /// Creates an LOD spatial hash with the specified levels.
        /// Each level is defined by (minRadius, cellSize) pairs.
        /// Levels should be ordered from smallest to largest minRadius.
        /// </summary>
        public LodSpatialHash(params (float minRadius, float cellSize)[] levelDefs)
        {
            _levels = new LodLevel[levelDefs.Length];
            for (int i = 0; i < levelDefs.Length; i++)
            {
                _levels[i] = new LodLevel(levelDefs[i].minRadius, levelDefs[i].cellSize);
            }
        }

        /// <summary>
        /// Clears all LOD levels.
        /// </summary>
        public void Clear()
        {
            foreach (ref readonly var level in _levels.AsSpan())
            {
                foreach (var kvp in level.Cells)
                {
                    kvp.Value.Clear();
                    level.ListPool.Add(kvp.Value);
                }
                level.Cells.Clear();
            }
        }

        /// <summary>
        /// Inserts an asteroid into all applicable LOD levels based on its radius.
        /// </summary>
        public void Insert(int index, Vector2 position, float radius)
        {
            foreach (ref readonly var level in _levels.AsSpan())
            {
                // Only insert into levels where the asteroid meets the minimum radius
                if (radius >= level.MinRadius)
                {
                    InsertIntoLevel(in level, index, position, radius);
                }
            }
        }

        private static void InsertIntoLevel(in LodLevel level, int index, Vector2 position, float radius)
        {
            int minCellX = (int)MathF.Floor((position.X - radius) / level.CellSize);
            int maxCellX = (int)MathF.Floor((position.X + radius) / level.CellSize);
            int minCellY = (int)MathF.Floor((position.Y - radius) / level.CellSize);
            int maxCellY = (int)MathF.Floor((position.Y + radius) / level.CellSize);

            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                for (int cy = minCellY; cy <= maxCellY; cy++)
                {
                    long key = GetKey(cx, cy);
                    if (!level.Cells.TryGetValue(key, out var list))
                    {
                        list = GetListFromPool(level);
                        level.Cells[key] = list;
                    }
                    list.Add(index);
                }
            }
        }

        /// <summary>
        /// Queries asteroids in the given area, using the appropriate LOD level based on minRadius.
        /// </summary>
        /// <param name="position">Center of query area</param>
        /// <param name="queryRadius">Radius of query area</param>
        /// <param name="minAsteroidRadius">Minimum asteroid radius to return (selects LOD level)</param>
        public IEnumerable<int> Query(Vector2 position, float queryRadius, float minAsteroidRadius = 0f)
        {
            // Find the highest LOD level (coarsest) that still includes asteroids >= minAsteroidRadius
            int levelIndex = 0;
            for (int i = _levels.Length - 1; i >= 0; i--)
            {
                if (_levels[i].MinRadius <= minAsteroidRadius)
                {
                    levelIndex = i;
                    break;
                }
            }

            // Get level data (structs are copied, but the Dictionary/List references are shared)
            var level = _levels[levelIndex];
            
            int minCellX = (int)MathF.Floor((position.X - queryRadius) / level.CellSize);
            int maxCellX = (int)MathF.Floor((position.X + queryRadius) / level.CellSize);
            int minCellY = (int)MathF.Floor((position.Y - queryRadius) / level.CellSize);
            int maxCellY = (int)MathF.Floor((position.Y + queryRadius) / level.CellSize);

            HashSet<int>? seen = null;

            for (int cx = minCellX; cx <= maxCellX; cx++)
            {
                for (int cy = minCellY; cy <= maxCellY; cy++)
                {
                    long key = GetKey(cx, cy);
                    if (level.Cells.TryGetValue(key, out var list))
                    {
                        foreach (int idx in list)
                        {
                            if (minCellX == maxCellX && minCellY == maxCellY)
                            {
                                yield return idx;
                            }
                            else
                            {
                                seen ??= new HashSet<int>();
                                if (seen.Add(idx))
                                    yield return idx;
                            }
                        }
                    }
                }
            }
        }

        private static long GetKey(int cellX, int cellY)
        {
            return ((long)cellX << 32) | (uint)cellY;
        }

        private static List<int> GetListFromPool(in LodLevel level)
        {
            if (level.ListPool.Count > 0)
            {
                var list = level.ListPool[^1];
                level.ListPool.RemoveAt(level.ListPool.Count - 1);
                return list;
            }
            return new List<int>();
        }
    }
}
