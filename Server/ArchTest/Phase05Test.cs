using UnityEngine;
using Arch;

/// <summary>
/// 规划阶段05 验收测试 — RPC限流器 + 服务器权威验证器 (纯逻辑)。
/// 运行: dotnet run --project Server/ArchTest -c Release -- phase05
/// </summary>
internal static class Phase05Test
{
    private static int _pass, _fail;
    private static void Check(string name, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
        if (ok) _pass++; else _fail++;
    }

    public static int Run()
    {
        // ---------- 验收: RPC 限流 (30条/秒, 违规5次断开) ----------
        var limiter = new Arch.RpcRateLimiter(1.0, 30, 5);
        int rejected = 0, kickSignal = 0;
        for (int i = 0; i < 40; i++)
        {
            int v = limiter.OnMessage(100.0 + i * 0.01); // 0.01s间隔 = 100条/秒超频
            if (v == 1) rejected++;
            if (v == 2) kickSignal++;
        }
        Check("限流: 40条/秒 超出30被拒", rejected > 0, $"rejected={rejected}");
        Check("限流: 违规计分达断开阈值", kickSignal > 0 || limiter.ShouldDisconnect);

        // 违规5次 → ShouldDisconnect (0.05s间隔真超频: 20条-10容量=10次断开信号)
        var limiter2 = new Arch.RpcRateLimiter(1.0, 10, 5);
        int kicks = 0;
        for (int i = 0; i < 20; i++)
            if (limiter2.OnMessage(100.0 + i * 0.05) == 2) kicks++;
        Check("限流: 违规计分触发断开信号", kicks >= 5, $"kicks={kicks}");

        // 新连接独立计数
        var registry = new Arch.RpcRateLimiterRegistry(1.0, 10, 5);
        int rejectedNew = 0;
        for (int i = 0; i < 15; i++)
            if (registry.OnMessage(1, "Move", 10.0) == 1) rejectedNew++;
        Check("限流: 新连接独立计数", rejectedNew > 0);

        // ---------- 验收: 移动验证 ----------
        // 正常速度: 6m/s × 0.1s = 0.6m 位移 → Ok
        var mv = Arch.ServerAuthority.ValidateMove(
            0f, 0f, 0.5f, 0f, 6f, 0.1f);
        Check("移动: 正常速度通过", mv == Arch.ServerAuthority.MoveVerdict.Ok);
        // 瞬移: 位移20m → TooFast
        var tp = Arch.ServerAuthority.ValidateMove(
            0f, 0f, 20f, 0f, 6f, 0.1f);
        Check("移动: 瞬移被拒(超速校正)", tp == Arch.ServerAuthority.MoveVerdict.TooFast);
        // 边界外
        var ob = Arch.ServerAuthority.ValidateMove(
            0f, 0f, 60f, 0f, 6f, 0.1f);
        Check("移动: 越界被拒", ob == Arch.ServerAuthority.MoveVerdict.OutOfBounds);

        // ---------- 验收: 攻击验证 ----------
        Check("攻击: 冷却中拒绝", Arch.ServerAuthority.ValidateAttack(
            0.1f, 0.5f, true, true, 1f, 3f) == Arch.ServerAuthority.AttackVerdict.OnCooldown);
        Check("攻击: 目标已死拒绝", Arch.ServerAuthority.ValidateAttack(
            1f, 0.5f, true, false, 1f, 3f) == Arch.ServerAuthority.AttackVerdict.TargetDead);
        Check("攻击: 距离过远拒绝", Arch.ServerAuthority.ValidateAttack(
            1f, 0.5f, true, true, 10f, 3f) == Arch.ServerAuthority.AttackVerdict.TargetTooFar);
        Check("攻击: 正常通过", Arch.ServerAuthority.ValidateAttack(
            1f, 0.5f, true, true, 2f, 3f) == Arch.ServerAuthority.AttackVerdict.Ok);

        // ---------- 验收: 伤害截断 (单次 ≤ maxHp 50%) ----------
        Check("伤害截断: 800→500(maxHp1000)", Arch.ServerAuthority.ClampDamage(800, 1000) == 500);
        Check("伤害截断: 正常伤害不截断", Arch.ServerAuthority.ClampDamage(300, 1000) == 300);

        Console.WriteLine($"==== 阶段05 服务器权威 验收: {_pass} 通过 / {_fail} 失败 ====");
        return _fail == 0 ? 0 : 1;
    }
}
