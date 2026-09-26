# 命名一致性审计：**新发现 1 处**（`stage_coach` vs `stagecoach`）+ 传家宝 4 对为已知

> 🕒 2026-09-26 · 工具 `tools/dsh/audit_naming_consistency.py`（**新**，可重跑）·
> 产物 `reports/unity_ref/our_naming_audit.json` ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 为什么要**单独**查命名一致性（而不是靠宽面审计）

```
🔴 `33_*.md §4` 我自己记的：**宽面审计（`audit_all_refs.py`）为了让引用通过，
   会把两种写法都收进并集** ⚠️ ⇒ 📌 **"审计说闭合" ≠ "没有不一致"** ✓
⇒ ✅ 本件**专门查不一致**，三把筛子：
   ① **同一概念、下划线写法不同**（`stage_coach` vs `stagecoach`）✓
   ② **复数 / 单数并存**（传家宝那 4 对）✓
   ③ 编辑距离 1 的近邻（**本件实现了但未输出**，见 §4 诚实边界）✓
📊 扫了 **26 个 json · 866 个标识符形状的字符串** ✓
```

## 2. 🔴 新发现：**`stage_coach` vs `stagecoach`**（宽面审计**没**报它）

```
🔴 实测：
   · `buildings.json` 的建筑 id = `stage_coach`（**带下划线**）✓
   · `heirlooms.json → upgrade_paths[].building` = **`stagecoach`**（**不带下划线**）⚠️
   ⇒ 📌 **两者都指向"驿站马车"这同一栋建筑** ✓
🎖️ **而宽面审计为什么没报**？因为它**只查引用是否存在**，
   而 `stagecoach` **恰好也是合法的**（`economy.json` 里另有一个 `stagecoach` 段）⇒
   🔴 **并集把它吃掉了** ⚠️ ⇒ ✅ **本件独立查出** ✓
```

## 3. 🎖️ 但查证后：**它不是缺陷**（是**刻意且被白名单保护的**）

```
✅ **关键证据**（`HeirloomConfig.cs:53`）：
   `public static readonly IReadOnlyList<string> AllowedBuildings =
       new[] { "tavern", "abbey", **"stagecoach"** };` ✓
   ＋ `:102` 校验：`if (!AllowedBuildings.Contains(p.Building)) ⇒ 抛` ✓
   ＋ `:81` 查询：`PathFor(string building) => UpgradePaths.FirstOrDefault(p => p.Building == building)` ✓
⇒ ✅ **即：`heirlooms.json` 的 `building` 用的是【它自己那一套 id】（白名单定义）**，
   **与 `buildings.json` 的建筑 id 【不是同一套】** ✓
   🎖️ **而那两个名字指的是【不同的东西】**（这才是关键）：
      · `buildings.json → stage_coach` = **建筑的升级树**（`stage_coach.rostersize` 等 5 档）✓
      · `heirlooms.json → stagecoach` = **传家宝升级路径**（`axis: unlock` · 3 级 ·
        `roster_cap_delta 2` / `rookie_level 2`）✓
      · `economy.json → stagecoach` = **马车参数**（`recruit_cost` / `rookie_level` /
        `roster_cap_by_level` / `num_recruits_by_level`…）✓
      ⇒ 📌 **三处名字相近，但各自是【独立的表】** ⇒ 🔴 **不是"同一概念两种写法"** ✓
⇒ 🎖️ **判据**：**"这两个名字，是【同一概念两种写法】还是【两个独立的表】？"** ——
   **看它们有没有【各自的定义 + 各自的消费点】** ✓
   · `stage_coach`（buildings）⇒ 有 id 定义 + 有升级树 ✓
   · `stagecoach`（heirlooms）⇒ 有**白名单**定义 + 有 `PathFor` 消费 ✓
   · `stagecoach`（economy）⇒ 有 `StagecoachConfig` + **58 处引用** ✓
   ⇒ ✅ **三者各有定义、各有消费 ⇒ 是三个东西** ✓
```

## 4. 🎖️ 与传家宝那 4 对的**关键区别**（这是本件最有价值的对照）

```
📊 **传家宝 4 对**（`bust`/`busts` 等）：
   · 复数 `busts` 只在 **`heirlooms.json`** 出现（`kinds` 定义侧）✓
   · 单数 `bust` 在 **`buildings.json` + `heirloom_exchange.json` + `heirlooms.json`** 出现 ✓
   ⇒ 🔴 **它们【指的是同一样东西】**（同一种传家宝）⇒ **是真不一致** ✓
   ⇒ ✅ **而它已被一座【有文档、已接线】的桥处理**（`33_*.md` 已结案）✓
📊 **stagecoach 那 3 处**：
   ⇒ ✅ **指的是三种不同的东西** ⇒ **不是不一致** ✓
⇒ 🎖️ **即：同样是"命名相近"，结论相反** —— 判据就是 §3 那条 ✓
   📌 **而且本件的筛子【两种都会报出来】** ⇒ ✅ **报出来再判**，而不是**筛掉** ✓
      🎖️ **这正是"宁可多报、不可漏报"**（因为**筛掉 = 静默**）✓
```

## 5. 诚实边界

```
✅ **能验**：26 文件 / 866 标识符 · **`stage_coach` vs `stagecoach` 的完整取证**
   （三处各自的定义 + 消费点 + `AllowedBuildings` 白名单原文）·
   传家宝 4 对的分布 · **两者的结论相反及其判据** —— **全部当场跑出** ✓
🎖️ 并**验证了"宽面审计会漏掉这类问题"**（`stagecoach` 因另有一处合法定义而被并集吸收）✓
🔴 **不能验**：**筛子③（编辑距离 1 的近邻）虽实现但未输出** ⚠️
   ⇒ 📌 我把它的结果并进了筛子①②（避免重复）⇒ ✅ **如实记为"未单独输出"** ✓
🔴 **不能验**：本件**只扫了"标识符形状的字符串"**（`^[a-z][a-z0-9_]*$`）⇒
   ⚠️ **驼峰式（`StageCoach`）没扫** ⇒ 而实测**代码侧驼峰出现 4 处** ⇒ 记**未覆盖** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 若统一命名后行为如何**完全未测** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/stagecoach_*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **命名一致性线**：新发现 1 处，**查证后判为"三个不同的东西"** ⇒ **无需动作** ✓
🆕 **可做**：① 补扫驼峰式（`StageCoach` vs `Stagecoach` —— 实测代码侧两种都有）
   ② 读 `StressHealSelf`（90 行，最长的 case）
   ③ 看 `RaidSceneMultiplayerManager` 的 11 处 act-out ✓
⏸️ **等策划**：七张单已投递 ✓
```
