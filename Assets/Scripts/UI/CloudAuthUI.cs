using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 独立全屏登录/注册页面 — Dark Souls 风格
/// 游戏启动第一个页面, 必须登录/注册后才能进入游戏
/// </summary>
public class CloudAuthUI : MonoBehaviour
{
    private GameObject _page;
    private InputField _usernameInput;
    private InputField _passwordInput;
    private Text _statusText;
    private Button _loginBtn;
    private Button _regBtn;
    private bool _busy;
    private Font _font;
    private Transform _parent;

    private const float PanelW = 460f;
    private const float PanelH = 520f;

    /// <summary>页面是否正在显示</summary>
    public bool IsVisible => _page != null && _page.activeSelf;

    /// <summary>关闭页面 (供外部调用)</summary>
    public void ClosePanel()
    {
        if (_page != null)
        {
            Destroy(_page);
            _page = null;
        }
    }

    /// <summary>从标题界面打开独立登录页 (隐藏标题, 显示独立页)</summary>
    public void ShowAuthPanelFromTitle(object[] args)
    {
        Transform parent = (Transform)args[0];
        Font font = (Font)args[1];
        ClosePanel();
        ShowAuthPage(parent, font);
    }

    // ========================================================
    //  独立全屏页面
    // ========================================================

    public void ShowAuthPage(Transform parent, Font font)
    {
        _font = font;
        _parent = parent;
        _busy = false;
        bool isLoggedIn = CloudSaveManager.Instance != null && CloudSaveManager.Instance.IsLoggedIn;

        // === 全屏页面容器 ===
        _page = new GameObject("CloudAuthPage");
        _page.transform.SetParent(parent, false);
        RectTransform pageRect = _page.AddComponent<RectTransform>();
        pageRect.anchorMin = Vector2.zero;
        pageRect.anchorMax = Vector2.one;
        pageRect.offsetMin = Vector2.zero;
        pageRect.offsetMax = Vector2.zero;

        // 全屏深色背景
        Image bg = _page.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.01f, 0.05f, 0.98f);

        // 背景雾气
        CreateFog(_page.transform, new Color(0.10f, 0.06f, 0.16f, 0.20f), 1400f, 0.25f);
        CreateFog(_page.transform, new Color(0.08f, 0.04f, 0.14f, 0.15f), 1200f, -0.18f);

        // === 中央金色边框面板 ===
        GameObject box = CreateChild(_page.transform, "AuthBox");
        RectTransform boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = new Vector2(PanelW, isLoggedIn ? 380f : PanelH);

        // 金色外框
        Image boxBorder = box.AddComponent<Image>();
        boxBorder.color = new Color(0.45f, 0.32f, 0.05f, 0.9f);

        // 内层 (深色背景)
        GameObject inner = CreateChild(box.transform, "Inner");
        RectTransform innerRect = inner.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2, 2);
        innerRect.offsetMax = new Vector2(-2, -2);
        Image innerImg = inner.AddComponent<Image>();
        innerImg.color = new Color(0.04f, 0.03f, 0.07f, 0.98f);

        // === 标题 ===
        CreateLabel(inner.transform, "Title", "卫冕战争", font, 36,
            new Color(0.85f, 0.65f, 0.15f), new Vector2(0, 130), new Vector2(400, 50));

        CreateLabel(inner.transform, "Subtitle", "云端存档系统", font, 22,
            new Color(0.6f, 0.5f, 0.3f), new Vector2(0, 90), new Vector2(400, 30));

        // 分隔线
        CreateDivider(inner.transform, new Vector2(0, 60), new Vector2(360, 2),
            new Color(0.45f, 0.32f, 0.05f, 0.6f));

        if (isLoggedIn)
        {
            // === 已登录: 显示用户信息 + 进入游戏 + 退出登录 ===
            CreateLabel(inner.transform, "UserLabel", "当前用户", font, 16,
                new Color(0.6f, 0.55f, 0.4f), new Vector2(0, 20), new Vector2(360, 24));
            CreateLabel(inner.transform, "UserName", CloudSaveManager.Instance.CurrentUsername, font, 24,
                new Color(0.4f, 0.9f, 0.4f), new Vector2(0, -15), new Vector2(360, 36));

            _statusText = CreateLabel(inner.transform, "Status", "", font, 16,
                new Color(1f, 0.85f, 0.3f), new Vector2(0, -60), new Vector2(400, 24));

            // 进入游戏按钮
            GameObject enterBtn = CreateButton(inner.transform, "EnterBtn", "进入游戏 →", font,
                new Vector2(0, -110), new Vector2(220, 52),
                new Color(0.10f, 0.18f, 0.08f, 0.95f),
                new Color(0.3f, 0.55f, 0.1f, 0.8f));
            enterBtn.GetComponent<Button>().onClick.AddListener(HandleEnterClick);

            // 退出登录按钮
            GameObject logoutBtn = CreateButton(inner.transform, "LogoutBtn", "退出登录", font,
                new Vector2(0, -170), new Vector2(180, 44),
                new Color(0.18f, 0.06f, 0.08f, 0.9f),
                new Color(0.5f, 0.15f, 0.1f, 0.6f));
            logoutBtn.GetComponent<Button>().onClick.AddListener(HandleLogoutClick);
        }
        else
        {
            // === 未登录: 显示登录/注册表单 ===
            // === 用户名输入区 ===
            CreateLabel(inner.transform, "UserLabel", "用户名", font, 16,
                new Color(0.6f, 0.55f, 0.4f), new Vector2(0, 20), new Vector2(360, 24));
            _usernameInput = CreateInputField(inner.transform, "UserInput", font,
                new Vector2(0, -20), new Vector2(360, 44), "请输入用户名 (2-20字)");

            // === 密码输入区 ===
            CreateLabel(inner.transform, "PassLabel", "密码 (至少6位)", font, 16,
                new Color(0.6f, 0.55f, 0.4f), new Vector2(0, -80), new Vector2(360, 24));
            _passwordInput = CreateInputField(inner.transform, "PassInput", font,
                new Vector2(0, -120), new Vector2(360, 44), "请输入密码");
            _passwordInput.inputType = InputField.InputType.Password;

            // === 状态提示 ===
            _statusText = CreateLabel(inner.transform, "Status", "请登录或注册后进入游戏", font, 16,
                new Color(0.5f, 0.5f, 0.6f, 0.8f), new Vector2(0, -170), new Vector2(400, 24));

            // === 登录 + 注册按钮 (同一行) ===
            _loginBtn = CreateButton(inner.transform, "LoginBtn", "登 录", font,
                new Vector2(-100, -220), new Vector2(180, 50),
                new Color(0.15f, 0.20f, 0.08f, 1f),
                new Color(0.55f, 0.40f, 0.08f, 0.8f)).GetComponent<Button>();
            _loginBtn.onClick.AddListener(HandleLoginClick);

            _regBtn = CreateButton(inner.transform, "RegBtn", "注 册", font,
                new Vector2(100, -220), new Vector2(180, 50),
                new Color(0.10f, 0.12f, 0.18f, 1f),
                new Color(0.40f, 0.45f, 0.55f, 0.7f)).GetComponent<Button>();
            _regBtn.onClick.AddListener(HandleRegisterClick);
        }
    }

    // ========================================================
    //  已登录面板 (全屏独立页面)
    // ========================================================

    public void ShowLogoutPage(Transform parent, Font font)
    {
        _font = font;
        _parent = parent;

        _page = new GameObject("CloudAuthPage");
        _page.transform.SetParent(parent, false);
        RectTransform pageRect = _page.AddComponent<RectTransform>();
        pageRect.anchorMin = Vector2.zero;
        pageRect.anchorMax = Vector2.one;
        pageRect.offsetMin = Vector2.zero;
        pageRect.offsetMax = Vector2.zero;
        Image bg = _page.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.01f, 0.05f, 0.98f);

        CreateFog(_page.transform, new Color(0.10f, 0.06f, 0.16f, 0.20f), 1400f, 0.25f);

        // 中央面板
        GameObject box = CreateChild(_page.transform, "AuthBox");
        RectTransform boxRect = box.AddComponent<RectTransform>();
        boxRect.anchorMin = new Vector2(0.5f, 0.5f);
        boxRect.anchorMax = new Vector2(0.5f, 0.5f);
        boxRect.pivot = new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = Vector2.zero;
        boxRect.sizeDelta = new Vector2(460, 360);
        Image boxBorder = box.AddComponent<Image>();
        boxBorder.color = new Color(0.45f, 0.32f, 0.05f, 0.9f);

        GameObject inner = CreateChild(box.transform, "Inner");
        RectTransform innerRect = inner.AddComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(2, 2);
        innerRect.offsetMax = new Vector2(-2, -2);
        Image innerImg = inner.AddComponent<Image>();
        innerImg.color = new Color(0.04f, 0.03f, 0.07f, 0.98f);

        CreateLabel(inner.transform, "Title", "云端存档", font, 28,
            new Color(0.85f, 0.65f, 0.15f), new Vector2(0, 130), new Vector2(400, 40));

        string user = CloudSaveManager.Instance?.CurrentUsername ?? "";
        CreateLabel(inner.transform, "User", $"当前用户: {user}", font, 22,
            new Color(0.7f, 0.75f, 0.6f), new Vector2(0, 70), new Vector2(400, 30));

        // 上传存档
        _statusText = CreateLabel(inner.transform, "Status", "", font, 16,
            new Color(1f, 0.85f, 0.3f), new Vector2(0, -100), new Vector2(400, 24));

        GameObject uploadBtn = CreateButton(inner.transform, "UploadBtn", "上传存档", font,
            new Vector2(0, 10), new Vector2(200, 50),
            new Color(0.12f, 0.18f, 0.08f, 1f),
            new Color(0.45f, 0.35f, 0.08f, 0.7f));
        uploadBtn.GetComponent<Button>().onClick.AddListener(HandleUploadClick);

        // 退出登录
        GameObject logoutBtn = CreateButton(inner.transform, "LogoutBtn", "退出登录", font,
            new Vector2(0, -50), new Vector2(200, 50),
            new Color(0.18f, 0.06f, 0.08f, 1f),
            new Color(0.5f, 0.15f, 0.1f, 0.6f));
        logoutBtn.GetComponent<Button>().onClick.AddListener(HandleLogoutClick);

        // 返回
        GameObject backBtn = CreateButton(inner.transform, "BackBtn", "← 返回标题", font,
            new Vector2(0, -130), new Vector2(200, 40),
            new Color(0.08f, 0.06f, 0.10f, 0.9f),
            new Color(0.3f, 0.25f, 0.1f, 0.6f));
        backBtn.GetComponent<Button>().onClick.AddListener(HandleBackClick);

        TitleScreen.Instance?.Hide();
    }

    // ========================================================
    //  按钮事件
    // ========================================================

    private void HandleLoginClick()
    {
        OnLoginClicked(_font);
    }

    private void HandleRegisterClick()
    {
        OnRegisterClicked(_font);
    }

    private void HandleEnterClick()
    {
        ClosePanel();
        GameManager.Instance?.ShowTitleScreen();
    }

    private void HandleLogoutClick()
    {
        CloudSaveManager.Instance?.Logout();
        ClosePanel();
        ShowAuthPage(_parent, _font);
    }

    private void HandleUploadClick()
    {
        if (_statusText) _statusText.text = "上传中...";
        CloudSaveManager.Instance?.UploadSave(CloudSaveManager.Instance.ActiveSlot,
            () => { if (_statusText) _statusText.text = "上传成功!"; },
            (err) => { if (_statusText) _statusText.text = $"失败: {err}"; });
    }

    private void HandleBackClick()
    {
        ClosePanel();
        GameManager.Instance?.ShowTitleScreen();
    }

    private void OnLoginClicked(Font font)
    {
        if (_busy) return;
        string user = _usernameInput.text.Trim();
        string pass = _passwordInput.text;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            _statusText.text = "请填写用户名和密码";
            return;
        }
        if (user.Length < 2 || user.Length > 20)
        {
            _statusText.text = "用户名长度需2-20个字符";
            return;
        }

        _busy = true;
        SetButtonsInteractive(false);
        _statusText.text = "登录中...";
        _statusText.color = new Color(1f, 0.85f, 0.3f);

        CloudSaveManager.Instance.Login(user, pass,
            onSuccess: () =>
            {
                _statusText.text = "登录成功! 正在下载存档...";
                _statusText.color = new Color(0.3f, 1f, 0.3f);
                if (_passwordInput != null) _passwordInput.text = "";

                CloudSaveManager.Instance.DownloadSave(CloudSaveManager.Instance.ActiveSlot, success =>
                {
                    _statusText.text = success ? "存档同步成功!" : "登录成功 (无云端存档)";
                    _statusText.color = new Color(0.3f, 1f, 0.3f);
                    // 登录后立即拉取好友列表（更新在线状态）
                    FriendUI.Instance?.StartCoroutine(FetchFriendsAfterLogin());
                    StartCoroutine(DelayBackToTitle(1.5f));
                });
            },
            onFail: (err) =>
            {
                _busy = false;
                SetButtonsInteractive(true);
                _statusText.text = err;
                _statusText.color = new Color(1f, 0.3f, 0.3f);
            });
    }

    private void OnRegisterClicked(Font font)
    {
        if (_busy) return;
        string user = _usernameInput.text.Trim();
        string pass = _passwordInput.text;

        if (string.IsNullOrEmpty(user) || pass.Length < 6)
        {
            _statusText.text = "用户名不能为空, 密码至少6位";
            return;
        }
        if (user.Length < 2 || user.Length > 20)
        {
            _statusText.text = "用户名长度需2-20个字符";
            return;
        }

        _busy = true;
        SetButtonsInteractive(false);
        _statusText.text = "注册中...";
        _statusText.color = new Color(1f, 0.85f, 0.3f);

        CloudSaveManager.Instance.Register(user, pass,
            onSuccess: () =>
            {
                _statusText.text = "注册成功! 已自动登录";
                _statusText.color = new Color(0.3f, 1f, 0.3f);
                if (_passwordInput != null) _passwordInput.text = "";
                StartCoroutine(DelayBackToTitle(1.5f));
            },
            onFail: (err) =>
            {
                _busy = false;
                SetButtonsInteractive(true);
                _statusText.text = err;
                _statusText.color = new Color(1f, 0.3f, 0.3f);
            });
    }

    private void SetButtonsInteractive(bool interactive)
    {
        if (_loginBtn != null) _loginBtn.interactable = interactive;
        if (_regBtn != null) _regBtn.interactable = interactive;
    }

    private IEnumerator DelayBackToTitle(float delay)
    {
        yield return new WaitForSeconds(delay);
        _busy = false;
        ClosePanel();
        // 登录/注册成功后显示服务器选择
        GameManager.Instance?.ShowServerSelect();
    }

    private static IEnumerator FetchFriendsAfterLogin()
    {
        string token = PlayerPrefs.GetString("ARPG_AuthToken", "");
        if (string.IsNullOrEmpty(token)) yield break;
        string url = "http://39.107.141.107:5132/api/friend/list";
        using (var req = UnityEngine.Networking.UnityWebRequest.Get(url))
        {
            req.SetRequestHeader("Authorization", "Bearer " + token);
            req.timeout = 5;
            yield return req.SendWebRequest();
            if (req.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
            {
                try
                {
                    var resp = JsonUtility.FromJson<ServerFriendListResp>(req.downloadHandler.text);
                    if (resp?.friends != null)
                    {
                        GameLog.Log($"[Friend] 登录后拉取好友列表: {resp.friends.Length}人");
                    }
                }
                catch { }
            }
        }
    }

    [System.Serializable]
    private class ServerFriendListResp { public ServerFriendItem[] friends; public int count; public int max; }
    [System.Serializable]
    private class ServerFriendItem { public int friendId; public string username; public int level; public int classType; public bool online; public string remark; }

    // ========================================================
    //  UI 工具方法
    // ========================================================

    private void CreateFog(Transform parent, Color color, float width, float speed)
    {
        GameObject fog = new GameObject("Fog");
        fog.transform.SetParent(parent, false);
        RectTransform fogRect = fog.AddComponent<RectTransform>();
        fogRect.anchorMin = Vector2.zero;
        fogRect.anchorMax = new Vector2(1, 1);
        fogRect.offsetMin = Vector2.zero;
        fogRect.offsetMax = Vector2.zero;
        Image fogImg = fog.AddComponent<Image>();
        fogImg.color = color;
        fogImg.raycastTarget = false;
    }

    private void CreateDivider(Transform parent, Vector2 pos, Vector2 size, Color color)
    {
        GameObject obj = CreateChild(parent, "Divider");
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Image img = obj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
    }

    private GameObject CreateChild(Transform parent, string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private Text CreateLabel(Transform parent, string name, string text, Font font,
        int fontSize, Color color, Vector2 pos, Vector2 size)
    {
        GameObject obj = CreateChild(parent, name);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        Text t = obj.AddComponent<Text>();
        t.text = text;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = fontSize;
        t.color = color;
        t.font = font;
        t.raycastTarget = false;
        return t;
    }

    private InputField CreateInputField(Transform parent, string name, Font font,
        Vector2 pos, Vector2 size, string placeholder)
    {
        GameObject obj = CreateChild(parent, name);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        // 边框
        GameObject border = CreateChild(obj.transform, "Border");
        RectTransform borderRect = border.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero; borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-1, -1); borderRect.offsetMax = new Vector2(1, 1);
        Image borderImg = border.AddComponent<Image>();
        borderImg.color = new Color(0.35f, 0.28f, 0.08f, 0.5f);
        borderImg.raycastTarget = false;

        // 背景
        Image bg = obj.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.07f, 0.10f, 1f);

        InputField input = obj.AddComponent<InputField>();

        // Placeholder
        GameObject phObj = CreateChild(obj.transform, "Placeholder");
        RectTransform phRect = phObj.AddComponent<RectTransform>();
        phRect.anchorMin = Vector2.zero; phRect.anchorMax = Vector2.one;
        phRect.offsetMin = new Vector2(12, 0); phRect.offsetMax = new Vector2(-12, 0);
        Text phText = phObj.AddComponent<Text>();
        phText.text = placeholder;
        phText.font = font;
        phText.fontSize = 18;
        phText.color = new Color(0.4f, 0.4f, 0.45f);
        phText.alignment = TextAnchor.MiddleLeft;
        phText.raycastTarget = false;

        // Text
        GameObject textObj = CreateChild(obj.transform, "Text");
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(12, 0); textRect.offsetMax = new Vector2(-12, 0);
        Text inputText = textObj.AddComponent<Text>();
        inputText.font = font;
        inputText.fontSize = 18;
        inputText.color = new Color(0.9f, 0.88f, 0.8f);
        inputText.alignment = TextAnchor.MiddleLeft;
        inputText.raycastTarget = false;

        input.placeholder = phText;
        input.textComponent = inputText;
        return input;
    }

    private GameObject CreateButton(Transform parent, string name, string label, Font font,
        Vector2 pos, Vector2 size, Color bgColor, Color borderColor)
    {
        GameObject obj = CreateChild(parent, name);
        RectTransform rect = obj.AddComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;

        // 边框
        GameObject borderObj = CreateChild(obj.transform, "Border");
        RectTransform borderRect = borderObj.AddComponent<RectTransform>();
        borderRect.anchorMin = Vector2.zero; borderRect.anchorMax = Vector2.one;
        borderRect.offsetMin = new Vector2(-1, -1); borderRect.offsetMax = new Vector2(1, 1);
        Image borderImg = borderObj.AddComponent<Image>();
        borderImg.color = borderColor;
        borderImg.raycastTarget = false;

        Image img = obj.AddComponent<Image>();
        img.color = bgColor;
        Button btn = obj.AddComponent<Button>();
        btn.targetGraphic = img;

        // 文字
        GameObject lblObj = CreateChild(obj.transform, "Label");
        RectTransform lblRect = lblObj.AddComponent<RectTransform>();
        lblRect.anchorMin = Vector2.zero; lblRect.anchorMax = Vector2.one;
        lblRect.offsetMin = Vector2.zero; lblRect.offsetMax = Vector2.zero;
        Text lblText = lblObj.AddComponent<Text>();
        lblText.text = label;
        lblText.alignment = TextAnchor.MiddleCenter;
        lblText.fontSize = 20;
        lblText.color = new Color(0.9f, 0.82f, 0.45f);
        lblText.font = font;
        lblText.raycastTarget = false;

        return obj;
    }
}
