using UnityEngine;

/// <summary>
/// 对话已读状态数据访问层 — 从DialogueSystem提取
/// </summary>
public static class DialogueData
{
    public static bool IsStartDialogueRead(int stageIndex)
    {
        return PlayerPrefs.GetInt($"ARPG_DialogueStart_{stageIndex}", 0) == 1;
    }

    public static void MarkStartDialogueRead(int stageIndex)
    {
        PlayerPrefs.SetInt($"ARPG_DialogueStart_{stageIndex}", 1);
    }

    public static bool IsEndDialogueRead(int stageIndex)
    {
        return PlayerPrefs.GetInt($"ARPG_DialogueEnd_{stageIndex}", 0) == 1;
    }

    public static void MarkEndDialogueRead(int stageIndex)
    {
        PlayerPrefs.SetInt($"ARPG_DialogueEnd_{stageIndex}", 1);
    }
}
