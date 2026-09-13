
---

### 【来自主程序】M8.0 二 金钱已落地（数据 加 校验 加 跨趟状态机）

- 新增 data/economy.json：battle_reward 1（**战斗数挂钩**）加 light_tier_bonus（radiant 0 ／ dim 0 ／ shadowy 1 ／ dark 2 ／ black 3，**光照档挂钩、越暗越多**）
  加 stress_relief_cost 3 加 比例自检（一趟 3~6，即一趟能减 1~2 次压，7.1）
- 新增 EconomyConfig：**P22 四** 的可测定义（红线 19）
  ① battle_reward 大于等于 1（与战斗数挂钩）
  ② light_tier_bonus **随变暗单调不减**且**总和 大于等于 1**（否则「与光照档挂钩」只是名义上的）
  ③ stress_relief_cost 大于 0 加 比例自检；并提供 RewardFor(档) 合成单场金钱
- 新增 GoldChangedEvent（继承 BattleEvent）：**所有金钱变更必写事件**（数字必须来自事件流）
- 新增 Economy（内核、零 Godot）：**跨趟状态**，按 blueprint 9.15 的归属由**组合根持有并注入**，**BattleDirector 不持有它**
  AwardBattle(log, 档) ／ TrySpend(log, 金额, 原因)（**不足即拒绝且不扣、不写事件**）／ StressReliefCost
- 新增 EconomyTests（3 条）：P22 四 正例（含 Black 大于 Radiant 断言）加 **3 条反例**（battle_reward 0 ／ black 0 ／ 减压价格 0）
  加 事件流对账（变更次数、Total 与状态一致、拒绝时不改状态）
- 全量 **344 项 / 344 通过**

- ⚠️ 两条明示（不虚报）
  1. **尚未接进远征流程**：ExpeditionFlow.OnBattleFinished 还没调 Economy.AwardBattle
     （需要一个可靠的跨趟持有者，属 ③ 回城接线的活）故本片只到「数据 加 校验 加 状态机」可测状态
  2. **P22 五与六 仍未做**（Tavern/Abbey 同价同效、招募免费加新兵 Lv1）—— 它们是 四与五 的数据，按 #283 顺序推进，不提前细化
- 下一步（按 #283 顺序）：③ 回城场景 Hamlet 加 入口接线 —— 落地时把 Economy 接进跨趟持有者，并让每场胜利后 AwardBattle（那时 ② 才真正生效）
