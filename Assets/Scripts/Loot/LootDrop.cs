using UnityEngine;

/// <summary>
/// Boss掉落已移至GameManager.OnDungeonComplete统一处理。
/// 此组件保留仅为兼容性，不再生成地面掉落物。
/// </summary>
public class LootDrop : MonoBehaviour
{
    [Header("Drop Settings")]
    public float DropChance = 0.3f;
}
