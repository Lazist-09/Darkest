
---

### 【来自主程序】M8.0 三 回城场景 Hamlet 已落地 加 入口接线 加 三项端到端冒烟

- 新增 scenes/hamlet/Hamlet.tscn 加 scripts/ui/HamletRoot.cs（只渲染加转发；**不自己算账**）
  · 显示：金钱 ／ 一次减压价格 ／ 现在能减几次
  · 按钮：再出发（远征）可用；**减压与招募置灰**（④与⑤ 未落地前**不假装可用**）
- 跨趟持有者（#283 归属：金钱是跨会话状态）：ExpeditionContext.Gold 加 EnsureEconomy(...)
  **不随 End() 清空**（与 Flow ／ Log 的一趟生命周期区分开）=> 回城后金钱保留
- 入口接线（**红线 18 可达性验收**）：
  · BattleRoot 新增 回城（Hamlet）按钮 加 `--hamlet` CLI
  · ExpeditionRoot 本趟结束（两处 default 分支）**自动回城**
  · ExpeditionRoot 新增 `--hamlet-next` CLI（供冒烟：跑图 到 回城）
- 🔴 三项端到端冒烟（均从**启动场景**出发，非场景直载）：
  1. `--hamlet`                      => 启动 到 回城 ✓（[HamletRoot] 回城就绪：金钱 0）
  2. `--expedition --hamlet-next`    => 启动 到 跑图 到 回城 ✓
  3. `--expedition`（回归）          => 启动 到 地牢层 ✓
- 全量 **345 项 / 345 通过**
- 下一步（按 #283 顺序）：④ 减压（Tavern ／ Abbey **同价同效、风险不同**）与 ⑤ 招募（免费 加 新兵 Lv1）
  之后 ⑥ 完整端到端：启动 到 跑图 到 回城 到 **花钱** 到 再出发（现在缺的只有 花钱 那一段）
