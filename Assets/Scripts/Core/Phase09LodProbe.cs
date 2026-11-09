using System.Collections;
using UnityEngine;

/// <summary>阶段09 LOD 行为验证宿主 — 三档距离怪物驱动tick验证</summary>
public class Phase09LodProbe : MonoBehaviour
{
    public static string Result = "未完成";
    public static bool Done;

    private IEnumerator Start()
    {
        var sb = new System.Text.StringBuilder();
        int pass = 0, fail = 0;

        void Check(string name, bool ok)
        {
            Debug.Log((ok ? "[PASS] " : "[FAIL] ") + name);
            if (ok) pass++; else fail++;
        }

        var goA = new GameObject("LodA");
        var a = goA.AddComponent<EnemyController>();
        goA.transform.position = new Vector2(5f, 0f);
        var brainA = goA.AddComponent<EnemyBrainController>();
        brainA.Initialize(a, goA.AddComponent<Rigidbody2D>(), goA.AddComponent<SpriteRenderer>());
        brainA.SetPlayerTransform(GameManager.Instance.Player.transform);

        var goB = new GameObject("LodB");
        var b = goB.AddComponent<EnemyController>();
        goB.transform.position = new Vector2(25f, 0f);
        var brainB = goB.AddComponent<EnemyBrainController>();
        brainB.Initialize(b, goB.AddComponent<Rigidbody2D>(), goB.AddComponent<SpriteRenderer>());
        brainB.SetPlayerTransform(GameManager.Instance.Player.transform);

        var goC = new GameObject("LodC");
        var c = goC.AddComponent<EnemyController>();
        goC.transform.position = new Vector2(60f, 0f);
        var brainC = goC.AddComponent<EnemyBrainController>();
        brainC.Initialize(c, goC.AddComponent<Rigidbody2D>(), goC.AddComponent<SpriteRenderer>());
        brainC.SetPlayerTransform(GameManager.Instance.Player.transform);

        var posA = goA.transform.position;
        var posB = goB.transform.position;
        var posC = goC.transform.position;

        for (int i = 0; i < 20; i++)
        {
            brainA.TickUpdate();
            brainB.TickUpdate();
            brainC.TickUpdate();
            yield return new WaitForSeconds(0.05f);
        }

        float movedA = Vector2.Distance(goA.transform.position, posA);
        float movedB = Vector2.Distance(goB.transform.position, posB);
        float movedC = Vector2.Distance(goC.transform.position, posC);

        Check("活跃档(5m): 追击移动", movedA > 0.3f);
        Check("远距离档(60m): LOD暂停静止", movedC < 0.5f);

        Result = $"movedA={movedA:F2} movedB={movedB:F2} movedC={movedC:F2} pass={pass} fail={fail}";
        Done = true;
        Debug.Log("[RESULT] " + Result);
    }
}
