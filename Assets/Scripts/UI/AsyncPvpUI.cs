using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 竞技场 — 从服务器获取真实对手，挑战后提交结果
/// </summary>
public class AsyncPvpUI : MonoBehaviour
{
    public static AsyncPvpUI Instance { get; private set; }

    private GameObject panel;
    private Coroutine showRoutine;
    private const int MaxDisplay = 10;
    private Transform _listContainer;
    private Text _loadingText;
    private readonly List<int> _shuffledIndices = new List<int>();

    [System.Serializable]
    public class GhostData { public string playerName; public int classType; public int level; public int gearScore; public int wins; public int losses; public int defenseRating; }

    private List<GhostData> _opponents = new List<GhostData>();

    private const string ApiBase = "http://39.107.141.107:5132";

    private static readonly string[] ClassNames = { "战士", "法师", "牧师" };
    private static readonly Color[] ClassColors = {
        new Color(0.7f, 0.2f, 0.2f, 1f), new Color(0.2f, 0.35f, 0.8f, 1f), new Color(0.8f, 0.75f, 0.4f, 1f)
    };

    private void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; }

    public void Show()
    {
        if (panel != null) { Destroy(panel); panel = null; }
        BuildUI();
        UIHelper.SetupTopBar("竞技场", () => { Hide(); UIHelper.EnsureUIManager(); UIManager.Instance.ShowHub(); });
        panel.SetActive(true);
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(UIHelper.FadeIn(panel, 0.2f));
        StartCoroutine(FetchOpponents());
    }

    public void Hide()
    {
        if (panel != null)
        {
            if (showRoutine != null) StopCoroutine(showRoutine);
            showRoutine = StartCoroutine(UIHelper.FadeOut(panel, 0.15f));
        }
    }

    private static string GetPlayerDisplayName()
    {
        string u = CloudSaveManager.Instance?.CurrentUsername ?? "";
        return !string.IsNullOrEmpty(u) ? u : "冒险者";
    }

    /// <summary>从服务器排行榜获取真实对手，无token时用本地好友数据fallback</summary>
    private IEnumerator FetchOpponents()
    {
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");

        if (!string.IsNullOrEmpty(token))
        {
            // 有token — 从服务器获取
            if (_loadingText != null) { _loadingText.text = "加载对手..."; _loadingText.gameObject.SetActive(true); }

            using (var req = UnityEngine.Networking.UnityWebRequest.Get($"{ApiBase}/api/leaderboard/top"))
            {
                req.SetRequestHeader("Authorization", "Bearer " + token);
                req.timeout = 10;
                yield return req.SendWebRequest();

                if (_loadingText != null) _loadingText.gameObject.SetActive(false);

                if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    _opponents.Clear();
                    try
                    {
                        var resp = JsonUtility.FromJson<LeaderboardResp>(req.downloadHandler.text);
                        if (resp?.leaderboard != null)
                        {
                            foreach (var entry in resp.leaderboard)
                            {
                                if (entry.username == GetPlayerDisplayName()) continue;

                                _opponents.Add(new GhostData
                                {
                                    playerName = entry.username,
                                    classType = Mathf.Clamp(entry.classType, 0, 2),
                                    level = entry.level,
                                    gearScore = entry.level * 200 + 1000,
                                    wins = entry.wave,
                                    losses = Mathf.Max(0, entry.wave / 3),
                                    defenseRating = Mathf.Clamp(entry.level * 4, 50, 100),
                                });
                            }
                        }
                    }
                    catch { }

                    if (_opponents.Count > 0)
                    {
                        RefreshPage();
                        yield break;
                    }
                }
            }
        }

        // 无token或服务器失败 — 从本地好友数据生成对手
        _opponents.Clear();
        string friendJson = PlayerPrefs.GetString("ARPG_FriendList", "");
        if (!string.IsNullOrEmpty(friendJson))
        {
            try
            {
                var fArr = JsonUtility.FromJson<FriendArray>(friendJson);
                if (fArr?.friends != null)
                {
                    foreach (var f in fArr.friends)
                    {
                        _opponents.Add(new GhostData
                        {
                            playerName = f.name,
                            classType = Mathf.Clamp(f.cls, 0, 2),
                            level = f.level,
                            gearScore = f.gearScore > 0 ? f.gearScore : f.level * 200 + 1000,
                            wins = Mathf.Max(1, f.level),
                            losses = Mathf.Max(0, f.level / 2),
                            defenseRating = Mathf.Clamp(f.level * 4, 50, 100),
                        });
                    }
                }
            }
            catch { }
        }

        if (_loadingText != null) _loadingText.gameObject.SetActive(false);

        if (_opponents.Count == 0)
        {
            if (_loadingText != null) { _loadingText.text = "暂无可挑战的对手\n请先添加好友"; _loadingText.gameObject.SetActive(true); }
        }
        else
        {
            RefreshPage();
        }
    }

    [System.Serializable]
    private class FriendArray { public FriendUI.FriendInfo[] friends; }

    [System.Serializable]
    private class LeaderboardResp { public LeaderboardEntry[] leaderboard; }
    [System.Serializable]
    private class LeaderboardEntry { public string username; public int classType; public int wave; public int level; }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();
        panel = UiPrefabLoader.TryLoad("AsyncPvpPanel", canvas);
        if (panel == null)
        panel = new GameObject("AsyncPvpPanel"); panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.AddComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero; panel.AddComponent<CanvasGroup>();
        var bg = new GameObject("BG"); bg.transform.SetParent(panel.transform, false); var bgr = bg.AddComponent<RectTransform>(); bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one; bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero; bg.AddComponent<Image>().color = UIHelper.BgDark; bg.GetComponent<Image>().raycastTarget = true;

        UIHelper.MakeSubtitle(panel.transform, "挑战其他玩家 — 基于战力匹配", font, 100, 14);

        var player = GameManager.Instance?.Player;
        int myGS = player?.Stats.GearScore ?? 0;
        int myW = PvpStatsData.Wins;
        int myL = PvpStatsData.Losses;
        int myD = PvpStatsData.DefRating;
        string myName = GetPlayerDisplayName();

        var statsObj = new GameObject("PlayerStats"); statsObj.transform.SetParent(panel.transform, false);
        var statsR = statsObj.AddComponent<RectTransform>(); statsR.anchorMin = new Vector2(0.05f, 0.84f); statsR.anchorMax = new Vector2(0.95f, 0.90f); statsR.offsetMin = Vector2.zero; statsR.offsetMax = Vector2.zero;
        var statsTxt = statsObj.AddComponent<Text>();
        statsTxt.text = $"{myName}  战力:{myGS}  胜:{myW}  负:{myL}  防守:{myD}";
        statsTxt.alignment = TextAnchor.MiddleCenter; statsTxt.fontSize = 16; statsTxt.color = UIHelper.TextPrimary; statsTxt.font = font; statsTxt.raycastTarget = false;

        // 上传防守阵容按钮
        var upBtn = new GameObject("UploadBtn"); upBtn.transform.SetParent(panel.transform, false);
        var upR = upBtn.AddComponent<RectTransform>(); upR.anchorMin = new Vector2(0.35f, 0.78f); upR.anchorMax = new Vector2(0.65f, 0.83f); upR.offsetMin = Vector2.zero; upR.offsetMax = Vector2.zero;
        upBtn.AddComponent<Image>().color = UIHelper.BtnConfirm;
        var upB = upBtn.AddComponent<Button>();
        var upL = new GameObject("Lbl"); upL.transform.SetParent(upBtn.transform, false); var upLr = upL.AddComponent<RectTransform>(); upLr.anchorMin = Vector2.zero; upLr.anchorMax = Vector2.one; upLr.offsetMin = Vector2.zero; upLr.offsetMax = Vector2.zero;
        var upT = upL.AddComponent<Text>(); upT.text = "更新防守阵容"; upT.alignment = TextAnchor.MiddleCenter; upT.fontSize = 16; upT.color = UIHelper.TextPrimary; upT.font = font; upT.raycastTarget = false;
        upB.onClick.AddListener(() =>
        {
            int defRating = Mathf.Max(50, myGS / 100);
            PvpStatsData.SetDefRating(defRating);
            LeaderboardUI.SubmitArenaScore(true, (int)(player?.HeroClass ?? HeroClass.Warrior), myGS, defRating);
            if (GameUI.Instance != null) GameUI.Instance.ShowItemPickupToast("防守阵容已更新", $"防守分:{defRating}");
            Show();
        });

        var listArea = UIHelper.MakeGlowCard(panel.transform, "ListArea", new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.76f), Vector2.zero, Vector2.zero, UIHelper.GlowTop, UIHelper.GlowBottom, UIHelper.Accent);
        _listContainer = listArea.transform;

        // 加载提示
        _loadingText = new GameObject("LoadingText").AddComponent<Text>();
        _loadingText.transform.SetParent(listArea.transform, false);
        var ltR = _loadingText.GetComponent<RectTransform>();
        ltR.anchorMin = new Vector2(0, 0.4f); ltR.anchorMax = new Vector2(1, 0.6f);
        ltR.offsetMin = Vector2.zero; ltR.offsetMax = Vector2.zero;
        _loadingText.text = "加载对手..."; _loadingText.alignment = TextAnchor.MiddleCenter;
        _loadingText.fontSize = 18; _loadingText.color = UIHelper.TextDim; _loadingText.font = font;
        _loadingText.raycastTarget = false; _loadingText.gameObject.SetActive(false);

        var refreshRow = new GameObject("RefreshRow"); refreshRow.transform.SetParent(panel.transform, false);
        var rfr = refreshRow.AddComponent<RectTransform>(); rfr.anchorMin = new Vector2(0.35f, 0.02f); rfr.anchorMax = new Vector2(0.65f, 0.07f); rfr.offsetMin = Vector2.zero; rfr.offsetMax = Vector2.zero;
        UIHelper.MakeButton(refreshRow.transform, "RefreshBtn", "刷新对手", font, new Vector2(0, 0), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, new Color(0.08f, 0.08f, 0.12f, 0.9f), 16, () => { RefreshPage(); });

        panel.SetActive(false);
    }

    private void RefreshPage()
    {
        var toRemove = new List<GameObject>();
        foreach (Transform child in _listContainer) if (child.name.StartsWith("Ghost_") || child.name == "Header") toRemove.Add(child.gameObject);
        foreach (var go in toRemove) Destroy(go);
        Font font = GameManager.GetUIFont();

        if (_opponents.Count == 0)
        {
            if (_loadingText != null) { _loadingText.text = "暂无对手"; _loadingText.gameObject.SetActive(true); }
            return;
        }

        if (_loadingText != null) _loadingText.gameObject.SetActive(false);

        // 随机洗牌，不足10个则循环填充
        _shuffledIndices.Clear();
        for (int i = 0; i < _opponents.Count; i++) _shuffledIndices.Add(i);
        for (int i = _shuffledIndices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
        }
        while (_shuffledIndices.Count < MaxDisplay)
            _shuffledIndices.Add(Random.Range(0, _opponents.Count));
        if (_shuffledIndices.Count > MaxDisplay)
            _shuffledIndices.RemoveRange(MaxDisplay, _shuffledIndices.Count - MaxDisplay);
        int count = MaxDisplay;

        // Header — 与行布局对齐（Name 0~45% / Stat 46~78% / 按钮区 80~98%）
        float rowH = 48f, gap = 4f;
        float totalH = count * rowH + (count - 1) * gap;
        var hdr = new GameObject("Header"); hdr.transform.SetParent(_listContainer, false);
        var hdrR = hdr.AddComponent<RectTransform>(); hdrR.anchorMin = new Vector2(0.02f, 0.5f); hdrR.anchorMax = new Vector2(0.98f, 0.5f); hdrR.anchoredPosition = new Vector2(0, totalH / 2f + 12); hdrR.sizeDelta = new Vector2(0, 20);
        var hdrName = new GameObject("HdrName"); hdrName.transform.SetParent(hdr.transform, false);
        var hnr = hdrName.AddComponent<RectTransform>(); hnr.anchorMin = new Vector2(0, 0); hnr.anchorMax = new Vector2(0.45f, 1); hnr.offsetMin = new Vector2(10, 0); hnr.offsetMax = Vector2.zero;
        var hdrNameTxt = hdrName.AddComponent<Text>(); hdrNameTxt.text = "玩家"; hdrNameTxt.alignment = TextAnchor.MiddleLeft; hdrNameTxt.fontSize = 13; hdrNameTxt.color = UIHelper.TextDim; hdrNameTxt.font = font; hdrNameTxt.raycastTarget = false;
        var hdrGS = new GameObject("HdrGS"); hdrGS.transform.SetParent(hdr.transform, false);
        var hgr = hdrGS.AddComponent<RectTransform>(); hgr.anchorMin = new Vector2(0.46f, 0); hgr.anchorMax = new Vector2(0.62f, 1); hgr.offsetMin = Vector2.zero; hgr.offsetMax = Vector2.zero;
        var hdrGSTxt = hdrGS.AddComponent<Text>(); hdrGSTxt.text = "战力"; hdrGSTxt.alignment = TextAnchor.MiddleCenter; hdrGSTxt.fontSize = 13; hdrGSTxt.color = UIHelper.TextDim; hdrGSTxt.font = font; hdrGSTxt.raycastTarget = false;
        var hdrWL = new GameObject("HdrWL"); hdrWL.transform.SetParent(hdr.transform, false);
        var hwr = hdrWL.AddComponent<RectTransform>(); hwr.anchorMin = new Vector2(0.62f, 0); hwr.anchorMax = new Vector2(0.78f, 1); hwr.offsetMin = Vector2.zero; hwr.offsetMax = Vector2.zero;
        var hdrWLTxt = hdrWL.AddComponent<Text>(); hdrWLTxt.text = "胜/负"; hdrWLTxt.alignment = TextAnchor.MiddleCenter; hdrWLTxt.fontSize = 13; hdrWLTxt.color = UIHelper.TextDim; hdrWLTxt.font = font; hdrWLTxt.raycastTarget = false;

        float startY = totalH / 2f - rowH / 2f;

        for (int i = 0; i < count; i++)
        {
            int ghostIdx = _shuffledIndices[i];
            float y = startY - i * (rowH + gap);
            BuildGhostRow(_listContainer, font, _opponents[ghostIdx], ghostIdx, i + 1, y, rowH);
        }
    }

    private void BuildGhostRow(Transform parent, Font font, GhostData ghost, int globalIndex, int displayRank, float y, float rowH)
    {
        var row = UIHelper.MakeGlowCard(parent, $"Ghost_{displayRank}", new Vector2(0.02f, 0.5f), new Vector2(0.98f, 0.5f), new Vector2(0, y - rowH / 2f), new Vector2(0, y + rowH / 2f), new Color(0.06f, 0.04f, 0.08f, 0.92f), UIHelper.GlowBottom, UIHelper.BorderSubtle);
        int clsIdx = Mathf.Clamp(ghost.classType, 0, 2);
        string classColor = ColorUtility.ToHtmlStringRGBA(ClassColors[clsIdx]);

        var nameObj = new GameObject("Name"); nameObj.transform.SetParent(row.transform, false);
        var nr = nameObj.AddComponent<RectTransform>(); nr.anchorMin = new Vector2(0, 0); nr.anchorMax = new Vector2(0.45f, 1); nr.offsetMin = new Vector2(10, 2); nr.offsetMax = new Vector2(0, -2);
        var nameTxt = nameObj.AddComponent<Text>(); nameTxt.text = $"{displayRank}. <color=#{classColor}>[{ClassNames[clsIdx]}]</color> {ghost.playerName}  Lv.{ghost.level}"; nameTxt.alignment = TextAnchor.MiddleLeft; nameTxt.fontSize = 15; nameTxt.color = UIHelper.TextPrimary; nameTxt.font = font; nameTxt.supportRichText = true; nameTxt.raycastTarget = false;

        var gsObj = new GameObject("GearScore"); gsObj.transform.SetParent(row.transform, false);
        var gsr = gsObj.AddComponent<RectTransform>(); gsr.anchorMin = new Vector2(0.46f, 0); gsr.anchorMax = new Vector2(0.62f, 1); gsr.offsetMin = Vector2.zero; gsr.offsetMax = Vector2.zero;
        var gsTxt = gsObj.AddComponent<Text>(); gsTxt.text = $"{ghost.gearScore}"; gsTxt.alignment = TextAnchor.MiddleCenter; gsTxt.fontSize = 14; gsTxt.color = UIHelper.TextSecondary; gsTxt.font = font; gsTxt.raycastTarget = false;

        var wlObj = new GameObject("WinLoss"); wlObj.transform.SetParent(row.transform, false);
        var wlr = wlObj.AddComponent<RectTransform>(); wlr.anchorMin = new Vector2(0.62f, 0); wlr.anchorMax = new Vector2(0.78f, 1); wlr.offsetMin = Vector2.zero; wlr.offsetMax = Vector2.zero;
        var wlTxt = wlObj.AddComponent<Text>(); wlTxt.text = $"{ghost.wins}/{ghost.losses}"; wlTxt.alignment = TextAnchor.MiddleCenter; wlTxt.fontSize = 14; wlTxt.color = UIHelper.TextSecondary; wlTxt.font = font; wlTxt.raycastTarget = false;

        var btnObj = new GameObject("ChallengeBtn"); btnObj.transform.SetParent(row.transform, false);
        var br = btnObj.AddComponent<RectTransform>(); br.anchorMin = new Vector2(0.80f, 0.15f); br.anchorMax = new Vector2(0.98f, 0.85f); br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
        btnObj.AddComponent<Image>().color = UIHelper.BtnConfirm; var btn = btnObj.AddComponent<Button>();
        var btnLbl = new GameObject("Lbl"); btnLbl.transform.SetParent(btnObj.transform, false); var blr = btnLbl.AddComponent<RectTransform>(); blr.anchorMin = Vector2.zero; blr.anchorMax = Vector2.one; blr.offsetMin = Vector2.zero; blr.offsetMax = Vector2.zero;
        var btnTxt = btnLbl.AddComponent<Text>(); btnTxt.text = "挑战"; btnTxt.alignment = TextAnchor.MiddleCenter; btnTxt.fontSize = 15; btnTxt.color = UIHelper.TextPrimary; btnTxt.font = font; btnTxt.raycastTarget = false;
        int idx = globalIndex; btn.onClick.AddListener(() => StartCoroutine(ChallengeWithAnimation(_opponents[idx])));
    }

    private IEnumerator ChallengeWithAnimation(GhostData ghost)
    {
        var player = GameManager.Instance?.Player; if (player == null) yield break;

        if (panel != null) panel.SetActive(false);

        string myName = GetPlayerDisplayName();
        int myGS = player.Stats.GearScore;
        int myClass = (int)player.HeroClass;

        bool won = false;
        yield return ArenaBattleUI.StartBattle(myName, myGS, myClass, ghost, result => won = result);

        // 提交战斗结果到服务器验证+发放奖励
        yield return SubmitArenaResult(ghost, won, myGS, myClass);

        // 提交竞技场成绩
        int defenseRating = PvpStatsData.DefRating;
        LeaderboardUI.SubmitArenaScore(won, myClass, player.Stats.GearScore, defenseRating);

        // 重建面板（重新获取对手）
        if (panel != null) { Destroy(panel); panel = null; }
        Show();
    }

    /// <summary>提交竞技场战斗结果到服务器，服务器验证+发放奖励</summary>
    private IEnumerator SubmitArenaResult(GhostData ghost, bool won, int myGS, int myClass)
    {
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (string.IsNullOrEmpty(token)) yield break;

        // 本地先更新胜负记录
        PvpStatsData.RecordResult(won);

        // 服务器提交
        string json = JsonUtility.ToJson(new ArenaResultBody
        {
            targetName = ghost.playerName,
            won = won,
            myGearScore = myGS,
            myClassType = myClass
        });

        using (var req = new UnityEngine.Networking.UnityWebRequest($"{ApiBase}/api/arena/result", "POST"))
        {
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 10;
            yield return req.SendWebRequest();

            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<ArenaResultResp>(req.downloadHandler.text);
                    if (resp?.success == true)
                    {
                        // 服务器发放奖励
                        var player = GameManager.Instance?.Player;
                        if (player != null && resp.goldReward > 0)
                        {
                            player.Stats.AddGold(resp.goldReward);
                            SeasonPass.AddXP(resp.xpReward);
                            if (GameUI.Instance != null)
                                GameUI.Instance.ShowItemPickupToast(
                                    won ? "竞技场胜利!" : "竞技场失败",
                                    $"{(won ? ghost.playerName : "对方")}\n金币+{resp.goldReward} 赛季XP+{resp.xpReward}");
                        }
                    }
                }
                catch { }
            }
            else
            {
                // 服务器不可用 — 本地fallback发奖
                var player = GameManager.Instance?.Player;
                if (player != null)
                {
                    int reward = won ? 100 + ghost.defenseRating * 2 : 20;
                    player.Stats.AddGold(reward);
                    SeasonPass.AddXP(won ? 30 : 5);
                    if (GameUI.Instance != null)
                        GameUI.Instance.ShowItemPickupToast(
                            won ? "竞技场胜利!" : "竞技场失败",
                            $"金币+{reward} 赛季XP+{(won ? 30 : 5)}");
                }
            }
        }
    }

    [System.Serializable]
    private class ArenaResultBody { public string targetName; public bool won; public int myGearScore; public int myClassType; }
    [System.Serializable]
    private class ArenaResultResp { public bool success; public int goldReward; public int xpReward; }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
