using UnityEngine;

/// <summary>
/// 角色体系(修炼)数据 — 17节点(空间/生命/法则/阴阳 + 精/智/耐/木/水/攻/敏/土 + 金/雷/火/风/冰)点位与锁定态、阴阳池。
/// 点数池复用 SkillPoints (真实属性点, 与技能升级共用); 重塑返还未锁定点位, 锁定节点保留。
/// 持久化: PlayerPrefs 槽位前缀 ARPG_S{n}_Sys*, 由 PlayerProgressData.Save/LoadSaved/ClearSave 调用。
/// 属性加成幂等(先减上次快照再加当前), 挂在 Stats.RecalculatePassiveTree 统一重算, 顺序与装备重算兼容。
/// </summary>
public static class CharacterSystemData
{
    // ====== 节点定义 ======
    public const int Space = 0, Life = 1, Law = 2, YinYang = 3;
    public const int Jing = 4, Zhi = 5, Nai = 6, Mu = 7, Shui = 8, Gong = 9, Min = 10, Tu = 11;
    public const int Jin = 12, Lei = 13, Huo = 14, Feng = 15, Bing = 16;   // 金/雷/火/风/冰: 元素节点(无属性加成, 同木/水/土)
    public const int NodeCount = 17;

    public struct NodeDef
    {
        public int Id;
        public string Name;
        public bool Big;      // 大节点(空间/生命/法则/阴阳)用大体系框
        public bool Orange;   // 法则/阴阳名字橙色
    }

    // 注意: 数组必须按 Id 顺序排列 (UI 层按 Nodes[Id] 索引取定义)
    public static readonly NodeDef[] Nodes = new NodeDef[NodeCount]
    {
        new NodeDef{ Id = Space,   Name = "空间", Big = true,  Orange = false },
        new NodeDef{ Id = Life,    Name = "生命", Big = true,  Orange = false },
        new NodeDef{ Id = Law,     Name = "法则", Big = true,  Orange = true },
        new NodeDef{ Id = YinYang, Name = "阴阳", Big = true,  Orange = true },
        new NodeDef{ Id = Jing,    Name = "精",   Big = false, Orange = false },
        new NodeDef{ Id = Zhi,     Name = "智",   Big = false, Orange = false },
        new NodeDef{ Id = Nai,     Name = "耐",   Big = false, Orange = false },
        new NodeDef{ Id = Mu,      Name = "木",   Big = false, Orange = false },
        new NodeDef{ Id = Shui,    Name = "水",   Big = false, Orange = false },
        new NodeDef{ Id = Gong,    Name = "攻",   Big = false, Orange = false },
        new NodeDef{ Id = Min,     Name = "敏",   Big = false, Orange = false },
        new NodeDef{ Id = Tu,      Name = "土",   Big = false, Orange = false },
        new NodeDef{ Id = Jin,     Name = "金",   Big = false, Orange = false },
        new NodeDef{ Id = Lei,     Name = "雷",   Big = false, Orange = false },
        new NodeDef{ Id = Huo,     Name = "火",   Big = false, Orange = false },
        new NodeDef{ Id = Feng,    Name = "风",   Big = false, Orange = false },
        new NodeDef{ Id = Bing,    Name = "冰",   Big = false, Orange = false },
    };

    // ====== 运行时状态 ======
    private static readonly int[] _points = new int[NodeCount];
    private static readonly bool[] _locked = new bool[NodeCount];
    private static int _yin;
    private static int _yang;

    public static int GetPoints(int id) => (id >= 0 && id < NodeCount) ? _points[id] : 0;
    public static bool IsLocked(int id) => (id >= 0 && id < NodeCount) && _locked[id];
    public static bool IsLearned(int id) => GetPoints(id) > 0;
    public static int Yin => _yin;
    public static int Yang => _yang;

    /// <summary>已分配总量(所有节点+阴阳, 含锁定) — 展示用</summary>
    public static int TotalAllocated()
    {
        int sum = _yin + _yang;
        for (int i = 0; i < NodeCount; i++) sum += _points[i];
        return sum;
    }

    /// <summary>节点加点; available=当前可用点数池, 返回实际分配量(不足则0)</summary>
    public static int Allocate(int id, int amount, int available)
    {
        if (id < 0 || id >= NodeCount || amount <= 0 || available <= 0) return 0;
        int grant = Mathf.Min(amount, available);
        _points[id] += grant;
        return grant;
    }

    /// <summary>阴阳池调整: delta>0 从点数池转入阴阳, delta<0 退回点数池; 返回实际移动量</summary>
    public static int MoveYin(int delta, int available)
    {
        if (delta > 0) { int mv = Mathf.Min(delta, available); _yin += mv; return mv; }
        if (delta < 0) { int mv = Mathf.Min(-delta, _yin); _yin -= mv; return -mv; }
        return 0;
    }

    public static int MoveYang(int delta, int available)
    {
        if (delta > 0) { int mv = Mathf.Min(delta, available); _yang += mv; return mv; }
        if (delta < 0) { int mv = Mathf.Min(-delta, _yang); _yang -= mv; return -mv; }
        return 0;
    }

    public static void SetLocked(int id, bool locked)
    {
        if (id >= 0 && id < NodeCount) _locked[id] = locked;
    }

    /// <summary>重塑可返还的点数(未锁定节点 + 阴阳池; 锁定节点保留)</summary>
    public static int ReshapeRefund()
    {
        int refund = _yin + _yang;
        for (int i = 0; i < NodeCount; i++)
            if (!_locked[i]) refund += _points[i];
        return refund;
    }

    /// <summary>执行重塑: 清空未锁定节点与阴阳池(锁定保留), 返回返还点数</summary>
    public static int Reshape()
    {
        int refund = ReshapeRefund();
        for (int i = 0; i < NodeCount; i++)
            if (!_locked[i]) _points[i] = 0;
        _yin = 0;
        _yang = 0;
        return refund;
    }

    // ====== 属性加成 (幂等: 减上次快照 + 加当前) ======
    private const float HpPerPoint = 2f;        // 生命
    private const float AtkPerPoint = 1f;       // 攻
    private const float DefPerPoint = 1f;       // 耐
    private const float SpdPerPoint = 0.005f;   // 敏
    private const float CritPerPoint = 0.001f;  // 智 (+0.1%)
    private const float LsPerPoint = 0.001f;    // 精 (+0.1%吸血)

    private static int _apHp, _apAtk, _apDef;
    private static float _apSpd, _apCrit, _apLs;

    public static void ApplyEffects(PlayerRuntimeStats runtime)
    {
        if (runtime == null) return;
        // 减去上次应用值, 防止重复重算时叠加
        runtime.BonusMaxHp -= _apHp;
        runtime.BonusAttack -= _apAtk;
        runtime.BonusDefense -= _apDef;
        runtime.BonusMoveSpeed -= _apSpd;
        runtime.BonusCritChance -= _apCrit;
        runtime.BonusLifeSteal -= _apLs;

        _apHp = Mathf.RoundToInt(GetPoints(Life) * HpPerPoint);
        _apAtk = Mathf.RoundToInt(GetPoints(Gong) * AtkPerPoint);
        _apDef = Mathf.RoundToInt(GetPoints(Nai) * DefPerPoint);
        _apSpd = GetPoints(Min) * SpdPerPoint;
        _apCrit = GetPoints(Zhi) * CritPerPoint;
        _apLs = GetPoints(Jing) * LsPerPoint;

        runtime.BonusMaxHp += _apHp;
        runtime.BonusAttack += _apAtk;
        runtime.BonusDefense += _apDef;
        runtime.BonusMoveSpeed += _apSpd;
        runtime.BonusCritChance += _apCrit;
        runtime.BonusLifeSteal += _apLs;
    }

    // ====== 持久化 (槽位前缀由 PlayerProgressData 提供) ======
    private static int _loadedSlot = -1;

    public static void Save(string keyPrefix)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < NodeCount; i++)
        {
            if (_points[i] <= 0) continue;
            if (sb.Length > 0) sb.Append(',');
            sb.Append(i).Append(':').Append(_points[i]);
        }
        PlayerPrefs.SetString(keyPrefix + "SysPts", sb.ToString());

        var lk = new System.Text.StringBuilder();
        for (int i = 0; i < NodeCount; i++)
        {
            if (!_locked[i]) continue;
            if (lk.Length > 0) lk.Append(',');
            lk.Append(i);
        }
        PlayerPrefs.SetString(keyPrefix + "SysLock", lk.ToString());
        PlayerPrefs.SetInt(keyPrefix + "SysYin", _yin);
        PlayerPrefs.SetInt(keyPrefix + "SysYang", _yang);
    }

    public static void Load(string keyPrefix)
    {
        for (int i = 0; i < NodeCount; i++) { _points[i] = 0; _locked[i] = false; }
        _yin = 0; _yang = 0;
        _loadedSlot = PlayerProgressData.ActiveSlot;

        string pts = PlayerPrefs.GetString(keyPrefix + "SysPts", "");
        if (!string.IsNullOrEmpty(pts))
        {
            foreach (var part in pts.Split(','))
            {
                var kv = part.Split(':');
                if (kv.Length == 2 && int.TryParse(kv[0], out int id) && int.TryParse(kv[1], out int p)
                    && id >= 0 && id < NodeCount && p > 0)
                    _points[id] = p;
            }
        }
        string locks = PlayerPrefs.GetString(keyPrefix + "SysLock", "");
        if (!string.IsNullOrEmpty(locks))
        {
            foreach (var part in locks.Split(','))
            {
                if (int.TryParse(part, out int id) && id >= 0 && id < NodeCount)
                    _locked[id] = true;
            }
        }
        _yin = PlayerPrefs.GetInt(keyPrefix + "SysYin", 0);
        _yang = PlayerPrefs.GetInt(keyPrefix + "SysYang", 0);
    }

    /// <summary>槽位是否与当前缓存一致 (UI判断是否需要重读)</summary>
    public static bool IsLoadedForActiveSlot => _loadedSlot == PlayerProgressData.ActiveSlot;
}
