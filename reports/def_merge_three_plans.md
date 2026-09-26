# `def` 合并 · **三份执行预案**（甲/乙/丙 · 2026-09-21 实测）

> 🔴 **用途**：你裁一句话，我按对应预案**当天执行**（每份都写清：**动哪几行 · 改哪些读数 · 风险**）✓
> 🔴 **裁定凭据已交**：`reports/def_merge_baseline.md`（28 条基线）+ `DefMergeBaselineTests` 的**原版口径对照**
>   ⇒ 关键事实：**命中列两口径同源**（原版 `def` = 我们的 `Dodge`）⇒ **只有减伤这一列要裁** ✓

## 0. 现状规模（实测）
```
`PhysDef` / `phys_def` 合计 **50 处 / 27 文件**：
   tests **19** · data(JSON) **12** · 战斗管线 **9** · 解析/契约 **5** · 其它 **5**
减伤公式 `BattleMath.PhysicalMitigation(...)` 的调用点 **7 处**：
   `BattleMath.cs`(定义+1) · `DamageStep.cs` 1 · `BattleProjector.cs` 1 · `TuningConfig.cs` 1 ·
   `PhysicalMitigationDataTests` 2 · `DefMergeBaselineTests` 2 · `FormulaTests` 2
读"减伤数"的用例 ≈ **46 处**，集中在：`DefMergeBaselineTests 16` · `PhysicalMitigationDataTests 15` · `FormulaTests 15`
```

## 预案（甲）：**`PhysDef` 退场**（严格照原版 · 减伤走 `prot`）
```
🔴 **行为改动**（最大的一份）
① 数据：`units.json` 删 `phys_def`（**12 处**）
② 契约：`UnitStats.PhysDef` 删 · `UnitConfig.PhysDef` 删（**5 处**）
③ 管线：`DamageStep`/`BattleProjector` 的减伤项改用 `prot`（**四原型 prot = 0 ⇒ 减伤归零** ✓）
④ 公式：`BattleMath.PhysicalMitigation` **保留**（`prot` 口径也要走它 ⇒ 只是入参换源 ✓）
⑤ 读数：改 3 个用例文件（≈ **46 处**）⇒ 依据 `DefMergeBaselineTests` 的对照表逐条改 ✓
📊 **前后读数我已经有**（对照夹具已跑）：
   我方减伤 warrior 21% / tank 29% / medic 12% / commissar 14% / 敌 9~21% ⇒ **全部归 0** ✓
   命中列**不变**（90/95/90/90/85/90 ✓）
⚠️ 风险：**战斗会明显变长/变难**（伤害 +9~29%）⇒ 这是**平衡决定**，必须策划点头 ✓
```

## 预案（乙）：**保留 `PhysDef`**，只把"它是我们自加的"写清楚
```
✅ **零代码改动**（0 行）
① 文档：在 `data_schema`/`units.json` 的说明里写明"`PhysDef` 是**我们自加**的拆分（原版只有一个 `def`；
   原版减伤走 `prot`）" ⇒ 🔴 那是**契约域**（`doc/**`）⇒ 我把**建议文字**给你/契约方，不擅自改 ✓
② 已有资产：`reports/def_merge_baseline.md` §3 + `DefMergeBaselineTests` 的对照用例**已经把这件事讲清**了 ✓
⇒ 我这边**一行不用动**；判据：**全量 797/797 不变** ✓
```

## 预案（丙）：**先只改名/注释**（不动行为）
```
🟢 **零行为**（我推荐作为"暂缓"时的落点 ✓）
① 在 **5 处解析/契约**（`UnitStats`/`UnitConfig`/`UnitsConfig`/`UnitStatsMapper`/`TuningConfig` 相关）
   给 `PhysDef` 加 XML 注释：**"⚠️ 这是**我们自加**的拆分，原版只有一个 `def`（= `Dodge`）；
   原版减伤走 `prot`（0~0.85 比例）；**合并/退场的裁定见 `reports/def_merge_three_plans.md`**"** ✓
② **不改名**（改名要动 50 处 ⇒ 纯 churn ✗；注释已能消除歧义 ✓）
③ 判据：**全量 797/797 不变** + 门禁绿 ✓
```

## 🔴 我的建议
```
**先做（丙）**（零行为、零风险、当天可完）⇒ 把"`PhysDef` 是我们自加"写进代码注释，**消除歧义** ✓
然后等策划对**平衡**（减伤归零 vs 保留）表态 ⇒ 再执行（甲）或（乙）✓
⚠️ 我**不碰数值**、**不代裁平衡**（预案（甲）会让伤害整体 +9~29%，那是玩法决定 ✓）
```

---

## 🆕 补充证据（2026-09-26 · `reports/unity_ref/166_*.md`）：**参考侧的 `.def` 实测曲线**

> 🔴 **本节的用途**：上面的三份预案讨论的是【我方要不要保留自加减伤】；
>   而**若裁定"对齐参考"，就需要参考侧的准确形态** —— 本节给出**实测值** ✓
> 🎖️ **来源**：`Darkest-Dungeon-Unity\…\Heroes\Info\*.bytes` 的 `.def` 字段（**逐职业读的**）✓

```
📊 **15 个职业的护甲 `.def` 五阶（实测）**：
   | 职业 | `.def` 五阶 | `.prot` |
   |---|---|---|
   | **Jester** | **15 → 35** | 全 0 |
   | **Antiquarian · GraveRobber · Hellion · Highwayman · HoundMaster · Occultist** | **10 → 30** | 全 0 |
   | **Abomination** | **7.5 → 27.5** | 全 0 |
   | **BountyHunter · Crusader · ManAtArms · PlagueDoctor** | **5 → 25** | 全 0 |
   | **Arbalest · Leper · Vestal** | **0 → 20** | 全 0 |

🎖️ **规律（极干净）**：
   · ✅ **每个职业都是【5 阶 · 每阶恰好 +5 个百分点】** ✓
   · ✅ **职业间只差【起点】**（0 / 5 / 7.5 / 10 / 15）✓
   · ✅ **`.prot` 在英雄护甲里【全部为 0】**（15/15）⇒ **不参与** ✓

🔴 **而它暴露一个对齐难点（供裁定时考虑）**：
   · 参考的减伤挂在 **【每职业 × 5 阶】** 上；
   · 而我方现结构是 **`tuning.physical_mitigation.divisor = 30`（全局单值）**
     ＋ **单位级 `prot` 整数（单值）** ⇒ 🔴 **我方【没有"5 阶"这个维度】** ⚠️
   ⇒ 📌 **要对齐就得先补"每职业 5 阶 def"这个维度** ⇒ ✅ **那是结构改动，不只是改数** ✓
   ＋ 🎖️ 而 `units.json` 的 `_align` 显示 **A3 已按参考抄了 `.def` 的 weapon/armour 前 5 阶**
     ⇒ ⚠️ **即：维度可能已部分存在**（`UnitTiers` 有 `prot`）⇒ **需核对** ✓
```

---

## 🆕🆕 更正 + 映射澄清（2026-09-26 · `reports/unity_ref/167_*.md`）

> 🔴 **本节更正上一节的两处** —— 我核了 `units.json` 的结构后推翻了自己的判断 ✓

```
🔴 **更正 ①**：上面写"我方没有 5 阶维度" —— **那是错的** ⚠️
   ✅ **实测：`units.json` 的 4 个英雄【都有】`armour` 数组（5 阶）**：
      | 我方 | 参考职业 | 我方 `def_pct` | 参考 `.def` | 判 |
      |---|---|---|---|---|
      | `warrior` | `Hellion` | `[10,15,20,25,30]` | `['10','15','20','25','30']` | ✅ **完全一致** |
      | `tank` | `ManAtArms` | `[5,10,15,20,25]` | `['5','10','15','20','25']` | ✅ **完全一致** |
      | `medic` | `PlagueDoctor` | `[5,10,15,20,25]` | `['5','10','15','20','25']` | ✅ **完全一致** |
      | `commissar` | `Highwayman` | `[10,15,20,25,30]` | `['10','15','20','25','30']` | ✅ **完全一致** |
      ＋ 而 `prot` 端我方 `[0,0,0,0,0]` ⇔ 参考全 0 ⇒ ✅ **也一致** ✓
   ⇒ ✅ **4/4 完全一致** ⇒ **维度存在且值已对齐** ✓
   （3 个**敌人**没有 5 阶 ⇒ 那是设计：`TierDefence.cs:33`「敌人 = 我们自设 ⇒ 退回顶层」）✓

🎖️ **映射澄清（关键）**：`TierDefence.cs:41-45` 实测 ——
   · **`DefAt`** ⇒ 读 `armour[tier].def_pct` ⇒ 🔴 **映射到 `Dodge`（闪避）** ✓
   · **`ProtAt`** ⇒ 读 `armour[tier].prot` ⇒ ✅ **映射到减伤** ✓
   ⇒ 🎖️ **即：参考的 `.def` 在我方是【闪避】，不是减伤** ✓
      📌 **这与本文件 §0 的"关键事实：原版 `def` = 我们的 `Dodge`"【完全一致】** ✓
   ⇒ 🔴 **所以待裁的核心【不在 `def_pct`】**（那已对齐），
     而在 **「我方自加的 `Prot` 减伤要不要留」** —— 因为**参考的 `.prot` 英雄侧全是 0** ✓
      ⇒ ✅ **这让裁定更清楚：不是"抄哪个数"，是"留不留这个机制"** ✓
```
