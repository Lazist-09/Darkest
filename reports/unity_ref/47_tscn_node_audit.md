# `.tscn` 节点名审计：**规范良好** —— 8 处"不存在的节点名"**全部是运行时创建的**

> 🕒 2026-09-26 · 补齐 `35_camel_case_audit.md §5` 的"`.tscn` 未扫" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 📊 审计读数（31 个 `.tscn` · 367 个节点名）

```
✅ **同字母序列、不同大小写分界**：🔴 **只有 1 组** —— `Name` vs `name`
   （`ui/slot_row.tscn` 用 `Name` · `ui/unit_card.tscn` 用 `name`）
   ⇒ 📌 **两个不同文件里的两个节点** ⇒ ⚠️ **不是"同一概念两种写法"** ✓
      🎖️ 判据：**"这两个名字在【同一个容器里】吗？"** —— 不同文件 ⇒ 各自独立 ✓
✅ **下划线 vs 无下划线**：**同一组**（`Name`/`name` 在归一化后也相同）⇒ 无新的 ✓
⇒ 🎖️ **即：367 个节点名的命名是【自洽的】** ✓
```

## 2. 🔴 跨文件交叉：**140 条代码引用里有 8 条指向"不存在的节点名"**

```
📊 代码侧 `GetNodeOrNull<…>("path")` 共 **140** 条 ⇒ 逐条比 `.tscn` 的节点名：
   🔴 **8 条末段【在任何 `.tscn` 里都不存在】**：
      `BattleMargin/BattleCol` · `BattleMargin` · `DungeonHost` · `HeroEquipmentRow` ·
      `HeroTrinketGrid` · `Hp` · `PopupUpgrade` · `Stress` ✓
🔴 **若不深究，会写成"8 处静默失联"** ⚠️ —— 而**那是错的** ✓
```

## 3. 🎖️🎖️ 查证后：**8/8 全部是【运行时代码创建】的节点**（且有 null 守卫）

| 节点名 | 创建点（`Name = "X"`） |
|---|---|
| `BattleMargin` | `ui/BattleUI.Build.cs:61` `new MarginContainer { Name = "BattleMargin" }` |
| `BattleCol` | `ui/BattleUI.Build.cs:69` `new VBoxContainer { Name = "BattleCol" }` |
| `DungeonHost` | `ui/BattleUI.Dungeon.cs:34` `Name = "DungeonHost"` |
| `HeroEquipmentRow` | `ui/HamletRoot.HeroDetail.cs:205` `new HBoxContainer { Name = "HeroEquipmentRow" }` |
| `HeroTrinketGrid` | `ui/HamletRoot.HeroDetail.cs:223` `new GridContainer { Name = "HeroTrinketGrid", Columns = 2 }` |
| `Hp` | `ui/BattleUI.StatusTray.cs:102`（**null 分支里创建**）`new ProgressBar { Name = "Hp" }` |
| `PopupUpgrade` | `ui/HamletRoot.BuildingPopup.cs:150` `Name = "PopupUpgrade"` |
| `Stress` | `ui/BattleUI.StatusTray.cs:113`（**null 分支里创建**）`new ProgressBar { Name = "Stress" }` |

```
🎖️ **而调用点全部【正确 guarded】**（逐处读过）：
   · `Hp` / `Stress` ⇒ `if (hp is null) { hp = new ProgressBar { Name = "Hp", … }; slot.AddChild(hp); }`
      ⇒ 📌 **"场景里没有就地造一个"** ⇒ ✅ **这是【有意设计】** ✓
   · `BattleMargin/BattleCol` ⇒ `if (col is not null) { … }`（**纯诊断打印**）✓
   · `DungeonHost` / `HeroEquipmentRow` / `HeroTrinketGrid` ⇒ `public …? X => GetNodeOrNull<…>("X")`
      ⇒ 🎖️ 而**骨架类里明写**（`BattleBottomBarSkeleton.cs:45`）：
        `/// 实例化骨架；场景缺失/类型不符 ⇒ null（**宿主回落代码构建，不崩不静默**）✓`
        ⇒ 📌 **即：null ⇒ 回落到代码构建** ⇒ ✅ **这是本仓【有意的双轨设计】** ✓
   · `PopupUpgrade` ⇒ `if (up is null) { GD.Print("…弹窗里没有升级按钮（没打开？）⇒ 未升级"); return; }`
      ⇒ ✅ **有日志 + 早退**（**不静默**）✓
⇒ 🎖️ **即：8/8 都不是缺陷** —— 而是**"场景优先 + 代码回落"**的一个**已知模式** ✓
```

## 4. 🎖️🎖️ 本件的方法价值：**"跨文件名字对不上"有两种，必须分开**

```
📊 同一形状的两种结局（本任务里都遇到过）：
   🔴 **A 类：名字对不上 = 真失联**
      · `heirlooms.json` 的 `stagecoach` vs `buildings.json` 的 `stage_coach`（`34_*.md`）
      · `Effects.txt` 引用但不存在（那 190 条孤儿里的一部分）
   ✅ **B 类：名字对不上 = 运行时才存在**
      · 本件的 8 个节点（**代码里 `Name = "X"` 造出来的**）✓
⇒ 🎖️ **判据**：**"这个名字对不上 —— 是【目标不存在】还是【目标在运行时才创建】？"** ——
   ✅ **去代码里搜 `Name = "X"`**（**不是搜 `name="X"`**）✓
   📌 **而这条判据能【避免一次误报】**：若不搜创建点，本件会报"8 处静默失联" ⚠️
      🎖️ **与前面同族**（度量与目标不匹配），但这次是**"只看了静态的一半"** ✓
      ⇒ ✅ **共同形状：凡"按名字找东西"，先问【这个名字有几种来源】** ✓
```

## 5. 🎖️ 顺带：我方这套"场景 + 代码回落"模式**值得肯定**

```
🎖️ **实测到的设计（散在 4 个骨架类里）**：
   ① `TryInstantiate()` ⇒ `GD.Load` 失败 ⇒ **返回 null**（不抛）
   ② `Instantiate<T>()` 换 `as`（**注释明写"在类型不符时会抛 InvalidCastException"**）✓
   ③ 场景缺失 ⇒ **日志 + 代码构建回落**（**"不崩不静默"**）✓
   ④ 节点缺失 ⇒ `GetNodeOrNull` + null 分支里**就地创建** ✓
⇒ 🎖️ **即：这套模式【既容错、又不静默】** —— 而我方的口径是"**能挡就别自建**"
   ⇒ 📌 **这里是"必须自建"的场合**（UI 骨架要能在场景缺失时存活）✓
   🎖️ **判据**：**"这个回落，是【掩盖问题】还是【设计的一部分】？"** ——
      **有日志 + 有明确默认 ⇒ 设计** ✓（本件 8/8 都符合）✓
```

## 6. 诚实边界

```
✅ **能验**：**31 个 `.tscn` / 367 个节点名** · **命名冲突只 1 组且非同概念** ·
   **140 条代码引用 / 8 条对不上** · **8/8 都有 `Name = "X"` 创建点** ·
   **调用点逐处读过（null 守卫）** · **骨架类的"不崩不静默"注释原文** ——
   **全部当场跑出** ✓
🎖️ 并**避免了一次误报**（8 处"失联"实为"运行时创建"）✓
🔴 **不能验**：**那 8 个创建点在运行时的【可达性】未验证**（如"弹窗没打开时当然没有按钮"）
   ⇒ 📌 从代码看**都有意义**，但**未实跑** ⇒ 记**未测** ✓
🔴 **不能验**：**本件只比了"节点名"** ⇒ 未比**类型是否匹配**（`GetNodeOrNull<T>` 的 T）⇒ 记**未做** ✓
🔴 **不能验**：`Name`/`name` 那 1 组**是否真无影响**（两个不同文件）⇒ 记为**推断** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/tscn_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **节点名审计【完成】**：命名自洽 · 8 处"失联"查证为"运行时创建" ✓
🆕 **可做**：① 比 `GetNodeOrNull<T>` 的**类型**是否与 `.tscn` 的节点 type 匹配
   ② 核那 190 条 effect 孤儿里"只在本地化里被提"的（`46_*.md` 记的局限）
   ③ 把"名字对不上分两类"这条判据写进 `observe_list` ✓
⏸️ **等策划**：八张单已投递 ✓
```
