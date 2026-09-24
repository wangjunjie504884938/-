using UnityEngine;

/// <summary>
/// 开放世界静态配置 — 4×4区域网格，每区60×60，世界240×240
/// 区域=逻辑概念(名称/主题/难度/传送点)，行走无缝无内墙；难度由西南(出生)向东北递增
/// </summary>
public static class WorldData
{
    public const int GridSize = 4;
    public const float ZoneSize = 60f;
    public const float WorldHalf = GridSize * ZoneSize / 2f; // 120

    public class Zone
    {
        public int Index;           // 0-15
        public int Gx, Gy;          // 网格坐标 (0,0)=西南角
        public string Name;
        public int Theme;           // 0-7 复用副本主题(地板贴图/难度)
        public int Tier => Theme;   // 敌人难度 = 主题关卡
        public Vector2 Center;      // 区域中心(世界坐标)
        public Vector2 WaypointPos; // 传送点(区域中心偏北8)
    }

    public static readonly Zone[] Zones = BuildZones();

    private static Zone[] BuildZones()
    {
        string[] names =
        {
            "幽暗森林", "翠绿平原", "废弃矿洞", "迷雾沼泽",
            "冰霜峡谷", "白狼雪原", "火焰神殿", "熔岩裂谷",
            "亡灵墓穴", "诅咒荒地", "龙巢深处", "灰烬高地",
            "混沌虚空", "风暴之巅", "魔王殿堂", "远古祭坛"
        };
        int[] themes = { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7 };

        var list = new System.Collections.Generic.List<Zone>(GridSize * GridSize);
        int i = 0;
        for (int gy = 0; gy < GridSize; gy++)       // gy=0为南，Index顺序与GetAt的gy*GridSize+gx一致
        {
            for (int gx = 0; gx < GridSize; gx++)
            {
                float cx = -WorldHalf + ZoneSize * (gx + 0.5f);
                float cy = -WorldHalf + ZoneSize * (gy + 0.5f);
                list.Add(new Zone
                {
                    Index = i,
                    Gx = gx,
                    Gy = gy,
                    Name = names[i],
                    Theme = Mathf.Clamp(themes[i], 0, 7),
                    Center = new Vector2(cx, cy),
                    WaypointPos = new Vector2(cx, cy + 8f)
                });
                i++;
            }
        }
        return list.ToArray();
    }

    public static Zone Get(int index) => Zones[Mathf.Clamp(index, 0, Zones.Length - 1)];

    /// <summary>世界坐标 → 所在区域</summary>
    public static Zone GetAt(float worldX, float worldY)
    {
        int gx = Mathf.Clamp(Mathf.FloorToInt((worldX + WorldHalf) / ZoneSize), 0, GridSize - 1);
        int gy = Mathf.Clamp(Mathf.FloorToInt((worldY + WorldHalf) / ZoneSize), 0, GridSize - 1);
        return Zones[gy * GridSize + gx];
    }

    // ===== 世界地图面板: 各区在地图图片上的归一化锚点 (按生态对位, 原点=左下) =====
    public static readonly Vector2[] MapAnchors =
    {
        new(0.36f, 0.46f), // 0 幽暗森林 → 青云城周边林地
        new(0.50f, 0.09f), // 1 翠绿平原 → 天苍草原
        new(0.70f, 0.25f), // 2 废弃矿洞 → 万兽窟
        new(0.30f, 0.16f), // 3 迷雾沼泽 → 落霞谷水泽
        new(0.37f, 0.83f), // 4 冰霜峡谷 → 北冥冰渊外围
        new(0.47f, 0.94f), // 5 白狼雪原 → 北冥冰渊
        new(0.73f, 0.73f), // 6 火焰神殿 → 血月堡熔岩带
        new(0.85f, 0.89f), // 7 熔岩裂谷 → 九幽魔渊
        new(0.60f, 0.45f), // 8 亡灵墓穴 → 埋骨之地
        new(0.15f, 0.80f), // 9 诅咒荒地 → 黄泉戈壁
        new(0.91f, 0.70f), // 10 龙巢深处 → 血月堡
        new(0.88f, 0.46f), // 11 灰烬高地 → 黑石城
        new(0.44f, 0.73f), // 12 混沌虚空 → 紫霄仙都
        new(0.27f, 0.63f), // 13 风暴之巅 → 浩然剑阁群峰
        new(0.87f, 0.33f), // 14 魔王殿堂 → 幽冥殿
        new(0.79f, 0.60f), // 15 远古祭坛 → 极乐谷
    };

    // ===== "家"(出生区)的NPC/家具 — 原大本营功能的世界内入口 =====
    public class HomeNpcDef
    {
        public string Name;     // 显示名(同时作为交互动作key)
        public string Icon;     // 单字图标(立柱顶部)
        public Vector2 Offset;  // 相对出生传送点的偏移
        public float R, G, B;   // 立柱颜色
    }

    public static readonly HomeNpcDef[] HomeNpcs =
    {
        new() { Name="副本入口",   Icon="门", Offset=new Vector2(0, -14),  R=0.95f, G=0.35f, B=0.35f },
        new() { Name="铁匠·装备",   Icon="铁", Offset=new Vector2(-9, 5),   R=0.95f, G=0.72f, B=0.3f },
        new() { Name="熔炉·强化",   Icon="炉", Offset=new Vector2(9, 5),    R=1f,    G=0.45f, B=0.2f },
        new() { Name="杂货商人",   Icon="商", Offset=new Vector2(-13, -5), R=0.4f,  G=0.8f,  B=0.4f },
        new() { Name="碎片商人",   Icon="碎", Offset=new Vector2(13, -5),  R=0.5f,  G=0.7f,  B=1f },
        new() { Name="技能导师",   Icon="技", Offset=new Vector2(0, 12),   R=0.85f, G=0.68f, B=0.3f },
        new() { Name="公会告示板", Icon="会", Offset=new Vector2(-14, 12), R=0.6f,  G=0.5f,  B=0.95f },
        new() { Name="邮箱",       Icon="邮", Offset=new Vector2(14, 12),  R=0.9f,  G=0.4f,  B=0.4f },
    };

    // ===== 传送点发现持久化 (16位0/1掩码, 全局不分存档槽) =====
    private const string KeyDiscovered = "ARPG_World_Waypoints";

    public static bool IsDiscovered(int zoneIndex)
    {
        if (zoneIndex == 0) return true; // 出生区默认已激活
        string s = PlayerPrefs.GetString(KeyDiscovered, "");
        if (s.Length != Zones.Length) return false;
        return s[zoneIndex] == '1';
    }

    public static void MarkDiscovered(int zoneIndex)
    {
        string s = PlayerPrefs.GetString(KeyDiscovered, "");
        if (s.Length != Zones.Length) s = new string('0', Zones.Length);
        var chars = s.ToCharArray();
        chars[zoneIndex] = '1';
        PlayerPrefs.SetString(KeyDiscovered, new string(chars));
        PlayerPrefs.Save();
    }

    // ===== 世界等级 (暗黑式: 1-3级, 敌人强度与奖励递增) =====
    private const string KeyWorldTier = "ARPG_World_Tier";

    public static int WorldTier => Mathf.Clamp(PlayerPrefs.GetInt(KeyWorldTier, 1), 1, 3);

    public static void SetWorldTier(int tier)
    {
        PlayerPrefs.SetInt(KeyWorldTier, Mathf.Clamp(tier, 1, 3));
        PlayerPrefs.Save();
    }

    /// <summary>世界等级对敌人等级的加成 (+0/+3/+6)</summary>
    public static int WorldTierLevelBonus => (WorldTier - 1) * 3;

    // ===== 安全区 (出生区无怪物 — "家"所在的野外) =====
    public static bool IsSafeZone(int zoneIndex) => zoneIndex == 0;

    // ===== 区域声望完成 (每区击杀10只+发现传送点 → 一次性金币奖励) =====
    private const string KeyZoneDone = "ARPG_World_ZoneDone";

    public static bool IsZoneDone(int zoneIndex)
    {
        if (zoneIndex == 0) return true;
        string s = PlayerPrefs.GetString(KeyZoneDone, "");
        if (s.Length != Zones.Length) return false;
        return s[zoneIndex] == '1';
    }

    public static void MarkZoneDone(int zoneIndex)
    {
        string s = PlayerPrefs.GetString(KeyZoneDone, "");
        if (s.Length != Zones.Length) s = new string('0', Zones.Length);
        var chars = s.ToCharArray();
        chars[zoneIndex] = '1';
        PlayerPrefs.SetString(KeyZoneDone, new string(chars));
        PlayerPrefs.Save();
    }
}
