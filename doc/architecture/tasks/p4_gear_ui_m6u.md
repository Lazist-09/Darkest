# 施工单 · `M6u`（建筑升级树 UI）— `P4 ②` 铁匠铺装备阶

> 🕒 2026-09-30 · 架构（本件随 `DELIVERY-ARCH-P4-GEAR-RULINGS-20260930` 投递）
> 🔴 **归属**：**UI 域写呈现**（落点 `darkest/scripts/ui/HamletRoot.*`）· 主程序只管缝①（`reports/arch_20260930_p4_gear_rulings.md §1`）
> 📄 上游：`doc/state.md #492`（策划 · `P4 ①` 入档）· `doc/architecture/open_issues.md` 的 `O-101`（**维持未结案**）
> 🧰 **验收跑法（全只读）**：`dotnet build darkest/Darkest.csproj -p:DarkestTargetFramework=net10.0`（scene 层编译证据）· 玩家路径 4 条（见 §5）

## §1 这张卡做什么（一句话）

让玩家**在游戏里**把某个英雄的**武器 / 护甲阶**买上去，并且**买之前看得见理由、买之后退出重进还在**。

```
✅ 已完成（主程序 · `6465e88`）：阶有持有者（`HeroGearState` · 按英雄 id）· 入了档（`SaveSnapshot.Gear` · 迁移 v1⇒v2）
🔴 本卡要做：① 入口可见 ② 两颗升级按钮 ③ 理由串直显 ④ 4 条验收
⚠️ 前置（主程序 · 同批）：**战斗侧读点换源**（`reports/arch_20260930_p4_gear_rulings.md §1` 五步）
   ⇒ 顺序：**先缝桥、后做 UI**（UI 先上而桥没缝 ⇒ 玩家买了阶、读数不变 = 更难归因）✓
```

## §2 数据面（**已就绪 · 不需要新表**）

```
· 树 id = **`blacksmith.weapon` / `blacksmith.armour`**
  —— `HeroGear.BuildingTreeId(axis)`（`scripts/gameplay/sim/run/HeroGear.cs:66`）✓
· `HeroGear` 树×级（`hero_upgrades.json`）· 前置 = 本树前一级 + `blacksmith.*` 建筑等级 a/b/c/d ✓
· `heirlooms.json:163/175`：两条 `blacksmith.*` 成本曲线（deed 8/20/32/44 · crest 8/21/35/49）✓
· `HeirloomConfig.AllowedBuildings`（`scripts/data/HeirloomConfig.cs:64-65`）**已含这两个 id** ✓
🔴 `HeirloomStock.LevelOf("blacksmith")` **会抛**（`HeirloomStock.cs:72-74`）⇒ **一律传树 id**，不要传"blacksmith" ✓
```

## §3 落点（**UI 域** · 逐处 · 我复核过）

```
① 入口清单（🔴 **两份字面量必须收敛到一处**）
   · `HamletRoot.Build.cs:200`：`string[] upgradable = { "tavern", "abbey", "stagecoach" }`
   · `HamletRoot.Refresh.cs:226`：`new[] { "tavern", "abbey", "stagecoach" }`
   ⇒ 加铁匠铺 = 两处同时改（**或先收敛成一份**）· 配套 `buildingNames` 同步 ✓
② 可见性：`HamletRoot.Build.cs:227` `unlocked = bId == "stagecoach" || unlockedBuildings.Contains(bId)`
   🔴 `unlocks.json` 今天**无 `building:blacksmith*` 条目** ⇒ **两轴都不可见** ⇒ 见 §6 待策划一问（**起手可见 vs 加解锁**）✓
③ 弹窗服务行：`HamletRoot.BuildingPopup.cs:202-223` `MountServiceRow` 加一档（`blacksmith.weapon` / `blacksmith.armour`）
   · ⚠️ **复用节点**：`:99-105` 的复用行清单 `{_reliefRow,_recruitRow,_saniRow}` **要加新行**（否则复用行会被 `QueueFree`）
   · 只 `AddChild / Reparent`，**绝不重复 `new`**（重复 `new` ⇒ `Pressed` 重复接线 = 一次点击升两阶）✓
④ 升级动作：`HamletRoot.Progression.cs:110-123` `UpgradeBuilding` ⇒ `h.TryUpgrade(_log, building)`
   （`h = ExpeditionContext.Heirlooms` · `HeirloomStock`）⇒ **传树 id 即够**（`AllowedBuildings` 已含两个树 id）✓
⑤ 🔴 **禁自造文案** —— 理由串一律直显内核返回值：
   · `HeroGear.Why(cfg, archetype, axis, currentTier, buildingLevel, resolveLevel, gold)`
     （`null` = 可升；否则 = **人话理由**，UI **直接显示** · `HeroGear.cs:96-104`）✓
   · 升级：`HeroGearState.TryUpgrade(log, cfg, economy, hero, axis, buildingLevel)`（`HeroGear.cs:202-208`）✓
   · `buildingLevel = heirlooms.LevelOf(HeroGear.BuildingTreeId(axis))`（`HeroGear.cs:93` 文档同款取法）✓
```

## §4 邻接缺陷（🔴 **登记 · 不擅改**）

```
① `HamletRoot.Build.cs:256`：`int ddIdx = bId switch { "stage_coach" => 0, "tavern" => 4, "abbey" => 5, _ => -1 }`
   而清单里的 id 是 **`stagecoach`** ⇒ 🔴 **键名不匹配** ⇒ `ddIdx` 恒 −1 ⇒ 恒走回落 `AddChild`
   （**现象我实测 · 根因未查**：今天是否真的每次都走回落 = **未在运行中验**）⚠️
② `HamletRoot.Build.cs:384`：打印硬编码「／3」⇒ 清单加第四行后读数**自己变错**（同族 `O-96`）✓
```

## §5 验收（**4 条 · 玩家路径** · 不以"编译过 + 测试绿"当功能验收）

```
① 进城 → 铁匠铺 → 点"升武器 / 升护甲" ⇒ **金币减少** ✓
② 同一动作 ⇒ `gear_upgraded` 事件**可见**（事件流/日志）✓
③ **退出重进 ⇒ 仍是高阶**（**这条才是 `P4 ①` 的主人**：存档 v1⇒v2 的语义）✓
④ 前置不满足（钱不够 / 建筑等级不够 / 已满级）⇒ **看得见理由**（`Why()` 的串**原样显示**）✓
+ 门禁：`--ui-audit`（`M6u` 既有验收：**0 重叠 / 0 透明**）✓
```

## §6 边界与待裁

```
· 🔴 **不碰内核**：不改 `HeroGear`/`HeroGearState`/`SaveController`（主程序域）· 不新增数据表 ✓
· 🔴 **不改数值**（`#307` 冻结不破）· **不自造理由串** · **不为好看改判据** ✓
· ⚠️ **待策划一问**：铁匠铺**可见性** —— **起手可见**，还是**加一条 `unlocks.json` 解锁**（第 N 趟）？
  （两条路都**不需要改内核**：`AllowedBuildings` 已含两个树 id）✓
· ⚠️ **文案**：建筑名沿用 `_buildingLabels`（**不抄第二份**）· 两轴按钮文案 = "升武器 / 升护甲"（贴 `HeroGear.AxisSuffix` 的语义）✓
```
