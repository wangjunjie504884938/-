using System;

namespace ArpgShared
{
    /// <summary>
    /// 战斗计算器 — 客户端预测和服务器权威共用的伤害公式
    /// </summary>
    public static class CombatCalculator
    {
        private static readonly Random _rng = new();

        /// <summary>计算物理伤害</summary>
        public static int CalculateDamage(int attackerAttack, int defenderDefense, float damageMultiplier, float critChance)
        {
            int baseDamage = (int)Math.Round(attackerAttack * damageMultiplier);
            int defenseReduction = Math.Max(1, defenderDefense / 2);
            int finalDamage = Math.Max(1, baseDamage - defenseReduction);

            bool isCrit = _rng.NextDouble() < critChance;
            if (isCrit) finalDamage = (int)Math.Round(finalDamage * 2f);

            return finalDamage;
        }

        /// <summary>计算经验奖励</summary>
        public static int CalculateXpReward(int dungeonLevel, int enemiesKilled)
        {
            return 50 + dungeonLevel * 30 + enemiesKilled * 5;
        }

        /// <summary>计算金币奖励</summary>
        public static (int baseGold, int speedBonus, int fullHpBonus) CalculateGoldReward(int dungeonId, float timeUsed, int hpRemaining, int hpMax)
        {
            int baseGold = dungeonId switch
            {
                0 => 200, 1 => 300, 2 => 400, 3 => 500, 4 => 600, 5 => 800, 6 => 1000, 7 => 1500,
                _ => 200
            };

            int speedBonus = 0;
            if (timeUsed < 60f) speedBonus = (int)Math.Round(baseGold * 0.5);
            else if (timeUsed < 120f) speedBonus = (int)Math.Round(baseGold * 0.2);

            int fullHpBonus = (hpRemaining > 0 && hpMax > 0 && hpRemaining >= hpMax)
                ? (int)Math.Round(baseGold * 0.5) : 0;

            return (baseGold, speedBonus, fullHpBonus);
        }

        /// <summary>滚动掉落稀有度</summary>
        public static int RollRarity()
        {
            double roll = _rng.NextDouble();
            return roll < 0.02 ? 3 : roll < 0.10 ? 2 : roll < 0.30 ? 1 : 0;
        }

        /// <summary>计算队伍成员奖励分配</summary>
        public static void DistributeRewards(SettlementData data, int totalGold, int totalXp, int playerCount)
        {
            int goldPerPlayer = totalGold / Math.Max(1, playerCount);
            int xpPerPlayer = totalXp / Math.Max(1, playerCount);

            foreach (var reward in data.playerRewards)
            {
                reward.goldReward = goldPerPlayer + reward.kills * 5;
                reward.xpReward = xpPerPlayer + reward.damageDealt / 100;
            }
        }
    }
}
