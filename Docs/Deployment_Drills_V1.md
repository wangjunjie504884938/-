# 阶段12 演练手册：故障注入 / 备份恢复 / 回滚 / 上线检查

适用: 39.107.141.107 (API 5132, 同机 MySQL 3306 / Redis 6379, systemd 或手动启动)。
所有命令以 root/部署账号在服务器上执行。演练前先 `mkdir -p /root/drill-$(date +%m%d)` 存放证据。

---

## 1. 进程强杀恢复 (P0)

目的: 验证 kill -9 后 API 能重启, 玩家会话转 Detached 3 分钟内可重连, 地牢从最后快照恢复。

```bash
# 1. 记录当前状态 (演练前基线)
curl -s http://127.0.0.1:5132/healthz
curl -s http://127.0.0.1:5132/api/session/lines
pid=$(pgrep -f ArpgServer || pgrep -f "dotnet.*ArpgServer")
echo "API pid=$pid" | tee /root/drill-$(date +%m%d)/kill-baseline.txt

# 2. 强杀
kill -9 $pid

# 3. 确认死亡 + 立即重启 (按实际启动方式; systemd 则 systemctl restart arpg-api)
curl -s --max-time 2 http://127.0.0.1:5132/healthz || echo "已死亡(预期)"
<你的启动命令>   # 例如: cd /opt/arpg && nohup ./ArpgServer > out.log 2>&1 &

# 4. 启动后 60 秒内轮询恢复
for i in $(seq 1 12); do
  curl -s --max-time 2 http://127.0.0.1:5132/healthz && echo " <- 恢复 (第${i}次探测)" && break
  sleep 5
done
```

判定: /healthz 恢复 = PASS。客户端表现 = 断线 → 3 分钟内重连 (session/reconnect) → 会话重分配到健康线 → 地牢经 dungeon/snapshot/load 恢复到最后快照 (最多回退 P0=5s)。

---

## 2. Redis 故障注入 (P0 — 货币热路径降级)

目的: Redis 停止后, 货币扣款降级 MySQL 兜底不崩溃; 恢复后自动回连。

```bash
# 1. 注入 (Redis 密码见 appsettings)
redis-cli -a '<REDIS_PASSWORD>' shutdown nosave

# 2. 验证 API 仍存活, 登录+开局+快照保存应降级成功 (Redis不可用走MySQL)
curl -s http://127.0.0.1:5132/healthz
# 用任一测试账号走一遍 login → session/open → dungeon/snapshot/save (Phase06Test 亦可用)

# 3. 恢复 Redis
systemctl start redis      # 或 docker start redis / 按实际部署方式
redis-cli -a '<REDIS_PASSWORD>' ping   # PONG
```

判定: Redis 宕机期间 API 不崩溃、登录/快照可用 (MySQL 兜底) = PASS; 恢复后 60 秒内 RedisCacheService 重新连接 (日志有 Redis 连接成功) = PASS。
注意: Lua 原子扣款在 Redis 宕机期间退化为 MySQL 直改, 期间并发扣款语义由 DB 行锁保证。

---

## 3. MySQL 故障注入 (P1)

目的: MySQL 停止时 API 返回 5xx 而非崩溃/挂死; 恢复后自动重连。

```bash
systemctl stop mysqld    # 或 docker stop mysql
# 探测: login 应快速失败 (5xx/超时), 不得无限挂起
time curl -s --max-time 10 -X POST http://127.0.0.1:5132/api/auth/login \
  -H 'Content-Type: application/json' -d '{"username":"testbot","password":"123456"}'
systemctl start mysqld
```

判定: 停库期间请求以明确错误快速返回 (无挂死、无进程退出) = PASS; 起库后 60 秒内业务恢复 = PASS。

---

## 4. 备份恢复演练 (P0 — 每周应例行)

```bash
# 1. 备份 (上线前必须手动执行一次)
mysqldump -uroot -p --single-transaction --routines weimian > /root/drill-$(date +%m%d)/weimian-backup.sql

# 2. 恢复演练到影子库 (不动线上库)
mysql -uroot -p -e "CREATE DATABASE IF NOT EXISTS weimian_drill"
mysql -uroot -p weimian_drill < /root/drill-$(date +%m%d)/weimian-backup.sql
mysql -uroot -p -e "SELECT COUNT(*) AS users FROM weimian_drill.Users; SELECT COUNT(*) AS ledger FROM weimian_drill.currency_ledger;"

# 3. 比对行数一致后删除影子库
mysql -uroot -p -e "DROP DATABASE weimian_drill"
```

Redis AOF 检查 (热数据可丢, AOF 只为减少回源):
```bash
redis-cli -a '<REDIS_PASSWORD>' CONFIG GET appendonly     # 应为 yes
redis-cli -a '<REDIS_PASSWORD>' CONFIG GET appendfsync    # everysec (规划§8)
ls -la /var/lib/redis/appendonlydir/ 2>/dev/null || ls -la /var/lib/redis/
```

---

## 5. 版本回滚演练 (P0)

前提: 每次上传新包前保留上一版 (mv /opt/arpg /opt/arpg.bak-$(date +%m%d))。

```bash
# 1. 停新版本
pkill -f ArpgServer

# 2. 回滚到备份目录
mv /opt/arpg /opt/arpg-rollback-$(date +%m%d)
mv /opt/arpg.bak-<上一版日期> /opt/arpg

# 3. 启动 + 验证老版本特征 (老版本无 /healthz → 404 即回滚成功)
<启动命令>
curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5132/healthz   # 404 = 旧版在跑
```

注意: 回滚版本读同一 MySQL, 新版本若已写入新表列 (如 payment_orders 新列) 需确认兼容; 当前各表均为新增表/新增列, 旧版本不读写新列 = 兼容。

---

## 6. 弱网测试 (P1 — 需 Mirror 侧, 在客户端/压测机执行)

Mirror 内置延迟模拟, 客户端启动参数或代码:
```csharp
// 150ms 延迟 + 1% 丢包 (Mirror LatencySimulation, 客户端与服务器都要挂)
Mirror.LatencySimulation.Settings = new Mirror.LatencySimulation.Settings(0.15, 0.01);
```
观察: 移动预测回滚是否平滑、AOI 进出是否闪断、服务器仍有最终权威 (超速被拉回)。
压测口径: 150ms/300ms/1s 三档延迟各跑 2 分钟, 移动消息量与服务端校验拒绝率对账。

---

## 7. 上线检查清单 (发布前逐项打勾)

- [ ] /healthz 200 (新版本特征)
- [ ] appsettings: MySQL/Redis 指向 127.0.0.1, Gm:Key/Node:Key 已改非 dev 默认值
- [ ] mysqldump 备份已完成并存放到 /root (第4节)
- [ ] 上一版本目录已保留 (回滚点, 第5节)
- [ ] 50 虚拟玩家×600s 压测报告 PASS (LoadTestHttp, 错误率<1%, 重放违规=0)
- [ ] 服务器 Bot 压测报告: 帧耗时<16.7ms, 快照带宽<10Mbps (ServerBotManager, -loadbots 50)
- [ ] Redis AOF 开启 + appendfsync everysec
- [ ] 日志目录可写 (logs/ 按天滚动, 14 天保留)
- [ ] payment_orders/currency_ledger/dungeon_snapshots 表存在 (启动自动补建)
- [ ] 对账抽查: currency_ledger 最近 100 条 vs 玩家余额一致

---

## 8. 已知遗留 (不阻塞上线)

- SnapshotUploader 上传快照的节点认证通道 (当前 401, 需 node token 下发机制) — 地牢崩溃恢复的远程持久化依赖此项
- GC 热点: 快照消息数组每 15Hz 分配 (~2MB/s@10bot) — 后续做数组池化
- GM 踢人只关权威会话, Mirror 连接靠写入校验失败自然回收 (无节点推送通道)
- 弱网 LatencySimulation 未集成进客户端构建
