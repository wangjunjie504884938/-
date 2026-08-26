using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 游戏配置中心 — ScriptableObject, 集中管理资产引用路径
/// 当前使用 Resources 路径, 后续可无缝迁移到 Addressables
/// </summary>
[CreateAssetMenu(fileName = "GameConfig", menuName = "Game/GameConfig")]
public class GameConfigSO : ScriptableObject
{
    [Header("角色预览资产路径 (Resources路径, 后续迁移Addressables)")]
    public List<CharacterAssetEntry> characterAssets = new()
    {
        new() { heroClass = HeroClass.Warrior, resourcesPath = ResourcePaths.WarriorSheet, spriteName = "Warrior_Idle" },
        new() { heroClass = HeroClass.Mage,    resourcesPath = ResourcePaths.MageSheet, spriteName = "Mage_Idle" },
        new() { heroClass = HeroClass.Priest,  resourcesPath = ResourcePaths.PriestSheet, spriteName = "Priest_Idle" },
    };

    [Header("UI面板资产引用 (占位, 待实现)")]
    public List<string> uiPanelReferences = new();

    private static GameConfigSO _instance;
    public static GameConfigSO Instance
    {
        get
        {
            if (_instance != null) return _instance;
            _instance = Resources.Load<GameConfigSO>("GameConfig");
            if (_instance == null)
            {
                _instance = CreateInstance<GameConfigSO>();
                GameLog.LogWarning("[GameConfigSO] Resources/GameConfig.asset 未找到, 使用默认配置");
            }
            return _instance;
        }
    }

    public CharacterAssetEntry GetCharacterAsset(HeroClass heroClass)
    {
        foreach (var entry in characterAssets)
            if (entry.heroClass == heroClass) return entry;
        return null;
    }
}

[System.Serializable]
public class CharacterAssetEntry
{
    public HeroClass heroClass;
    [Tooltip("Resources目录下的路径, 不含扩展名")]
    public string resourcesPath;
    [Tooltip("精灵图中目标sprite的名称")]
    public string spriteName;
    [Tooltip("Addressables地址 (预留, 当前不用)")]
    public string addressableKey;
}
