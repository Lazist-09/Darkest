
---

### 【来自主程序】M8.0 四 减压（Tavern ／ Abbey）已落地：同价同效、风险不同

- 数据（data/economy.json 增 stress_relief）：两栋建筑
  · tavern 酒馆：cost 3 ／ morale_restore 30 ／ next_run_penalty 8 ／ penalty_chance 0.5（**快而不稳**）
  · abbey 修道院：cost 3 ／ morale_restore 30 ／ next_run_penalty 8 ／ penalty_chance 0.2（**慢而稳**）
- **P22 五** 校验（可测定义，红线 19）：两栋必须**同价**、**同效**（恢复量与下趟惩罚相同）、且**风险必须不同**
  （penalty_chance 相同即报错 —— 否则两栋等价，选风格退化成选更优）；另有 3 条反例用例
- 内核 StressRelief（零 Godot）：钱不够**拒绝且不扣、不掷骰、不写事件**；成交则恢复士气、
  **掷骰必写 RngDraw**（红线：所有新随机必写）、结算必写 StressReliefEvent（数字来自事件流）
  NextRunOpeningMorale(...) 实现 7.3 的「下一趟开局士气 −N」
- 用例 StressReliefTests（5 条）：P22 五 正例与反例 ／ 恢复与扣钱 ／ 拒绝路径不掷骰 ／ 400 次对照试验
  （Tavern 触发率高于 Abbey ⇒ 风险分离成立；且每次判定各写一条 RngDraw，数量可对账）
- 全量 **350 项 / 350 通过**；实测副作用触发率：**Tavern 50%（配置 50%）／ Abbey 18%（配置 20%）**（400 次对照）

- 🔴 一个**结构点**需要你裁（与 ⑤ 的顾虑同类）：
  目前**英雄士气是单趟状态**（ExpeditionSession.Retained，回城即随趟结束）；
  而减压是**局外**行为 ⇒ 要让减压真正有意义，**英雄士气必须跨趟存活**（像金钱那样由名册 ／ 跨会话持有者持有）
  => 这会给 ExpeditionSession 的归属带来一次改动（与 ① 的 (c) 同类：谁的答案换主人）
  => 我**没有擅自动它**：本片只落「规则 加 校验 加 事件」，跨趟士气待你一句话
- 下一步：若你同意把英雄士气移到跨趟持有者，我就先做那一步再做 ⑤ 招募；否则先做 ⑤ 与 ⑥ 的回路
