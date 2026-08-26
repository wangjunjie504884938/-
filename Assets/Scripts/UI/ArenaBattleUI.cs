using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public static class ArenaBattleUI
{
    public static IEnumerator StartBattle(string myName, int myGS, int myClass,
        AsyncPvpUI.GhostData enemy, System.Action<bool> onResult)
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.Player == null) { onResult?.Invoke(false); yield break; }

        Font font = GameManager.GetUIFont();
        int myMaxHp = 500 + myGS / 10, myAtk = Mathf.Max(50, myGS / 20), myDef = Mathf.Max(10, myGS / 50);
        int eMaxHp = 500 + enemy.gearScore / 10, eAtk = Mathf.Max(50, enemy.gearScore / 20), eDef = Mathf.Max(10, enemy.gearScore / 50);
        int myHp = myMaxHp, eHp = eMaxHp;
        bool myTurn = true, battleOver = false, playerWon = false;
        int round = 1;
        string[] cn = { "战士", "法师", "牧师" };

        if (GameUI.Instance != null) GameUI.Instance.SetHUDVisible(false);
        if (CombatDirector.Instance != null) CombatDirector.Instance.StopCombat();
        SceneRegistry.ClearAll();
        VFXPool.ClearAll();

        // 延迟销毁旧地图，确保渲染线程完成当前帧的GPU资源释放
        if (DungeonVisuals.Instance != null)
        { Object.Destroy(DungeonVisuals.Instance.gameObject); DungeonVisuals.Instance = null; }
        var dvGO = new GameObject("ArenaDungeon");
        dvGO.AddComponent<DungeonVisuals>().BuildMap(0);
        yield return null;

        // 删除障碍物和装饰物，只保留地板和墙壁
        if (DungeonVisuals.Instance != null)
        {
            var obs = DungeonVisuals.Instance.transform.Find("Obstacles");
            if (obs != null) Object.Destroy(obs.gameObject);
            var deco = DungeonVisuals.Instance.transform.Find("Decorations");
            if (deco != null) Object.Destroy(deco.gameObject);
        }

        Camera.main.transform.position = new Vector3(0, 0, -10f);
        Camera.main.orthographic = true;
        Camera.main.orthographicSize = 6f;
        Camera.main.backgroundColor = new Color(0.08f, 0.06f, 0.12f);
        if (CameraFollow.Instance != null) CameraFollow.Instance.enabled = false;

        gm.Player.gameObject.SetActive(true);
        gm.Player.transform.position = new Vector3(-2.5f, 0f, 0f);
        gm.Player.Stats.Hp = gm.Player.Stats.TotalMaxHp;
        gm.Player.InputCtrl.enabled = false;
        // 禁用玩家Rigidbody防止被推
        var pRb = gm.Player.GetComponent<Rigidbody2D>();
        if (pRb != null) pRb.simulated = false;
        gm.Player.CharSprite.SetFacing(false);

        var eGO = new GameObject("ArenaEnemy");
        eGO.transform.position = new Vector3(2.5f, 0f, 0f);
        var eCS = eGO.AddComponent<CharacterSprite>();
        eCS.Setup(eGO.transform, (HeroClass)enemy.classType);
        eCS.SetFacing(true);

        // UI Canvas
        var uiGO = new GameObject("ArenaUI");
        var cc = uiGO.AddComponent<Canvas>();
        cc.renderMode = RenderMode.ScreenSpaceOverlay; cc.sortingOrder = 500;
        uiGO.AddComponent<CanvasScaler>(); uiGO.AddComponent<GraphicRaycaster>();

        var titleT = AddText(uiGO.transform, "T", 0.3f, 0.92f, 0.7f, 0.99f, "", 22, new Color(0.55f, 0.3f, 0.75f), font);

        // 左侧面板 — 名字+HP在同一面板内对齐
        var lp = AddPanel(uiGO.transform, "LP", 0.01f, 0.78f, 0.35f, 0.98f, new Color(0.04f, 0.10f, 0.06f, 0.95f));
        var lnT = AddText(lp.transform, "LN", 0.05f, 0.65f, 0.95f, 0.92f, $"[{cn[Mathf.Clamp(myClass,0,2)]}] {myName}", 16, new Color(0.3f,1f,0.3f), font);
        var lhBg = AddPanel(lp.transform, "LHB", 0.05f, 0.35f, 0.95f, 0.55f, new Color(0.08f, 0.03f, 0.03f, 0.9f));
        var lhFill = AddPanel(lhBg.transform, "LHF", 0, 0, 1, 1, new Color(0.2f, 0.8f, 0.2f, 1f));
        var lhTxt = AddText(lp.transform, "LHT", 0.05f, 0.10f, 0.95f, 0.30f, "", 14, Color.white, font);

        // 右侧面板 — 名字+HP在同一面板内对齐
        var rp = AddPanel(uiGO.transform, "RP", 0.65f, 0.78f, 0.99f, 0.98f, new Color(0.10f, 0.04f, 0.06f, 0.95f));
        var rnT = AddText(rp.transform, "RN", 0.05f, 0.65f, 0.95f, 0.92f, $"[{cn[Mathf.Clamp(enemy.classType,0,2)]}] {enemy.playerName}", 16, new Color(1f,0.4f,0.4f), font);
        var rhBg = AddPanel(rp.transform, "RHB", 0.05f, 0.35f, 0.95f, 0.55f, new Color(0.08f, 0.03f, 0.03f, 0.9f));
        var rhFill = AddPanel(rhBg.transform, "RHF", 0, 0, 1, 1, new Color(0.8f, 0.2f, 0.2f, 1f));
        var rhTxt = AddText(rp.transform, "RHT", 0.05f, 0.10f, 0.95f, 0.30f, "", 14, Color.white, font);

        var logT = AddText(uiGO.transform, "Log", 0.1f, 0.02f, 0.9f, 0.08f, "", 18, Color.white, font);
        logT.supportRichText = true;
        var dmgT = AddText(uiGO.transform, "Dmg", 0.35f, 0.40f, 0.65f, 0.55f, "", 48, Color.yellow, font);
        dmgT.supportRichText = true;
        var vsT = AddText(uiGO.transform, "VS", 0.45f, 0.55f, 0.55f, 0.70f, "VS", 40, new Color(0.55f, 0.3f, 0.75f, 0.7f), font);

        void UpdateHp()
        {
            float mp = (float)myHp / myMaxHp, ep = (float)eHp / eMaxHp;
            lhFill.GetComponent<RectTransform>().anchorMax = new Vector2(mp, 1);
            rhFill.GetComponent<RectTransform>().anchorMax = new Vector2(ep, 1);
            lhTxt.text = $"{myHp} / {myMaxHp}"; rhTxt.text = $"{eHp} / {eMaxHp}";
            lhFill.GetComponent<Image>().color = mp > 0.5f ? new Color(0.2f,0.8f,0.2f,1f) : mp > 0.25f ? new Color(0.8f,0.6f,0.2f,1f) : new Color(0.8f,0.2f,0.2f,1f);
            titleT.text = $"竞技场 — 第{round}回合";
        }
        UpdateHp();

        while (!battleOver)
        {
            logT.text = myTurn ? $"<color=#44FF44>▶ {myName} 的回合</color>" : $"<color=#FF4444>▶ {enemy.playerName} 的回合</color>";
            yield return new WaitForSeconds(0.5f);

            if (myTurn)
            {
                Vector3 bp = gm.Player.transform.position;
                gm.Player.transform.position = bp + Vector3.right * 3f;
                gm.Player.CharSprite.TriggerAttack(Vector2.right);
                bool crit = Random.value < 0.2f;
                int dmg = Mathf.Max(1, myAtk - eDef / 2 + Random.Range(-10, 10));
                if (crit) dmg = (int)(dmg * 1.5f);
                eHp = Mathf.Max(0, eHp - dmg);
                dmgT.text = crit ? $"<color=#FF4444>-{dmg}!</color>" : $"<color=#FFDD44>-{dmg}</color>";
                eCS.TriggerHurt(Vector2.left);
                CameraFollow.Shake(0.12f, 0.06f);
                UpdateHp();
                // 等待攻击动画播放，每帧驱动角色动画更新
                for (float t = 0; t < 0.6f; t += Time.deltaTime)
                {
                    gm.Player.CharSprite.UpdateAnimation(0);
                    eCS.UpdateAnimation(0);
                    yield return null;
                }
                gm.Player.transform.position = bp;
                dmgT.text = "";
            }
            else
            {
                Vector3 bp = eGO.transform.position;
                eGO.transform.position = bp + Vector3.left * 3f;
                eCS.TriggerAttack(Vector2.left);
                bool crit = Random.value < 0.2f;
                int dmg = Mathf.Max(1, eAtk - myDef / 2 + Random.Range(-10, 10));
                if (crit) dmg = (int)(dmg * 1.5f);
                myHp = Mathf.Max(0, myHp - dmg);
                dmgT.text = crit ? $"<color=#FF4444>-{dmg}!</color>" : $"<color=#FF8844>-{dmg}</color>";
                gm.Player.CharSprite.TriggerHurt(Vector2.right);
                CameraFollow.Shake(0.12f, 0.06f);
                UpdateHp();
                // 等待攻击动画播放，每帧驱动角色动画更新
                for (float t = 0; t < 0.6f; t += Time.deltaTime)
                {
                    gm.Player.CharSprite.UpdateAnimation(0);
                    eCS.UpdateAnimation(0);
                    yield return null;
                }
                eGO.transform.position = bp;
                dmgT.text = "";
            }

            if (eHp <= 0) { battleOver = true; playerWon = true; eGO.SetActive(false); CameraFollow.Shake(0.3f, 0.15f); }
            else if (myHp <= 0) { battleOver = true; playerWon = false; gm.Player.gameObject.SetActive(false); CameraFollow.Shake(0.3f, 0.15f); }
            else { myTurn = !myTurn; round++; UpdateHp(); yield return new WaitForSeconds(0.4f); }
        }

        logT.text = playerWon ? $"<color=#44FF44>{myName} 胜利!</color>" : $"<color=#FF4444>{enemy.playerName} 胜利!</color>";
        vsT.text = playerWon ? "" : "";
        vsT.color = playerWon ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);
        vsT.fontSize = 64;
        yield return new WaitForSeconds(2.5f);

        Object.Destroy(uiGO);
        Object.Destroy(eGO);
        if (DungeonVisuals.Instance != null) { Object.Destroy(DungeonVisuals.Instance.gameObject); DungeonVisuals.Instance = null; }
        gm.Player.gameObject.SetActive(true);
        gm.Player.InputCtrl.enabled = true;
        var pRb2 = gm.Player.GetComponent<Rigidbody2D>();
        if (pRb2 != null) pRb2.simulated = true;
        gm.Player.transform.position = Vector3.zero;
        gm.Player.Stats.Hp = gm.Player.Stats.TotalMaxHp;
        if (CameraFollow.Instance != null) CameraFollow.Instance.enabled = true;
        if (GameUI.Instance != null) GameUI.Instance.ShowHubMode(true);
        onResult?.Invoke(playerWon);
    }

    private static GameObject AddPanel(Transform parent, string name, float x1, float y1, float x2, float y2, Color c)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(x1, y1); r.anchorMax = new Vector2(x2, y2); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        go.AddComponent<Image>().color = c;
        return go;
    }

    private static Text AddText(Transform parent, string name, float x1, float y1, float x2, float y2, string text, int size, Color c, Font f)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false);
        var r = go.AddComponent<RectTransform>();
        r.anchorMin = new Vector2(x1, y1); r.anchorMax = new Vector2(x2, y2); r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.text = text; t.alignment = TextAnchor.MiddleCenter; t.fontSize = size; t.color = c; t.font = f; t.raycastTarget = false;
        return t;
    }
}
