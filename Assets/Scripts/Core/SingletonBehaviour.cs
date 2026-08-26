using UnityEngine;

/// <summary>
/// 泛型单例基类 — 统一单例模式，消除重复的 Awake/Instance 逻辑
/// 用法: public class MyManager : SingletonBehaviour<MyManager>
/// </summary>
public abstract class SingletonBehaviour<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool _applicationIsQuitting;

    public static T Instance
    {
        get
        {
            if (_applicationIsQuitting) return null;
            if (_instance == null)
            {
                _instance = FindObjectOfType<T>();
                if (_instance == null)
                {
                    var go = new GameObject(typeof(T).Name);
                    _instance = go.AddComponent<T>();
                    DontDestroyOnLoad(go);
                }
            }
            return _instance;
        }
    }

    public static bool HasInstance => _instance != null;

    protected virtual void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this as T;
    }

    protected virtual void OnDestroy()
    {
        // 应用退出时不访问任何实例或ScriptableObject，防止ScriptableSingleton报错
        if (!Application.isPlaying) return;
        if (_instance == this)
            _instance = null;
    }

    protected virtual void OnApplicationQuit()
    {
        _applicationIsQuitting = true;
        // 退出时绝不访问任何ScriptableObject或静态配置实例
        _instance = null;
    }
}
