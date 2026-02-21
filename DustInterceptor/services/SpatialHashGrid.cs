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
}
