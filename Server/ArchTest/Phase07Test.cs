using Arch;

/// <summary>
/// 规划阶段07 验收测试 — AOI 空间网格 (SpatialGrid) 纯逻辑。
/// 运行: dotnet run --project Server/ArchTest -c Release -- phase07
/// </summary>
internal static class Phase07Test
{
    private static int _pass, _fail;
    private static void Check(string name, bool ok, string detail = "")
    {
        Console.WriteLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail.Length > 0 ? "  (" + detail + ")" : ""));
        if (ok) _pass++; else _fail++;
    }

    public static int Run()
    {
        // ---------- 基础: 插入+圆查询 ----------
        var grid = new SpatialGrid(20f);
        grid.Move(1, 0f, 0f);
        grid.Move(2, 10f, 0f);
        grid.Move(3, 100f, 100f); // 远处

        var r1 = grid.QueryCircle(0f, 0f, 5f);
        Check("圆查询: 只含近处实体", r1.Contains(1) && !r1.Contains(2) && !r1.Contains(3), $"count={r1.Count}");
        var r2 = grid.QueryCircle(0f, 0f, 15f);
        Check("圆查询: 半径扩大含2个", r2.Contains(1) && r2.Contains(2), $"count={r2.Count}");

        // ---------- Move 跨格迁移 ----------
        grid.Move(2, 30f, 0f); // 从格(0)跨到格(1)
        var r3 = grid.QueryCircle(0f, 0f, 5f);
        Check("跨格迁移: 旧格不再返回", !r3.Contains(2));
        var r4 = grid.QueryCircle(30f, 0f, 5f);
        Check("跨格迁移: 新格能查到", r4.Contains(2));

        // ---------- Remove ----------
        grid.Remove(2);
        var r5 = grid.QueryCircle(30f, 0f, 5f);
        Check("移除后不再返回", !r5.Contains(2));

        // ---------- 边界: 负坐标 (地牢地图允许负半区) ----------
        grid.Move(4, -25f, -5f);
        var r6 = grid.QueryCircle(-25f, -5f, 3f);
        Check("负坐标查询", r6.Contains(4));

        // ---------- AOI 模拟: 观察者移动, 实体进出视野 ----------
        var aoi = new HashSet<long>();
        grid.Move(10, 24f, 0f);  // 观察者附近
        grid.Move(11, 60f, 0f);  // 远处
        var visible = grid.QueryCircle(24f, 0f, 5f);
        Check("AOI: 近处实体可见", visible.Contains(10) && visible.Contains(4));
        Check("AOI: 远处实体不可见", !visible.Contains(11));

        // 观察者移动 30m → 11 进入视野, 10 离开
        var visAfter = grid.QueryCircle(50f, 0f, 5f);
        Check("AOI: 移动后目标切换", visAfter.Contains(11) && !visAfter.Contains(10));

        // ---------- 大量实体性能抽样 (1000实体, 网格查询应毫秒级) ----------
        var big = new SpatialGrid(20f);
        for (int i = 0; i < 1000; i++)
            big.Move(10000 + i, (i % 50) * 2f, (i / 50) * 2f);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var hit = big.QueryCircle(50f, 50f, 10f);
        sw.Stop();
        Check("千实体查询<5ms", sw.ElapsedMilliseconds < 5, $"hits={hit.Count} elapsed={sw.ElapsedMilliseconds}ms");

        Console.WriteLine($"==== 阶段07 AOI 空间网格 验收: {_pass} 通过 / {_fail} 失败 ====");
        return _fail == 0 ? 0 : 1;
    }
}
