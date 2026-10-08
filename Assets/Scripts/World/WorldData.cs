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
        // 区名对齐官方世界地图标注: 南→北难度递增与图中(新手→高级)一致
        string[] names =
        {
            "落霞谷", "落霞镇", "青云城", "魔道新手村",
            "天剑山脉", "云清宗", "埋骨之地", "血煞堂",
            "浩然剑阁", "天苍草原", "黑石城", "血月堡",
            "黄泉戈壁", "北冥冰渊", "万兽窟", "九幽魔渊"
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

    // ===== 世界地图面板: 各区在地图图片上的归一化锚点 (对齐图中城池标注框中心, 原点=左下)
    // ===== 数值由1536x1024官方图逐标注实测换算 (nx=px/1536, ny=1-py/1024), 改图需重新标定
    public static readonly Vector2[] MapAnchors =
    {
        new(0.140f, 0.290f), // 0 落霞谷(新手村)      px(215,727)
        new(0.228f, 0.395f), // 1 落霞镇(采药区)      px(350,620)
        new(0.326f, 0.326f), // 2 青云城(新手主城)    px(500,690)
        new(0.781f, 0.310f), // 3 魔道新手村(新手区域) px(1200,707)
        new(0.184f, 0.487f), // 4 天剑山脉(野外资源区) px(282,525)
        new(0.241f, 0.570f), // 5 云清宗(宗门)        px(370,440)
        new(0.524f, 0.500f), // 6 埋骨之地(正魔争夺区) px(805,512)
        new(0.771f, 0.541f), // 7 血煞堂(宗门)        px(1185,470)
        new(0.244f, 0.656f), // 8 浩然剑阁(宗门)      px(375,352)
        new(0.566f, 0.217f), // 9 天苍草原(高级探索区) px(870,802)
        new(0.921f, 0.561f), // 10 黑石城(军事要塞)   px(1415,450)
        new(0.939f, 0.443f), // 11 血月堡(PvP区)      px(1442,570)
        new(0.133f, 0.790f), // 12 黄泉戈壁(高级探索区) px(205,215)
        new(0.508f, 0.899f), // 13 北冥冰渊(高级探索区) px(780,103)
        new(0.891f, 0.766f), // 14 万兽窟(宗门)       px(1368,240)
        new(0.778f, 0.819f), // 15 九幽魔渊(魔道最高权力中心) px(1195,185)
    };

    // ===== 区域UI样式 (地图面板: 类型化图标按钮, 参照图例配色) =====
    public class ZoneUiDef
    {
        public string Icon;     // 图标字符
        public string Kind;     // 类型名(图例)
        public float R, G, B;   // 图标底色
    }

    public static readonly ZoneUiDef[] ZoneUi =
    {
        new() { Icon="村", Kind="新手", R=0.45f, G=0.85f, B=0.45f }, // 0 落霞谷
        new() { Icon="镇", Kind="新手", R=0.45f, G=0.85f, B=0.45f }, // 1 落霞镇
        new() { Icon="城", Kind="主城", R=0.40f, G=0.70f, B=1.00f }, // 2 青云城
        new() { Icon="村", Kind="新手", R=0.70f, G=0.50f, B=0.95f }, // 3 魔道新手村
        new() { Icon="野", Kind="野外", R=0.30f, G=0.80f, B=0.70f }, // 4 天剑山脉
        new() { Icon="宗", Kind="宗门", R=0.45f, G=0.65f, B=1.00f }, // 5 云清宗
        new() { Icon="争", Kind="争夺", R=1.00f, G=0.70f, B=0.30f }, // 6 埋骨之地
        new() { Icon="宗", Kind="宗门", R=1.00f, G=0.45f, B=0.40f }, // 7 血煞堂
        new() { Icon="宗", Kind="宗门", R=0.45f, G=0.65f, B=1.00f }, // 8 浩然剑阁
        new() { Icon="探", Kind="高级", R=1.00f, G=0.55f, B=0.75f }, // 9 天苍草原
        new() { Icon="塞", Kind="要塞", R=0.60f, G=0.65f, B=0.80f }, // 10 黑石城
        new() { Icon="战", Kind="PvP",  R=1.00f, G=0.35f, B=0.30f }, // 11 血月堡
        new() { Icon="探", Kind="高级", R=1.00f, G=0.55f, B=0.75f }, // 12 黄泉戈壁
        new() { Icon="探", Kind="高级", R=1.00f, G=0.55f, B=0.75f }, // 13 北冥冰渊
        new() { Icon="宗", Kind="宗门", R=1.00f, G=0.45f, B=0.40f }, // 14 万兽窟
        new() { Icon="渊", Kind="魔都", R=0.95f, G=0.30f, B=0.30f }, // 15 九幽魔渊
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
