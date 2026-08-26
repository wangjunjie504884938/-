using System.Text;

/// <summary>
/// String pool for UI text formatting. Reuses StringBuilder to avoid per-frame GC allocations.
/// </summary>
public static class UIStringBuilderPool
{
    private const int PoolSize = 10;
    private static readonly StringBuilder[] _pool = new StringBuilder[PoolSize];
    private static int _index = 0;

    static UIStringBuilderPool()
    {
        for (int i = 0; i < PoolSize; i++)
            _pool[i] = new StringBuilder(256);
    }

    public static StringBuilder Get()
    {
        var sb = _pool[_index];
        _index = (_index + 1) % PoolSize;
        sb.Clear();
        return sb;
    }

    public static string FormatLevel(int level)
    {
        var sb = Get();
        sb.Append("Lv.");
        sb.Append(level);
        return sb.ToString();
    }

    public static string FormatXp(int xp, int xpToNext)
    {
        var sb = Get();
        sb.Append("XP: ");
        sb.Append(xp);
        sb.Append('/');
        sb.Append(xpToNext);
        return sb.ToString();
    }

    public static string FormatGold(int gold)
    {
        var sb = Get();
        sb.Append("金币: ");
        sb.Append(FormatNumber(gold));
        return sb.ToString();
    }

    /// <summary>
    /// 格式化数字显示：≥10亿→1.78B, ≥1亿→123.46M, ≥1万→12.34K, <1万→1,234
    /// </summary>
    public static string FormatNumber(int value)
    {
        var sb = Get();
        long v = value;
        if (v >= 1_000_000_000L)
        {
            sb.Append((v / 10_000_000L / 100.0).ToString("F2")).Append('B');
        }
        else if (v >= 100_000_000L)
        {
            sb.Append((v / 1_000_000L / 100.0).ToString("F2")).Append('M');
        }
        else if (v >= 10_000L)
        {
            sb.Append((v / 10_000L / 100.0).ToString("F2")).Append('K');
        }
        else
        {
            sb.Append(v.ToString("#,0"));
        }
        return sb.ToString();
    }

    public static string FormatStats(int atk, int def, float spd, float regen, float crit, float ls, float range, float aspd, int shield)
    {
        var sb = Get();
        sb.Append("ATK:");
        sb.Append(atk);
        sb.Append(" DEF:");
        sb.Append(def);
        sb.Append(" SPD:");
        sb.Append(spd.ToString("F1"));
        sb.Append('\n');
        sb.Append("回复:");
        sb.Append(regen.ToString("F1"));
        sb.Append("/s 暴击:");
        sb.Append((crit * 100).ToString("F0"));
        sb.Append("% 吸血:");
        sb.Append((ls * 100).ToString("F0"));
        sb.Append("%\n");
        sb.Append("范围:");
        sb.Append(range.ToString("F1"));
        sb.Append(" 攻速:");
        sb.Append(aspd.ToString("F1"));
        sb.Append("x 护盾:");
        sb.Append(shield);
        return sb.ToString();
    }

    public static string FormatEnemyCount(int alive, int killed)
    {
        var sb = Get();
        sb.Append("怪物: ");
        sb.Append(alive);
        sb.Append(" | 击杀: ");
        sb.Append(killed);
        return sb.ToString();
    }

    public static string FormatDungeonInfo(int dungeonLevel, int wave, int maxWaves)
    {
        var sb = Get();
        sb.Append("地下城: ");
        sb.Append(dungeonLevel);
        sb.Append("F  波次:");
        sb.Append(wave);
        sb.Append('/');
        sb.Append(maxWaves);
        return sb.ToString();
    }

    public static string FormatEnemyRemaining(int alive)
    {
        var sb = Get();
        sb.Append("剩余: ");
        sb.Append(alive);
        return sb.ToString();
    }
}
