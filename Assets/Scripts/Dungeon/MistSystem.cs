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

    private struct FogCell { public SpriteRenderer Sr; public float Alpha; public float Target; }

    private class ResourceNode
    {
        public GameObject Go;
        public Vector2 Pos;
        public int Kind;      // 0草药 1矿石 2补给箱
        public bool Taken;
        public SpriteRenderer Sr;
        public float Phase;
    }

    private readonly List<FogCell> _cells = new List<FogCell>();
    private readonly List<ResourceNode> _nodes = new List<ResourceNode>();
    private bool[,] _revealed;
    private int _revealedCount;
    private int _gw, _gh;
    private float _cellSize;
    private float _halfW, _halfH;
    private Vector2 _campPos = Vector2.zero;
    private float _revealTimer;

    // 小地图
    private Texture2D _mmTex;
    private GameObject _mmPanel;
    private RectTransform _mmMapRect;
    private RectTransform _playerDot;
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
    public int CellCount => _cells.Count;
    public int RevealedCount => _revealedCount;
    public int NodeCount => _nodes.Count;
    public int NodeTakenCount { get; private set; }
    public bool MinimapReady => _mmTex != null;
    public Vector2 GetNodePos(int i) => i >= 0 && i < _nodes.Count ? _nodes[i].Pos : Vector2.zero;
    public int GetNodeKind(int i) => i >= 0 && i < _nodes.Count ? _nodes[i].Kind : -1;
    public bool IsNodeTaken(int i) => i >= 0 && i < _nodes.Count && _nodes[i].Taken;

    private Transform Player => GameManager.Instance != null ? GameManager.Instance.Player?.transform : null;

    /// <summary>在当前副本地图上构建迷雾/营地/资源点/小地图 (仅秘境Dungeon状态)</summary>
    public static void SpawnForMap()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.CurrentState != GameState.Dungeon) return;
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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var gm = GameManager.Instance;
        int theme = GameManager.RiftLayerToTheme(gm.CurrentStageIndex);
        var map = DungeonMapData.GetStageMap(theme); // 仅用尺寸(表值固定)
        _halfW = map.mapHalfWidth;
        _halfH = map.mapHalfHeight;
        float minHalf = Mathf.Min(_halfW, _halfH);
        _revealRadius = Mathf.Clamp(minHalf * 0.35f, 4f, 8f);
        _campReveal = Mathf.Clamp(minHalf * 0.55f, 6f, 13f);
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
        if (Instance == this) Instance = null;
    }

    // ===== 迷雾网格 =====
    private void BuildFog()
    {
        // cell为正方形: 限制列≈18行≈14, 总量≤252
        _cellSize = Mathf.Max((_halfW * 2f) / 18f, (_halfH * 2f) / 14f);
        _gw = Mathf.Max(1, Mathf.CeilToInt((_halfW * 2f) / _cellSize));
        _gh = Mathf.Max(1, Mathf.CeilToInt((_halfH * 2f) / _cellSize));
        _revealed = new bool[_gw, _gh];

        var sprite = SpriteCache.WhitePixel;
        for (int x = 0; x < _gw; x++)
        {
            for (int y = 0; y < _gh; y++)
            {
                var cellGo = new GameObject($"Fog_{x}_{y}");
                cellGo.transform.SetParent(transform, false);
                float wx = -_halfW + (x + 0.5f) * _cellSize;
                float wy = -_halfH + (y + 0.5f) * _cellSize;
                cellGo.transform.position = new Vector3(wx, wy, 8f);
                var sr = cellGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(0.02f, 0.03f, 0.05f, FogAlpha);
                sr.sortingOrder = 40;
                float over = 1.04f; // 轻微重叠防缝隙
                cellGo.transform.localScale = new Vector3(_cellSize * over, _cellSize * over, 1f);
                _cells.Add(new FogCell { Sr = sr, Alpha = FogAlpha, Target = FogAlpha });
            }
        }
    }

    private void Update()
    {
        var p = Player;
        if (p == null) return;
        float dt = Time.deltaTime;

        _revealTimer -= dt;
        if (_revealTimer <= 0f)
        {
            _revealTimer = 0.12f;
            RevealAround(p.position, _revealRadius);
        }

        // 迷雾淡出插值
        for (int i = 0; i < _cells.Count; i++)
        {
            var c = _cells[i];
            if (Mathf.Approximately(c.Alpha, c.Target)) continue;
            c.Alpha = Mathf.MoveTowards(c.Alpha, c.Target, dt * 1.8f);
            if (c.Sr != null) { var col = c.Sr.color; col.a = c.Alpha; c.Sr.color = col; }
            _cells[i] = c;
        }

        UpdateNodes(dt, p.position);
        UpdateMinimap(p.position);
    }

    private void RevealAround(Vector3 pos, float radius)
    {
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
                int idx = x * _gh + y; // BuildFog: x外层y内层
                var c = _cells[idx];
                c.Target = 0f;
                _cells[idx] = c;
                changed = true;
            }
        }
        if (changed) _mmDirty = true;
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
        _mmPanel.transform.SetParent(canvas.transform, false);
        var pr = _mmPanel.AddComponent<RectTransform>();
        pr.anchorMin = new Vector2(1f, 1f); pr.anchorMax = new Vector2(1f, 1f);
        pr.pivot = new Vector2(1f, 1f);
        pr.anchoredPosition = new Vector2(-12f, -60f);
        pr.sizeDelta = new Vector2(232f, 172f);
        var bg = _mmPanel.AddComponent<Image>();
        bg.color = new Color(0.03f, 0.04f, 0.07f, 0.72f);

        var titleGo = new GameObject("Title");
        titleGo.transform.SetParent(_mmPanel.transform, false);
        var tr = titleGo.AddComponent<RectTransform>();
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f); tr.anchoredPosition = new Vector2(0f, -2f);
        tr.sizeDelta = new Vector2(0f, 20f);
        var tt = titleGo.AddComponent<Text>();
        tt.text = $"秘境地图 · 第{GameManager.Instance.RiftLayer}层";
        tt.alignment = TextAnchor.MiddleCenter;
        tt.fontSize = 14;
        tt.color = new Color(0.85f, 0.82f, 0.6f);
        tt.font = font;
        tt.raycastTarget = false;

        // 迷雾纹理 (未探索=暗, 已探索=亮, 资源/营地染色)
        _mmTex = new Texture2D(_gw, _gh, TextureFormat.RGBA32, false);
        _mmTex.filterMode = FilterMode.Point;
        for (int x = 0; x < _gw; x++)
            for (int y = 0; y < _gh; y++)
                _mmTex.SetPixel(x, y, _revealed != null && _revealed[x, y] ? MmLand : MmFog);
        _mmTex.Apply();

        var rawGo = new GameObject("Map");
        rawGo.transform.SetParent(_mmPanel.transform, false);
        var rr = rawGo.AddComponent<RectTransform>();
        rr.anchorMin = new Vector2(0.5f, 0f); rr.anchorMax = new Vector2(0.5f, 0f);
        rr.pivot = new Vector2(0.5f, 0.5f);
        // 保持世界宽高比
        float boxW = 215f, boxH = 145f;
        float worldAspect = (_halfW * 2f) / Mathf.Max(0.01f, _halfH * 2f);
        float w, h;
        if (boxW / boxH > worldAspect) { h = boxH; w = h * worldAspect; }
        else { w = boxW; h = w / worldAspect; }
        rr.anchoredPosition = new Vector2(0f, 80f);
        rr.sizeDelta = new Vector2(w, h);
        var raw = rawGo.AddComponent<RawImage>();
        raw.texture = _mmTex;
        raw.raycastTarget = false;
        _mmMapRect = rr;

        var dotGo = new GameObject("PlayerDot");
        dotGo.transform.SetParent(rawGo.transform, false);
        var dr = dotGo.AddComponent<RectTransform>();
        dr.sizeDelta = new Vector2(7f, 7f);
        var dImg = dotGo.AddComponent<Image>();
        dImg.color = new Color(1f, 0.9f, 0.3f);
        dImg.raycastTarget = false;
        _playerDot = dr;

        _mmDirty = true;
    }

    private void UpdateMinimap(Vector3 p)
    {
        if (_playerDot == null || _mmMapRect == null || _mmTex == null) return;

        float w = _mmMapRect.sizeDelta.x, h = _mmMapRect.sizeDelta.y;
        float nx = Mathf.Clamp01((p.x + _halfW) / (_halfW * 2f));
        float ny = Mathf.Clamp01((p.y + _halfH) / (_halfH * 2f));
        _playerDot.anchoredPosition = new Vector2((nx - 0.5f) * w, (ny - 0.5f) * h);

        if (_mmDirty && Time.unscaledTime - _mmApplyTimer > 0.25f)
        {
            _mmApplyTimer = Time.unscaledTime;
            _mmDirty = false;
            for (int x = 0; x < _gw; x++)
                for (int y = 0; y < _gh; y++)
                    _mmTex.SetPixel(x, y, _revealed[x, y] ? MmLand : MmFog);
            SetTexPixelWorld(_campPos, MmCamp);
            for (int i = 0; i < _nodes.Count; i++)
            {
                var n = _nodes[i];
                if (!n.Taken) SetTexPixelWorld(n.Pos, MmNodeColors[n.Kind]);
            }
            _mmTex.Apply();
        }
    }

    private void SetTexPixelWorld(Vector2 worldPos, Color c)
    {
        int x = WorldToCellX(worldPos.x), y = WorldToCellY(worldPos.y);
        if (x < 0 || x >= _gw || y < 0 || y >= _gh) return;
        _mmTex.SetPixel(x, y, c);
    }
}
