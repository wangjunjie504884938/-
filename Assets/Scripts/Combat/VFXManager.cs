using UnityEngine;

/// <summary>
/// 独立VFX管理器 — 运行所有VFX协程，不随敌人回收而销毁
/// 解决VFXRunner被EnemyPool.ReturnRaw销毁导致协程中断、粒子泄漏的问题
/// </summary>
public class VFXManager : MonoBehaviour
{
    private static VFXManager _instance;
    public static VFXManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[VFXManager]");
                _instance = go.AddComponent<VFXManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
    }
}