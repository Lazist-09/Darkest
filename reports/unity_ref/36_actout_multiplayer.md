# 联机侧的 act-out：**是同一 switch 的第二份实现** —— 而 `HealSelf` 两处不同

> 🕒 2026-09-26 · 承接 `32_actout_switch_detail.md §5` 的"联机那 11 处仍未看" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🎖️ 先订正**我上一件的一处读数错**（第 10 次同族）

```
🔴 `32_*.md` 我写：`StressHealSelf` **90 行**（"最长的一个 case"）⚠️
✅ **精确切块后实测**：它只有 **10 行** ✓
🔴 **根因**：我那个脚本的切块逻辑是
   `end = starts[k+1][0] if k+1 < len(starts) else **i + 90**`
   ⇒ 📌 **`StressHealSelf` 是【最后一个 case】** ⇒ 走了 `i + 90` 的**兜底** ⚠️
   ⇒ 🔴 **我把自己填的兜底值（90）当成【实测长度】写进了报告** ⚠️
🎖️ **判据（本件新增）**：**"这个数是【量出来的】还是【我手里的默认/兜底值】？"** ——
   ✅ **输出前必须能指出【它是从哪来的】** ✓
   📌 **与前面几次同族**（度量与目标不匹配），但这次更具体：
      **不是"量错了"，而是"把它当量了"** ✓
      🎖️ **共同形状**：**报告里的数，必须是【从数据里读出来的】，不能是【代码里填的】** ✓
```

### ✅ 精确切块后的**真实行数表**

| `case` | 行数 | | `case` | 行数 |
|---|---|---|---|---|
| `AttackSelf` | **49** | | `BuffParty` | 14 |
| `ChangePosition` | **39** | | `StressHealParty` | 13 |
| `RandomCommand` | 36 | | `MarkSelf` | 10 |
| `BarkStress` / `BuffAlly` / `HealSelf` | 16 | | **`StressHealSelf`** | **10** |
| | | | **`IgnoreCommand`** | **7**（最短）|

⇒ 🎖️ **最长的是 `AttackSelf`（49 行）**，不是我上次说的 `StressHealSelf` ✓

## 2. 🎖️🎖️ 而联机侧：**是同一 switch 的第二份完整实现**

```
📊 实测：
   `RaidSceneManager.cs`（单机）          ⇒ **11 个 case** ✓
   `RaidSceneMultiplayerManager.cs`（联机）⇒ **11 个 case** ✓
   🎖️ **两者 case 集合【完全相同】**（逐名比对 True）✓
   ＋ 两者都是 `RandomSolver.ChooseByRandom(actionHero.Trait.StartTurnActs)` + `switch` ✓
⇒ 📌 **即：这套 act-out 逻辑在参考项目里【被复制了两份】** ⚠️
   🎖️ **对本任务的意义（重要）**：
      ✅ **采用时【只需抄一份】**（两处逻辑是重复的）⇒ **不用抄两遍** ✓
      🔴 但**必须知道有两份** ⇒ 否则**将来只改一份 ⇒ 两处行为分叉** ⚠️
      📌 **判据**：**"这段逻辑在参考里有几份？"** —— 两份 ⇒ **抄一份，但要记下"另一份存在"** ✓
```

## 3. 🔴🔴 而两处**真的有一处不同**：`HealSelf`

```
🔴 **单机**（`RaidSceneManager.cs:3048-3063`）：
   `if (actionHero.HealthRatio == 1) break;`
   `int healAmount = actionHero.**HealPercent(actOut.NumberParameter, true)**;`
   `… ShowPopupMessage(… Heal, healAmount)`
   `if (actionHero.AtDeathsDoor) actionHero.RevertDeathsDoor();`
🔴 **联机**（`RaidSceneMultiplayerManager.cs:1472-1489`）：
   `if (actionHero.HealthRatio == 1) break;`
   `int healAmount = Mathf.RoundToInt(actOut.NumberParameter * actionHero.Health.ModifiedValue);`
   ⇒ 🎖️ **即：联机侧【自己算】了血量**（`NumberParameter × 当前最大血量` 四舍五入）⚠️
   ＋ 🔴 **`if (actionHero.AtDeathsDoor) actionHero.RevertDeathsDoor();` 【出现了两次】**
      （`L1477-1478` 与 `L1484-1485`）⇒ ⚠️ **重复调用** ✓
⇒ 🔴 **两个发现**：
   ① **数学不同**：单机走 `HealPercent(...)`，联机走 `RoundToInt(pct × ModifiedValue)`
      ⇒ ⚠️ **四舍五入方式可能不同** ⇒ **同一 act-out 在两处可能治不同的血量** ✓
   ② 🔴 **联机的 `RevertDeathsDoor()` 被调了两次** ⇒ **疑似复制粘贴的重复** ✓
      📌 **第二次调用在【弹出消息之后】** ⇒ 从行为看**幂等**（已 revert 再 revert 应无副作用）
      ⚠️ **但这是【推断】—— 本件未核 `RevertDeathsDoor` 的实现** ⇒ 记为**推断** ✓
```

## 4. 🎖️ 而**守卫条件**的比对：**我第一版报了假阳性**

```
🔴 **我第一版**逐 case 比"守卫条件"，报 `HealSelf` 单机=[] / 联机=[HealthRatio == 1] ⚠️
✅ **读原文后**：**单机的守卫是【跨两行】写的**：
   ```csharp
   if (actionHero.HealthRatio == 1)
       break;
   ```
   ⇒ 🔴 **我那个 `guards()` 只抓【同一行里既有 `if (` 又有 `break;`】的行** ⇒
      **跨行的抓不到** ⇒ **误报"单机没有守卫"** ⚠️
🎖️ **判据（本件新增）**：**"我抓到的'缺失'，是【真的没有】还是【它的写法和我预期的不一样】？"** ✓
   📌 **这与 §1 同族**（**度量与目标不匹配**），但表现是**假阳性**而不是**假读数** ✓
   🎖️ **修正后**：**11 个 case 的守卫【全部相同】** ✓
      ⇒ ✅ **即：两处只在 `HealSelf` 的【计算方式】上不同** ✓
```

## 5. 🎖️ 所以联机侧这一块的**总账**

```
✅ **11 case 集合相同** · ✅ **11 守卫条件相同** · 🔴 **1 处计算方式不同**（`HealSelf`）·
   🔴 **1 处重复调用**（联机 `RevertDeathsDoor` ×2）✓
⇒ 📌 **对采用的意义**：
   ① **只需抄一份逻辑**（不要抄两遍）✓
   ② 🔴 **但要知道【参考自己两处不一致】** ⇒ 采用时要**选一份**并**记下选的是哪份** ✓
      🎖️ **建议选【单机那份】** —— 理由：它走 `HealPercent(...)`（**复用了角色的治疗算法**），
         而联机那份**自己算** ⇒ 📌 **单机那份更可能是【正典】** ✓
      ⚠️ **但这是推断**（未核 `HealPercent` 是否就是 `RoundToInt(pct × maxHp)`）⇒ 记**推断** ✓
   ③ 🔴 **那处重复的 `RevertDeathsDoor()` ——【不要照抄】** ✓
```

## 6. 诚实边界

```
✅ **能验**：**11 个 case 的精确行数**（含订正 `StressHealSelf` 90 → **10**）·
   **两文件 case 集合相同（逐名）** · **11 守卫条件比对（修正后全同）** ·
   **`HealSelf` 两处原文（逐行）** · **联机 `RevertDeathsDoor` 两次调用的行号** ——
   **全部当场跑出** ✓
🎖️ 并**订正两处我自己的错**：
   ① `StressHealSelf` 的 90 行（**我填的兜底值当成实测**）
   ② 守卫比对的假阳性（**跨行 `if` 抓不到**）✓
🔴 **不能验**：`HealPercent(...)` 的**实现未读** ⇒ "单机那份是正典"是**推断** ✓
🔴 **不能验**：`RevertDeathsDoor()` 是否**幂等** ⇒ 未读其实现 ⇒ 记**推断** ✓
🔴 **不能验**：**没有落任何 act-out 数据** ⇒ 接上后行为**完全未测**（纪律 BK）✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{stressheal,mp}_*.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **联机侧【看完】**：同一 switch 的第二份实现 · 只需抄一份 · 但要知道有一处不同 ✓
🆕 **可做**：① 读 `HealPercent` 核"单机是正典"这个推断
   ② 读 `RevertDeathsDoor` 核幂等性
   ③ 扫 `darkest/scenes/**` 节点名一致性（`35_*.md` 记的覆盖缺口）✓
⏸️ **等策划**：七张单已投递 ✓
```
