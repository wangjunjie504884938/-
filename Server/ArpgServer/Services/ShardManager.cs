using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Arch;

namespace ArpgServer.Services
{
    /// <summary>
    /// 分线管理器 (规划阶段04) — Dictionary&lt;int, LineData&gt;, 最少人数可用线分配。
    /// 规则: Healthy && Accepting && PlayerCount &lt; Capacity 的线中取 PlayerCount 最小者;
    ///       人数相同取 LineId 升序 (确定性规则)。
    /// 占位容量: 单线 50 (用户未定最终值, 阶段04验收用 50 验证 51 Bot 分线)。
    /// </summary>
    public class ShardManager
    {
        public const int DefaultLineCapacity = 50;
        public const int PreAllocatedLines = 4; // 预留4线 (用户占位值)

        private readonly ConcurrentDictionary<int, LineData> _lines = new();

        public ShardManager()
        {
            for (int i = 1; i <= PreAllocatedLines; i++)
                _lines[i] = new LineData { LineId = i, Capacity = DefaultLineCapacity };
        }

        public IReadOnlyCollection<LineData> Lines => _lines.Values.ToList().AsReadOnly();

        public LineData Get(int lineId) => _lines.TryGetValue(lineId, out var l) ? l : null;

        /// <summary>分配最少人数可用线; 全满/全不健康返回 null (触发阶段11动态开线)</summary>
        public LineData AllocateLine()
        {
            return _lines.Values
                .Where(l => l.Healthy && l.Accepting && l.PlayerCount < l.Capacity)
                .OrderBy(l => l.PlayerCount)
                .ThenBy(l => l.LineId)
                .FirstOrDefault();
        }

        /// <summary>玩家加入线 (容量原子校验)</summary>
        public bool TryJoin(int lineId)
        {
            var line = Get(lineId);
            if (line == null || !line.Healthy || !line.Accepting) return false;
            if (line.PlayerCount >= line.Capacity) return false;
            line.PlayerCount++;
            return true;
        }

        /// <summary>玩家离开线 (断线/登出/被踢 — 计数修正)</summary>
        public void Leave(int lineId)
        {
            var line = Get(lineId);
            if (line == null) return;
            if (line.PlayerCount > 0) line.PlayerCount--;
        }

        /// <summary>健康状态变更 (阶段11健康检查调用)</summary>
        public void SetHealth(int lineId, bool healthy, bool accepting)
        {
            var line = Get(lineId);
            if (line == null) return;
            line.Healthy = healthy;
            line.Accepting = accepting;
        }

        /// <summary>动态开线 (阶段11): 新增一条线</summary>
        public LineData AddLine(int lineId, int capacity = DefaultLineCapacity)
        {
            return _lines.GetOrAdd(lineId, id => new LineData { LineId = id, Capacity = capacity });
        }
    }
}
