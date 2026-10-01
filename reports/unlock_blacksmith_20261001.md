# 铁匠铺解锁落地（2026-10-01 · §39 解冻窗口第一件）

**用户裁定（2026-10-01）**：「铁匠铺加一条解锁」＋「铁匠铺加一条解锁解冻窗口」⇒ 本件 = ① 加一条解锁（铁匠铺两条升级树）② 同时开启 §39 解冻窗口（后续包按解冻口径四件推进）。

## 一、解冻条目（`darkest/data/unlocks.json` 第 3 条）

- `id = unlock_blacksmith` · `required_runs_finished = 3` · `required_battles_won = 0`；
  解锁对象 = `building:blacksmith.weapon` ＋ `building:blacksmith.armour`（**两条升级树 id**，不是建筑名 `blacksmith`）。
- 🔴 **为什么用树 id**：`HeirloomStock.LevelOf / NextLevel / CanUpgrade` 的键 = `heirlooms.json` 的 `building` 字段
  （与 `hero_upgrades.json` 的 `prerequisites.tree_id` 逐字一致 ⇒ 是 **H-1 前置**）⇒ 传建筑名 `blacksmith` 在这三个口上会**抛**（P23 ④）✓;
  `HeirloomConfig.AllowedBuildings` 已含两条树 id ⇒ `UnlocksConfig.Parse` 白名单**无需改契约** ✓
- 🔴 **「按趟数解锁建筑」是我方自创**（原版无对应 · 红线 30 · `#423` ⑨）⇒ 阈值来源见下表（一手只给"完成几个任务"，折算成 `required_runs_finished`）。

### 一手证据（E 盘 `campaign/town/buildings/*.building.json` 的 `requirements.number_of_quests_finished`）

| 建筑 | 一手阈值 | 一手 `highest_dungeon_level` | 我方现状 |
|---|---|---|---|
| abbey | 2 | 0 | 3（**不一致 · 只登记**） |
| **blacksmith** | **3** | 0 | 🆕 **3**（本次落地） |
| camping_trainer | 0 | 2 | —（未接线） |
| guild | 3 | 0 | —（未接线） |
| nomad_wagon | 0 | 0 | —（未接线） |
| sanitarium | 4 | 0 | —（未接线） |
| stage_coach | 0 | 0 | 硬编码兜底 `bId == "stagecoach"`（等价） |
| statue | 0 | 0 | —（未接线） |
| tavern | 2 | 0 | 1（**不一致 · 只登记**） |

⚠️ **tavern / abbey 两处一手不一致** = **只登记、本轮不改**（改数值要走解冻口径；归属策划）。

## 二、代码改动（4 处 · 全部围绕"入口可见 + 可解释"）

| 文件 | 改动 |
|---|---|
| `darkest/data/unlocks.json` | 🆕 第 3 条 `unlock_blacksmith`（上表阈值 + 出处注） |
| `darkest/scripts/ui/HamletRoot.Build.cs` | `upgradable` / `buildingNames` 扩为 **5 条**（+ 铁匠铺·武器 / 护甲）；`ddIdx` 加 `"blacksmith.weapon" => 1`（骨架 `DDNav1_blacksmith` 就是为它留的槽），取到槽时**清掉骨架占位**（`PurposeLabel` + `NavPlaceholder`），否则占位与真按钮**重叠**（= `#319①` 同族）；护甲轴无槽 ⇒ `-1` 直接追加并**留痕**；计数行分母改 `upgradable.Length`（此前写死 3） |
| `darkest/scripts/ui/HamletRoot.BuildingPopup.cs` | `RefreshBuildingPopup` ＋ `ShowBuildingInfo` **两处** func switch 各加两条（红线 21：不留不可解释的 `—`）；悬停行建筑名改取**同一份清单** `_buildingIds/_buildingLabels`（不抄第二份） |
| `darkest/scripts/ui/HamletRoot.Refresh.cs` | 建筑等级摘要行加入两条树（`LevelOf` 键命中 `heirlooms.json` 的 `building` ⇒ 合法） |

## 三、读数（最低限度验证 · 用户约束）

| 项 | 命令 | 读数 |
|---|---|---|
| 用例 ① | `dotnet test Darkest.Tests.csproj -p:DarkestTargetFramework=net10.0 --filter "FullyQualifiedName~UnlocksConfigTests\|FullyQualifiedName~UnlockConsumptionTests\|FullyQualifiedName~NextUnlockProgressionTests"` | **全绿**（UnlocksConfigTests 断言 2⇒3 条 / 阈值 1,3,3；UnlockConsumption 第 3 趟 ⇒ 两条树已解锁） |
| 用例 ② | 同上（第 3 趟那三条新断言） | ✅ `UnlockedBuildings` 含 `blacksmith.weapon` / `blacksmith.armour` |
| 构建 | `dotnet build Darkest.csproj -p:DarkestTargetFramework=net10.0 -m:1 -nodeReuse:false -tl:off -v:q` | **0 错**（71 警告 · 均为既存 nullable/未用字段，与本次无关） |

## 四、deviation（已登记 `O-105`）

- 🔴 原版铁匠铺 = **一栋建筑内两个页签**（武器 / 护甲）；我方弹窗一次只显示一条树 ⇒ 暂以**两个 nav 入口**表达。
- 🔴 本次只让**建筑树等级**可买（H-1 的前置门槛）；**英雄装备阶**那层仍断（`HeroGearState.TryUpgrade` 生产调用 **0 处** ⇒ `P4 ②` / `M6u` ⇒ 玩家仍买不到"英雄的阶"）。

## 五、§39 解冻窗口 = 已开启（后续包）

- **P0 数据请单**：23 个技能的 `dmg%`（口径：一手 E 盘 `heroes\<name>\<name>.info.darkest` 的 `.dmg`；只做现有 4 职业池内技能，逐条带出处）—— **零依赖，当天可发**。
- **P2（M1c 阶段 3 切默认）**：走解冻口径四件（逐条登记 + 原版证据 / 一次只改一类 / 前后读数对照 / 改完回冻结）。
- **P3（`§39` 解冻清单三项）**：buff 原语层（1801→2020 · 契约与数据必须同批）· 英雄 5 阶表 · A2 技能 `dmg%` 14 条。
- **P4 H-1 收尾**：`HeroGearState` 进 `SaveSnapshot`（本次已随存档 v2 落地）· 铁匠铺升级 UI（红线 18/21）。

📄 关联：`doc/architecture/open_issues.md`（`O-105`）· `doc/state.md`（决策 496）· `darkest/data/unlocks.json` ✓

