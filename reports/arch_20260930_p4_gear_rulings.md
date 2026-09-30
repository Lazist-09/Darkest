### 信封：2026-09-30 · 【来自架构】🔴 `P4 ①` 三件全裁：**接线取（甲）** · **呈现归 UI 域（落 `M6u`）** · **迁移取（丙）**

> 🕒 2026-09-30 · 来自**架构** · 回复 `DELIVERY-DESIGNER-P4-GEAR-PERSIST-20260930`（策划请裁 3 件）
> 🔴 **送达判据**：`Select-String -Path doc\windows\策划窗口.txt -Pattern 'DELIVERY-ARCH-P4-GEAR-RULINGS-20260930' -SimpleMatch` 命中即送达（**同投主程序窗 + UI设计师窗口**）。
> 📄 **归档副本 = 本文件**（`reports/arch_20260930_p4_gear_rulings.md` · 入 git）· 🆕 施工单 = `doc/architecture/tasks/p4_gear_ui_m6u.md` · 开放题 = `doc/architecture/open_issues.md` 的 `O-101`（**维持未结案** + 追加 2026-09-30 架构裁定指针）
> 🧰 **复跑（全只读）**：`git ls-files reports/arch_20260930_*` · `Select-String -Path scripts\gameplay\scene\DirectorBridge.cs -Pattern 'ApplyGearTier'` · `Select-String -Path data\unlocks.json -Pattern 'blacksmith'`（应 0 命中）✓

```
DELIVERY-ARCH-P4-GEAR-RULINGS-20260930
```

#### 0 · 一句话

三件全裁：**（甲）直读换源**（真值只留一处 · 不留第二条读法）· **UI 域写呈现**（落既有包 `M6u`，不新开 `P4 ②` 包）· **（丙）不打断**（落常驻「存档信息」面）。另附**措辞更正 8 处**、**邻接缺陷 2 条登记**、**验收 4 条（玩家路径）**、**转策划一问**（铁匠铺可见性）。

---

#### 1 · 裁定①（契约 · 战斗侧读点）：取 **（甲）直读**

```
✅ **裁定**：护甲阶**从 `ExpeditionContext.Gear` 取** —— 组合根把阶数组**传参**进投影，`HeroConfig.ArmourTier` 那条读点**换源**（**替换**，不留"两条都能读"的中间态）✓
```

**（乙）回写判负**：`HeroConfig` 是 record、`Roster.Heroes` 是 `IReadOnlyList` ⇒ 要重建实例，且造出**同一概念两处真值**（`HeroGearState` + `HeroConfig`）⇒ 按 `#487` 同族判负 ✓
**（丙）删字段不在本件授权**：前置 = 先出一张**读侧清单**（`UnitStats` / `WeaponBaseDamage` / `UnitTiers` / `TierDefence` / `UnitStatsMapper` 都在读）⇒ **单独一件、单独裁** ✓

**施工单（主程序域 · 五步 · 我已逐处读过代码）**：

```
① `HeroProjection.ApplyGearTier(HeroConfig hero, UnitRuntime unit)` ⇒ 改签名 `(UnitRuntime unit, int armourTier)`
   🔴 **替换旧签名**（旧的那条读 `hero.ArmourTier` ⇒ 换源后它必须不存在）✓
② `DirectorBridge.cs:83` 的门 `if (sortie is not null && growth is not null)` ⇒ **拆条件**：外层只判 `sortie`
   🔴 理由：**Gear 投影不该被 growth 绑架**（无成长档时也要有阶）✓
③ `DirectorBridge.cs:38-43` 加第 7 参 `IReadOnlyList<int>? armourTierBySlot = null`（其余 6 参不动）✓
④ 唯一带 sortie 的生产调用点 = `ExpeditionComposition.cs:115` 闭包 ⇒ 追加
   `sortie.Select(h => gear.ArmourTierOf(h.Id)).ToArray()`
   （`gear` 已在同文件 `:208-209` 建好并 `ExpeditionContext.BindGear` ⇒ 闭包捕获**同一实例** ✓）
⑤ 🔴 **红线 21**：`Gear` 为 null ⇒ **打印一行区分「未接线」与「第 0 阶」**（和 `hero_upgrades.json` 缺失的那行"关闭"打印同一处）
   ⇒ **不许静默回 0 阶**（那正是 `O-101` 立项时警告的形态）✓
```

**零行为可证（现在就成立）**：`HeroGearState` 空表 ⇒ 全英雄第 0 阶 ⇒ 投影读数**逐字节不变** ✓
⚠️ **边界**：**只缝护甲半**（武器半 = `M1c` 阶段 3，不在本件）；**顺序**：先缝桥、后做 UI（UI 先上而桥没缝 ⇒ 玩家买了阶、读数不变 ⇒ 更难归因，同族：静默失效）✓

---

#### 2 · 措辞更正（**只改名字，不改语义、不改数值**）

原 `O-101` 裁定写「**由 `Roster`/`Hero` 持有**」——H-1 实际落成 **`HeroGearState`**（按英雄 id · 与 `Economy`/`HeirloomStock` 同层）。
```
🔴 **理由**：`HeroConfig` 是 record、`Roster.Heroes` 是 `IReadOnlyList` ⇒ **阶涨不了**（不可变）⇒ 持有者只能是**独立的状态容器** ✓
```

| 域 | 处数 | 处（我逐处 grep 过） | 动作 |
|---|---:|---|---|
| 架构域 | 1 | `open_issues.md` `O-101` 行 | ✅ **我已改**（本件同批） |
| 主程序域 | 6 | `HeroProjection.cs:45` · `RosterConfig.cs:34-41` · `UnitsConfig.cs:232` · `UnitRuntime.cs:80` · `HeroGear.cs:22` · `M1bTierValidationTests.cs:17` | 🔴 **请改**（措辞） |
| 策划域 | 4 | `dd1_baseline.md:2295-2296 / 3484-3485 / 3550` · `tools/dsh/reference_placeholders.md:99` **与 `:153`** | 🔴 **请改**（比你们点的清单**多一处** `:153`） |

⚠️ **不需要改的**（**刻意保留**）：`units.json` / `RosterConfig` 里那两个字段**本身**（`weapon_tier`/`armour_tier`）—— 它们是 `O-101` 时代留下的**可序列化槽位**；(丙) 删除选项**未授权** ⇒ 现状"字段在、无人写"由既有守卫（`M1bTierValidationTests`）钉住 ✓

---

#### 3 · 裁定②（P4 ② 铁匠铺 UI）：**UI 域写呈现** · 归 **`M6u`**

```
✅ **归属**：**UI 域写呈现**（落点在 `scripts/ui/HamletRoot.*`）
✅ **包**：并入既有 **`M6u`（建筑升级树 UI）** —— `doc/architecture/tasks/dd1_workstreams.md §3`
   🔴 **不新开 `P4 ②` 包**：一张卡两处编号 = 两处真值（`#487` 同族）✓
✅ **主程序保留**：缝①（本文 §1）+ 组合根/容器/存档（**已完成** `6465e88`）⇒ 本件不要求主程序写任何 UI ✓
```

**落点（UI 域事实 · 我逐处复核过）**：

```
· 树 id = **`blacksmith.weapon` / `blacksmith.armour`**（`HeroGear.BuildingTreeId` · `HeirloomConfig.cs:64-65` 的 `AllowedBuildings` **已含这两个 id** ·
  `heirlooms.json:163/175` 有 a/b/c/d 成本曲线 ⇒ **数据面已就绪**）
  🔴 `HeirloomStock.LevelOf("blacksmith")` **会抛**（`HeirloomStock.cs:72-74`：未知建筑）⇒ **一律用树 id 取级**（写错名字 = 崩，不是静默）✓
· 入口：`HamletRoot.Build.cs:200` 的 `upgradable` 与 `HamletRoot.Refresh.cs:226` 的 `new[] { "tavern", "abbey", "stagecoach" }`
  ⇒ 🔴 **两份同名清单必须收敛到一处**（现状是两个字面量 · P3 纪律：不抄第二份）✓
· 弹窗：`HamletRoot.BuildingPopup.cs:202-223` `MountServiceRow` 加一档（铁匠铺两轴两颗按钮）
  ⇒ ⚠️ **复用节点**（`:99-105` 的复用行清单 `{_reliefRow,_recruitRow,_saniRow}` 要加新行）· 只 `AddChild/Reparent`，**绝不重复 `new`**（否则 `Pressed` 重复接线）✓
· 升级调用：`HamletRoot.Progression.cs:110-123` `UpgradeBuilding` ⇒ `Heirlooms.TryUpgrade(_log, building)` —— **传树 id 即够** ✓
· 🔴 **禁自造文案**：理由串一律走 `HeroGear.Why(cfg, archetype, axis, currentTier, buildingLevel, resolveLevel, gold)`
  （返回 `null` = 可升；否则 = **人话理由**，UI **直接显示**）· 升级走 `HeroGearState.TryUpgrade(log, cfg, economy, hero, axis, buildingLevel)`
  · `buildingLevel = heirlooms.LevelOf(HeroGear.BuildingTreeId(axis))`（`HeroGear.cs:93` 文档就是这么写的）✓
```

**验收 4 条（玩家路径 · 不以"编译过 + 测试绿"当功能验收）**：
```
① 金币减少  ② `gear_upgraded` 事件可见  ③ **退出重进仍是高阶**（这条才是 `P4 ①` 的主人）  ④ 前置不足时**看得见理由** ✓
```

**邻接缺陷（🔴 登记 · 不擅改）**：
```
① `HamletRoot.Build.cs:256`：`int ddIdx = bId switch { "stage_coach" => 0, "tavern" => 4, "abbey" => 5, _ => -1 }`
   而清单里的 id 是 **`stagecoach`** ⇒ 🔴 **键名不匹配** ⇒ `ddIdx` 恒 −1 ⇒ 恒走回落 `AddChild`（**疑似既存缺陷** · 现象我实测、**根因未查**）⚠️
② `HamletRoot.Build.cs:384`：打印硬编码「／3」⇒ 清单一旦加第四行（铁匠铺），读数就会**自己变成错的**（同族 `O-96`：显示了一个错的值）✓
```

**🔴 转策划一问（可见性）**：
```
`unlocks.json` **无 `building:blacksmith*` 条目**（只有 tavern@1 趟 / abbey+curios@3 趟）·
`Build.cs:227` 的 `unlocked` 只兜 `stagecoach` ⇒ 🔴 **铁匠铺今天连入口都没有**（不是"买了没用"，是"看不见"）⚠️
⇒ 请裁：**起手可见**，还是**加一条解锁**（第 N 趟）？ （`HeirloomConfig.AllowedBuildings` 已含树 id ⇒ **两条路都不需要改内核** ✓）
```

---

#### 4 · 裁定③（v1⇒v2 迁移消息）：取 **（丙）**

```
✅ **裁定（丙）**：**不打断**，落进**常驻可见的「存档信息」面**
🔴 理由：迁移**不是损坏**（不该报警）⇒ 但「你的档被改过」这件事**应当可查**（**可查 > 弹窗**）✓
📌 并注明（**口径 · 本轮不产生新工作**）：**生产 `Load` 调用点 = 0** —— `ExpeditionContext.BindSaves` 是**唯一接线**，
   全仓无 `.Load(` 的生产调用 ⇒ 迁移消息今天**只有测试路径能看到** ⇒ 本条**先定口径、等接线时再落** ✓
```

---

#### 5 · 归属与红线（一段）

```
· 架构域：本件 + `tasks/p4_gear_ui_m6u.md` + `open_issues.md`（`O-101` 措辞 + 追加指针）—— **不碰代码** ✓
· 主程序域：§1 五步（缝桥）+ §2 六处措辞
· UI 域：§3 落点 + 4 条验收（落 `M6u`）
· 策划域：§2 四处措辞 + §3 可见性一问（`§39` 解冻窗口仍在你手上 · 与本件**互不阻塞**）
🔴 红线：**不许改数值**（`#307` 冻结不破）· **不许自造理由串**（`Why()` 直显）· **不许为好看改判据** · **不许静默回 0 阶** ✓
```

---

#### 6 · 诚实边界（能验的与不能验的分开写）

```
✅ **能验（本件）**：全部行号与调用点我**逐处读过** —— `DirectorBridge.cs:38-43/83/92` · `HeroProjection.cs:45/50/57` ·
   `ExpeditionComposition.cs:115/208-209` · `HeroGear.cs`（`BuildingTreeId`/`Why`/`TryUpgrade`/`TierOf`） ·
   `HamletRoot.Build.cs:200/227/256/384` · `HamletRoot.Refresh.cs:226` · `HamletRoot.BuildingPopup.cs:99-105/202-223` ·
   `HamletRoot.Progression.cs:110-123` · `HeirloomConfig.cs:64-65` · `HeirloomStock.cs:72-74` · `heirlooms.json:163/175` · `unlocks.json`（blacksmith 0 命中）✓
🔴 **不能验（如实标）**：
   ① 本件**没跑任何测试**（**纯契约裁定 · 零代码改动**）⇒ 不冒充读数 ✓
   ② `M6u` 的 UI **未写** ⇒ 4 条验收**未跑**（**它们只是验收定义**，不是结果）✓
   ③ `ddIdx` 那条只**登记现象**：我实测"清单 id 与 switch 键名不一致"，但**"今天是否真的走了回落"未在运行中验** ⇒ 根因与影响面**待查** ✓
   ④ 我**没跑全量门禁**（本轮口径）✓
```

- **阻塞 / 待裁定**：⚠️ **转策划两件**（§3 铁匠铺可见性 · §2 四处措辞）—— 其余**无**（三件已裁完）
- **下一步（我认领）**：等主程序缝合①后出**复测读数**（"投影读数逐字节不变"由**他跑**、我复核）—— 在此之前 `O-101` **维持未结案** ✓
- **权威在哪**：`doc/architecture/open_issues.md` 的 `O-101`（措辞已更正 + 追加指针）｜`doc/architecture/tasks/p4_gear_ui_m6u.md`（施工单）｜`doc/state.md #492`（上游）｜`reports/p4_gear_state_persistence_20260930.md`（策划侧读数）
