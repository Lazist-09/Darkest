# 传家宝命名不一致：**已存在一座有文档、已接线的桥**（不是缺陷）

> 🕒 2026-09-26 · 承接 `29_audit_gap_closed.md §5` 我记的"我的方法可能掩盖了一处不一致" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓

---

## 1. 🔴 我记的那处不一致**确实存在** —— 但它**已经被处理过了**

```
🔴 **不一致本身**（实测）：
   · `heirlooms.json → kinds` = **复数** `['busts', 'crests', 'deeds', 'portraits']` ✓
   · `heirloom_exchange.json` 的 `exchange_*_type` = **单数** `['bust', 'crest', 'deed', 'portrait']` ✓
   ⇒ 🎖️ **即：同一套东西在【同一条链上】有两个写法** ✓
   📌 复数是**定义侧**（`kinds`）· 单数是**引用侧**（兑换表）✓
   ⇒ ✅ **判定：是"同一套的两种写法"，不是两套**（它们没有各自的定义）✓
```

## 2. 🎖️🎖️ 而它**已经被显式处理** —— 且处理得很干净

```
✅ **有一座桥**（`HeirloomStock.cs:241`）：
   ```csharp
   private static bool TryStockKindToReward(string stockKind, out string rewardKind) {
       switch (stockKind) {
           case "busts":     rewardKind = "bust";     return true;
           case "crests":    rewardKind = "crest";    return true;
           case "deeds":     rewardKind = "deed";     return true;
           case "portraits": rewardKind = "portrait"; return true;
           default:          rewardKind = string.Empty; return false;
       }
   }
   ```
   ✅ **调用点 1 处，在【生产路径】上**（`HeirloomStock.cs:118`）⇒ **不是死代码** ✓
🎖️ **而它的注释把三件事都写清了**（`HeirloomStock.cs:234-239`）：
   ① **这是什么陷阱**：`reward.TryGetValue("busts")` **永远 miss** ⇒ **静默一件都不发** ⚠️
   ② **为什么显式列出而不是自动转换**：
      `不用 TrimEnd('s') 之类的猜法 —— 那是推断，不是数据 ✓ 纪律 BL` ✓
   ③ **两侧各自的口径**（复数 vs 单数）✓
⇒ 🎖️ **即：这处不一致【已被发现 + 已记录 + 已接线 + 明说了为什么不偷懒】** ✓
   📌 这正是本仓纪律 **BL（推的不能落库）** 的一次**正向应用** ✓
```

## 3. 🎖️ 而且**第二处也记了**（同一件事的两处留痕）

```
✅ `HeirloomExchangeConfig.cs:38-40`：
   `/// 一手数据用**单数**（bust/crest/deed/portrait ✓）——`
   `/// ⚠️ 注意 heirlooms.json 的 kinds 用的是**复数**（busts/… ✗）⇒ 两处口径不一致，已如实记 ✓`
   `public static readonly string[] SingleKinds = { "bust", "crest", "deed", "portrait" };` ✓
⇒ 🎖️ **即：不一致在【两侧各自的类型】里都留了痕** ✓
```

## 4. 🎖️ 所以我上一轮的**自我怀疑是错的**（这是好事）

```
🔴 `29_*.md §5` 我写：**"为了让审计通过，我把 busts 与 bust 都收进并集 ⇒ 可能掩盖了真问题"** ✓
✅ **本件查证后**：**没有掩盖任何"未处理"的问题** ——
   那处不一致**本来就有桥、有注释、有调用点** ✓
⇒ 🎖️ **但仍要保留我那条戒心**（判据有效）：
   **"为了让审计通过，我是不是把两个写法都收进并集了？是 ⇒ 可能掩盖了真问题"** ✓
   📌 **它这次没抓到问题，是因为问题【已经被别人处理过了】** ——
      而不是因为判据错了 ✓
   🎖️ **这里有个值得记的区别**：
      · **审计说"闭合"** ⇒ 只说明**当时能解析** ✓
      · **审计说"闭合"≠"没有不一致"** ⚠️ ⇒ 因为**并集会把不一致吃掉** ✓
      ⇒ ✅ **所以"不一致"必须【单独查】**（如本件这样）✓
```

## 5. 诚实边界

```
✅ **能验**：两处命名的**原本写法** · 桥的**全文** · **调用点在生产路径上（1 处）** ·
   两处注释的**原文** · 我上一轮的自我怀疑**经查证为否** —— **全部当场跑出** ✓
🎖️ 并**验证了那座桥不是死代码**（有生产调用点 —— 这正是本任务反复用的判据）✓
🔴 **不能验**：**桥的转换在运行时是否真的生效**（未跑起来）⇒ 记**未测**（纪律 BK）✓
🔴 **不能验**：我方还有没有**别处**的同类"复数/单数"不一致（本件只查了传家宝）⇒ 记**未做** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 统一命名后行为如何**完全未测** ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/heirloom_*.py` **不入库**（scratch）✓
```

## 6. 下一步

```
✅ **这处"不一致"结案**：**已被处理**（桥 + 注释 + 生产调用点）⇒ **无需动作** ✓
🆕 **可做**：① 全库扫一遍**别的**"复数/单数"或"命名风格"不一致
   ② 看 `RaidSceneMultiplayerManager.cs` 那 11 处 act-out（仍未看）✓
   ③ 逐行读 `StressHealSelf`（90 行，最长的一个 case）✓
⏸️ **等策划**：七张单已投递 ✓
```
