namespace Arch
{
    /// <summary>
    /// 分线数据 — ShardManager 维护 Dictionary&lt;int, LineData&gt; (规划阶段04/11)。
    /// 分配规则: 最少人数优先; 人数相同走确定性规则 (LineId 升序)。
    /// 注意: 在线人数/单线容量尚未最终定 (规划§8标注), 默认容量50仅占位, 阶段04前必须确认。
    /// </summary>
    public class LineData
    {
        public int LineId;
        public string ServerNodeId;   // 归属节点 (Docker 注册)
        public int Capacity = 50;     // 占位值, 阶段04前确认
        public int PlayerCount;
        public bool Healthy = true;
        public bool Accepting = true; // 停止接收新玩家但保留存量
        public int BuildVersion;      // 协议/构建版本 (版本不兼容拒绝分配)
    }
}
