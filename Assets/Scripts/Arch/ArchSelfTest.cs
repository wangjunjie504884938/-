using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Arch
{
    /// <summary>
    /// 架构数据模型自检 — 规划阶段02验收项: JSON可往返序列化 / 货币溢出保护 / 配置校验报错。
    /// 编辑器菜单 "Arch/数据模型自检" 与运行时验证脚本共用。
    /// </summary>
    public static class ArchSelfTest
    {
        public static string Run()
        {
            var sb = new System.Text.StringBuilder();
            int pass = 0, fail = 0;
            void Check(string name, bool ok, string detail = null)
            {
                sb.AppendLine((ok ? "[PASS] " : "[FAIL] ") + name + (detail != null ? "  (" + detail + ")" : ""));
                if (ok) pass++; else fail++;
            }

            // ---- JSON 往返: PlayerData ----
            var p = new PlayerData
            {
                PlayerId = 12345,
                Gold = CurrencyMath.MaxGold,
                LifeCoins = 10683,
                PlaneCoins = 9360,
                Fragments = 73,
                Level = 77,
                Backpack = { new ItemEntry { TemplateId = 1, SlotType = 0, Rarity = 3, Name = "史诗·暗影长剑", UpgradeLevel = 5 } },
                PassiveNodeIds = { "101", "102" },
            };
            string json = JsonConvert.SerializeObject(p, Formatting.Indented);
            var back = JsonConvert.DeserializeObject<PlayerData>(json);
            PlayerData.Migrate(back);
            Check("PlayerData JSON往返", back.PlayerId == 12345 && back.Gold == p.Gold && back.LifeCoins == 10683
                && back.Backpack.Count == 1 && back.Backpack[0].UpgradeLevel == 5 && back.PassiveNodeIds.Count == 2);
            Check("PlayerData 货币封顶", back.Gold == CurrencyMath.MaxGold);

            // ---- 版本迁移: 高版本拒绝 / 低版本补齐 ----
            bool rejected = false;
            try { PlayerData.Migrate(new PlayerData { Version = PlayerData.CurrentVersion + 1 }); }
            catch (InvalidOperationException) { rejected = true; }
            Check("高版本数据拒绝加载", rejected);
            var old = new PlayerData { Version = 0, Level = 0, Gold = -5 };
            PlayerData.Migrate(old);
            Check("v0迁移+规整", old.Version == PlayerData.CurrentVersion && old.Level == 1 && old.Gold == 0);

            // ---- 货币运算: 溢出/非负 ----
            Check("入账封顶", !CurrencyMath.TryAdd(999, 5, 1000, out var r1) && r1 == 1000);
            Check("正常入账", CurrencyMath.TryAdd(990, 5, 1000, out var r2) && r2 == 995);
            Check("拒绝负数入账", !CurrencyMath.TryAdd(100, -1, 1000, out _));
            Check("余额不足拒绝扣减", !CurrencyMath.TrySpend(100, 200, out _));
            Check("正常扣减", CurrencyMath.TrySpend(100, 30, out var r3) && r3 == 70);
            Check("批量乘法溢出保护", !CurrencyMath.TryMul(int.MaxValue, 500, CurrencyMath.MaxGold, out _));

            // ---- CurrencyOperation 幂等记录 ----
            var op = CurrencyOperation.New(1, "Gold", -500, 24440, "gacha_draw");
            string opJson = JsonConvert.SerializeObject(op);
            var opBack = JsonConvert.DeserializeObject<CurrencyOperation>(opJson);
            Check("CurrencyOperation 往返+幂等键", opBack.OperationId == op.OperationId && opBack.BalanceAfter == 24440);

            // ---- 配置校验: 重复ID/越界明确报错 ----
            var badCfg = new StaticConfig
            {
                DailyTasks =
                {
                    new TaskTemplate { TaskType = 0, TargetValue = 50, RewardGold = 100 },
                    new TaskTemplate { TaskType = 0, TargetValue = 30, RewardGold = 90 }, // 重复类型
                    new TaskTemplate { TaskType = 9, TargetValue = 0, RewardGold = -1 },  // 越界+非法
                },
                Items = { new ItemTemplate { TemplateId = 7, SlotType = 0, Rarity = 1, Name = "测试剑" }, new ItemTemplate { TemplateId = 7 } },
            };
            var errors = ConfigValidator.Validate(badCfg);
            Check("配置校验抓出问题数≥4", errors.Count >= 4, string.Join(" / ", errors.ToArray()));

            var goodCfg = new StaticConfig
            {
                Version = 1,
                DailyTasks =
                {
                    new TaskTemplate { TaskType = 0, TargetValue = 50, RewardGold = 100 },
                    new TaskTemplate { TaskType = 1, TargetValue = 1, RewardGold = 150 },
                },
                Items = { new ItemTemplate { TemplateId = 1, SlotType = 0, Rarity = 1, Name = "测试剑" } },
            };
            Check("合法配置零报错", ConfigValidator.Validate(goodCfg).Count == 0);

            sb.AppendLine($"==== 自检结果: {pass} 通过 / {fail} 失败 ====");
            return sb.ToString();
        }
    }
}
