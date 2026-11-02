using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace ArpgGameServer
{
    /// <summary>阶段08采样器 — 协程驱动: HUB/副本各30秒 GC 分配基线复测 (挂到 DontDestroyOnLoad)</summary>
    public class Phase08Sampler : MonoBehaviour
    {
        public static string Result = "未完成";
        public static bool Done;
        private StringBuilder _sb = new StringBuilder();
        private MethodInfo M(Type t, string n, int pc)
        {
            foreach (var m in t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                if (m.Name == n && m.GetParameters().Length == pc) return m;
            return null;
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            StartCoroutine(RunCo(_sb));
        }

        private IEnumerator RunCo(StringBuilder sb)
        {
            for (int i = 0; i < 75; i++) { if (GameObject.Find("Canvas") != null) break; yield return null; }
            var ca = UnityEngine.Object.FindObjectOfType<CloudAuthUI>();
            if (ca != null && ca.gameObject.activeInHierarchy) { M(typeof(CloudAuthUI), "HandleBackClick", 0)?.Invoke(ca, null); yield return new WaitForSeconds(0.6f); }
            var ts = UnityEngine.Object.FindObjectOfType<TitleScreen>();
            if (ts != null) M(typeof(TitleScreen), "OnStartGame", 0)?.Invoke(ts, null);
            for (int i = 0; i < 150; i++) { if (UnityEngine.Object.FindObjectOfType<CharacterSlotUI>() != null) break; yield return null; }
            var cs = UnityEngine.Object.FindObjectOfType<CharacterSlotUI>();
            if (cs != null)
            {
                M(typeof(CharacterSlotUI), "OnCardClicked", 1)?.Invoke(cs, new object[] { 0 });
                yield return new WaitForSeconds(0.6f);
                M(typeof(CharacterSlotUI), "OnStartGame", 0)?.Invoke(cs, null);
            }
            for (int i = 0; i < 225; i++) { if (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Hub) break; yield return null; }
            sb.Append("hub=" + (GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.Hub) + ";");
            for (int i = 0; i < 8; i++) DialogueData.MarkStartDialogueRead(i);
            yield return new WaitForSeconds(2.5f);

            // ---- HUB 采样30秒 (GC.GetTotalMemory 差值口径) ----
            long hubGc0 = GC.GetTotalMemory(false);
            int hubCol0 = GC.CollectionCount(0);
            long hubF = 0;
            float tEnd = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < tEnd)
            {
                hubF++;
                yield return null;
            }
            long hubGcDelta = GC.GetTotalMemory(false) - hubGc0;
            sb.Append($"HUB: frames={(int)hubF} gcManagedDelta={hubGcDelta / 1024.0:F1}KB gcCollects={GC.CollectionCount(0) - hubCol0};");

            // ---- 进副本 ----
            var gm = GameManager.Instance;
            M(typeof(GameManager), "StartDungeon", 1)?.Invoke(gm, new object[] { 0 });
            for (int i = 0; i < 125; i++) { if (gm.CurrentState == GameState.Dungeon) break; yield return null; }
            for (int i = 0; i < 100; i++) { var p = GameObject.Find("LoadingScreenPanel"); if (p == null || !p.activeInHierarchy) break; yield return null; }
            yield return new WaitForSeconds(1.5f);

            // ---- 副本采样30秒 ----
            long dgGc0 = GC.GetTotalMemory(false);
            int dgF = 0;
            float dgT = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < dgT)
            {
                dgF++;
                yield return null;
            }
            long dgDelta = GC.GetTotalMemory(false) - dgGc0;
            sb.Append($"DUNGEON: frames={dgF} gcManagedDelta={dgDelta / 1024.0:F1}KB;");

            // ---- 返回大厅 ----
            M(typeof(GameManager), "OnDungeonComplete", 0)?.Invoke(gm, null);
            yield return new WaitForSeconds(0.5f);
            sb.Append("returned=" + (gm.CurrentState == GameState.Hub));
            Result = sb.ToString();
            Done = true;
        }
    }
}
