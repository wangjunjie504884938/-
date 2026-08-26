using UnityEngine;

/// <summary>
/// 全局常量 — 集中管理魔法数字，修改配置只需改一处
/// </summary>
public static class GameConstants
{
    // ====== UI 布局 ======
    public const float NavHeight = 56f;
    public const float TitleFontSize = 30;
    public const float SubtitleFontSize = 16;
    public const float CardHeight = 52f;
    public const float CardSpacing = 4f;
    public const float FadeInDuration = 0.2f;
    public const float FadeOutDuration = 0.15f;

    // ====== 战斗参数 ======
    public const float HitStopDuration = 0.08f;
    public const float DeathFlashDuration = 0.2f;
    public const float DamageNumberLifetime = 0.8f;
    public const float EnemyDespawnDelay = 2f;
    public const int MaxEnemiesPerWave = 30;
    public const float EnemyAggroRange = 8f;
    public const float EnemyAttackRange = 1.5f;

    // ====== 经济系统 ======
    public const int NormalGachaCost = 100;
    public const int PremiumGachaCost = 300;
    public const int ClassPoolCost = 200;
    public const float TenPullDiscount = 0.9f;
    public const int BossDropMinCount = 3;
    public const float BossDropExtraChance = 0.2f;
    public const int PetEvolveCost = 2000;

    // ====== 存档 ======
    public const int MaxSlots = 3;
    public const string SavePrefix = "ARPG_S";
    public const string PetKeyPrefix = "ARPG_Pet_";
    public const string TipKeyPrefix = "ARPG_Tip_";

    // ====== 对象池 ======
    public const int VFXPoolMaxSize = 128;
    public const int VFXParticlePrewarm = 16;
    public const int VFXDamageNumPrewarm = 8;

    // ====== 网络 ======
    public const int NetworkTimeout = 10;
    public const int ServerPingTimeout = 5;
    public const string DefaultServerUrl = "http://39.107.141.107:5132";


    // ====== 场景清理 ======
    public const float OfflineRewardMaxHours = 8f;
    public const float OfflineRewardGoldPerHour = 50f;
}
