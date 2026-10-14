using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Animated title screen for "卫冕战争" with design-asset background:
/// - Full-screen design background image (Sprites/Title/TitleBackground)
/// - Design title logo (Sprites/Title/TitleLogo)
/// - Golden plaque enter button (Sprites/Title/EnterBtn)
/// - Floating particle system (embers / stars)
/// </summary>
public partial class TitleScreen : MonoBehaviour
{
    public static TitleScreen Instance { get; private set; }

    private GameObject panel;
    private bool started;

    // Animation references
    private List<ParticleData> particles = new List<ParticleData>();
    private float animTime;

    private struct ParticleData
    {
        public RectTransform rect;
        public float speed;
        public float phase;
        public float amplitude;
        public float baseAlpha;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void Show()
    {
        started = false;
        if (panel == null) BuildUI();
        panel.SetActive(true);
        animTime = 0f;

    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (panel == null || !panel.activeSelf) return;

        animTime += Time.deltaTime;

        // Animate particles (float upward with sinusoidal drift)
        for (int i = 0; i < particles.Count; i++)
        {
            var p = particles[i];
            Vector2 pos = p.rect.anchoredPosition;
            pos.y += p.speed * Time.deltaTime * 60f;
            pos.x += Mathf.Sin(animTime * 1.5f + p.phase) * p.amplitude * Time.deltaTime * 30f;

            // Reset when off top
            if (pos.y > 900f)
            {
                pos.y = -50f;
                pos.x = Random.Range(-400f, 400f);
            }
            p.rect.anchoredPosition = pos;

            // Fade in/out
            float alpha = p.baseAlpha;
            if (pos.y < 50f) alpha *= pos.y / 50f;
            if (pos.y > 750f) alpha *= (900f - pos.y) / 150f;
            var img = p.rect.GetComponent<Image>();
            if (img != null)
            {
                var c = img.color;
                c.a = alpha;
                img.color = c;
            }
        }
    }

    private void BuildUI()
    {
        Font font = GameManager.GetUIFont();
        Canvas canvas = GameManager.EnsureCanvas();

        panel = new GameObject("TitleScreenPanel");
        panel.transform.SetParent(canvas.transform, false);
        RectTransform panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // === 设计图背景 (2560x1440 全屏铺满, 加载失败回退渐变底) ===
        Sprite bgSprite = Resources.Load<Sprite>(ResourcePaths.TitleBackground);
        GameObject bgObj = new GameObject("DesignBG");
        bgObj.transform.SetParent(panel.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.sprite = bgSprite != null ? bgSprite : CreateGradientBG(256, 256);
        bgImg.color = Color.white;
        bgImg.raycastTarget = false;

        // === Floating particle system (embers / stars) ===
        CreateParticles(panel, 35);

        // === 游戏标题 (设计图素材, 标题文字已烙图) ===
        Sprite logoSprite = Resources.Load<Sprite>(ResourcePaths.TitleLogo);
        if (logoSprite == null)
            Debug.LogError("TitleScreen: missing sprite Sprites/Title/TitleLogo");
        GameObject logoObj = new GameObject("TitleLogo");
        logoObj.transform.SetParent(panel.transform, false);
        RectTransform logoRect = logoObj.AddComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0.5f, 0.5f);
        logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = new Vector2(0, -20);
        logoRect.sizeDelta = new Vector2(1516, 193);
        Image logoImg = logoObj.AddComponent<Image>();
        logoImg.sprite = logoSprite;
        logoImg.preserveAspect = true;
        logoImg.raycastTarget = false;

        // === 进入游戏按钮 (金牌设计图, 铜牌面位于图上部约10%~58%区域) ===
        Sprite btnSprite = Resources.Load<Sprite>(ResourcePaths.TitleEnterBtn);
        if (btnSprite == null)
            Debug.LogError("TitleScreen: missing sprite Sprites/Title/EnterBtn");
        GameObject startObj = new GameObject("StartBtn");
        startObj.transform.SetParent(panel.transform, false);
        RectTransform startRect = startObj.AddComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0.5f, 0.5f);
        startRect.anchorMax = new Vector2(0.5f, 0.5f);
        startRect.pivot = new Vector2(0.5f, 0.5f);
        startRect.anchoredPosition = new Vector2(0, -280);
        startRect.sizeDelta = new Vector2(330, 108);
        Image startImg = startObj.AddComponent<Image>();
        startImg.sprite = btnSprite;
        if (btnSprite == null) startImg.color = new Color(0.6f, 0.15f, 0.1f, 0.9f);

        // Button text — 深色字对准铜牌面 (牌面在图顶部8%~55%, Unity锚点自底起算)
        GameObject btnLabel = new GameObject("Label");
        btnLabel.transform.SetParent(startObj.transform, false);
        RectTransform lblRect = btnLabel.AddComponent<RectTransform>();
        lblRect.anchorMin = new Vector2(0f, 0.45f);
        lblRect.anchorMax = new Vector2(1f, 0.92f);
        lblRect.offsetMin = Vector2.zero;
        lblRect.offsetMax = Vector2.zero;
        Text lblText = btnLabel.AddComponent<Text>();
        lblText.text = "进入游戏";
        lblText.alignment = TextAnchor.MiddleCenter;
        lblText.fontSize = 36;
        lblText.color = new Color(0.2f, 0.1f, 0.03f);
        lblText.font = font;
        lblText.raycastTarget = false;

        Button startBtn = startObj.AddComponent<Button>();
        startBtn.onClick.AddListener(OnStartGame);

        // === Version Text ===
        GameObject verObj = new GameObject("Version");
        verObj.transform.SetParent(panel.transform, false);
        RectTransform verRect = verObj.AddComponent<RectTransform>();
        verRect.anchorMin = new Vector2(1, 0);
        verRect.anchorMax = new Vector2(1, 0);
        verRect.pivot = new Vector2(1, 0);
        verRect.anchoredPosition = new Vector2(-15, 15);
        verRect.sizeDelta = new Vector2(200, 25);
        Text verText = verObj.AddComponent<Text>();
        verText.text = "v0.7.0 Alpha";
        verText.alignment = TextAnchor.MiddleRight;
        verText.fontSize = 14;
        verText.color = new Color(0.4f, 0.4f, 0.5f, 0.6f);
        verText.font = font;

        // === Cloud Login Button (bottom-left) ===
        CreateCloudLoginButton(panel.transform, font);

        // === Server Select Button (bottom-center) ===
        CreateServerSelectButton(panel.transform, font);

        panel.SetActive(false);
    }

    // ==== Animated background sprites — moved to TitleScreen.Bg.cs (partial class) ====
    // ======================== CLOUD LOGIN ========================

    private void CreateCloudLoginButton(Transform parent, Font font)
    {
        GameObject btnObj = new GameObject("CloudLoginBtn");
        btnObj.transform.SetParent(parent, false);
        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0, 0);
        btnRect.anchorMax = new Vector2(0, 0);
        btnRect.pivot = new Vector2(0, 0);
        btnRect.anchoredPosition = new Vector2(20, 20);
        btnRect.sizeDelta = new Vector2(180, 48);

        // Dark Souls style: dark bg + golden border
        Image btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(0.08f, 0.06f, 0.10f, 0.9f);

        // Golden border
        GameObject borderObj = new GameObject("Border");
        borderObj.transform.SetParent(btnObj.transform, false);
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero; borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-2, -2); borderRect.offsetMax = new Vector2(2, 2);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = new Color(0.62f, 0.47f, 0.08f, 0.85f);

        // Inner
        GameObject innerObj = new GameObject("Inner");
        innerObj.transform.SetParent(borderObj.transform, false);
        RectTransform innerRect = innerObj.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero; innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2, 2); innerRect.offsetMax = new Vector2(-2, -2);
        Image innerImg = innerObj.AddComponent<Image>();
        innerImg.color = new Color(0.05f, 0.04f, 0.08f, 0.95f);

        // Label
        GameObject lblObj = new GameObject("Label");
        lblObj.transform.SetParent(btnObj.transform, false);
        RectTransform lblRect = lblObj.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero; lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = Vector2.zero; lblRect.offsetMax = Vector2.zero;
        Text lblText = lblObj.AddComponent<Text>();
        lblText.alignment = TextAnchor.MiddleCenter;
        lblText.fontSize = 20;
        lblText.color = new Color(1f, 0.85f, 0.3f);
        lblText.font = font;

        // Cloud icon prefix
        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(btnObj.transform, false);
        RectTransform iconRect = iconObj.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0, 0.5f); iconRect.anchorMax = new Vector2(0, 0.5f);
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(20, 0);
        iconRect.sizeDelta = new Vector2(20, 20);
        Text iconText = iconObj.AddComponent<Text>();
        iconText.text = "";
        iconText.alignment = TextAnchor.MiddleCenter;
        iconText.fontSize = 18;
        iconText.color = new Color(1f, 0.85f, 0.3f);
        iconText.font = font;
        iconText.raycastTarget = false;

        // Update label based on login state
        bool loggedIn = CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn;
        string user = loggedIn ? CloudSaveManager.Instance.CurrentUsername : "";
        lblText.text = loggedIn ? $"已登录: {user}" : "云端存档";
        lblText.raycastTarget = false;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(() =>
        {
            var authUI = GetComponent<CloudAuthUI>();
            if (authUI == null)
                authUI = gameObject.AddComponent<CloudAuthUI>();

            authUI.ShowAuthPage(panel.transform, font);
        });
    }

    private void CreateServerSelectButton(Transform parent, Font font)
    {
        // 显示当前服务器 + 切换按钮
        var server = ServerRegionManager.CurrentServer;
        string serverName = server != null ? server.name : "未选择";
        int status = server != null ? server.status : 0;
        string statusText = ServerRegionManager.GetStatusText(status);
        Color statusColor = ServerRegionManager.GetStatusColor(status);

        GameObject btnObj = new GameObject("ServerSelectBtn");
        btnObj.transform.SetParent(parent, false);
        RectTransform r = btnObj.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(0.5f, 0); r.anchorMax = new Vector2(0.5f, 0);
        r.pivot = new Vector2(0.5f, 0); r.anchoredPosition = new Vector2(0, 60);
        r.sizeDelta = new Vector2(300, 35);

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.08f, 0.06f, 0.12f, 0.8f);

        GameObject lbl = new GameObject("Lbl");
        lbl.transform.SetParent(btnObj.transform, false);
        RectTransform lr = lbl.AddComponent<RectTransform>();
        lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one;
        lr.offsetMin = Vector2.zero; lr.offsetMax = Vector2.zero;
        Text txt = lbl.AddComponent<Text>();
        txt.text = $"服务器: {serverName}  <color=#{ColorUtility.ToHtmlStringRGBA(statusColor)}>[{statusText}]</color>  点击切换";
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 16; txt.color = new Color(0.8f, 0.75f, 0.9f, 1f); txt.font = font;
        txt.supportRichText = true;
        txt.raycastTarget = false;

        Button btn = btnObj.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            Hide();
            GameManager.Instance.ShowServerSelect();
        });
    }

    private void OnStartGame()
    {
        if (started) return;
        started = true;
        StartCoroutine(CheckServerAndStart());
    }

    private System.Collections.IEnumerator CheckServerAndStart()
    {
        // 统一加载遮罩页 (异步分步推进, 全屏遮罩防误操作)
        LoadingScreen.Show("正在连接服务器...");
        yield return null; // 等一帧让遮罩渲染

        // 检查服务器是否连接
        string serverUrl = CloudSaveManager.Instance?.ServerUrl ?? "http://39.107.141.107:5132";
        bool serverOk = false;

        LoadingScreen.SetStage("正在连接服务器...");

        string testJson = "{\"username\":\"__ping__\",\"password\":\"__ping__\"}";
        using (var req = UnityEngine.Networking.UnityWebRequest.PostWwwForm($"{serverUrl}/api/auth/login", ""))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(testJson);
            req.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 5;
            yield return req.SendWebRequest();
            serverOk = req.result == UnityEngine.Networking.UnityWebRequest.Result.Success
                     || req.result == UnityEngine.Networking.UnityWebRequest.Result.ProtocolError;
        }

        if (!serverOk)
        {
            LoadingScreen.SetStage("服务器连接失败");
            LoadingScreen.SetProgress(0f);
            yield return new WaitForSeconds(1.5f);
            LoadingScreen.Hide();
            started = false;
            yield break;
        }
        LoadingScreen.SetStage("服务器连接成功");
        LoadingScreen.SetProgress(0.15f);
        yield return new WaitForSeconds(0.2f);

        // Step 2: 加载游戏配置 (15-40%)
        LoadingScreen.SetStage("正在加载游戏配置...");
        LoadingScreen.SetProgress(0.20f);
        if (GameConfigManager.Instance != null && !GameConfigManager.Instance.IsLoaded)
        {
            yield return StartCoroutine(GameConfigManager.Instance.LoadConfigCo());
        }
        LoadingScreen.SetStage("游戏配置加载完成");
        LoadingScreen.SetProgress(0.40f);
        yield return new WaitForSeconds(0.2f);

        // Step 3: 加载玩家存档 (40-70%)
        LoadingScreen.SetStage("正在加载存档数据...");
        LoadingScreen.SetProgress(0.50f);
        yield return new WaitForSeconds(0.3f);
        LoadingScreen.SetStage("存档加载完成");
        LoadingScreen.SetProgress(0.70f);
        yield return new WaitForSeconds(0.2f);

        // Step 4: 预加载资源 (70-90%)
        LoadingScreen.SetStage("正在预加载资源...");
        LoadingScreen.SetProgress(0.75f);
        // 异步预加载所有角色预览Sprite (通过GameConfigSO配置路径)
        yield return CharacterSpriteFactory.PreloadAllAsync();
        Resources.LoadAsync<Sprite>(ResourcePaths.MeleeGoblin);
        yield return null;
        LoadingScreen.SetStage("资源预加载完成");
        LoadingScreen.SetProgress(0.90f);
        yield return new WaitForSeconds(0.2f);

        // Step 5: 进入游戏 (90-100%)
        LoadingScreen.SetStage("正在进入游戏...");
        LoadingScreen.SetProgress(0.95f);
        GameManager.Instance.OnTitleStartNewGame();
        LoadingScreen.SetStage("欢迎来到卫冕战争!");
        LoadingScreen.SetProgress(1f);
        yield return new WaitForSeconds(0.8f);
        LoadingScreen.Hide();
    }

    private void OnDestroy()
    {
        if (panel != null) Destroy(panel);
        if (Instance == this) Instance = null;
    }
}
