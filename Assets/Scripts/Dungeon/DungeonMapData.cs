using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Data-driven map layout system. Each stage has unique:
/// - Map size (walls/floor)
/// - Camera orthographic size
/// - Fixed enemy spawn points
/// - Obstacles (ONLY round colliders — pillars, rocks, trees — no walls that enemies get stuck on)
/// Maps scale progressively bigger and more complex.
/// Stage 2+ uses PROCEDURALLY GENERATED obstacle layouts — every run is different.
/// </summary>
public class DungeonMapData
{
    public enum ObstacleType
    {
        Pillar,
        Tree,
        Rock,
        IceSpike,
        LavaPool,
        Tombstone
    }

    [System.Serializable]
    public struct ObstacleData
    {
        public ObstacleType type;
        public Vector3 position;
        public Vector3 size;
        public float rotation;

        public ObstacleData(ObstacleType type, Vector3 position, Vector3 size, float rotation = 0f)
        {
            this.type = type;
            this.position = position;
            this.size = size;
            this.rotation = rotation;
        }
    }

    [System.Serializable]
    public struct StageMapConfig
    {
        public float mapHalfWidth;
        public float mapHalfHeight;
        public float cameraOrthoSize;
        public Vector3[] spawnPoints;
        public ObstacleData[] obstacles;

        public float EnemyClampX => mapHalfWidth - 3f;
        public float EnemyClampY => mapHalfHeight - 1.5f;
    }

    private static StageMapConfig[] configs;
    // _lastSeed removed — seed logging no longer needed

    /// <summary>获取地图对角线长度</summary>
    public static float GetMapDiagonal(int stageIndex)
    {
        if (configs == null) BuildConfigs();
        var cfg = configs[Mathf.Clamp(stageIndex, 0, configs.Length - 1)];
        float w = cfg.mapHalfWidth * 2f;
        float h = cfg.mapHalfHeight * 2f;
        return Mathf.Sqrt(w * w + h * h);
    }

    /// <summary>获取基于地图尺寸的攻击范围（对角线×12%）</summary>
    public static float GetBaseAttackRange(int stageIndex)
    {
        return GetMapDiagonal(stageIndex) * 0.12f;
    }

    /// <summary>
    /// 获取关卡地图 — Stage 0 使用固定布局, Stage 1+ 每次进入随机生成不同布局
    /// </summary>
    public static StageMapConfig GetStageMap(int stageIndex)
    {
        if (configs == null) BuildConfigs();

        if (stageIndex == 0)
            return configs[0]; // Stage 1: 固定手绘地图

        // Stage 2+: 使用固定地图尺寸 + 随机障碍物布局
        var baseConfig = configs[Mathf.Clamp(stageIndex, 0, configs.Length - 1)];
        baseConfig.obstacles = GenerateRandomObstacles(stageIndex, baseConfig.mapHalfWidth, baseConfig.mapHalfHeight);
        return baseConfig;
    }

    /// <summary>
    /// 程序化生成随机障碍物布局 — roguelite风格, 每次进入副本都不同
    /// </summary>
    private static ObstacleData[] GenerateRandomObstacles(int stageIndex, float halfW, float halfH)
    {
        var obstacles = new List<ObstacleData>();

        // 根据关卡选择障碍物类型
        ObstacleType[] obstacleTypes = stageIndex switch
        {
            1 => new[] { ObstacleType.Rock, ObstacleType.Pillar, ObstacleType.Pillar },
            2 => new[] { ObstacleType.IceSpike, ObstacleType.Rock, ObstacleType.Pillar },
            3 => new[] { ObstacleType.Rock, ObstacleType.LavaPool, ObstacleType.Pillar },
            4 => new[] { ObstacleType.Tombstone, ObstacleType.Rock, ObstacleType.Pillar },
            5 => new[] { ObstacleType.Rock, ObstacleType.LavaPool, ObstacleType.Pillar },
            6 => new[] { ObstacleType.Rock, ObstacleType.Pillar, ObstacleType.IceSpike },
            7 => new[] { ObstacleType.Rock, ObstacleType.Pillar, ObstacleType.Tombstone },
            _ => new[] { ObstacleType.Rock, ObstacleType.Pillar }
        };

        // 障碍物数量随关卡增加
        int obstacleCount = 6 + stageIndex * 2;
        int lavaCount = stageIndex >= 3 ? Random.Range(1, 4) : 0;

        // 玩家起始位置 (0,0) — 保留周围安全区
        float safeRadius = 4f;
        float maxW = halfW - 2f;
        float maxH = halfH - 2f;

        for (int i = 0; i < obstacleCount; i++)
        {
            ObstacleType type = obstacleTypes[Random.Range(0, obstacleTypes.Length)];

            // 找到不与玩家起始位置重叠的位置
            Vector3 pos;
            int attempts = 0;
            do
            {
                pos = new Vector3(
                    Random.Range(-maxW, maxW),
                    Random.Range(-maxH, maxH),
                    0f
                );
                attempts++;
            }
            while (Vector2.Distance(pos, Vector2.zero) < safeRadius && attempts < 20);

            // 小尺寸, 不阻挡移动
            float size = Random.Range(0.5f, 0.8f);
            obstacles.Add(new ObstacleData(type, pos, new Vector3(size, size, 1f), Random.Range(0f, 360f)));
        }

        // 添加熔岩池 (纯视觉, 无碰撞)
        for (int i = 0; i < lavaCount; i++)
        {
            Vector3 pos;
            int attempts = 0;
            do
            {
                pos = new Vector3(
                    Random.Range(-maxW, maxW),
                    Random.Range(-maxH, maxH),
                    0f
                );
                attempts++;
            }
            while (Vector2.Distance(pos, Vector2.zero) < safeRadius && attempts < 20);

            obstacles.Add(new ObstacleData(ObstacleType.LavaPool, pos,
                new Vector3(Random.Range(1.5f, 3f), Random.Range(1f, 2f), 1f), 0f));
        }

        return obstacles.ToArray();
    }

    private static void BuildConfigs()
    {
        configs = new StageMapConfig[8];

        // Stage 1: 幽暗森林（Map_Stage0 图片 1719×2048，宽高比 0.84:1，PPU 74.19）
        // 图片中心(859.5,1024) → 世界中心(0,0)；worldX=(px-859.5)/PPU*scale, worldY=(1024-py)/PPU*scale
        configs[0] = new StageMapConfig
        {
            mapHalfWidth = 12f,
            mapHalfHeight = 15f,
            cameraOrthoSize = 10.0f,
            spawnPoints = new Vector3[]
            {
                new Vector3(-3f, -5f, 0f),
                new Vector3(3f, -5f, 0f),
                new Vector3(-3f, 5f, 0f),
                new Vector3(3f, 5f, 0f)
            },
            obstacles = new ObstacleData[]
            {
                // ===== 蓝色水晶 (IceSpike) =====
                new ObstacleData(ObstacleType.IceSpike, new Vector3(-6.1f, -2.5f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.IceSpike, new Vector3(5.8f, 3.2f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.IceSpike, new Vector3(9.9f, 1.1f, 0f), new Vector3(1f, 1f, 1f)),

                // ===== 魔法树桩 (Tree) =====
                new ObstacleData(ObstacleType.Tree, new Vector3(-5.9f, 5.3f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Tree, new Vector3(5.8f, -0.9f, 0f), new Vector3(1f, 1f, 1f)),

                // ===== 粉色蜡烛 (Pillar) =====
                new ObstacleData(ObstacleType.Pillar, new Vector3(-6f, 11.6f, 0f), new Vector3(0.6f, 0.6f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(6f, 11.6f, 0f), new Vector3(0.6f, 0.6f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(9.2f, -4.1f, 0f), new Vector3(0.6f, 0.6f, 1f)),

                // ===== 岩石堆 (Rock) =====
                new ObstacleData(ObstacleType.Rock, new Vector3(-5.8f, 8.1f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(5.8f, 8.1f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-5f, -12f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(5f, -12f, 0f), new Vector3(1.2f, 1.2f, 1f)),
            }
        };

        // Stage 2: 废弃矿洞
        configs[1] = new StageMapConfig
        {
            mapHalfWidth = 14f,
            mapHalfHeight = 10f,
            cameraOrthoSize = 9.5f,
            spawnPoints = new Vector3[]
            {
                new Vector3(-6f, 3f, 0f),
                new Vector3(6f, 3f, 0f),
                new Vector3(-6f, -3f, 0f),
                new Vector3(6f, -3f, 0f),
                new Vector3(0f, -5f, 0f),
                new Vector3(0f, 5f, 0f)
            },
            obstacles = new ObstacleData[]
            {
                new ObstacleData(ObstacleType.Pillar, new Vector3(-9f, 4f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(9f, 4f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-9f, -4f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(9f, -4f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(0f, 0f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-4f, -6f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(4f, 6f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-5f, 0f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(5f, 0f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(0f, 6f, 0f), new Vector3(1.5f, 1.5f, 1f))
            }
        };

        // Stage 3: 冰霜峡谷
        configs[2] = new StageMapConfig
        {
            mapHalfWidth = 16f,
            mapHalfHeight = 11f,
            cameraOrthoSize = 9.5f,
            spawnPoints = new Vector3[]
            {
                new Vector3(-7f, 4f, 0f),
                new Vector3(7f, 4f, 0f),
                new Vector3(-7f, -4f, 0f),
                new Vector3(7f, -4f, 0f),
                new Vector3(0f, -5f, 0f),
                new Vector3(0f, 5f, 0f)
            },
            obstacles = new ObstacleData[]
            {
                new ObstacleData(ObstacleType.IceSpike, new Vector3(-10f, 6f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.IceSpike, new Vector3(10f, 6f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.IceSpike, new Vector3(-4f, -3f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.IceSpike, new Vector3(4f, 3f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-10f, 0f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(10f, 0f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(6f, -5f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-6f, 5f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(0f, -7f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(0f, 7f, 0f), new Vector3(1.2f, 1.2f, 1f))
            }
        };

        // Stage 4: 火焰神殿
        configs[3] = new StageMapConfig
        {
            mapHalfWidth = 17f,
            mapHalfHeight = 12f,
            cameraOrthoSize = 10.0f,
            spawnPoints = new Vector3[]
            {
                new Vector3(-5f, 4f, 0f),
                new Vector3(5f, 4f, 0f),
                new Vector3(-5f, -4f, 0f),
                new Vector3(5f, -4f, 0f),
                new Vector3(0f, 5f, 0f),
                new Vector3(0f, -5f, 0f),
                new Vector3(-3f, 0f, 0f),
                new Vector3(3f, 0f, 0f)
            },
            obstacles = new ObstacleData[]
            {
                new ObstacleData(ObstacleType.LavaPool, new Vector3(-4f, 0f, 0f), new Vector3(3f, 2f, 1f)),
                new ObstacleData(ObstacleType.LavaPool, new Vector3(4f, 0f, 0f), new Vector3(3f, 2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-12f, 5f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(12f, 5f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-12f, -5f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(12f, -5f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-3f, 0f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(3f, 0f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.LavaPool, new Vector3(0f, 7f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.LavaPool, new Vector3(0f, -7f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-8f, 0f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(8f, 0f, 0f), new Vector3(1.5f, 1.5f, 1f))
            }
        };

        // Stage 5: 亡灵墓穴
        configs[4] = new StageMapConfig
        {
            mapHalfWidth = 19f,
            mapHalfHeight = 13f,
            cameraOrthoSize = 10.5f,
            spawnPoints = new Vector3[]
            {
                new Vector3(-6f, 5f, 0f),
                new Vector3(6f, 5f, 0f),
                new Vector3(-6f, -5f, 0f),
                new Vector3(6f, -5f, 0f),
                new Vector3(0f, 6f, 0f),
                new Vector3(0f, -6f, 0f),
                new Vector3(-3f, 0f, 0f),
                new Vector3(3f, 0f, 0f)
            },
            obstacles = new ObstacleData[]
            {
                new ObstacleData(ObstacleType.Tombstone, new Vector3(-11f, 6f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Tombstone, new Vector3(11f, 6f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Tombstone, new Vector3(-11f, -6f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Tombstone, new Vector3(11f, -6f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-13f, 0f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(13f, 0f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(0f, 6f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(0f, -6f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-9f, 8f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(9f, -8f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-4f, -9f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(4f, 9f, 0f), new Vector3(2f, 2f, 1f))
            }
        };

        // Stage 6: 龙巢深处 — open cavern with scattered egg clusters and pillars, no blocking walls
        configs[5] = new StageMapConfig
        {
            mapHalfWidth = 20f,
            mapHalfHeight = 14f,
            cameraOrthoSize = 10.5f,
            spawnPoints = new Vector3[]
            {
                new Vector3(-4f, -10f, 0f),
                new Vector3(4f, -10f, 0f),
                new Vector3(-12f, 2f, 0f),
                new Vector3(12f, 2f, 0f),
                new Vector3(-8f, 8f, 0f),
                new Vector3(8f, 8f, 0f),
                new Vector3(-3f, 3f, 0f),
                new Vector3(3f, 3f, 0f)
            },
            obstacles = new ObstacleData[]
            {
                // Small egg clusters (scattered, not blocking)
                new ObstacleData(ObstacleType.Rock, new Vector3(0f, 7f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(2f, 6f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-2f, 6f, 0f), new Vector3(0.8f, 0.8f, 1f)),

                // Decorative pillars (small colliders)
                new ObstacleData(ObstacleType.Pillar, new Vector3(-14f, 6f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(14f, 6f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-14f, -6f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(14f, -6f, 0f), new Vector3(0.8f, 0.8f, 1f)),

                // Central nest rocks (small, non-blocking)
                new ObstacleData(ObstacleType.Rock, new Vector3(-4f, 0f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(4f, 0f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(0f, -4f, 0f), new Vector3(0.8f, 0.8f, 1f)),

                // Lava pools (visual only, no collider)
                new ObstacleData(ObstacleType.LavaPool, new Vector3(0f, 3f, 0f), new Vector3(2.5f, 2f, 1f)),
                new ObstacleData(ObstacleType.LavaPool, new Vector3(-8f, -4f, 0f), new Vector3(2f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.LavaPool, new Vector3(8f, -4f, 0f), new Vector3(2f, 1.5f, 1f)),

                // A few scattered rocks near edges (small colliders)
                new ObstacleData(ObstacleType.Rock, new Vector3(-10f, 10f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(10f, 10f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-10f, -10f, 0f), new Vector3(0.8f, 0.8f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(10f, -10f, 0f), new Vector3(0.8f, 0.8f, 1f)),
            }
        };

        // Stage 7: 混沌虚空
        configs[6] = new StageMapConfig
        {
            mapHalfWidth = 23f,
            mapHalfHeight = 15f,
            cameraOrthoSize = 11.0f,
            spawnPoints = new Vector3[]
            {
                new Vector3(-8f, 5f, 0f),
                new Vector3(8f, 5f, 0f),
                new Vector3(-8f, -5f, 0f),
                new Vector3(8f, -5f, 0f),
                new Vector3(-3f, 6f, 0f),
                new Vector3(3f, 6f, 0f),
                new Vector3(-3f, -6f, 0f),
                new Vector3(3f, -6f, 0f)
            },
            obstacles = new ObstacleData[]
            {
                new ObstacleData(ObstacleType.Pillar, new Vector3(-16f, 7f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(16f, 7f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-16f, -7f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(16f, -7f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-19f, 0f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(19f, 0f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(0f, 0f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-7f, -10f, 0f), new Vector3(2.5f, 2.5f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(7f, 10f, 0f), new Vector3(2.5f, 2.5f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-11f, 4f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(11f, -4f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Tombstone, new Vector3(-13f, 10f, 0f), new Vector3(1f, 1f, 1f)),
                new ObstacleData(ObstacleType.Tombstone, new Vector3(13f, -10f, 0f), new Vector3(1f, 1f, 1f))
            }
        };

        // Stage 8: 魔王殿堂
        configs[7] = new StageMapConfig
        {
            mapHalfWidth = 25f,
            mapHalfHeight = 16f,
            cameraOrthoSize = 10f,
            spawnPoints = new Vector3[]
            {
                new Vector3(-9f, 6f, 0f),
                new Vector3(9f, 6f, 0f),
                new Vector3(-9f, -6f, 0f),
                new Vector3(9f, -6f, 0f),
                new Vector3(-4f, 7f, 0f),
                new Vector3(4f, 7f, 0f),
                new Vector3(-4f, -7f, 0f),
                new Vector3(4f, -7f, 0f),
                new Vector3(0f, 8f, 0f),
                new Vector3(0f, -8f, 0f)
            },
            obstacles = new ObstacleData[]
            {
                new ObstacleData(ObstacleType.Pillar, new Vector3(-19f, 10f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(19f, 10f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-19f, -10f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(19f, -10f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-8f, 6f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(8f, 6f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-8f, -6f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(8f, -6f, 0f), new Vector3(1.2f, 1.2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(-5f, 0f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(5f, 0f, 0f), new Vector3(2f, 2f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(-14f, 0f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Pillar, new Vector3(14f, 0f, 0f), new Vector3(1.5f, 1.5f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(0f, 12f, 0f), new Vector3(2.5f, 2.5f, 1f)),
                new ObstacleData(ObstacleType.Rock, new Vector3(0f, -12f, 0f), new Vector3(2.5f, 2.5f, 1f))
            }
        };
    }
}
