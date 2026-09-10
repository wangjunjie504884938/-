using UnityEngine;

/// <summary>
/// 锻造历史数据访问层 — 从ForgeUI提取
/// </summary>
public static class ForgeData
{
    private const string KeyHistory = "ARPG_ForgeHistory";

    /// <summary>读取锻造历史（每行一条）</summary>
    public static string[] GetHistory()
    {
        string history = PlayerPrefs.GetString(KeyHistory, "");
        return string.IsNullOrEmpty(history) ? new string[0] : history.Split('\n');
    }

    /// <summary>追加一条锻造历史（保留最近5条）</summary>
    public static void AppendHistory(string entry)
    {
        string history = PlayerPrefs.GetString(KeyHistory, "");
        string[] lines = history.Split('\n');
        var newLines = new System.Collections.Generic.List<string>();
        newLines.Add(entry);
        for (int i = 0; i < lines.Length && newLines.Count < 5; i++)
        {
            if (!string.IsNullOrEmpty(lines[i]) && lines[i] != entry)
                newLines.Add(lines[i]);
        }
        PlayerPrefs.SetString(KeyHistory, string.Join("\n", newLines.ToArray()));
        PlayerPrefs.Save();
    }
}
