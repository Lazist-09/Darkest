# 反应实现点**全盘收口**：只有 3 个文件提到 —— 联机侧**确实残缺**（不是"写在别处"）

> 🕒 2026-09-26 · 工具 `tools/dsh/find_reaction_sites.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `86_*.md §6` 的"只查了一个文件"** ✓

---

## 1. 📊 全盘只有 **3 个文件**提到反应符号

| 文件 | 处数 | 性质 |
|---|---|---|
| **`Character/Utils/CharacterHelper.cs`** | **62** | 🔴 **纯映射（string ↔ enum）** |
| **`Managers/RaidSceneManager.cs`**（单机） | **21** | ✅ **真实行为** |
| **`Networking/RaidSceneMultiplayerManager.cs`**（联机） | 🔴 **1** | ⚠️ **只有 `BlockMove`** |

```
⇒ ✅ **即：联机侧的反应【确实残缺】** —— **不是"写在别处"** ✓
   📌 因为全盘只有这 3 个文件，而第 3 个只有 1 处 ✓
   🎖️ **判据（第 49 条）**：**"A 少 B 多 —— 是【B 真的残缺】还是【B 写在别处】？
      ⇒ 全盘搜【符号】而不是只看一个文件"** ✓
```

## 2. 🎖️ 而 `CharacterHelper.cs` 的 **62 处全是映射**（逐行验证）

```
📊 **62 处 = 62 个 `case` / `return` 行** ⇒ 🔴 **非映射行：`0`** ✓
⇒ ✅ **即：它只是 `"block_move"` ↔ `ReactionType.BlockMove` 的双向转换表** ✓
   📌 与 `30_*.md` 的判定**一致**（那次我把它排除在"消费点"之外）✓
   🎖️ **判据（第 50 条）**：**"这个文件里的 N 处，是【行为】还是【映射】？
      ⇒ 逐行看有无 `case`/`return` 之外的东西；全是 ⇒ 映射表"** ✓
      📌 **而这条我这次是用【计数】验证的**（62/62 都是映射形状）⇒ ✅ 比"看几行"可靠 ✓
```

## 3. 🎖️ 顺带确认：**全盘 30 种符号**（15 enum + 15 string）

```
🎖️ **15 个 enum 名**：`BlockMove` `BlockHeal` `BlockBuff` `BlockItem` **`BlockRetreat`** ·
   `CommentSelfHit` `CommentSelfMissed` `CommentAllyHit` `CommentAllyMissed` ·
   `CommentAllyAttackHit` `CommentAllyAttackMiss` `CommentMove` `CommentCurioInteraction` ·
   `CommentTrapTriggered` · **`BlockEffect`** ✓
🎖️ **15 个 string 名**：`"block_move"` … `"block_effect"` ✓
   ⇒ 📌 **而全部 15 个 string 名【只出现在 `CharacterHelper`】** ✓
      ⇒ ✅ **即：`CharactersHelper` 是唯一做 string↔enum 的地方** ✓
```

## 4. 🔴 而那两个"没人读"的（`BlockRetreat` / `BlockEffect`）**只出现在映射表里**

```
📊 `BlockRetreat`：`L525`（`return ReactionType.BlockRetreat`）· `L563`（`case ReactionType.BlockRetreat:`）
📊 `BlockEffect`：`L545`（`return …`）· `L583`（`case …:`）
⇒ 🔴 **各只有【映射的一进一出】两处，没有第三处** ✓
   📌 **与 `30_*.md` 的"13/15 有消费 · 2 个没有"完全一致** ✓
   ⇒ ✅ **本件是那条结论的【第二次独立确认】**（换了搜法：全盘搜符号）✓
```

## 5. 🎖️ 于是"反应"这条线**彻底收口**

```
📊 **完整图景**：
| | 单机 | 联机 | 映射表 |
|---|---|---|---|
| **15 种反应** | **13 有消费**（`30_*.md`）| 🔴 **1**（`BlockMove`）| **15/15** |
| **2 个无人读** | `BlockRetreat` · `BlockEffect` | — | 有映射、无行为 |

⇒ 🎖️ **三条独立读数指向同一结论**：
   ① `30_*.md`：单机 13/15 有消费 ⇒ **2 个没消费** ✓
   ② `36_*.md`：联机的 act-out switch 与单机【名称相同】✓
   ③ **本件**：全盘只有 3 个文件提到反应 ⇒ **联机确实只有 1 个** ✓
⇒ ✅ **即：`BlockRetreat`/`BlockEffect` 是"参考自己没实现"，
      而联机侧的反应是"另一种残缺"** —— 两者**不可混为一谈** ✓
   🎖️ **判据（第 51 条）**：**"'没用上'有两种 —— 【参考没实现】与【联机没复制】；
      处置不同：前者要自己设计，后者照抄单机即可"** ✓
```

## 6. 诚实边界

```
✅ **能验**：**全盘只有 3 个文件** · **逐文件的符号计数（62 / 21 / 1）** ·
   **`CharacterHelper` 的 62/62 全是映射形状** · **`BlockRetreat`/`BlockEffect` 各 2 处的行号** ·
   **全盘 30 种符号（15 enum + 15 string）** —— **全部当场跑出** ✓
🎖️ 并**独立确认了 `30_*.md` 的"13/15"结论**（换了搜法）✓
🔴 **不能验**：**全盘搜用的是【符号名】** ⇒ ⚠️ 若某处用反射/字符串拼接调用 ⇒ 搜不到 ⇒
   记**可能不全**（但全盘只有 3 个文件提到，概率低）✓
🔴 **不能验**：**联机侧是否【本该】有更多反应**（还是它只承担网络所需）⇒ 记**未判** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/ch_verify.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **反应这条线【彻底收口】**：3 文件 · 单机 13/15 · 联机 1/15 · 2 个参考自己没实现 ✓
🆕 **可做**：① 把第 49/50/51 条判据补进 `observe_list` D11
   ② **换线**：核 A12 的 `obstacles`(5) 与 `traps`(4) 与我方 `trap_defs.json`(4)（**小、可快**）
   ③ 核 A10 的 `provision` 三个清单（`79_*.md` 记过但未细看）
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
