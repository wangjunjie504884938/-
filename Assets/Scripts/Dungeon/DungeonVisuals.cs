using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Dynamic dungeon visuals built from DungeonMapData.
/// Sprites loaded from Resources/Sprites/Dungeon assets (cached after first load).
/// Animated decorations: flickering torches, pulsing crystals, swaying vines, animated lava.
/// </summary>
public class DungeonVisuals : MonoBehaviour
{
    public static DungeonVisuals Instance { get; set; }

    // Static sprite cache — survives scene transitions, avoids repeated Resources.Load
    private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

    private static Sprite LoadCachedSprite(string path)
    {
        if (_spriteCache.TryGetValue(path, out var cached)) return cached;
        var sprite = Resources.Load<Sprite>(path);
        _spriteCache[path] = sprite;
        return sprite;
    }

    // ===== 遗迹素材图集 (Sprites/Dungeon/RuinsTileset, 多片切片) =====
    private static Sprite[] _ruinsAll;
    private static readonly List<Sprite> RuinsFloor = new List<Sprite>();
    private static readonly List<Sprite> RuinsWallBig = new List<Sprite>();
    private static readonly List<Sprite> RuinsPillar = new List<Sprite>();
    private static readonly List<Sprite> RuinsDeco = new List<Sprite>();
    private static readonly List<Sprite> RuinsSmall = new List<Sprite>();
    private static readonly List<Sprite> RuinsRock = new List<Sprite>();

    private static void LoadRuins()
    {
        if (_ruinsAll != null) return;
        _ruinsAll = Resources.LoadAll<Sprite>("Sprites/Dungeon/RuinsTileset");
        foreach (var s in _ruinsAll)
        {
            if (s == null) continue;
            float w = s.rect.width, h = s.rect.height;
            if (s.name.Contains("_Floor_")) RuinsFloor.Add(s);
            else if (s.name.Contains("_Column_")) RuinsPillar.Add(s);
            else if (s.name.Contains("_Wall_"))
            {
                if (w >= 55f && h >= 55f) RuinsWallBig.Add(s);
                else RuinsSmall.Add(s);
            }
            else if (s.name.Contains("_Rubble") || s.name.Contains("_Stone_"))
            {
                RuinsSmall.Add(s);
                if (w >= 60f && h >= 60f) RuinsRock.Add(s);
            }
            else RuinsDeco.Add(s); // Arch_/Chain_ 残段作装饰
        }
    }

    private static Sprite PickRandom(List<Sprite> list)
        => list != null && list.Count > 0 ? list[Random.Range(0, list.Count)] : null;

    private DungeonMapData.StageMapConfig currentMap;

    private GameObject floorObj;
    private GameObject wallContainer;
    private GameObject decoContainer;
    private GameObject obstacleContainer;

    private SpriteRenderer floorSpriteRenderer;
    private SpriteRenderer[] wallSpriteRenderers;
    private SpriteRenderer vignetteSpriteRenderer;
    private readonly List<SpriteRenderer> floorTiles = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> wallTiles = new List<SpriteRenderer>();

    private Color floorTint = new Color(0.9f, 0.88f, 0.92f);
    private Color wallTint = new Color(0.6f, 0.55f, 0.65f);
    private Color vignetteTint = Color.black;

    private List<TorchData> torches = new List<TorchData>();
    private List<CrystalData> crystals = new List<CrystalData>();
    private List<VineData> vines = new List<VineData>();
    private List<LavaData> lavaCracks = new List<LavaData>();

    private List<FogData> fogLayers = new List<FogData>();
    private List<LightRayData> lightRays = new List<LightRayData>();

    private int currentTheme = 0;
    private float animTime;
    private bool mapBuilt;

    private struct TorchData { public SpriteRenderer flameRenderer; public float baseAlpha; public float phase; }
    private struct CrystalData { public float baseAlpha; public float phase; public SpriteRenderer renderer; }
    private struct VineData { public Transform transform; public float phase; public float amplitude; }
    private struct LavaData { public SpriteRenderer renderer; public float phase; }

    private struct FogData { public Transform transform; public float speed; public float baseAlpha; public float width; public SpriteRenderer renderer; }
    private struct LightRayData { public Transform transform; public SpriteRenderer renderer; public float phase; public float baseAlpha; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Only build default map if BuildMap wasn't called externally
        if (!mapBuilt) BuildMap(0);
    }

    private void Update()
    {
        animTime += Time.deltaTime;
        AnimateTorches();
        AnimateCrystals();
        AnimateVines();
        AnimateLavaCracks();

        AnimateFogLayers();
        AnimateLightRays();
    }

    // ======================== MAP BUILDING ========================

    public void BuildMap(int stageIndex)
    {
        mapBuilt = true;
        LoadRuins();
        currentMap = DungeonMapData.GetStageMap(stageIndex);
        SetStageTheme(stageIndex);
        CreateFloor();
        CreateWalls();
        CreateObstacles();
        CreateDecorations();
        CreateAmbientLighting();

        CreateFogLayers();
        CreateLightRays();
    }

    /// <summary>Rebuilds map for a given stage (called by GameManager.StartDungeon).</summary>
    public void SetStageTheme(int stageIndex)
    {
        // Each stage gets its own unique theme (no modulo recycling)
        currentTheme = Mathf.Clamp(stageIndex, 0, 10);
        switch (stageIndex)
        {
            case 0: // 幽暗森林
                floorTint = new Color(0.9f, 0.88f, 0.92f); wallTint = new Color(0.6f, 0.55f, 0.65f); vignetteTint = new Color(0f, 0f, 0f); break;
            case 1: // 废弃矿洞
                floorTint = new Color(0.75f, 0.88f, 0.7f); wallTint = new Color(0.45f, 0.6f, 0.4f); vignetteTint = new Color(0f, 0.05f, 0f); break;
            case 2: // 冰霜峡谷
                floorTint = new Color(0.8f, 0.9f, 0.95f); wallTint = new Color(0.5f, 0.65f, 0.8f); vignetteTint = new Color(0f, 0.02f, 0.06f); break;
            case 3: // 火焰神殿
                floorTint = new Color(0.95f, 0.8f, 0.7f); wallTint = new Color(0.75f, 0.45f, 0.35f); vignetteTint = new Color(0.06f, 0.01f, 0f); break;
            case 4: // 亡灵墓穴
                floorTint = new Color(0.85f, 0.78f, 0.92f); wallTint = new Color(0.55f, 0.4f, 0.7f); vignetteTint = new Color(0.03f, 0f, 0.05f); break;
            case 5: // 龙巢深处 — 独特暗红+金色主题
                floorTint = new Color(0.6f, 0.35f, 0.2f); wallTint = new Color(0.4f, 0.2f, 0.1f); vignetteTint = new Color(0.08f, 0.02f, 0f); break;
            case 6: // 混沌虚空 — 独特紫黑主题
                floorTint = new Color(0.3f, 0.2f, 0.4f); wallTint = new Color(0.5f, 0.3f, 0.6f); vignetteTint = new Color(0.05f, 0f, 0.08f); break;
            case 7: // 魔王殿堂 — 独特深红主题
                floorTint = new Color(0.5f, 0.15f, 0.15f); wallTint = new Color(0.3f, 0.1f, 0.1f); vignetteTint = new Color(0.1f, 0f, 0f); break;
            case 8: // 熔岩回廊 — 熔金橙主题
                floorTint = new Color(1f, 0.75f, 0.55f); wallTint = new Color(0.7f, 0.3f, 0.15f); vignetteTint = new Color(0.08f, 0.02f, 0f); break;
            case 9: // 幽冥深井 — 幽蓝紫主题
                floorTint = new Color(0.35f, 0.3f, 0.5f); wallTint = new Color(0.45f, 0.4f, 0.65f); vignetteTint = new Color(0.04f, 0f, 0.08f); break;
            case 10: // 玄冰广原 — 冰蓝主题
                floorTint = new Color(0.85f, 0.95f, 1f); wallTint = new Color(0.6f, 0.75f, 0.9f); vignetteTint = new Color(0f, 0.02f, 0.05f); break;
            default:
                floorTint = new Color(0.9f, 0.88f, 0.92f); wallTint = new Color(0.6f, 0.55f, 0.65f); vignetteTint = new Color(0f, 0f, 0f); break;
        }
        ApplyThemeColors();
    }

    private void ApplyThemeColors()
    {
        if (floorSpriteRenderer != null) floorSpriteRenderer.color = floorTint;
        if (wallSpriteRenderers != null) { foreach (var sr in wallSpriteRenderers) { if (sr != null) sr.color = wallTint; } }
        if (vignetteSpriteRenderer != null) vignetteSpriteRenderer.color = new Color(vignetteTint.r, vignetteTint.g, vignetteTint.b, 1f);
        for (int i = 0; i < floorTiles.Count; i++) { if (floorTiles[i] != null) floorTiles[i].color = floorTint; }
        for (int i = 0; i < wallTiles.Count; i++) { if (wallTiles[i] != null) wallTiles[i].color = wallTint; }
        torches.Clear(); crystals.Clear(); vines.Clear(); lavaCracks.Clear();
        if (decoContainer != null) Destroy(decoContainer);
        CreateDecorations();
    }

    // ======================== FLOOR ========================

    private void CreateFloor()
    {
        if (floorObj != null) Destroy(floorObj);
        floorObj = new GameObject("DungeonFloor");
        floorObj.transform.SetParent(transform, false);
        floorObj.transform.position = new Vector3(0f, 0f, 5f);
        floorTiles.Clear();

        float hw = currentMap.mapHalfWidth;
        float hh = currentMap.mapHalfHeight;

        // Stage 0 uses a hand-painted map image as the floor
        Sprite floorSprite = null;
        bool useMapImage = false;

        if (currentTheme == 0)
        {
            floorSprite = LoadCachedSprite(ResourcePaths.Map_Stage0);
            if (floorSprite != null) useMapImage = true;
        }

        // 遗迹地砖拼贴: 每格随机砖块+随机翻转/旋转+明度抖动, 消除整图单贴图的重复感
        if (!useMapImage && RuinsFloor.Count > 0)
        {
            // 暗色底衬: 主题地板贴图压暗铺底, 石板间缝隙呈暗色石基而非纯黑
            string underName = currentTheme switch
            {
                2 => "Floor_Ice",
                3 => "Floor_Fire",
                4 => "Floor_Shadow",
                8 => "Floor_Fire",
                9 => "Floor_Shadow",
                10 => "Floor_Ice",
                1 => "Floor_Forest",
                _ => "Floor_Stone"
            };
            var underSprite = LoadCachedSprite(ResourcePaths.DungeonBase + underName);
            if (underSprite != null)
            {
                var underGo = new GameObject("FloorUnderlay");
                underGo.transform.SetParent(floorObj.transform, false);
                underGo.transform.localPosition = Vector3.zero;
                var usr = underGo.AddComponent<SpriteRenderer>();
                usr.sprite = underSprite;
                usr.sortingOrder = -11;
                float uw = underSprite.rect.width / underSprite.pixelsPerUnit;
                float uh = underSprite.rect.height / underSprite.pixelsPerUnit;
                underGo.transform.localScale = new Vector3(hw * 2f / uw, hh * 2f / uh, 1f);
                usr.color = new Color(floorTint.r * 0.3f, floorTint.g * 0.3f, floorTint.b * 0.35f, 1f);
            }

            float tile = 2.6f;
            int nx = Mathf.Max(1, Mathf.CeilToInt(hw * 2f / tile));
            int ny = Mathf.Max(1, Mathf.CeilToInt(hh * 2f / tile));
            float tw = hw * 2f / nx;
            float th = hh * 2f / ny;
            for (int gx = 0; gx < nx; gx++)
            {
                for (int gy = 0; gy < ny; gy++)
                {
                    var sp = RuinsFloor[Random.Range(0, RuinsFloor.Count)];
                    var cellGo = new GameObject($"Floor_{gx}_{gy}");
                    cellGo.transform.SetParent(floorObj.transform, false);
                    cellGo.transform.localPosition = new Vector3(-hw + (gx + 0.5f) * tw, -hh + (gy + 0.5f) * th, 0f);
                    var sr = cellGo.AddComponent<SpriteRenderer>();
                    sr.sprite = sp;
                    sr.sortingOrder = -10;
                    float sw = sp.rect.width / sp.pixelsPerUnit;
                    float sh = sp.rect.height / sp.pixelsPerUnit;
                    float s = Mathf.Max(tw / sw, th / sh) * 1.04f; // 轻微重叠防缝隙
                    cellGo.transform.localScale = new Vector3(s, s, 1f);
                    sr.flipX = Random.value < 0.5f;
                    sr.flipY = Random.value < 0.5f;
                    if (Mathf.Abs(sw - sh) / Mathf.Max(sw, sh) < 0.2f)
                        cellGo.transform.localRotation = Quaternion.Euler(0f, 0f, 90 * Random.Range(0, 4));
                    float jitter = Random.Range(0.86f, 1f);
                    sr.color = new Color(floorTint.r * jitter, floorTint.g * jitter, floorTint.b * jitter, 1f);
                    floorTiles.Add(sr);
                }
            }
            floorSpriteRenderer = null;
            return;
        }

        if (!useMapImage)
        {
            string floorName = currentTheme switch
            {
                1 => "Floor_Forest",
                2 => "Floor_Ice",
                3 => "Floor_Fire",
                4 => "Floor_Shadow",
                5 => "Floor_Fire",     // 龙巢: 复用火焰地板(暗红)
                6 => "Floor_Shadow",   // 虚空: 复用暗影地板(紫黑)
                7 => "Floor_Fire",     // 魔王殿: 复用火焰地板(深红)
                8 => "Floor_Fire",     // 熔岩回廊: 复用火焰地板(熔金橙)
                9 => "Floor_Shadow",   // 幽冥深井: 复用暗影地板(幽紫)
                10 => "Floor_Ice",     // 玄冰广原: 复用冰霜地板(冰蓝)
                _ => "Floor_Stone"
            };
            floorSprite = LoadCachedSprite(ResourcePaths.DungeonBase + floorName);
        }

        if (floorSprite == null) floorSprite = SpriteCache.WhitePixel;

        var floorSr = floorObj.AddComponent<SpriteRenderer>();
        floorSr.sprite = floorSprite;
        floorSr.sortingOrder = -10;

        if (useMapImage)
        {
            float spriteWorldW = floorSprite.rect.width / floorSprite.pixelsPerUnit;
            float spriteWorldH = floorSprite.rect.height / floorSprite.pixelsPerUnit;
            float scaleX = (hw * 2f) / spriteWorldW;
            float scaleY = (hh * 2f) / spriteWorldH;
            float uniformScale = Mathf.Min(scaleX, scaleY);
            floorObj.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
            floorSr.color = Color.white;
        }
        else
        {
            // Scale floor sprite instead of Tiled mode (avoids Sprite Tiling import warning)
            float spriteWorldW = floorSprite.rect.width / floorSprite.pixelsPerUnit;
            float spriteWorldH = floorSprite.rect.height / floorSprite.pixelsPerUnit;
            float scaleX = (hw * 2f) / spriteWorldW;
            float scaleY = (hh * 2f) / spriteWorldH;
            floorObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);
            floorSr.color = floorTint;
        }

        floorSpriteRenderer = floorSr;
    }

    // ======================== WALLS ========================

    private void CreateWalls()
    {
        if (wallContainer != null) Destroy(wallContainer);
        wallContainer = new GameObject("DungeonWalls");
        wallContainer.transform.SetParent(transform, false);

        float hw = currentMap.mapHalfWidth;
        float hh = currentMap.mapHalfHeight;
        wallTiles.Clear();

        float wallThickness = 3f;

        // 碰撞条(无渲染) — 视觉与碰撞分离
        CreateWallCollider("WallTopCollider", new Vector3(0f, hh + wallThickness / 2f, 0f), new Vector3(hw * 2f + wallThickness * 2f, wallThickness, 1f));
        CreateWallCollider("WallBottomCollider", new Vector3(0f, -hh - wallThickness / 2f, 0f), new Vector3(hw * 2f + wallThickness * 2f, wallThickness, 1f));
        CreateWallCollider("WallLeftCollider", new Vector3(-hw - wallThickness / 2f, 0f, 0f), new Vector3(wallThickness, hh * 2f + wallThickness * 2f, 1f));
        CreateWallCollider("WallRightCollider", new Vector3(hw + wallThickness / 2f, 0f, 0f), new Vector3(wallThickness, hh * 2f + wallThickness * 2f, 1f));

        if (RuinsWallBig.Count > 0)
        {
            // 墙段拼贴: 随机墙块+翻转, 告别整条拉伸贴图
            TileWallRun(true, hw * 2f + wallThickness * 2f, new Vector3(0f, hh + wallThickness / 2f, 0f), wallThickness, true);
            TileWallRun(true, hw * 2f + wallThickness * 2f, new Vector3(0f, -hh - wallThickness / 2f, 0f), wallThickness, true);
            TileWallRun(false, hh * 2f + wallThickness * 2f, new Vector3(-hw - wallThickness / 2f, 0f, 0f), wallThickness, false);
            TileWallRun(false, hh * 2f + wallThickness * 2f, new Vector3(hw + wallThickness / 2f, 0f, 0f), wallThickness, false);
        }
        else
        {
            // 素材缺失回退: 拉伸砖墙(旧行为)
            Sprite wallSprite = LoadCachedSprite(ResourcePaths.Wall_Brick);
            if (wallSprite == null) wallSprite = SpriteCache.WhitePixel;

            wallSpriteRenderers = new SpriteRenderer[4];
            float wallSpanH = hw * 2f + wallThickness * 2f;
            float wallSpanV = hh * 2f + wallThickness * 2f;

            wallSpriteRenderers[0] = CreateWallSprite("WallTop", wallContainer, wallSprite, new Vector3(0f, hh + wallThickness / 2f, 0f), new Vector3(wallSpanH, wallThickness, 1f));
            wallSpriteRenderers[1] = CreateWallSprite("WallBottom", wallContainer, wallSprite, new Vector3(0f, -hh - wallThickness / 2f, 0f), new Vector3(wallSpanH, wallThickness, 1f));
            wallSpriteRenderers[2] = CreateWallSprite("WallLeft", wallContainer, wallSprite, new Vector3(-hw - wallThickness / 2f, 0f, 0f), new Vector3(wallThickness, wallSpanV, 1f));
            wallSpriteRenderers[3] = CreateWallSprite("WallRight", wallContainer, wallSprite, new Vector3(hw + wallThickness / 2f, 0f, 0f), new Vector3(wallThickness, wallSpanV, 1f));
        }

        CreateWallShadow();
    }

    // 沿一条墙线循环铺随机墙块; horizontal=横墙(块高对齐thickness), 竖墙(块宽对齐thickness)
    private void TileWallRun(bool horizontal, float span, Vector3 center, float thickness, bool alignByHeight)
    {
        float cursor = -span * 0.5f;
        int guard = 0;
        while (cursor < span * 0.5f - 0.05f && guard++ < 64)
        {
            var sp = RuinsWallBig[Random.Range(0, RuinsWallBig.Count)];
            float pieceMain = (alignByHeight ? sp.rect.height : sp.rect.width) / sp.pixelsPerUnit;
            float pieceCross = (alignByHeight ? sp.rect.width : sp.rect.height) / sp.pixelsPerUnit;
            float s = thickness / Mathf.Max(0.01f, pieceMain);
            float pieceLen = pieceCross * s;
            var go = new GameObject("WallPiece");
            go.transform.SetParent(wallContainer.transform, false);
            float along = Mathf.Min(cursor + pieceLen * 0.5f, span * 0.5f - pieceLen * 0.5f);
            if (horizontal) go.transform.position = new Vector3(center.x + along, center.y, 0f);
            else go.transform.position = new Vector3(center.x, center.y + along, 0f);
            go.transform.localScale = new Vector3(s, s, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = -5;
            sr.color = wallTint;
            sr.flipX = Random.value < 0.5f;
            wallTiles.Add(sr);
            cursor += pieceLen * 0.96f; // 轻微搭接防缝隙
        }
    }

    private void CreateWallCollider(string name, Vector3 pos, Vector3 scale)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(wallContainer.transform, false);
        obj.transform.position = pos;
        obj.transform.localScale = scale;
        obj.AddComponent<BoxCollider2D>();
    }

    private void CreateWallShadow()
    {
        float hw = currentMap.mapHalfWidth;
        float hh = currentMap.mapHalfHeight;
        Sprite shadowSprite = LoadCachedSprite(ResourcePaths.WallShadow);
        if (shadowSprite == null) { shadowSprite = SpriteCache.WhitePixel; }
        GameObject shadowObj = new GameObject("WallShadow");
        shadowObj.transform.SetParent(wallContainer.transform, false);
        shadowObj.transform.position = new Vector3(0f, hh - 1f, 0f);
        var sr = shadowObj.AddComponent<SpriteRenderer>();
        sr.sprite = shadowSprite;
        sr.sortingOrder = -4;
        shadowObj.transform.localScale = new Vector3(hw * 2f / 5f, hh / 3f, 1f);
    }

    private SpriteRenderer CreateWallSprite(string name, GameObject parent, Sprite sprite, Vector3 pos, Vector3 scale)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        obj.transform.position = pos; obj.transform.localScale = scale;
        var sr = obj.AddComponent<SpriteRenderer>();
        sr.sprite = sprite; sr.sortingOrder = -5; sr.color = wallTint;
        obj.AddComponent<BoxCollider2D>();
        return sr;
    }

    // ======================== OBSTACLES ========================

    // 小地图建筑标记 — 地图生成完毕时遍历一次地图障碍数据得出, 由MistSystem以Canvas+Image实例化绘制
    public struct BuildingMark { public Vector2 Pos; public float Size; public Color Color; }
    public static readonly List<BuildingMark> BuildingMarks = new List<BuildingMark>();

    private void CreateObstacles()
    {
        if (obstacleContainer != null) Destroy(obstacleContainer);
        obstacleContainer = new GameObject("Obstacles");
        obstacleContainer.transform.SetParent(transform, false);

        foreach (var obs in currentMap.obstacles)
        {
            GameObject obj = new GameObject("Obs_" + obs.type);
            obj.transform.SetParent(obstacleContainer.transform, false);
            obj.transform.position = obs.position;
            obj.transform.localEulerAngles = new Vector3(0f, 0f, obs.rotation);

            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -2;
            sr.sprite = CreateObstacleSprite(obs.type, out bool ruinsSourced);
            if (ruinsSourced)
            {
                // 灰白石质素材 → 主题色轻染 + 随机镜像, 同类障碍不再千人一面
                sr.color = Color.Lerp(wallTint, Color.white, 0.35f);
                sr.flipX = Random.value < 0.5f;
            }

            // All obstacles use CircleCollider2D — radius clamped to prevent player getting stuck
            if (obs.type != DungeonMapData.ObstacleType.LavaPool)
            {
                var cc = obj.AddComponent<CircleCollider2D>();
                // Cap collider radius at 0.8 — visual can be larger, but collision stays small
                cc.radius = Mathf.Min(0.8f, Mathf.Max(obs.size.x, obs.size.y) * 0.4f);
            }

            // Scale visual to match collider
            float visScale = obs.type == DungeonMapData.ObstacleType.Pillar ? 1.2f : (obs.type == DungeonMapData.ObstacleType.Rock ? 1.5f : 1f);
            obj.transform.localScale = new Vector3(visScale, visScale, 1f);

            // Special: lava pool gets glow
            if (obs.type == DungeonMapData.ObstacleType.LavaPool)
            {
                lavaCracks.Add(new LavaData { renderer = sr, phase = Random.Range(0f, Mathf.PI * 2f) });
            }
        }

        // 地图生成完毕: 遍历一次地图障碍数据, 生成小地图建筑标记
        BuildingMarks.Clear();
        foreach (var obs in currentMap.obstacles)
        {
            Color c;
            float size;
            switch (obs.type)
            {
                case DungeonMapData.ObstacleType.Pillar: c = new Color(0.78f, 0.72f, 0.60f, 0.95f); size = 9f; break;
                case DungeonMapData.ObstacleType.Tree: c = new Color(0.30f, 0.62f, 0.34f, 0.95f); size = 8f; break;
                case DungeonMapData.ObstacleType.IceSpike: c = new Color(0.62f, 0.85f, 1f, 0.95f); size = 8f; break;
                case DungeonMapData.ObstacleType.LavaPool: c = new Color(1f, 0.45f, 0.15f, 0.9f); size = 11f; break;
                case DungeonMapData.ObstacleType.Tombstone: c = new Color(0.62f, 0.62f, 0.66f, 0.95f); size = 7f; break;
                default: c = new Color(0.55f, 0.55f, 0.58f, 0.95f); size = 8f; break;   // Rock
            }
            BuildingMarks.Add(new BuildingMark { Pos = obs.position, Size = size, Color = c });
        }
        MistSystem.Instance?.DrawBuildings();
    }

    private Sprite CreateObstacleSprite(DungeonMapData.ObstacleType type, out bool ruinsSourced)
    {
        ruinsSourced = false;
        // 石柱/岩石/墓碑优先用遗迹素材随机variant
        switch (type)
        {
            case DungeonMapData.ObstacleType.Pillar:
                var col = PickRandom(RuinsPillar);
                if (col != null) { ruinsSourced = true; return col; }
                break;
            case DungeonMapData.ObstacleType.Rock:
            case DungeonMapData.ObstacleType.Tombstone:
                var rub = PickRandom(RuinsRock);
                if (rub != null) { ruinsSourced = true; return rub; }
                break;
        }
        string assetName = type switch
        {
            DungeonMapData.ObstacleType.Pillar => "Obstacle_Pillar",
            DungeonMapData.ObstacleType.Tree => "Obstacle_Tree",
            DungeonMapData.ObstacleType.Rock => "Obstacle_Rock",
            DungeonMapData.ObstacleType.IceSpike => "Obstacle_IceSpike",
            DungeonMapData.ObstacleType.LavaPool => "Obstacle_LavaPool",
            DungeonMapData.ObstacleType.Tombstone => "Obstacle_Tombstone",
            _ => "Obstacle_Rock"
        };
        Sprite loaded = LoadCachedSprite(ResourcePaths.DungeonBase + assetName);
        return loaded != null ? loaded : SpriteCache.WhitePixel;
    }

    // ======================== DECORATIONS (same as before) ========================

    private void CreateDecorations()
    {
        decoContainer = new GameObject("Decorations");
        decoContainer.transform.SetParent(transform, false);
        decoContainer.transform.position = Vector3.zero;
        switch (currentTheme)
        {
            case 0: CreateStoneDecorations(); break;
            case 1: CreateForestDecorations(); break;
            case 2: CreateIceDecorations(); break;
            case 3: CreateFireDecorations(); break;
            case 4: CreateShadowDecorations(); break;
            case 5: CreateFireDecorations(); break;  // 龙巢: 复用火焰装饰
            case 6: CreateShadowDecorations(); break; // 虚空: 复用暗影装饰
            case 7: CreateFireDecorations(); break;  // 魔王殿: 复用火焰装饰
        }
        // 遗迹残件散布(主题0手绘图地板除外): 补足8/9/10层无专属装饰的空缺
        if (currentTheme != 0) CreateRuinsScatter();
    }

    // 随机散布拱门残段/锁链柱/碎石堆 — 每次进图布局都不同
    private void CreateRuinsScatter()
    {
        var pool = new List<Sprite>();
        pool.AddRange(RuinsDeco);
        pool.AddRange(RuinsSmall);
        if (pool.Count == 0) return;
        int count = Mathf.Clamp(Mathf.RoundToInt(W * H / 45f), 6, 16);
        int attempts = 0;
        int placed = 0;
        while (placed < count && attempts++ < count * 6)
        {
            var sp = pool[Random.Range(0, pool.Count)];
            float x = Random.Range(-W + 3f, W - 3f);
            float y = Random.Range(-H + 2.5f, H - 2.5f);
            if (new Vector2(x, y).sqrMagnitude < 36f) continue; // 让开出生营地
            var go = new GameObject("RuinsDeco");
            go.transform.SetParent(decoContainer.transform, false);
            go.transform.position = new Vector3(x, y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.sortingOrder = -3;
            sr.flipX = Random.value < 0.5f;
            float sw = sp.rect.width / sp.pixelsPerUnit;
            float sh = sp.rect.height / sp.pixelsPerUnit;
            float s = Mathf.Clamp(Mathf.Min(2.8f / Mathf.Max(sw, 0.01f), 2.8f / Mathf.Max(sh, 0.01f)), 0.5f, 1.3f)
                      * Random.Range(0.8f, 1.15f);
            go.transform.localScale = new Vector3(s, s, 1f);
            sr.color = Color.Lerp(wallTint, Color.white, 0.25f);
            placed++;
        }
    }

    private float W => currentMap.mapHalfWidth;
    private float H => currentMap.mapHalfHeight;

    private void CreateStoneDecorations()
    {
        CreateTorch(new Vector3(-W + 4, H * 0.4f, 0f)); CreateTorch(new Vector3(W - 4, H * 0.4f, 0f));
        CreateTorch(new Vector3(-W + 4, -H * 0.4f, 0f)); CreateTorch(new Vector3(W - 4, -H * 0.4f, 0f));
        CreateTorch(new Vector3(-W * 0.4f, H - 1f, 0f)); CreateTorch(new Vector3(W * 0.4f, H - 1f, 0f));
        CreateTorch(new Vector3(-W * 0.4f, -H + 1f, 0f)); CreateTorch(new Vector3(W * 0.4f, -H + 1f, 0f));
        CreateDebris(new Vector3(-W * 0.3f, -H * 0.6f, 0f)); CreateDebris(new Vector3(W * 0.3f, H * 0.6f, 0f));
    }

    private void CreateForestDecorations()
    {
        CreateTorch(new Vector3(-W + 4, H * 0.3f, 0f)); CreateTorch(new Vector3(W - 4, H * 0.3f, 0f));
        CreateMushroom(new Vector3(-W * 0.5f, -H + 2f, 0f)); CreateMushroom(new Vector3(W * 0.5f, -H + 2f, 0f));
        CreateVine(new Vector3(-W - 0.5f, H * 0.3f, 0f)); CreateVine(new Vector3(W + 0.5f, -H * 0.2f, 0f));
    }

    private void CreateIceDecorations()
    {
        CreateCrystal(new Vector3(-W * 0.4f, -H * 0.5f, 0f)); CreateCrystal(new Vector3(W * 0.4f, -H * 0.5f, 0f));
        CreateTorch(new Vector3(-W + 4, H * 0.2f, 0f)); CreateTorch(new Vector3(W - 4, H * 0.2f, 0f));
        CreateIcicle(new Vector3(-W * 0.3f, H - 0.5f, 0f)); CreateIcicle(new Vector3(W * 0.3f, H - 0.5f, 0f));
    }

    private void CreateFireDecorations()
    {
        CreateTorch(new Vector3(-W + 4, H * 0.4f, 0f)); CreateTorch(new Vector3(W - 4, H * 0.4f, 0f));
        CreateTorch(new Vector3(-W + 4, -H * 0.4f, 0f)); CreateTorch(new Vector3(W - 4, -H * 0.4f, 0f));
        CreateTorch(new Vector3(-W * 0.5f, 0f, 0f)); CreateTorch(new Vector3(W * 0.5f, 0f, 0f));
        CreateLavaCrack(new Vector3(-W * 0.2f, -H * 0.3f, 0f)); CreateLavaCrack(new Vector3(W * 0.2f, H * 0.3f, 0f));
    }

    private void CreateShadowDecorations()
    {
        CreateCrystal(new Vector3(-W * 0.5f, -H * 0.5f, 0f)); CreateCrystal(new Vector3(W * 0.5f, H * 0.3f, 0f));
        CreateTorch(new Vector3(-W + 4, H * 0.3f, 0f)); CreateTorch(new Vector3(W - 4, -H * 0.3f, 0f));
        CreateCobweb(new Vector3(-W + 2, H - 2, 0f), false); CreateCobweb(new Vector3(W - 2, H - 2, 0f), true);
    }

    // ======================== DECORATION BUILDERS (animated) ========================

    private void CreateTorch(Vector3 pos)
    {
        Sprite torchSprite = LoadCachedSprite(ResourcePaths.Deco_Torch);
        Sprite flameSprite = LoadCachedSprite(ResourcePaths.Deco_Flame);

        GameObject torchObj = new GameObject("Torch");
        torchObj.transform.SetParent(decoContainer.transform, false);
        torchObj.transform.position = pos;
        var sr = torchObj.AddComponent<SpriteRenderer>();
        sr.sprite = torchSprite != null ? torchSprite : SpriteCache.WhitePixel;
        sr.sortingOrder = -3;

        GameObject flameObj = new GameObject("Flame"); flameObj.transform.SetParent(torchObj.transform, false); flameObj.transform.localPosition = new Vector3(0, 0.6f, 0);
        var flameSr = flameObj.AddComponent<SpriteRenderer>();
        flameSr.sprite = flameSprite != null ? flameSprite : SpriteCache.WhitePixel;
        flameSr.sortingOrder = -2;
        flameObj.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        torches.Add(new TorchData { flameRenderer = flameSr, baseAlpha = 0.25f, phase = Random.Range(0f, Mathf.PI * 2f) });
    }

    private void CreateCobweb(Vector3 pos, bool flipX)
    {
        Sprite webSprite = LoadCachedSprite(ResourcePaths.Deco_Cobweb);
        GameObject o = new GameObject("Cobweb"); o.transform.SetParent(decoContainer.transform, false); o.transform.position = pos;
        var sr = o.AddComponent<SpriteRenderer>(); sr.sprite = webSprite != null ? webSprite : SpriteCache.WhitePixel;
        sr.sortingOrder = -1; sr.flipX = flipX; o.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
    }

    private void CreateDebris(Vector3 pos)
    {
        Sprite debrisSprite = LoadCachedSprite(ResourcePaths.Deco_Debris);
        GameObject o = new GameObject("Debris"); o.transform.SetParent(decoContainer.transform, false); o.transform.position = pos;
        var sr = o.AddComponent<SpriteRenderer>(); sr.sprite = debrisSprite != null ? debrisSprite : SpriteCache.WhitePixel;
        sr.sortingOrder = -3;
    }

    private void CreateMushroom(Vector3 pos)
    {
        Sprite mushSprite = LoadCachedSprite(ResourcePaths.Deco_Mushroom);
        GameObject o = new GameObject("Mushroom"); o.transform.SetParent(decoContainer.transform, false); o.transform.position = pos;
        var sr = o.AddComponent<SpriteRenderer>(); sr.sprite = mushSprite != null ? mushSprite : SpriteCache.WhitePixel;
        sr.sortingOrder = -3; o.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
    }

    private void CreateVine(Vector3 pos)
    {
        Sprite vineSprite = LoadCachedSprite(ResourcePaths.Deco_Vine);
        GameObject o = new GameObject("Vine"); o.transform.SetParent(decoContainer.transform, false); o.transform.position = pos;
        var sr = o.AddComponent<SpriteRenderer>(); sr.sprite = vineSprite != null ? vineSprite : SpriteCache.WhitePixel;
        sr.sortingOrder = -1; o.transform.localScale = new Vector3(0.6f, 0.4f, 1f);
        vines.Add(new VineData { transform = o.transform, phase = Random.Range(0f, Mathf.PI * 2f), amplitude = Random.Range(2f, 5f) });
    }

    private void CreateCrystal(Vector3 pos)
    {
        bool isShadow = currentTheme == 4;
        Sprite crystalSprite = LoadCachedSprite(isShadow ? ResourcePaths.Deco_Crystal_Shadow : ResourcePaths.Deco_Crystal_Ice);
        GameObject o = new GameObject("Crystal"); o.transform.SetParent(decoContainer.transform, false); o.transform.position = pos;
        var sr = o.AddComponent<SpriteRenderer>(); sr.sprite = crystalSprite != null ? crystalSprite : SpriteCache.WhitePixel;
        sr.sortingOrder = -3; o.transform.localScale = new Vector3(0.5f, 0.4f, 1f);

        crystals.Add(new CrystalData { baseAlpha = 0.2f, phase = Random.Range(0f, Mathf.PI * 2f), renderer = sr });
    }

    private void CreateIcicle(Vector3 pos)
    {
        Sprite icicleSprite = LoadCachedSprite(ResourcePaths.Deco_Icicle);
        GameObject o = new GameObject("Icicle"); o.transform.SetParent(decoContainer.transform, false); o.transform.position = pos;
        var sr = o.AddComponent<SpriteRenderer>(); sr.sprite = icicleSprite != null ? icicleSprite : SpriteCache.WhitePixel;
        sr.sortingOrder = -1; o.transform.localScale = new Vector3(0.4f, 0.3f, 1f);
    }

    private void CreateLavaCrack(Vector3 pos)
    {
        Sprite lavaSprite = LoadCachedSprite(ResourcePaths.Deco_LavaCrack);
        GameObject o = new GameObject("LavaCrack"); o.transform.SetParent(decoContainer.transform, false); o.transform.position = pos;
        var sr = o.AddComponent<SpriteRenderer>(); sr.sprite = lavaSprite != null ? lavaSprite : SpriteCache.WhitePixel;
        sr.sortingOrder = -3;

        lavaCracks.Add(new LavaData { renderer = sr, phase = Random.Range(0f, Mathf.PI * 2f) });
    }

    // ======================== ATMOSPHERIC ========================

    private void CreateAmbientLighting()
    {
        // No runtime Light component — 2D game uses sprite-based visuals only

        Sprite vigSprite = LoadCachedSprite(ResourcePaths.Vignette);
        // Vignette现在挂到DungeonVisuals下随DV销毁，只需清理Camera子物体上可能残留的旧Vignette
        var cam = Camera.main;
        if (cam != null)
        {
            for (int i = cam.transform.childCount - 1; i >= 0; i--)
            {
                var child = cam.transform.GetChild(i);
                if (child.name == "Vignette") Destroy(child.gameObject);
            }
        }
        GameObject vignetteObj = new GameObject("Vignette");
        vignetteObj.transform.SetParent(transform, false); // 挂到DungeonVisuals下，随DV一起销毁
        vignetteObj.transform.position = new Vector3(0f, 0f, 2f);
        var vigSr = vignetteObj.AddComponent<SpriteRenderer>();
        vigSr.sprite = vigSprite != null ? vigSprite : SpriteCache.WhitePixel;
        vigSr.sortingOrder = 20; vignetteSpriteRenderer = vigSr;
        // 不再挂到Camera上 — 挂到DungeonVisuals下，随DV销毁
        Camera mainCam = Camera.main;
        if (mainCam != null) { vignetteObj.transform.localScale = new Vector3(68f, 50f, 1f); }
    }



    private void CreateFogLayers()
    {
        // 雾层已禁用 — WhitePixel fallback导致黑色方块
    }

    private void CreateLightRays()
    {
        Sprite raySprite = LoadCachedSprite(ResourcePaths.LightRay);
        if (raySprite == null) raySprite = SpriteCache.WhitePixel;

        for (int i = 0; i < 3; i++)
        {
            Color rc = new Color(1f, 0.95f, 0.8f, 0.08f);
            GameObject rayObj = new GameObject("LightRay_" + i);
            rayObj.transform.SetParent(transform, false);
            rayObj.transform.position = new Vector3(-W * 0.5f + i * W * 0.5f, H * 0.5f, -3f);
            rayObj.transform.localEulerAngles = new Vector3(0f, 0f, Random.Range(-15f, 15f));
            var sr = rayObj.AddComponent<SpriteRenderer>();
            sr.sprite = raySprite;
            sr.sortingOrder = -7; sr.color = rc; rayObj.transform.localScale = new Vector3(2f, H, 1f);
            lightRays.Add(new LightRayData { transform = rayObj.transform, renderer = sr, phase = Random.Range(0f, Mathf.PI * 2f), baseAlpha = 0.08f });
        }
    }

    // ======================== ANIMATION UPDATES ========================

    private void AnimateTorches() { for (int i = 0; i < torches.Count; i++) { var t = torches[i]; if (t.flameRenderer == null) continue; float f = 1f + Mathf.Sin(animTime * 8f + t.phase) * 0.15f + Mathf.Sin(animTime * 13f + t.phase * 2f) * 0.08f + Mathf.Sin(animTime * 21f + t.phase * 3f) * 0.05f; t.flameRenderer.color = new Color(1f, 0.65f * f, 0.1f * f, 0.9f); } }
    private void AnimateCrystals() { for (int i = 0; i < crystals.Count; i++) { var c = crystals[i]; if (c.renderer == null) continue; float p = 1f + Mathf.Sin(animTime * 2f + c.phase) * 0.3f; c.renderer.color = new Color(c.renderer.color.r, c.renderer.color.g, c.renderer.color.b, c.baseAlpha * p); } }
    private void AnimateVines() { for (int i = 0; i < vines.Count; i++) { var v = vines[i]; if (v.transform == null) continue; v.transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(animTime * 1.2f + v.phase) * v.amplitude); } }
    private void AnimateLavaCracks() { for (int i = 0; i < lavaCracks.Count; i++) { var l = lavaCracks[i]; float p = 1f + Mathf.Sin(animTime * 3f + l.phase) * 0.4f + Mathf.Sin(animTime * 7f + l.phase * 2f) * 0.15f; if (l.renderer != null) l.renderer.color = new Color(1f, 0.3f + p * 0.15f, 0.05f + p * 0.05f, 1f); } }

    private void AnimateFogLayers() { for (int i = 0; i < fogLayers.Count; i++) { var f = fogLayers[i]; if (f.transform == null) continue; Vector3 pos = f.transform.position; pos.x += f.speed * Time.deltaTime; if (f.speed > 0 && pos.x > f.width * 0.5f) pos.x -= f.width; else if (f.speed < 0 && pos.x < -f.width * 0.5f) pos.x += f.width; f.transform.position = pos; } }
    private void AnimateLightRays() { for (int i = 0; i < lightRays.Count; i++) { var r = lightRays[i]; if (r.renderer == null) continue; float a = r.baseAlpha * (0.5f + Mathf.Sin(animTime * 0.6f + r.phase) * 0.5f); Color c = r.renderer.color; c.a = a; r.renderer.color = c; if (r.transform != null) r.transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Sin(animTime * 0.3f + r.phase) * 2f); } }

}
