using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开放世界视觉构建 — 16块区域地板(主题换色) + 区域边框/悬浮区名 + 世界边界墙 + 传送点光柱
/// 一次性整图构建(纯Sprite, 无分块流式) — 走动无缝，区域间无碰撞体
/// </summary>
public class WorldVisuals : MonoBehaviour
{
    public static WorldVisuals Instance { get; private set; }

    /// <summary>销毁世界视觉 (供WorldManager清理)</summary>
    public static void DestroyInstance()
    {
        if (Instance == null) return;
        var obj = Instance.gameObject;
        Instance = null;
        obj.SetActive(false);
        Object.Destroy(obj);
    }

    private const float WallThickness = 3f;

    public static void Build()
    {
        if (Instance != null)
        {
            var old = Instance.gameObject;
            Instance = null;
            Destroy(old);
        }
        var go = new GameObject("WorldVisuals");
        go.AddComponent<WorldVisuals>();
    }

    private void Awake()
    {
        Instance = this;
        BuildAll();
    }

    private void BuildAll()
    {
        BuildFloors();
        BuildZoneOverlays();
        BuildBoundaryWalls();
        BuildWaypoints();
        BuildHome();
        BuildQuestNpcs();
    }

    // ===== "家"区域: 标志牌 + NPC/家具立柱 (原大本营功能入口) =====
    private void BuildHome()
    {
        var home = new GameObject("Home");
        home.transform.SetParent(transform, false);
        var spawn = WorldData.Get(0).WaypointPos;

        MakeMarker(home.transform, "Home", "家", spawn, new Color(1f, 0.85f, 0.4f), 2.4f);
        foreach (var npc in WorldData.HomeNpcs)
        {
            var pos = spawn + npc.Offset;
            MakeMarker(home.transform, $"Npc_{npc.Name}", npc.Icon, pos, new Color(npc.R, npc.G, npc.B), 1.7f);
        }
    }

    // ===== 任务发布人 (每个野外区一名, "!"标记) =====
    private void BuildQuestNpcs()
    {
        var parent = new GameObject("QuestNpcs");
        parent.transform.SetParent(transform, false);
        foreach (var zone in WorldData.Zones)
        {
            if (!WorldQuests.HasNpc(zone.Index)) continue;
            MakeMarker(parent.transform, $"任务·{zone.Name}", "!", WorldQuests.NpcPos(zone.Index), new Color(1f, 0.8f, 0.25f), 1.7f);
        }
    }

    private static void MakeMarker(Transform parent, string name, string icon, Vector2 pos, Color color, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, pos.y, -1f);

        // 立柱
        var post = new GameObject("Post");
        post.transform.SetParent(go.transform, false);
        post.transform.localPosition = new Vector3(0, height / 2f, 0);
        var psr = post.AddComponent<SpriteRenderer>();
        psr.sprite = SpriteCache.WhitePixel;
        psr.color = color;
        psr.sortingOrder = -4;
        post.transform.localScale = new Vector3(0.5f, height, 1f);

        // 顶部图标 (世界空间文字)
        var iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(go.transform, false);
        iconObj.transform.localPosition = new Vector3(0, height + 0.7f, 0);
        var itxt = iconObj.AddComponent<Text>();
        itxt.text = icon;
        itxt.fontSize = 48;
        itxt.color = color;
        itxt.alignment = TextAnchor.MiddleCenter;
        itxt.font = GameManager.GetUIFont();
        iconObj.transform.localScale = new Vector3(0.06f, 0.06f, 1f);

        // 底部名牌
        var lblObj = new GameObject("Label");
        lblObj.transform.SetParent(go.transform, false);
        lblObj.transform.localPosition = new Vector3(0, height + 1.55f, 0);
        var ltxt = lblObj.AddComponent<Text>();
        ltxt.text = name;
        ltxt.fontSize = 26;
        ltxt.color = new Color(1f, 1f, 1f, 0.85f);
        ltxt.alignment = TextAnchor.MiddleCenter;
        ltxt.font = GameManager.GetUIFont();
        lblObj.transform.localScale = new Vector3(0.05f, 0.05f, 1f);
    }

    private static Sprite LoadFloorSprite(int theme)
    {
        string path = theme switch
        {
            0 => ResourcePaths.Floor_Forest,
            1 => ResourcePaths.Floor_Forest,
            2 => ResourcePaths.Floor_Ice,
            3 => ResourcePaths.Floor_Fire,
            4 => ResourcePaths.Floor_Shadow,
            5 => ResourcePaths.Floor_Fire,
            6 => ResourcePaths.Floor_Shadow,
            7 => ResourcePaths.Floor_Fire,
            _ => ResourcePaths.Floor_Stone
        };
        return Resources.Load<Sprite>(path);
    }

    private static Color ThemeTint(int theme)
    {
        return theme switch
        {
            0 => new Color(0.72f, 0.95f, 0.72f),   // 幽暗森林
            1 => new Color(0.85f, 1f, 0.8f),       // 翠绿平原
            2 => new Color(0.7f, 0.85f, 1f),       // 冰霜
            3 => new Color(1f, 0.72f, 0.55f),      // 火焰
            4 => new Color(0.55f, 0.5f, 0.72f),    // 亡灵
            5 => new Color(1f, 0.6f, 0.45f),       // 龙巢
            6 => new Color(0.5f, 0.42f, 0.75f),    // 虚空
            7 => new Color(0.9f, 0.5f, 0.4f),      // 魔王
            _ => new Color(0.8f, 0.8f, 0.8f)
        };
    }

    private void BuildFloors()
    {
        var floorParent = new GameObject("Floors");
        floorParent.transform.SetParent(transform, false);
        foreach (var zone in WorldData.Zones)
        {
            var sprite = LoadFloorSprite(zone.Theme);
            var floorObj = new GameObject($"Zone_{zone.Index}");
            floorObj.transform.SetParent(floorParent.transform, false);
            floorObj.transform.position = new Vector3(zone.Center.x, zone.Center.y, 5f);

            var sr = floorObj.AddComponent<SpriteRenderer>();
            if (sprite != null)
            {
                sr.sprite = sprite;
                float sx = WorldData.ZoneSize / sprite.bounds.size.x;
                float sy = WorldData.ZoneSize / sprite.bounds.size.y;
                floorObj.transform.localScale = new Vector3(sx, sy, 1f);
            }
            else
            {
                sr.sprite = SpriteCache.WhitePixel;
                floorObj.transform.localScale = new Vector3(WorldData.ZoneSize, WorldData.ZoneSize, 1f);
            }
            sr.color = ThemeTint(zone.Theme);
            sr.sortingOrder = -10;
        }
    }

    // ===== 区域边框 + 悬浮区名 (叠加轻量网格辅助辨认所在区域) =====
    private void BuildZoneOverlays()
    {
        var parent = new GameObject("ZoneOverlays");
        parent.transform.SetParent(transform, false);
        float half = WorldData.WorldHalf;
        float step = WorldData.ZoneSize;

        var lineColor = new Color(1f, 1f, 1f, 0.10f);
        for (int i = 0; i <= WorldData.GridSize; i++)
        {
            float p = -half + i * step;
            MakeLine(parent.transform, $"LineV_{i}", new Vector3(p, 0f, 4f), new Vector2(0.15f, half * 2f), lineColor);
            MakeLine(parent.transform, $"LineH_{i}", new Vector3(0f, p, 4f), new Vector2(half * 2f, 0.15f), lineColor);
        }

        // 区域中心悬浮区名 (独立世界空间Text, 面向相机, 半透明不挡立柱)
        var font = GameManager.GetUIFont();
        foreach (var zone in WorldData.Zones)
        {
            var go = new GameObject($"ZoneName_{zone.Index}");
            go.transform.SetParent(parent.transform, false);
            go.transform.position = new Vector3(zone.Center.x, zone.Center.y, 0f);
            var lbl = go.AddComponent<Text>();
            lbl.text = zone.Name;
            lbl.font = font;
            lbl.fontSize = 48;
            lbl.color = new Color(1f, 0.95f, 0.8f, 0.5f);
            lbl.alignment = TextAnchor.MiddleCenter;
            lbl.raycastTarget = false;
            go.transform.localScale = new Vector3(0.09f, 0.09f, 1f);
        }
    }

    private static void MakeLine(Transform parent, string name, Vector3 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteCache.WhitePixel;
        sr.color = color;
        sr.sortingOrder = -9;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
    }

    private void BuildBoundaryWalls()
    {
        var wallParent = new GameObject("WorldBounds");
        wallParent.transform.SetParent(transform, false);
        float half = WorldData.WorldHalf;

        CreateWall(wallParent.transform, "Wall_Top", new Vector3(0, half + WallThickness / 2f), new Vector2(half * 2 + WallThickness * 2, WallThickness));
        CreateWall(wallParent.transform, "Wall_Bottom", new Vector3(0, -half - WallThickness / 2f), new Vector2(half * 2 + WallThickness * 2, WallThickness));
        CreateWall(wallParent.transform, "Wall_Left", new Vector3(-half - WallThickness / 2f, 0), new Vector2(WallThickness, half * 2));
        CreateWall(wallParent.transform, "Wall_Right", new Vector3(half + WallThickness / 2f, 0), new Vector2(WallThickness, half * 2));
    }

    private static void CreateWall(Transform parent, string name, Vector3 pos, Vector2 size)
    {
        var wall = new GameObject(name);
        wall.transform.SetParent(parent, false);
        wall.transform.position = pos;
        wall.AddComponent<BoxCollider2D>().size = size;
    }

    private void BuildWaypoints()
    {
        var wpParent = new GameObject("Waypoints");
        wpParent.transform.SetParent(transform, false);

        foreach (var zone in WorldData.Zones)
        {
            var wp = new GameObject($"Waypoint_{zone.Index}");
            wp.transform.SetParent(wpParent.transform, false);
            wp.transform.position = new Vector3(zone.WaypointPos.x, zone.WaypointPos.y, -1f);

            // 光柱: 白像素竖条 + 底座
            var beam = new GameObject("Beam");
            beam.transform.SetParent(wp.transform, false);
            beam.transform.localPosition = new Vector3(0, 2f, 0f);
            var beamSr = beam.AddComponent<SpriteRenderer>();
            beamSr.sprite = SpriteCache.WhitePixel;
            beamSr.color = WorldData.IsDiscovered(zone.Index)
                ? new Color(0.4f, 0.9f, 1f, 0.85f)
                : new Color(0.6f, 0.6f, 0.65f, 0.5f);
            beamSr.sortingOrder = -5;
            beam.transform.localScale = new Vector3(0.5f, 4f, 1f);

            var baseDisc = new GameObject("Base");
            baseDisc.transform.SetParent(wp.transform, false);
            var baseSr = baseDisc.AddComponent<SpriteRenderer>();
            baseSr.sprite = SpriteCache.WhitePixel;
            baseSr.color = new Color(0.3f, 0.7f, 0.9f, 0.6f);
            baseSr.sortingOrder = -6;
            baseDisc.transform.localScale = new Vector3(3f, 0.6f, 1f);
        }
    }

    /// <summary>传送后刷新光柱颜色(已发现的更亮)</summary>
    public void RefreshWaypointVisuals()
    {
        var wpParent = transform.Find("Waypoints");
        if (wpParent == null) return;
        foreach (Transform wp in wpParent)
        {
            int idx = wp.name.StartsWith("Waypoint_") && int.TryParse(wp.name.Substring(9), out int parsed) ? parsed : -1;
            if (idx < 0) continue;
            var beam = wp.Find("Beam")?.GetComponent<SpriteRenderer>();
            if (beam != null)
                beam.color = WorldData.IsDiscovered(idx)
                    ? new Color(0.4f, 0.9f, 1f, 0.85f)
                    : new Color(0.6f, 0.6f, 0.65f, 0.5f);
        }
    }
}
