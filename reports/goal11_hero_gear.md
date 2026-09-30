# 目标 11 · `H-1` 英雄装备阶（Phase 2 第一刀）

> 日期：2026-09-27　·　裁定：用户（`直接接线` + `补 blacksmith 升级路径` + `只做 H-1`）
> 前置：`Phase 0` 文件尺寸治理 ✅ · `Phase 1` 存档系统 ✅（全量 858）

---

## 一句话

**「当前阶从哪来」这个卡了三份文档的问题被回答了** —— 装备阶 = **升级树等级**（文档推荐方案 (A)），
且减伤侧真的按阶读了（`TierDefence` 从「零生产调用方」变成「被 `UnitRuntime` 消费」）。

---

## §1 为什么是这一刀（三份文档共同指向同一个洞）

| 出处 | 原文要点 |
|---|---|
| `reports/tier_source_options.md` §1 | 「**唯一缺的就是『一个单位现在是第几阶』** —— 那是设计决定，不是数值」；推荐 **(A) 升级树等级驱动** |
| `HeroConfig.WeaponTier` 注释 | 「🔴 默认 0 ⇒ 零行为。**激活条件**：减伤读阶接线时从本字段取阶」 |
| `TierDefence.cs` 文件头 | 「🔴 零行为：本文件无任何生产调用方 ⇒ 被守卫钉住。⚠️ 激活条件：先定『当前阶从哪来』」 |

⇒ 三个说法指向同一件事：**机制全在，只差"阶的来源"**。本刀就是把它接上。

---

## §2 (A) 方案的完整语义（逐条对一手数据，不是我发明的）

**阶的定义**：装备树有 **4 个可购买等级**（code `0`/`1`/`2`/`3`）⇒
起始 0 阶，买到 code N ⇒ **阶 = N+1** ⇒ 正好对上 `units.json` 的 **5 阶（0~4）**
（`UnitsConfig` 校验强制"阶数恰好 5"）⇒ **自洽，不是凑出来的** ✓

**双前置**（原版结构，`hero_upgrades.json` 一手）：
1. 本树前一级 ⇒ 买 code N 必须**当前阶 == N**
2. 铁匠铺建筑树等级：`blacksmith.weapon` / `blacksmith.armour` 的 `a`/`b`/`c`/`d` ⇒ 建筑等级 1/2/3/4
3. 另有 `prerequisite_resolve_level`（1/2/3/5）⇒ 用 `HeroConfig.Level`（1~6）判定

**成本**：金币（`currency_cost.type = gold`）⇒ `Economy.TrySpend` ✓
实测：750 / 1750 / 3000 / 6000（四职业一致）

**原型 → DD 职业映射**（一手出处 = `units.json` 的 `_align` 注释，**不是我编的**）：

| 原型 | DD 职业 | 一手出处 |
|---|---|---|
| warrior | hellion | `Hellion.bytes` weapon 16-20 / armour 21-25 行 |
| tank | man_at_arms | `ManAtArms.bytes` |
| medic | plague_doctor | `PlagueDoctor.bytes` |
| commissar | highwayman | `Highwayman.bytes` |

🔴 映射表**只认这四条**；未登记原型 ⇒ **抛**（不静默当 0 阶 —— 那与"还没接线"无法区分）。

---

## §3 落地清单

### 数据
| 文件 | 改动 |
|---|---|
| `darkest/data/heirlooms.json` | 新增 `blacksmith.weapon` / `blacksmith.armour` 两条升级路径（各 4 级） |
| `scripts/data/HeirloomConfig.cs` | `AllowedBuildings` 追加这两条（否则 P23 ④ 加载期报错） |

成本**照抄一手** `buildings.json` 的 `blacksmith.*` 树：`deed` 8/20/32/44 + `crest` 8/21/35/49。
🔴 `building` 字段**用树 id 而不是建筑名** ⇒ 与 `hero_upgrades.json` 的 `prerequisites.tree_id` **逐字对得上**。
🔴 `effect` 全 0 是**正确**的：铁匠铺的效果**就是它的等级本身**（`HeirloomStock.LevelOf` ⇒ `HeroGear` 读它判前置）。

### 内核（零 Godot）
| 文件 | 行数 | 职责 |
|---|---:|---|
| `scripts/gameplay/sim/run/HeroGear.cs` | 🆕 259 | `HeroGear` 纯判定（`Why` / `TreeOf` / `GoldCostOf` / `BuildingLevelOfCode`）+ `HeroGearState` 可变持有者 |
| `scripts/gameplay/sim/board/UnitRuntime.cs` | 254 | 加 `GearProtOverride` / `GearDodgeOverride` / `ApplyGearTier` / `EffectiveDodge`；`EffectiveProt` 改为覆盖优先 |
| `scripts/gameplay/sim/run/HeroProjection.cs` | +20 | `ApplyGearTier(hero, unit)`（与 `ApplyLevel` 同法：运行时投影、不改基准） |
| `scripts/gameplay/sim/pipeline/HitStep.cs` | +4 | `Base.Dodge` ⇒ `EffectiveDodge` |
| `scripts/gameplay/sim/director/BattleProjector.cs` | +2 | 同上（纪律 V：面板预测值 == 实际结算值） |

### scene
| 文件 | 改动 |
|---|---|
| `scripts/gameplay/scene/ExpeditionContext.cs` | 加 `Gear` / `Upgrades` 跨趟绑定口 |
| `scripts/gameplay/scene/ExpeditionComposition.cs` | 加载 `hero_upgrades.json`（`Validate` 传外部树目录）+ `BindGear` + `[片5·H-1]` 打印 |
| `scripts/gameplay/scene/DirectorBridge.cs` | 调 `HeroProjection.ApplyGearTier` |

### 测试
`tests/HeroGearTests.cs`（🆕 130 行 / 4 条）+ `WeaponDamageModelStage1Tests` 的守卫改写。

---

## §4 🔴 行为变更（用户裁定「直接接线」）

`reports/top_level_vs_tier0_consistency.md` §1 实测的**顶层 vs 第 0 阶**差异，接线后**真的生效了**：

| 单位 | 顶层 prot | 护甲第 0 阶 prot | 顶层 dodge | 护甲第 0 阶 def_pct |
|---|---:|---:|---:|---:|
| warrior | 8 | **0** | 10 | 10 ✓ |
| tank | 12 | **0** | 5 | 5 ✓ |
| medic | 4 | **0** | 10 | **0** |
| commissar | 5 | **0** | 10 | 10 ✓ |

⇒ **玩家单位 prot 全部落 0**（4/4）、medic dodge 10→0。
🔴 **敌人零影响**（无 5 阶 ⇒ `TierDefence` 退回顶层，既有语义 ✓）—— 由用例 4 钉住。

⚠️ **伤害半仍未接**：`WeaponBaseDamage` 的硬前置 ②（23 个技能的 `dmg%`）**数据还没给**
（策划只给了 `smite 0%` / `zealous_accusation −40%` 两个试点值）⇒ 保持 0 生产调用点，由守卫钉住。

---

## §5 验证

| 项 | 结果 |
|---|---|
| 双工程构建 | ✅ 0 错误（测试工程 + Godot 主工程） |
| `HeroGearTests` | ✅ 4/4 |
| `TierDefenceTests` / `WeaponDamageModelStage1Tests` | ✅ 全绿（含改写后的守卫） |
| 全量回归 | 见 §6 |
| 三扫 | 数字 0（2 处已按「结构性常量」登记白名单）· 死函数 0（删掉冗余 `TreeIdOf`）· 死键 0 |
| 内核零 Godot | ✅ 0 hits（含 O-84） |
| 文件尺寸 | 新增文件最大 263 行（≤400 ✅） |

---

## §6 全量测试的诚实交代

**859 通过 / 3 失败**（总数 862 = 基线 858 + 新增 4 条 ⇒ 通过数正好 +4，一条不多一条不少）。

| 用例 | 性质 | 说明 |
|---|---|---|
| `E2UI_Resources_RecomputableFromEventStream` | **既有失败** | 口粮 10 vs 11（疑似真缺陷，待修） |
| `M6Acceptance_WinRateBand_And_Rhythm` | **既有失败** | 死门 **0.48/场** > 0.4 —— 与接线前**逐位一致** ⇒ 本刀未放大 |
| `V5_35_SingleUnitDamageDelta_ConvergesBothSides` | **既有失败** | 特质伤害 13.8% vs 10%±3pp |

`NoProductionCodeCallsTheNewModel_...`（**守卫**）在本刀中途变红一次 —— 它检测到 `TierDefence`
被 `UnitRuntime.cs` 消费了，**这正是"接线成功"的信号**（文档原话：机制在、接线等解冻）。
已从「必须无人消费」改写为**正向断言**（减伤侧必须被消费 / 伤害侧必须仍无人消费）⇒ 现在绿 ✓

⇒ **没有一条是我引入的新回归**。

---

## §7 遗留（有界、自证）

1. **伤害半未接**：缺 23 个技能的 `dmg%`（硬前置 ②）。
2. **UI 未接**：`HeroGearState.TryUpgrade` 已可用，但**没有 UI 入口**（玩家在哪点升级）——目前只能由代码调用。
3. **存档未含阶**：`HeroGearState` 是跨趟状态，但 Phase 1 的 `SaveSnapshot` **没带它**（退出即丢阶）。
4. **铁匠铺建筑无 UI**：`heirlooms.json` 有升级路径了，但 UI 里没有"升级铁匠铺"的入口 ⇒ 建筑等级仍只能靠代码推。
5. **周循环（week）/ H-2 技能阶**：按裁定本刀不做。

---

## 下一刀建议

**给英雄装备阶加 UI 入口 + 存进存档**（把 2、3 两条遗留关掉，H-1 才闭环），
或按 Phase 2 排期进 **周循环 + H-2 技能阶**。
