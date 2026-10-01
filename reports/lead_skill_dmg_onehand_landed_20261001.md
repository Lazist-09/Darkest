DELIVERY-LEAD-SKILL-DMG-LANDED-20261001

## 执行回执：`#490` 裁定已落库（零行为 · 提交号见下）

```
① 落库 4 行 = darkest/data/skills.json：
   · warrior_lunge        −55 ⇒ −50（:86）＋ 出处 :87 ⇒ "dd1:breakthru (一手 .dmg · hellion.info.darkest:34-38)"
   · commissar_burst_fire −60 ⇒ −50（:1374）＋ 出处 :1375 ⇒ "dd1:grape_shot_blast (一手 .dmg · highwayman.info.darkest:28-32)"
② 出处分布实测：8 ref: + 6 dd1: ⇒ 【6 ref: + 8 dd1:】（= 你裁的就是这个数 ✓）· git diff --stat = 1 file · 4+/4− · 文件 CRLF=0（LF 保持）
③ 不入 §39（按你 §2：回一手 = 对齐 ≠ 平衡改动 · 同型先例 #486）⇒ 留痕 = #490 + 本封 + 报告 reports/skill_dmg_onehand_alignment_20261001.md（前后读数）
④ 🔴 提交号 = 439a912（数据落库）· 报告 + doc/state.md #497 = 同一链的下一笔 · 已推送 origin/master
⑤ 读数（最低限度 · 用户约束「只允许最低限度的必要性测试」）：Darkest.csproj 构建 0 错 ·
   筛选 M1cStage3MechanismTests / M1cDmgPctTableTests / M1cPilotComparisonTests = 失败 0 / 通过 7 / 总计 7 ✓
⑥ 你 §4 顺带登记的 3 处（:2 _dmg_pct_note 旧口径 · :1800 _sigma_note 留了 §61 已否的「Σ 归一为 1」 · §5 差最远三条）
   ⇒ 只登记不动 · 归解冻窗口同批改文本 ✓ · 历史文本里的 ref:Hellion/breakthru 字面量一处不改（惯例：裁定为准）✓
⑦ 阻塞 / 待裁：无。本件结清 ⇒ 下一件 = P2（M1c 阶段 3 切默认 · 走解冻口径四件）。
```

- **权威在哪**：`doc/state.md #490 / #497` · `reports/planner_20260930_to_lead_skill_dmg_ruling.md`（你的裁定）· `darkest/data/skills.json`
