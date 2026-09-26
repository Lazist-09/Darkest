# `stress_heal_*` 的落点**定案：趟级**（不是我倾向的战斗级）—— 消费侧证据

> 🕒 2026-09-26 · 工具 `tools/dsh/locate_stress_heal_layer.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收窗口第 ⑬ 件（我报的那 2 条去向不符）** ✓

---

## 1. 🔴🔴 判决：**趟级** —— **我上轮的倾向错了**

```
🔴 **我上轮写**："**我倾向战斗级** —— 因为 `118_*.md` 实测 `StressHealEffect` 是战斗内效果
   （`MoraleLedger.ApplyTeamOnce`）" ⚠️
✅ **本件去消费侧查 ⇒ 事实相反** ✓
```

### 证据链（三条，全在消费侧）

```
📊 **① `stress_heal_percent`/`stress_heal_received_percent` 在【战斗侧】0 命中**：
   · `StressHealPercent` ⇒ **0** 处 ✓
   · `HealStress` ⇒ **0** · `IncreaseMorale` ⇒ **0** · `MoraleHeal` ⇒ **0** ✓
   ⇒ 🔴 **即：战斗代码里【没有】读这两个原语** ✓

📊 **② 而「士气恢复」实际发生在【趟层】**：
   · **`ExpeditionSession.ApplyTeamMorale(log, delta, reason)`**（`ExpeditionSession.cs:153`）✓
     ＋ 它操作的是 **`Retained`**（**远征名册**）：
       `Retained[id] = (hp, Math.Clamp(morale + delta, 0, 100), weak);` ✓
   · **而它的两个调用点都在【趟层】**：
     · `ExpeditionFlow.RoomInteractions.cs:263` ⇒ `ApplyTeamMorale(_log, amount, **"curio"**)` ✓
     · `ExpeditionSession.Traps.cs:57` ⇒ `ApplyTeamMorale(log, defs.DisarmStressHeal, "**trap_disarmed**")` ✓
   · **`GrantCampMorale`/`GrantCampMoraleTeam`**（`CampAndBonuses.cs:263`/`:116-126`）
     ⇒ **扎营**士气（3 条营火技能：`morale_plus_8`/`morale_plus_5_team`/`morale_plus_8_team`）✓
⇒ 🎖️🎖️ **即：`stress_heal_*` 的落点【真的是趟级/扎营】—— 与 `DestinationNote` 的说法一致** ✓
   🔴 **而 `ByStatType` 把它归 `Target.MoraleMod`（战斗级）是【粗的】** ⚠️
```

## 2. 🎖️ 而 `ExpeditionSession` 的类注释**直接印证**

```
📊 **`ExpeditionSession.cs:16-21` 的原文**：
   > **M7 远征层会话**（E0：#240 / O-66）——**由 `RunSession` 扩**：
   > N = 6 场战斗（`tuning.expedition.n_battles`）；每场后可选【扎营】…
   > 约束：`BattleDirector` **仍保持单场纯**（每场工厂新建 → 同 seed 可复现单场）✓
⇒ 🎖️ **即：它是【趟层】的会话** ⇒ ✅ **它管的士气恢复是【趟级】** ✓
```

## 3. 🔴 所以我上轮的错误**根因值得记**

```
🔴 **我引的"证据"是**：`118_*.md` 记过 **`StressHealEffect` 是战斗内效果**
   ⇒ 📌 **而那条说的是一种 Effect（`Heal Stress TrapD` 之类）**，
      ⚠️ **不是【`stress_heal_percent` 这个原语】** ✓
   ⇒ 🎖️ **判据（第 233 条）**：**"这个原语的落点有争议 ——
      那就去【消费侧】看它实际被用在哪，而不是看说明"** ✓
   ＋ 🎖️ **判据（第 234 条）**：**"我引用的'证据'——
      说的是【同一个东西】吗？（`StressHealEffect` ≠ `stress_heal_percent`）"** ✓
      📌 **这族错误本任务第 6 次**（第 7/26/66/83/125/204 条之后）✓
      🔴 **而这次特别值得记**：**我是在"倾向"里犯错** ——
         倾向往往比读数更容易滑向"看起来像的证据" ✓
```

## 4. 🎖️ 所以那个比**应改为 `9 : 15`**（不是待确认，是已定）

```
📊 **修正后的分类**：
   · **🔴 缺载体 9 条** = 原 7 条（`ExpeditionLayer`）＋
     **`stress_heal_percent`** ＋ **`stress_heal_received_percent`** ✓
   · **⚠️ 未接线 15 条**（战斗级）✓
⇒ 🎖️ **而修正的依据不是"倾向"，是【消费侧 0 命中 + 趟层有真实调用点】** ✓
    ＋ 🎖️ **且 `DestinationNote` 的文字【本来就对】** ⇒
      🔴 **错的是 `ByStatType` 的枚举归类**（把趟级原语归了 `MoraleMod`）⚠️
   ⇒ 🎖️ **判据（第 235 条）**：**"当【枚举】与【说明】冲突时 ——
      谁是权威？⇒ 去消费侧看（那里是事实）"** ✓
      ⇒ ✅ **本件：说明对、枚举粗** ✓
```

## 5. 🔴 而这意味着 `PendingCondition` 要修**两处**

```
📊 **两处要动**：
   ① **`ByStatType`** ⇒ `stress_heal_percent`/`stress_heal_received_percent` 的
      去向要不要从 `MoraleMod` 改成 **`ExpeditionLayer`**？⚠️
      ⇒ 🎖️ **而那是【我方的枚举语义】问题** ⇒ ✅ **属我方域，但改它会影响 `Tally` 读数** ⇒
         **要报**
   ② **`PendingCondition`** ⇒ 若①不改，则那 2 条会**继续被误判为"未接线"** ⚠️
⇒ 🎖️ **选哪条路要谨慎** —— 因为 `Target` 枚举的语义是"**翻译去向**"，
   ⚠️ **而 `MoraleMod` 的定义是**"士气（压力）轴：伤害 / 恢复 / 决心检定与经验" ✓
   ⇒ 📌 **即：`MoraleMod` 【包含】"恢复"** ⇒ ✅ **所以归 `MoraleMod` 在"轴"意义上不错** ✓
      🔴 **而"层"（战斗/趟）是【另一个维度】** ⇒ ⚠️ **`Target` 枚举【混了两个维度】** ✓
   🎖️ **判据（第 236 条）**：**"这个枚举分类是【按轴】还是【按层】？
      混了的 ⇒ 不能用一个枚举回答两个问题"** ✓
```

## 6. 诚实边界

```
✅ **能验**：**战斗侧 4 个符号全 0（`StressHealPercent`/`HealStress`/`IncreaseMorale`/`MoraleHeal`）** ·
   **`ApplyTeamMorale` 的完整实现（操作 `Retained`）** · **它的 2 个调用点全在趟层** ·
   **`GrantCampMorale`/`GrantCampMoraleTeam`（扎营）** ·
   **`ExpeditionSession.cs:16-21` 的类注释（"M7 远征层会话"）** —— **全部当场跑出** ✓
🎖️ 并**订正了我自己上轮的"倾向战斗级"**（依据是消费侧事实）✓
🔴 **不能验**：**`stress_heal_percent` 会不会【将来】也在战斗内用** ⇒
   📌 参考的 `stress_heal_*` 本意可能两处都有 ⇒ ⚠️ **记"可能两处"** ⇒ 不改结论（**缺载体**）✓
🔴 **不能验**：**`Target` 枚举该不该拆成"轴 × 层"两个维度** ⇒
   📌 **那是结构改动** ⇒ ⚠️ **属架构** ⇒ 记**待报** ✓
🔴 **不能验**：**`resolve_check_percent`/`resolve_xp_bonus_percent` 是不是也有同样问题** ⇒
   📌 它们归 `MoraleMod`，而 note 说"决心检定 ⇒ 士气系统"/"结算管线" ⇒
   ⚠️ **可能同族** ⇒ 记**未逐个核** ✓
   🎖️ **判据（第 237 条）**：**"我改了 1 处分类 ——
      同族的【其它处】要一起看吗？"** ✓
🔴 **不能验**：**没有改任何数据/代码** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/morale_recover.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **`stress_heal_*` 落点【定案：趟级】** —— 消费侧证据，且**订正了我上轮的倾向** ✓
🆕 **两条可做**：① 🔴 **核 `resolve_check_percent`/`resolve_xp_bonus_percent` 是否同族**（判据 237）
   ② 🔴 **报"`Target` 枚举混了【轴】与【层】两个维度"**（判据 236）
🆕 **可做**：③ 把第 233~237 条判据补进 `observe_list` D11
   ④ **更新 `142_*.md` 的 `7:17` → `9:15`**（已定，不是待确认）✓
   ⑤ **更新窗口第 ⑬ 件**（那条现在有答案了）✓
⏸️ **等策划**：十三张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
