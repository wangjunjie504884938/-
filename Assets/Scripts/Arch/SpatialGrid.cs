using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Arch
{
    /// <summary>
    /// 空间网格索引 (规划阶段07 AOI) — 纯 C# 无 Unity 依赖, 三端共享。
    /// 实体按 Chunk(格子) 索引: Move 更新归属, QueryCircle 返回圆内实体Id。
    /// 线程安全: ConcurrentDictionary。非通用物理引擎 — 只服务 AOI 兴趣过滤。
    /// </summary>
    public class SpatialGrid
    {
        private readonly float _cellSize;
        private readonly ConcurrentDictionary<long, (float x, float y)> _positions = new();
        private readonly ConcurrentDictionary<long, long> _cells = new();     // entityId → cellKey
        private readonly ConcurrentDictionary<long, HashSet<long>> _cellMembers = new(); // cellKey → ids

        public SpatialGrid(float cellSize) => _cellSize = Math.Max(0.1f, cellSize);

        private long CellKey(float x, float y)
        {
            long cx = (long)Math.Floor(x / _cellSize);
            long cy = (long)Math.Floor(y / _cellSize);
            return (cx << 32) ^ (uint)cy;
        }

        /// <summary>插入或更新实体位置 (AOI 观察者与被观察实体统一走此入口)</summary>
        public void Move(long id, float x, float y)
        {
            var newKey = CellKey(x, y);
            _positions[id] = (x, y);

            if (_cells.TryGetValue(id, out var oldKey))
            {
                if (oldKey == newKey) return;
                if (_cellMembers.TryGetValue(oldKey, out var oldSet))
                {
                    oldSet.Remove(id);
                    if (oldSet.Count == 0) _cellMembers.TryRemove(oldKey, out _);
                }
            }
            _cells[id] = newKey;
            var set = _cellMembers.GetOrAdd(newKey, _ => new HashSet<long>());
            lock (set) set.Add(id);
        }

        /// <summary>移除实体</summary>
        public void Remove(long id)
        {
            _positions.TryRemove(id, out var pos);
            if (_cells.TryRemove(id, out var oldKey) && _cellMembers.TryGetValue(oldKey, out var set))
            {
                lock (set) set.Remove(id);
                if (set.Count == 0) _cellMembers.TryRemove(oldKey, out _);
            }
        }

        public bool TryGet(long id, out float x, float y)
        {
            if (_positions.TryGetValue(id, out var p)) { x = p.x; y = p.y; y = p.y; return true; }
            x = y = 0; return false;
        }

        /// <summary>圆形范围查询 (AOI): 返回圆内实体Id (含边界容差一格)</summary>
        public HashSet<long> QueryCircle(float x, float y, float radius)
        {
            var result = new HashSet<long>();
            long minX = (long)Math.Floor((x - radius) / _cellSize);
            long maxX = (long)Math.Floor((x + radius) / _cellSize);
            long minY = (long)Math.Floor((y - radius) / _cellSize);
            long maxY = (long)Math.Floor((y + radius) / _cellSize);
            float rSq = radius * radius;

            for (long cx = minX; cx <= maxX; cx++)
            for (long cy = minY; cy <= maxY; cy++)
            {
                var key = (cx << 32) ^ (uint)cy;
                if (!_cellMembers.TryGetValue(key, out var set)) continue;
                lock (set)
                {
                    foreach (var id in set)
                    {
                        if (_positions.TryGetValue(id, out var p))
                        {
                            float dx = p.x - x, dy = p.y - y;
                            if (dx * dx + dy * dy <= rSq) result.Add(id);
                        }
                    }
                }
            }
            return result;
        }

        public int EntityCount => _positions.Count;
    }
}
