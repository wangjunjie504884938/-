using UnityEngine;

namespace ArpgGameServer
{
    /// <summary>视线检测 — 服务器 2D Raycast (仅服务器/headless 执行; 数学部分在 Arch/ServerAuthority.cs)</summary>
    public static class ServerAuthorityLOS
    {
        /// <summary>命中墙体等非玩家非怪物碰撞体 = 无视线</summary>
        public static bool HasLineOfSight(Vector2 from, Vector2 to)
        {
            var dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return true;

            int wallMask = ~(LayerMask.GetMask("Player") | LayerMask.GetMask("Monster"));
            var hit = Physics2D.Raycast(from, dir.normalized, dist, wallMask);
            return hit.collider == null;
        }
    }
}