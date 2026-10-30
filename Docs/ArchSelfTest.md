# Arch 数据模型自检

```
[PASS] PlayerData JSON往返
[PASS] PlayerData 货币封顶
[PASS] 高版本数据拒绝加载
[PASS] v0迁移+规整
[PASS] 入账封顶
[PASS] 正常入账
[PASS] 拒绝负数入账
[PASS] 余额不足拒绝扣减
[PASS] 正常扣减
[PASS] 批量乘法溢出保护
[PASS] CurrencyOperation 往返+幂等键
[PASS] 配置校验抓出问题数≥4  (日常任务类型重复: 0 / 日常任务类型越界: 9 / 任务 9 目标值非法: 0 / 任务 9 奖励为负: -1 / 物品模板ID重复: 7 / 物品 7 缺少名称)
[PASS] 合法配置零报错
==== 自检结果: 13 通过 / 0 失败 ====

```
