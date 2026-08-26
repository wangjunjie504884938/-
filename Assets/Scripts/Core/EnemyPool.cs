using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敌人对象池 — 替代Destroy，预创建复用敌人实例
/// </summary>
public static class EnemyPool
{
    private static readonly Dictionary<int, Queue<GameObject>> _pools = new Dictionary<int, Queue<GameObject>>();
    private const int PreCreateCount = 20;

    /// <summary>初始化预创建池</summary>
    public static void Initialize()
    {
        // 预创建占位GameObject，实际敌人在Get时配置
        for (int i = 0; i < PreCreateCount; i++)
        {
            var obj = new GameObject($"EnemyPool_{i}");
            obj.SetActive(false);
            Object.DontDestroyOnLoad(obj);
            ReturnRaw(obj);
        }
    }

    /// <summary>获取一个可复用的GameObject</summary>
    public static GameObject Get()
    {
        if (_pools.TryGetValue(0, out var queue) && queue.Count > 0)
        {
            var obj = queue.Dequeue();
            obj.SetActive(true);
            return obj;
        }
        return new GameObject("Enemy");
    }

    /// <summary>回收到池（延迟销毁改为回收）</summary>
    public static void Return(GameObject obj, float delay = 0f)
    {
        if (obj == null) return;
        if (delay > 0f)
        {
            InstanceHelper.Instance?.StartCoroutine(ReturnAfterDelay(obj, delay));
        }
        else
        {
            ReturnRaw(obj);
        }
    }

    private static void ReturnRaw(GameObject obj)
    {
        // 先清理EnemyHealthBar的barObj（根级对象，不随敌人销毁）
        var healthBar = obj.GetComponent<EnemyHealthBar>();
        if (healthBar != null) healthBar.Cleanup();

        obj.SetActive(false);
        obj.transform.SetParent(null);
        // 清理所有子物体
        for (int i = obj.transform.childCount - 1; i >= 0; i--)
            Object.Destroy(obj.transform.GetChild(i).gameObject);
        // 移除动态添加的组件（保留Transform）
        var comps = obj.GetComponents<Component>();
        for (int i = comps.Length - 1; i >= 0; i--)
        {
            if (comps[i] is Transform) continue;
            Object.Destroy(comps[i]);
        }
        if (!_pools.ContainsKey(0)) _pools[0] = new Queue<GameObject>();
        _pools[0].Enqueue(obj);
    }

    private static System.Collections.IEnumerator ReturnAfterDelay(GameObject obj, float delay)
    {
        yield return new WaitForSeconds(delay);
        ReturnRaw(obj);
    }

    /// <summary>清空所有池</summary>
    public static void ClearAll()
    {
        foreach (var kvp in _pools)
        {
            while (kvp.Value.Count > 0)
            {
                var obj = kvp.Value.Dequeue();
                if (obj != null) Object.Destroy(obj);
            }
        }
        _pools.Clear();
    }

    /// <summary>辅助单例（用于启动协程）</summary>
    private class InstanceHelper : MonoBehaviour
    {
        public static InstanceHelper Instance;
        [RuntimeInitializeOnLoadMethod]
        static void Init() { var go = new GameObject("[EnemyPool]"); Instance = go.AddComponent<InstanceHelper>(); Object.DontDestroyOnLoad(go); }
    }
}
