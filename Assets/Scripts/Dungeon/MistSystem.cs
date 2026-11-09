using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 秘境迷雾系统 — 迷雾网格(移动驱散) + HUD小地图 + 散布资源点 + 出生营地
/// 根物体挂在DungeonVisuals子物体下, 地图销毁时自动清理; 仅秘境Dungeon状态构建
/// </summary>
public class MistSystem : MonoBehaviour
{
    public static MistSystem Instance { get; private set; }

    private const float FogAlpha = 0.93f;        // 未探索迷雾浓度
    private const float NodeCollectRadius = 1.7f;

    // 照亮半径随地图尺寸自适应 (小地图不至于一点营地亮半图)
    private float _revealRadius;   // 玩家照亮半径
    private float _campReveal;     // 营地初始照亮半径
    private float _nodeMinDist;    // 资源点离营地最小距离

    private class ResourceNode
    {
        public GameObject Go;
        public Vector2 Pos;
        public int Kind;      // 0草药 1矿石 2补给箱
        public bool Taken;
        public SpriteRenderer Sr;
        public float Phase;
    }

    private readonly List<ResourceNode> _nodes = new List<ResourceNode>();
    private bool[,] _revealed;
    private int _revealedCount;
    private int _gw, _gh;
    private float _cellSize;
    private float _halfW, _halfH;
    private Vector2 _campPos = Vector2.zero;
    private float _revealTimer;
    private bool _worldMode;              // 大世界小地图模式 (无迷雾, 全图可见)
    private static bool _spawnAsWorld;    // SpawnForWorld传给Awake的标记

    // 软迷雾 (单张纹理, 径向渐变驱散, 无方块感)
    private const float FogPxPerUnit = 8f;
    private Texture2D _fogTex;
    private bool _fogDirty;
    private float _fogApplyTimer;

    // 漂浮雾絮 (径向渐变运行时生成, 避免WhitePixel方块)
    private class WispData { public GameObject Go; public SpriteRenderer Sr; public Vector2 Vel; public float Phase; public float BaseAlpha; }
    private readonly List<WispData> _wisps = new List<WispData>();
    private Texture2D _wispTex;
    private Sprite _wispSprite;

    // 小地图
    private Texture2D _mmTex;
    private GameObject _mmPanel;
    private RectTransform _mmMapRect;
    private RectTransform _playerDot;
    private RectTransform _campMarker;
    private readonly List<Image> _enemyDots = new List<Image>();
    private float _enemyDotTimer;
    private bool _mmDirty;
    private float _mmApplyTimer;
    private static readonly Color MmFog = new Color(0.045f, 0.05f, 0.09f);
    private static readonly Color MmLand = new Color(0.42f, 0.45f, 0.52f);
    private static readonly Color MmCamp = new Color(1f, 0.85f, 0.3f);
    private static readonly Color[] MmNodeColors =
    {
        new Color(0.3f, 0.9f, 0.4f),
        new Color(0.55f, 0.75f, 1f),
        new Color(1f, 0.7f, 0.2f)
    };

    // ===== 验证/调试只读 =====
    public int CellCount => _gw * _gh;
    public int RevealedCount => _revealedCount;
    public int NodeCount => _nodes.Count;
    public int NodeTakenCount { get; private set; }
    public bool MinimapReady => _mmTex != null;
    public Vector2 GetNodePos(int i) => i >= 0 && i < _nodes.Count ? _nodes[i].Pos : Vector2.zero;
    public int GetNodeKind(int i) => i >= 0 && i < _nodes.Count ? _nodes[i].Kind : -1;
    public bool IsNodeTaken(int i) => i >= 0 && i < _nodes.Count && _nodes[i].Taken;

    private Transform Player => GameManager.Instance != null ? GameManager.Instance.Player?.transform : null;

    /// <summary>在当前副本地图上构建迷雾/营地/资源点/小地图 (秘境Dungeon与无尽Endless)</summary>
    public static void SpawnForMap()
    {
        var gm = GameManager.Instance;
        if (gm == null || (gm.CurrentState != GameState.Dungeon && gm.CurrentState != GameState.Endless)) return;
        if (DungeonVisuals.Instance == null) return;

        if (Instance != null)
        {
            var old = Instance.gameObject;
            Instance = null;
            Destroy(old);
        }

        var go = new GameObject("MistSystem");
        go.transform.SetParent(DungeonVisuals.Instance.transform, false);
        go.AddComponent<MistSystem>();
    }

    /// <summary>大世界小地图 (无迷雾全图可见, 不建雾/营地/资源点)</summary>
    public static void SpawnForWorld()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.InWorld) return;
        if (WorldVisuals.Instance == null) return;

        if (Instance != null)
        {
            var old = Instance.gameObject;
            Instance = null;
            Destroy(old);
        }

        var go = new GameObject("MistSystem");
        go.transform.SetParent(WorldVisuals.Instance.transform, false);
        _spawnAsWorld = true;
        go.AddComponent<MistSystem>();
        _spawnAsWorld = false;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _worldMode = _spawnAsWorld;

        if (_worldMode)
        {
            // 大世界: 仅小地图, 无迷雾/营地/资源点
            _halfW = WorldData.WorldHalf;
            _halfH = WorldData.WorldHalf;
            _cellSize = (_halfW * 2f) / 72f; // 72x72纹理: 每区18像素便于画分界
            _gw = _gh = 72;
            _revealed = null;
            BuildMinimap();
            return;
        }

        var gm = GameManager.Instance;
        int theme = GameManager.RiftLayerToTheme(gm.CurrentStageIndex);
        var map = DungeonMapData.GetStageMap(theme); // 仅用尺寸(表值固定)
        _halfW = map.mapHalfWidth;
        _halfH = map.mapHalfHeight;
        // 照亮半径按两半轴几何平均缩放: 非正方形图(30x12/13x30/34x18)不再因minHalf塌陷到4,
        // 各图解锁范围收敛到6.0~7.5(旧公式4.0~7.5, 面积差3倍); 几何平均仍保证窄井(半宽6.5)不被全亮
        float geoHalf = Mathf.Sqrt(_halfW * _halfH);
        float minHalf = Mathf.Min(_halfW, _halfH);
        _revealRadius = Mathf.Clamp(geoHalf * 0.45f, 5.5f, 7.5f);
        _campReveal = Mathf.Clamp(geoHalf * 0.75f, 9f, 12f);
        _nodeMinDist = Mathf.Min(12f, minHalf * 0.55f);

        BuildFog();
        BuildCamp();
        BuildResourceNodes();
        BuildMinimap();
    }

    private void OnDestroy()
    {
        if (_mmPanel != null) Destroy(_mmPanel);
        if (_mmTex != null) Destroy(_mmTex);
        if (_fogTex != null) Destroy(_fogTex);
        if (_wispTex != null) Destroy(_wispTex);
        if (_wispSprite != null) Destroy(_wispSprite);
        if (Instance == this) Instance = null;
    }

    // ===== 迷雾网格 =====
    private void BuildFog()
    {
        // 逻辑网格仅供小地图门控 (列≈18行≈14)
        _cellSize = Mathf.Max((_halfW * 2f) / 18f, (_halfH * 2f) / 14f);
        _gw = Mathf.Max(1, Mathf.CeilToInt((_halfW * 2f) / _cellSize));
        _gh = Mathf.Max(1, Mathf.CeilToInt((_halfH * 2f) / _cellSize));
        _revealed = new bool[_gw, _gh];

        // 软迷雾纹理: 双线性过滤 + 驱散时径向渐变, 替代方块格子
        int tw = Mathf.Clamp(Mathf.RoundToInt(_halfW * 2f * FogPxPerUnit), 32, 384);
        int th = Mathf.Clamp(Mathf.RoundToInt(_halfH * 2f * FogPxPerUnit), 32, 384);
        _fogTex = new Texture2D(tw, th, TextureFormat.RGBA32, false);
        _fogTex.filterMode = FilterMode.Bilinear;
        _fogTex.wrapMode = TextureWrapMode.Clamp;
        var full = new Color(0.02f, 0.03f, 0.05f, FogAlpha);
        var pixels = new Color[tw * th];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = full;

        // 低频噪点烘焙: 迷雾呈薄雾明暗起伏, 不再是死黑平面
        int cw = tw / 24 + 2, ch = th / 24 + 2;
        var noise = new float[cw * ch];
        for (int i = 0; i < noise.Length; i++) noise[i] = Random.Range(-1f, 1f);
        for (int y = 0; y < th; y++)
        {
            float fy = y / 24f;
            int ny0 = Mathf.FloorToInt(fy);
            float ty = Mathf.SmoothStep(0f, 1f, fy - ny0);
            for (int x = 0; x < tw; x++)
            {
                float fx = x / 24f;
                int nx0 = Mathf.FloorToInt(fx);
                float tx = Mathf.SmoothStep(0f, 1f, fx - nx0);
                float n = Mathf.Lerp(
                    Mathf.Lerp(noise[ny0 * cw + nx0], noise[ny0 * cw + nx0 + 1], tx),
                    Mathf.Lerp(noise[(ny0 + 1) * cw + nx0], noise[(ny0 + 1) * cw + nx0 + 1], tx), ty);
                var p = pixels[y * tw + x];
                p.a = Mathf.Clamp01(FogAlpha + n * 0.05f);
                pixels[y * tw + x] = p;
            }
        }
        _fogTex.SetPixels(pixels);
        _fogTex.Apply();

        BuildWisps();

        var go = new GameObject("FogSprite");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(0f, 0f, 8f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Sprite.Create(_fogTex, new Rect(0, 0, tw, th), new Vector2(0.5f, 0.5f), tw / (_halfW * 2f));
        sr.sortingOrder = 40;
    }

    // 雾絮: 数张柔和雾团在迷雾上层缓慢漂移
    private void BuildWisps()
    {
        const int S = 128;
        _wispTex = new Texture2D(S, S, TextureFormat.RGBA32, false);
        _wispTex.filterMode = FilterMode.Bilinear;
        _wispTex.wrapMode = TextureWrapMode.Clamp;
        var wpx = new Color[S * S];
        var c = new Vector2(S / 2f - 0.5f, S / 2f - 0.5f);
        float maxD = S * 0.5f;
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), c) / maxD;
                float a = Mathf.Clamp01(1f - d);
                wpx[y * S + x] = new Color(1f, 1f, 1f, a * a * (3f - 2f * a)); // smoothstep径向渐变
            }
        _wispTex.SetPixels(wpx);
        _wispTex.Apply();
        _wispSprite = Sprite.Create(_wispTex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), S / 4f); // 基础4单位

        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject("FogWisp_" + i);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(Random.Range(-_halfW, _halfW), Random.Range(-_halfH, _halfH), 6f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _wispSprite;
            sr.sortingOrder = 41; // 迷雾(40)之上
            sr.color = new Color(0.72f, 0.78f, 0.9f, 0f);
            float sc = Random.Range(1.8f, 3f);
            go.transform.localScale = new Vector3(sc, sc * Random.Range(0.6f, 0.85f), 1f);
            var ang = Random.Range(0f, Mathf.PI * 2f);
            _wisps.Add(new WispData
            {
                Go = go,
                Sr = sr,
                Vel = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.4f) * Random.Range(0.25f, 0.5f),
                Phase = Random.Range(0f, 6.28f),
                BaseAlpha = Random.Range(0.05f, 0.09f)
            });
        }
    }

    private void Update()
    {
        var p = Player;
        if (p == null) return;
        float dt = Time.deltaTime;

        if (!_worldMode)
        {
            _revealTimer -= dt;
            if (_revealTimer <= 0f)
            {
                _revealTimer = 0.12f;
                RevealAround(p.position, _revealRadius);
            }
        }

        // 迷雾纹理上屏节流
        if (_fogDirty && Time.unscaledTime - _fogApplyTimer > 0.15f)
        {
            _fogApplyTimer = Time.unscaledTime;
            _fogDirty = false;
            _fogTex.Apply();
        }

        UpdateNodes(dt, p.position);
        UpdateWisps(dt);
        UpdateMinimap(p.position);
    }

    private void UpdateWisps(float dt)
    {
        for (int i = 0; i < _wisps.Count; i++)
        {
            var w = _wisps[i];
            if (w.Go == null) continue;
            var pos = w.Go.transform.position;
            pos.x += w.Vel.x * dt;
            pos.y += w.Vel.y * dt;
            // 越界环绕
            if (pos.x > _halfW + 3f) pos.x = -_halfW - 3f;
            if (pos.x < -_halfW - 3f) pos.x = _halfW + 3f;
            if (pos.y > _halfH + 3f) pos.y = -_halfH - 3f;
            if (pos.y < -_halfH - 3f) pos.y = _halfH + 3f;
            w.Go.transform.position = pos;
            w.Phase += dt * 0.7f;
            if (w.Sr != null)
            {
                var c = w.Sr.color;
                c.a = w.BaseAlpha * (0.6f + Mathf.Sin(w.Phase) * 0.4f);
                w.Sr.color = c;
            }
        }
    }

    private void RevealAround(Vector3 pos, float radius)
    {
        // 逻辑网格 (小地图门控)
        int cx = WorldToCellX(pos.x), cy = WorldToCellY(pos.y);
        int r = Mathf.CeilToInt(radius / _cellSize);
        float rSq = radius * radius;
        bool changed = false;
        for (int x = cx - r; x <= cx + r; x++)
        {
            if (x < 0 || x >= _gw) continue;
            for (int y = cy - r; y <= cy + r; y++)
            {
                if (y < 0 || y >= _gh) continue;
                if (_revealed[x, y]) continue;
                float wx = -_halfW + (x + 0.5f) * _cellSize;
                float wy = -_halfH + (y + 0.5f) * _cellSize;
                float dx = wx - pos.x, dy = wy - pos.y;
                if (dx * dx + dy * dy > rSq) continue;
                _revealed[x, y] = true;
                _revealedCount++;
                changed = true;
            }
        }
        if (changed) _mmDirty = true;

        // 软迷雾径向烙印: 内圈完全透明, 外圈45%半径内smoothstep过渡到全雾
        if (_fogTex != null)
        {
            int tw = _fogTex.width, th = _fogTex.height;
            float pxPerUnit = tw / (_halfW * 2f);
            int pr = Mathf.CeilToInt(radius * pxPerUnit);
            int px = Mathf.RoundToInt((pos.x + _halfW) * pxPerUnit);
            int py = Mathf.RoundToInt((pos.y + _halfH) * pxPerUnit);
            int x0 = Mathf.Max(0, px - pr), y0 = Mathf.Max(0, py - pr);
            int x1 = Mathf.Min(tw - 1, px + pr), y1 = Mathf.Min(th - 1, py + pr);
            int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
            if (bw > 0 && bh > 0)
            {
                float inner = radius * 0.45f * pxPerUnit; // 更宽的柔边过渡带
                float outer = radius * pxPerUnit;
                var block = _fogTex.GetPixels(x0, y0, bw, bh);
                bool touched = false;
                for (int lx = 0; lx < bw; lx++)
                {
                    float dx = (x0 + lx) - px;
                    for (int ly = 0; ly < bh; ly++)
                    {
                        float dy = (y0 + ly) - py;
                        float dSq = dx * dx + dy * dy;
                        if (dSq > outer * outer) continue;
                        float t = Mathf.Clamp01((Mathf.Sqrt(dSq) - inner) / Mathf.Max(0.01f, outer - inner));
                        float a = FogAlpha * (t * t * (3f - 2f * t)); // smoothstep软边
                        int idx = ly * bw + lx;
                        if (block[idx].a > a) { block[idx].a = a; touched = true; }
                    }
                }
                if (touched)
                {
                    _fogTex.SetPixels(x0, y0, bw, bh, block);
                    _fogDirty = true;
                }
            }
        }
    }

    private int WorldToCellX(float wx) => Mathf.FloorToInt((wx + _halfW) / _cellSize);
    private int WorldToCellY(float wy) => Mathf.FloorToInt((wy + _halfH) / _cellSize);

    // ===== 出生营地 =====
    private void BuildCamp()
    {
        _campPos = Vector2.zero;
        var sprite = SpriteCache.WhitePixel;

        var glow = new GameObject("CampGlow");
        glow.transform.SetParent(transform, false);
        glow.transform.position = new Vector3(0f, 0f, 3f);
        var gsr = glow.AddComponent<SpriteRenderer>();
        gsr.sprite = sprite;
        gsr.color = new Color(1f, 0.85f, 0.4f, 0.15f);
        gsr.sortingOrder = 2;
        glow.transform.localScale = new Vector3(16f, 16f, 1f);

        var glow2 = new GameObject("CampGlowInner");
        glow2.transform.SetParent(transform, false);
        glow2.transform.position = new Vector3(0f, 0f, 3f);
        var g2sr = glow2.AddComponent<SpriteRenderer>();
        g2sr.sprite = sprite;
        g2sr.color = new Color(1f, 0.9f, 0.5f, 0.12f);
        g2sr.sortingOrder = 2;
        glow2.transform.localScale = new Vector3(9f, 9f, 1f);

        var post = new GameObject("CampPost");
        post.transform.SetParent(transform, false);
        post.transform.position = new Vector3(0f, 2.4f, -1f);
        var psr = post.AddComponent<SpriteRenderer>();
        psr.sprite = sprite;
        psr.color = new Color(1f, 0.85f, 0.4f, 0.9f);
        psr.sortingOrder = 3;
        post.transform.localScale = new Vector3(0.4f, 3.4f, 1f);

        var lblGo = new GameObject("CampLabel");
        lblGo.transform.SetParent(transform, false);
        lblGo.transform.position = new Vector3(0f, 5f, -1f);
        var lbl = lblGo.AddComponent<Text>();
        lbl.text = "营地";
        lbl.font = GameManager.GetUIFont();
        lbl.fontSize = 40;
        lbl.color = new Color(1f, 0.9f, 0.55f, 0.95f);
        lbl.alignment = TextAnchor.MiddleCenter;
        lbl.raycastTarget = false;
        lblGo.transform.localScale = new Vector3(0.08f, 0.08f, 1f);

        RevealAround(Vector3.zero, _campReveal);
    }

    // ===== 散布资源点 =====
    private void BuildResourceNodes()
    {
        int layer = GameManager.Instance != null ? GameManager.Instance.RiftLayer : 1;
        int count = Mathf.Clamp(6 + layer / 2, 6, 14);
        var sprite = SpriteCache.WhitePixel;
        int attempts = 0;
        while (_nodes.Count < count && attempts < count * 40)
        {
            attempts++;
            float x = Random.Range(-_halfW + 4f, _halfW - 4f);
            float y = Random.Range(-_halfH + 4f, _halfH - 4f);
            var pos = new Vector2(x, y);
            if (pos.sqrMagnitude < _nodeMinDist * _nodeMinDist) continue; // 离营地足够远(随地图自适应)
            bool tooClose = false;
            foreach (var n in _nodes)
            {
                if ((n.Pos - pos).sqrMagnitude < 6f * 6f) { tooClose = true; break; }
            }
            if (tooClose) continue;

            int kind = Random.Range(0, 3);
            var go = new GameObject($"Node_{kind}_{_nodes.Count}");
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(x, y, -2f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = kind switch
            {
                0 => new Color(0.35f, 0.95f, 0.45f),   // 草药
                1 => new Color(0.55f, 0.75f, 1f),      // 矿石
                _ => new Color(1f, 0.72f, 0.25f)       // 补给箱
            };
            sr.sortingOrder = 5;
            go.transform.localScale = new Vector3(1.1f, 1.1f, 1f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

            _nodes.Add(new ResourceNode { Go = go, Pos = pos, Kind = kind, Sr = sr, Phase = Random.Range(0f, 6.28f) });
        }
    }

    private void UpdateNodes(float dt, Vector3 p)
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            var n = _nodes[i];
            if (n.Taken) continue;
            if (n.Sr != null)
            {
                n.Phase += dt * 2.5f;
                var c = n.Sr.color;
                c.a = 0.75f + Mathf.Sin(n.Phase) * 0.2f;
                n.Sr.color = c;
            }
            float dx = n.Pos.x - p.x, dy = n.Pos.y - p.y;
            if (dx * dx + dy * dy <= NodeCollectRadius * NodeCollectRadius)
                CollectNode(i);
        }
    }

    private void CollectNode(int i)
    {
        var n = _nodes[i];
        n.Taken = true;
        NodeTakenCount++;
        if (n.Go != null) Destroy(n.Go);
        _mmDirty = true;

        var player = GameManager.Instance?.Player;
        if (player == null) return;
        int layer = GameManager.Instance.RiftLayer;
        var ui = GameUI.Instance;

        switch (n.Kind)
        {
            case 0: // 草药 → 金币 + 血瓶
                int gold = 20 + layer * 10;
                player.Stats.AddGold(gold);
                player.Stats.Runtime.Potions = Mathf.Min(PlayerRuntimeStats.MaxPotions, player.Stats.Runtime.Potions + 1);
                VFXHelper.SpawnHitParticles(n.Pos, new Color(0.35f, 0.95f, 0.45f), 3);
                ui?.ShowItemPickupToast("采集·草药", $"+{gold}金币 血瓶+1");
                break;
            case 1: // 矿石 → 碎片
                int frag = Random.Range(2, 4) + layer / 5;
                DismantleSystem.Fragments += frag;
                VFXHelper.SpawnHitParticles(n.Pos, new Color(0.55f, 0.75f, 1f), 3);
                ui?.ShowItemPickupToast("采集·矿石", $"碎片+{frag}");
                break;
            default: // 补给箱 → 装备
                var item = EquipmentItem.GenerateRandom(layer);
                if (player.Inventory.AddToBackpack(item))
                    ui?.ShowItemPickupToast("采集·补给箱", $"{item.Name} {item.GetStatSummary()}");
                else
                    LocalMailSystem.SendItemMail($"秘境补给: {item.Name}", "背包已满，装备已发送至邮箱。", item);
                VFXHelper.SpawnHitParticles(n.Pos, new Color(1f, 0.72f, 0.25f), 4);
                break;
        }
        player.Stats.Save();
    }

    // ===== 小地图 =====
    private void BuildMinimap()
    {
        var canvas = GameManager.EnsureCanvas();
        var font = GameManager.GetUIFont();

        _mmPanel = new GameObject("MistMinimap");
        var pr = _mmPanel.AddComponent<RectTransform>();
        // 优先嵌入InGameHud小地图占位框; HUD未显示时回退右上角独立面板
        RectTransform host = InGameHud.GetMinimapHost();
        bool embedded = host != null;
        if (embedded)
        {
            _mmPanel.transform.SetParent(host, false);
            pr.anchorMin = new Vector2(0.5f, 0.5f); pr.anchorMax = new Vector2(0.5f, 0.5f);
            pr.pivot = new Vector2(0.5f, 0.5f);
            pr.anchoredPosition = Vector2.zero;
            pr.sizeDelta = new Vector2(328f, 328f);
        }
        else
        {
            _mmPanel.transform.SetParent(canvas.transform, false);
            pr.anchorMin = new Vector2(1f, 1f); pr.anchorMax = new Vector2(1f, 1f);
            pr.pivot = new Vector2(1f, 1f);
            pr.anchoredPosition = new Vector2(-12f, -60f);
            pr.sizeDelta = new Vector2(232f, 172f);
        }
        var bg = _mmPanel.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.07f, 0.72f);

        // 点击小地图打开地图 (WaypointUI 世界地图)
        var openBtn = _mmPanel.AddComponent<Button>();
        openBtn.transition = Selectable.Transition.None;
        openBtn.onClick.AddListener(() => WaypointUI.Show());

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(_mmPanel.transform, false);
        var tr = titleGo.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f); tr.anchoredPosition = new Vector2(0f, -2f);
        tr.sizeDelta = new Vector2(0f, 20f);
        var tt = titleGo.AddComponent<Text>();
        tt.text = _worldMode ? "世界地图" : $"秘境地图 · 第{GameManager.Instance.RiftLayer}层";
        tt.alignment = TextAnchor.MiddleCenter;
        tt.fontSize = embedded ? 18 : 14;
        tt.color = new Color(0.85f, 0.82f, 0.6f);
        tt.font = font;
        tt.raycastTarget = false;

        // 迷雾纹理 (未探索=暗, 已探索=亮; 世界模式全亮+区分界)
        _mmTex = new Texture2D(_gw, _gh, TextureFormat.RGBA32, false);
        _mmTex.filterMode = FilterMode.Point;
        for (int x = 0; x < _gw; x++)
            for (int y = 0; y < _gh; y++)
                _mmTex.SetPixel(x, y, _worldMode || (_revealed != null && _revealed[x, y]) ? MmLand : MmFog);
        if (_worldMode) DrawWorldGrid();
        _mmTex.Apply();

        var rawGo = new GameObject("Map");
        rawGo.transform.SetParent(_mmPanel.transform, false);
        var rr = rawGo.AddComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.5f, embedded ? 0.5f : 0f); rr.anchorMax = new Vector2(0.5f, embedded ? 0.5f : 0f);
        rr.pivot = new Vector2(0.5f, 0.5f);
        // 保持世界宽高比
        float boxW = embedded ? 308f : 215f, boxH = embedded ? 282f : 145f;
        float worldAspect = (_halfW * 2f) / Mathf.Max(0.01f, _halfH * 2f);
        float w, h;
        if (boxW / boxH > worldAspect) { h = boxH; w = h * worldAspect; }
        else { w = boxW; h = w / worldAspect; }
        rr.anchoredPosition = new Vector2(0f, embedded ? -22f : 80f);
        rr.sizeDelta = new Vector2(w, h);
        var raw = rawGo.AddComponent<RawImage>();
        raw.texture = _mmTex;
        raw.raycastTarget = false;
        // 地图边框
        var frame = rawGo.AddComponent<Outline>();
        frame.effectColor = new Color(1f, 0.85f, 0.5f, 0.4f);
        frame.effectDistance = new Vector2(2f, -2f);
        _mmMapRect = rr;

        // 营地金色菱形标记 (世界模式无营地)
        if (!_worldMode)
        {
            var campGo = new GameObject("CampMarker");
            campGo.transform.SetParent(rawGo.transform, false);
            var cr = campGo.AddComponent<RectTransform>();
            cr.sizeDelta = new Vector2(9f, 9f);
            cr.localRotation = Quaternion.Euler(0f, 0f, 45f);
            var cImg = campGo.AddComponent<Image>();
            cImg.color = new Color(1f, 0.85f, 0.3f, 0.95f);
            cImg.raycastTarget = false;
            float cw = rr.sizeDelta.x, ch = rr.sizeDelta.y;
            float cnx = Mathf.Clamp01((_campPos.x + _halfW) / (_halfW * 2f));
            float cny = Mathf.Clamp01((_campPos.y + _halfH) / (_halfH * 2f));
            cr.anchoredPosition = new Vector2((cnx - 0.5f) * cw, (cny - 0.5f) * ch);
            _campMarker = cr;
        }

        // 敌人红点池 (仅显示已探索区内的存活敌人)
        for (int i = 0; i < 24; i++)
        {
            var eGo = new GameObject($"EnemyDot_{i}");
            eGo.transform.SetParent(rawGo.transform, false);
            var er = eGo.AddComponent<RectTransform>();
            er.sizeDelta = new Vector2(5f, 5f);
            var eImg = eGo.AddComponent<Image>();
            eImg.color = new Color(1f, 0.25f, 0.25f, 0.9f);
            eImg.raycastTarget = false;
            eGo.SetActive(false);
            _enemyDots.Add(eImg);
        }

        var dotGo = new GameObject("PlayerDot");
        dotGo.transform.SetParent(rawGo.transform, false);
        var dr = dotGo.AddComponent<RectTransform>();
        dr.sizeDelta = new Vector2(9f, 9f);
        var dImg = dotGo.AddComponent<Image>();
        dImg.color = new Color(1f, 0.9f, 0.3f);
        dImg.raycastTarget = false;
        var dOl = dotGo.AddComponent<Outline>();
        dOl.effectColor = Color.white;
        dOl.effectDistance = new Vector2(1.5f, -1.5f);
        _playerDot = dr;

        DrawBuildings();   // 地图建筑 (Canvas+Image, DungeonVisuals生成时遍历地图数据得出)
        _mmDirty = true;
    }

    /// <summary>在小地图上以Canvas+Image实例化建筑标记 (DungeonVisuals生成完毕后调用; 重复调用先清后画)</summary>
    public void DrawBuildings()
    {
        if (_worldMode || _mmMapRect == null || _mmPanel == null) return;
        for (int i = _mmMapRect.childCount - 1; i >= 0; i--)
            if (_mmMapRect.GetChild(i).name.StartsWith("Building_"))
                Destroy(_mmMapRect.GetChild(i).gameObject);

        var marks = DungeonVisuals.BuildingMarks;
        for (int i = 0; i < marks.Count; i++)
        {
            var m = marks[i];
            var bGo = new GameObject("Building_" + i);
            bGo.transform.SetParent(_mmMapRect, false);
            var br = bGo.AddComponent<RectTransform>();
            br.sizeDelta = new Vector2(m.Size, m.Size);
            var bImg = bGo.AddComponent<Image>();
            bImg.color = m.Color;
            bImg.raycastTarget = false;
            float nx = Mathf.Clamp01((m.Pos.x + _halfW) / (_halfW * 2f));
            float ny = Mathf.Clamp01((m.Pos.y + _halfH) / (_halfH * 2f));
            br.anchoredPosition = new Vector2((nx - 0.5f) * _mmMapRect.sizeDelta.x, (ny - 0.5f) * _mmMapRect.sizeDelta.y);
        }
    }

    private void UpdateMinimap(Vector3 p)
    {
        if (_playerDot == null || _mmMapRect == null || _mmTex == null) return;

        float w = _mmMapRect.sizeDelta.x, h = _mmMapRect.sizeDelta.y;
        float nx = Mathf.Clamp01((p.x + _halfW) / (_halfW * 2f));
        float ny = Mathf.Clamp01((p.y + _halfH) / (_halfH * 2f));
        _playerDot.anchoredPosition = new Vector2((nx - 0.5f) * w, (ny - 0.5f) * h);
        float pulse = 0.85f + Mathf.Sin(Time.unscaledTime * 4f) * 0.18f;
        _playerDot.localScale = new Vector3(pulse, pulse, 1f);

        _enemyDotTimer -= Time.deltaTime;
        if (_enemyDotTimer <= 0f)
        {
            _enemyDotTimer = 0.15f;
            UpdateEnemyDots(w, h);
        }

        if (_mmDirty && Time.unscaledTime - _mmApplyTimer > 0.25f)
        {
            _mmApplyTimer = Time.unscaledTime;
            _mmDirty = false;
            for (int x = 0; x < _gw; x++)
                for (int y = 0; y < _gh; y++)
                    _mmTex.SetPixel(x, y, _worldMode || _revealed[x, y] ? MmLand : MmFog);
            if (_worldMode)
            {
                DrawWorldGrid();
            }
            else
            {
                SetTexPixelWorld(_campPos, MmCamp);
                for (int i = 0; i < _nodes.Count; i++)
                {
                    var n = _nodes[i];
                    if (n.Taken) continue;
                    int ncx = WorldToCellX(n.Pos.x), ncy = WorldToCellY(n.Pos.y);
                    if (ncx < 0 || ncx >= _gw || ncy < 0 || ncy >= _gh) continue;
                    if (!_revealed[ncx, ncy]) continue; // 未探索迷雾中的资源点不显示
                    SetTexPixelWorld(n.Pos, MmNodeColors[n.Kind]);
                }
            }
            _mmTex.Apply();
        }
    }

    private void UpdateEnemyDots(float w, float h)
    {
        int used = 0;
        var enemies = SceneRegistry.GetEnemiesSnapshot();
        for (int i = 0; i < enemies.Count && used < _enemyDots.Count; i++)
        {
            var e = enemies[i];
            if (e == null || e.IsDead) continue;
            var ep = e.transform.position;
            if (!_worldMode)
            {
                int cx = WorldToCellX(ep.x), cy = WorldToCellY(ep.y);
                if (cx < 0 || cx >= _gw || cy < 0 || cy >= _gh) continue;
                if (!_revealed[cx, cy]) continue; // 迷雾中的敌人不显示
            }
            float nx = Mathf.Clamp01((ep.x + _halfW) / (_halfW * 2f));
            float ny = Mathf.Clamp01((ep.y + _halfH) / (_halfH * 2f));
            var img = _enemyDots[used];
            img.rectTransform.anchoredPosition = new Vector2((nx - 0.5f) * w, (ny - 0.5f) * h);
            if (!img.gameObject.activeSelf) img.gameObject.SetActive(true);
            used++;
        }
        for (int i = used; i < _enemyDots.Count; i++)
        {
            if (_enemyDots[i].gameObject.activeSelf) _enemyDots[i].gameObject.SetActive(false);
        }
    }

    private void SetTexPixelWorld(Vector2 worldPos, Color c)
    {
        int x = WorldToCellX(worldPos.x), y = WorldToCellY(worldPos.y);
        if (x < 0 || x >= _gw || y < 0 || y >= _gh) return;
        _mmTex.SetPixel(x, y, c);
    }

    private void DrawWorldGrid()
    {
        // 4x4区分界线 (每区_gw/4像素) + 安全区(区0=西南)暖色
        int per = _gw / 4;
        var lineCol = new Color(0.28f, 0.30f, 0.38f);
        for (int g = 1; g < 4; g++)
        {
            int p = g * per;
            for (int i = 0; i < _gw; i++)
            {
                _mmTex.SetPixel(p, i, lineCol);
                _mmTex.SetPixel(i, p, lineCol);
            }
        }
        var safeCol = new Color(0.5f, 0.5f, 0.42f);
        for (int x = 0; x < per; x++)
            for (int y = 0; y < per; y++)
                _mmTex.SetPixel(x, y, safeCol);
    }
}
