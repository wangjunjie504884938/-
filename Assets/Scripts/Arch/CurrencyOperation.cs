using System;

namespace Arch
{
    /// <summary>
    /// 货币操作流水 — 幂等操作记录 (规划阶段03: Lua 一次性完成 余额校验/扣减/幂等结果记录)。
    /// 同一 OperationId 只允许成功一次; 服务器返回"重复操作"时客户端必须复用首次结果。
    /// 持久化到 currency_ledger 表 (MySQL), Redis 保存 currency:operation:{operationId} 热记录。
    /// </summary>
    public class CurrencyOperation
    {
        public string OperationId;     // 幂等键 (GUID)
        public long PlayerId;
        public string Kind;            // Gold / Life / Plane / Fragments
        public long Amount;            // 正=入账 负=扣减 (经 CurrencyMath 校验)
        public long BalanceAfter;      // 操作后余额 (对账用)
        public string Reason;          // dungeon_clear / daily_claim / gacha_draw / death_drop / ...
        public string OrderId;         // 关联支付订单 (支付场景非空)
        public DateTime CreatedAtUtc = DateTime.UtcNow;

        public static CurrencyOperation New(long playerId, string kind, long amount, long balanceAfter, string reason)
        {
            return new CurrencyOperation
            {
                OperationId = Guid.NewGuid().ToString("N"),
                PlayerId = playerId,
                Kind = kind,
                Amount = amount,
                BalanceAfter = balanceAfter,
                Reason = reason ?? "",
            };
        }
    }
}
