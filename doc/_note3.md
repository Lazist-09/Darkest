
---

### 【来自主程序】M8.0 二 已真正生效：金钱接进远征流程（每场胜利按光照档记账）

- ExpeditionFlow 增可选的 Economy 注入（组合根持有、以引用传入；本类只是调用方）
  每打赢一场调用 AwardBattle(log, 当前光照档, "battle")：**战斗数 乘 光照档** 两轴都在（越暗越多）
  未注入时不记 —— **不静默造一份平行账**（未接入就如实为空）
- 新增用例 Flow_Victory_AwardsGoldByLightTier_WhenEconomyInjected：
  断言金钱等于 RewardFor(当前档)，且事件流里必有一条 reason 为 battle 的 GoldChangedEvent
- 全量 **345 项 / 345 通过**

- 至此 M8.0 二（金钱）闭环程度：数据 加 P22 四 校验 加 跨趟状态机 加 **流程接线（已生效）**
  仍缺：**跨趟持有者**（跨场景存活，属 ③ 回城场景的活）与**花钱出口**（④ 减压 ／ ⑤ 招募）
- 下一步（按 #283 顺序）：③ 回城场景 Hamlet 加 入口接线（含把 Economy 放进跨趟持有者）
