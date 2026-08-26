/// <summary>
/// 场景名常量 — 集中管理，避免硬编码字符串散落各处
/// </summary>
public static class SceneNames
{
    public const string Main = "SampleScene";
    public const string Dungeon = "SampleScene"; // 当前所有关卡在同一场景内动态生成
    public const string Hub = "SampleScene";
}

/// <summary>
/// 资源路径常量 — 集中管理 Resources.Load 路径
/// </summary>
public static class ResourcePaths
{
    // 角色
    public const string CharacterBase = "Sprites/Character/";
    public const string WarriorSheet = "Sprites/Character/Sprite_20260707_163517";
    public const string MageSheet = "Sprites/Character/Sprite_20260707_165540";
    public const string PriestSheet = "Sprites/Character/Sprite_20260707_165905";

    // 敌人
    public const string EnemyBase = "Sprites/Enemy/";
    public const string MeleeGoblin = "Sprites/Enemy/Melee_Goblin";

    // 地牢
    public const string DungeonBase = "Sprites/Dungeon/";
    public const string Floor_Stone = "Sprites/Dungeon/Floor_Stone";
    public const string Floor_Forest = "Sprites/Dungeon/Floor_Forest";
    public const string Floor_Ice = "Sprites/Dungeon/Floor_Ice";
    public const string Floor_Fire = "Sprites/Dungeon/Floor_Fire";
    public const string Floor_Shadow = "Sprites/Dungeon/Floor_Shadow";
    public const string Wall_Brick = "Sprites/Dungeon/Wall_Brick";
    public const string WallShadow = "Sprites/Dungeon/WallShadow";
    public const string Vignette = "Sprites/Dungeon/Vignette";
    public const string Fog_A = "Sprites/Dungeon/Fog_A";
    public const string Fog_B = "Sprites/Dungeon/Fog_B";
    public const string LightRay = "Sprites/Dungeon/LightRay";
    public const string Map_Stage0 = "Sprites/Dungeon/Map_Stage0";

    // 装饰物
    public const string Deco_Torch = "Sprites/Dungeon/Deco_Torch";
    public const string Deco_Flame = "Sprites/Dungeon/Deco_Flame";
    public const string Deco_Cobweb = "Sprites/Dungeon/Deco_Cobweb";
    public const string Deco_Debris = "Sprites/Dungeon/Deco_Debris";
    public const string Deco_Mushroom = "Sprites/Dungeon/Deco_Mushroom";
    public const string Deco_Vine = "Sprites/Dungeon/Deco_Vine";
    public const string Deco_Crystal_Ice = "Sprites/Dungeon/Deco_Crystal_Ice";
    public const string Deco_Crystal_Shadow = "Sprites/Dungeon/Deco_Crystal_Shadow";
    public const string Deco_Icicle = "Sprites/Dungeon/Deco_Icicle";
    public const string Deco_LavaCrack = "Sprites/Dungeon/Deco_LavaCrack";

    // 障碍物
    public const string Obstacle_Pillar = "Sprites/Dungeon/Obstacle_Pillar";
    public const string Obstacle_Tree = "Sprites/Dungeon/Obstacle_Tree";
    public const string Obstacle_Rock = "Sprites/Dungeon/Obstacle_Rock";
    public const string Obstacle_IceSpike = "Sprites/Dungeon/Obstacle_IceSpike";
    public const string Obstacle_LavaPool = "Sprites/Dungeon/Obstacle_LavaPool";
    public const string Obstacle_Tombstone = "Sprites/Dungeon/Obstacle_Tombstone";

    // 字体
    public const string DefaultFont = "Fonts/DefaultFont";

    // UI Prefabs
    public const string BossHealthPanel = "UI/BossHealthPanel";
}
