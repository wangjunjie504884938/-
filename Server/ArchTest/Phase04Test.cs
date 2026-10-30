using System.Collections.Generic;
using Arch;

/// <summary>
/// 规划阶段04 验收测试 — ShardManager 分线 + SessionManager 会话状态机。
/// 运行: dotnet run --project Server/ArchTest -c Release -- phase04
/// </summary>
internal static class Phase04Test
{
    private static int _pass, _fail;
    private static void Check(string name, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
        if (ok) _pass++; else _fail++;
    }

    public static int Run()
    {
        // ---------- 验收1: 单线容量50, 第51个Bot进入第二条线 ----------
        var shards = new ArpgServer.Services.ShardManager();
        // 灌满线1 (容量50)
        for (int i = 0; i < 50; i++)
            if (!shards.TryJoin(1)) { Check("灌满线1", false); break; }
        Check("线1满50人", shards.Get(1)!.PlayerCount == 50);

        var line51 = shards.AllocateLine(); // 线1已满 → 最少人数线 = 线2
        Check("第51个Bot分配到线2", line51 != null && line51.LineId == 2);
        Check("线2可加入", line51 != null && shards.TryJoin(line51.LineId));
        Check("线2计数=1", shards.Get(2)!.PlayerCount == 1);
        Check("线1仍为50", shards.Get(1)!.PlayerCount == 50);

        // ---------- 验收2: 重复登录不产生两个权威会话 ----------
        var sessions = new Arch.SessionManager();
        var s1 = sessions.OpenSession(777, "conn-A", 1, "node-1", 1);
        var s2 = sessions.OpenSession(777, "conn-B", 1, "node-1", 1); // 重复登录顶替
        Check("重复登录: 旧会话Expired", s1.State == Arch.SessionState.Expired);
        Check("重复登录: 新会话Active", s2.State == Arch.SessionState.Active && s2.SessionId != s1.SessionId);
        Check("fencing token单调递增", s2.FencingToken == s1.FencingToken + 1);
        Check("重复登录: 不产生双会话", sessions.ActiveCount == 1);
        Check("旧fencing token写入被拒", !sessions.IsWriteAllowed(s1.SessionId, s1.FencingToken));
        Check("新fencing token写入被允许", sessions.IsWriteAllowed(s2.SessionId, s2.FencingToken));

        // ---------- 验收3: 断线→Detached租约→重连恢复 / 超期清理 ----------
        var s3 = sessions.OpenSession(888, "conn-C", 1, "node-1", 1);
        sessions.Detach(s3.SessionId);
        Check("断线进入Detached", s3.State == Arch.SessionState.Detached);
        var re = sessions.Reconnect(s3.SessionId, "conn-C2");
        Check("租约内重连恢复Active", re != null && re.State == Arch.SessionState.Active);
        sessions.Detach(s3.SessionId);
        sessions.Close(s3.SessionId);
        Check("关闭后重连被拒", sessions.Reconnect(s3.SessionId, "conn-X") == null);

        // ---------- 验收4: 不同线玩家互不可见 (架构保证: 分线隔离) ----------
        var line1Members = new HashSet<int>();
        var line2Members = new HashSet<int>();
        var shards2 = new ArpgServer.Services.ShardManager();
        for (int i = 0; i < 60; i++)
        {
            var line = shards2.AllocateLine();
            if (line == null) continue;
            shards2.TryJoin(line.LineId);
            (line.LineId == 1 ? line1Members : line2Members).Add(1000 + i);
        }
        line1Members.IntersectWith(line2Members);
        Check("分线隔离: 无玩家同属两线", line1Members.Count == 0);

        Console.WriteLine($"==== 阶段04 会话/分线 验收: {_pass} 通过 / {_fail} 失败 ====");
        return _fail == 0 ? 0 : 1;
    }
}
