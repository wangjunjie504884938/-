using System;

namespace Arch
{
    /// <summary>
    /// 货币运算 — long + 非负校验 + 上限封顶 + 溢出保护 (规划阶段02: 货币使用long, 校验非负/上限/溢出)。
    /// 所有货币变更必须经此入口, 禁止裸算术。
    /// </summary>
    public static class CurrencyMath
    {
        public const long MaxGold = 999_999_999_999L;    // 游戏币上限 ~1万亿
        public const long MaxLifeCoins = 999_999_999L;   // 寿元币上限 ~10亿
        public const long MaxPlaneCoins = 999_999_999L;  // 位面币上限 ~10亿
        public const long MaxFragments = 999_999_999L;   // 碎片上限 ~10亿

        public static long Clamp(long v, long min, long max) => v < min ? min : (v > max ? max : v);

        /// <summary>入账: 金额非负; 封顶时返回 false(未足额入账) 且 result=上限</summary>
        public static bool TryAdd(long balance, long amount, long max, out long result)
        {
            result = balance;
            if (amount < 0) return false;
            if (balance > max - amount) { result = max; return false; } // checked语义防溢出
            result = balance + amount;
            return true;
        }

        /// <summary>扣减: 金额非负且余额充足才成功</summary>
        public static bool TrySpend(long balance, long amount, out long result)
        {
            result = balance;
            if (amount < 0 || balance < amount) return false;
            result = balance - amount;
            return true;
        }

        /// <summary>数量×单价: 溢出保护 (十连等批量扣费用)</summary>
        public static bool TryMul(int count, long unit, long max, out long total)
        {
            total = 0;
            if (count < 0 || unit < 0) return false;
            if (count != 0 && unit > max / count) { total = max; return false; }
            total = count * unit;
            return total <= max;
        }
    }
}
