using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 服务器区服选择系统 — 管理多个游戏服务器
/// 不同区服数据隔离，玩家选择后连接对应服务器
/// </summary>
public static class ServerRegionManager
{
    [Serializable]
    public class ServerInfo
    {
        public string id;           // 服务器ID (如 "cn-east-1")
        public string name;         // 显示名称 (如 "华东一区")
        public string serverUrl;    // 服务器地址 (如 "http://39.107.141.107:5132")
        public string description;  // 描述 (如 "推荐 | 新服")
        public int status;          // 0=维护 1=流畅 2=繁忙 3=爆满
        public bool isNew;          // 是否新服
        public bool isRecommended;  // 是否推荐
    }

    [Serializable]
    public class ServerListResponse
    {
        public List<ServerInfo> servers;
    }

    // 服务器列表 — 本地测试 + 远程正式
    public static List<ServerInfo> Servers = new List<ServerInfo>
    {
        new ServerInfo
        {
            id = "local",
            name = "本地测试",
            serverUrl = "http://localhost:5132",
            description = "本机测试服",
            status = 1,
            isNew = false,
            isRecommended = true
        },
        new ServerInfo
        {
            id = "main",
            name = "卫冕大陆",
            serverUrl = "http://39.107.141.107:5132",
            description = "正式服",
            status = 1,
            isNew = false,
            isRecommended = false
        }
    };

    private const string SelectedServerKey = "ARPG_SelectedServer";

    /// <summary>当前选中的服务器</summary>
    public static ServerInfo CurrentServer { get; private set; }

    /// <summary>获取上次选中的服务器ID</summary>
    public static string GetLastServerId()
    {
        return PlayerPrefs.GetString(SelectedServerKey, "");
    }

    /// <summary>选择服务器</summary>
    public static void SelectServer(string serverId)
    {
        var server = Servers.Find(s => s.id == serverId);
        if (server == null) return;

        CurrentServer = server;
        PlayerPrefs.SetString(SelectedServerKey, serverId);
        PlayerPrefs.Save();

        // 更新CloudSaveManager和GameConfigManager的服务器地址
        if (CloudSaveManager.Instance != null)
        {
            CloudSaveManager.Instance.ServerUrl = server.serverUrl;
        }
        if (GameConfigManager.Instance != null)
        {
            GameConfigManager.Instance.ServerUrl = server.serverUrl;
        }

                    GameLog.Log($"[ServerRegion] 选择服务器: {server.name} ({server.serverUrl})");
    }

    /// <summary>根据ID获取服务器</summary>
    public static ServerInfo GetServer(string id)
    {
        return Servers.Find(s => s.id == id);
    }

    /// <summary>获取服务器状态文本</summary>
    public static string GetStatusText(int status)
    {
        return status switch
        {
            0 => "维护中",
            1 => "流畅",
            2 => "繁忙",
            3 => "爆满",
            _ => "未知"
        };
    }

    /// <summary>获取服务器状态颜色</summary>
    public static Color GetStatusColor(int status)
    {
        return status switch
        {
            0 => new Color(0.6f, 0.6f, 0.6f, 1f),     // 灰色
            1 => new Color(0.3f, 0.9f, 0.3f, 1f),     // 绿色
            2 => new Color(0.9f, 0.8f, 0.2f, 1f),     // 黄色
            3 => new Color(0.9f, 0.3f, 0.2f, 1f),     // 红色
            _ => Color.white
        };
    }
}
