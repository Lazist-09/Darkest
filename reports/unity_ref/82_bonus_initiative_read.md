# 第三张表读通：`bonus_initiative_desires` —— **换技能的先手奖励**（6 子类 · 全键闭合）

> 🕒 2026-09-26 · 承接 `81_*.md §7①` 的"第三张表消费代码未读" ✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **A5 三张表的最后一块** ⇒ 本件把它读通 ✓

---

## 1. 🎖️🎖️ 机制：**不是"选什么"，而是"先手 + 换技能"**

```
🎖️ **基类** `abstract class BonusInitiativeDesire`（46 行，**注意：不实现 `IProportionValue`**）✓
   ⇒ 🔴 **即：它【没有 `Chance` 权重】** ⚠️ —— 与另两张表**根本不同** ✓
   · `CombatSkillOverride`（**要换成的技能**）✓
   · `IsRoundStart` / `IsRoundInProgress` / `IsRoundFinish` / `IsPostTurn`（**4 个时机开关**）✓
   · **`abstract bool CheckBonusInitiative(FormationUnit performer)`** ⇒ **纯布尔判定** ✓
⇒ 🎖️ **即：它是"若条件成立 ⇒ 本回合改用 `CombatSkillOverride`"** ✓
   📌 与另两张表的关系：**那两张选"做什么"，这张【改"做什么"】** ✓
      🎖️ **判据（第 37 条）**：**"这张表和别的表【同构】吗？
         —— 看它有没有【权重字段】（`IProportionValue`）"** ✓
```

## 2. 📊 6 个子类**逐键闭合**

| 数据 type | 代码类 | 数据键 | 未认 |
|---|---|---|---|
| **`ally_actor_class_count`** | `BonusInitiativeAllyClassCount` | 11 | **`[]`** ✓ |
| **`ally_last_damaged`** | `BonusInitiativeAllyLastDamaged` | 8 | **`[]`** ✓ |
| **`death`** | `BonusInitiativeDeath` | 6 | 🔴 **6** ⚠️ |
| **`guaranteed`** | `BonusInitiativeGuaranteed` | 9 | **`[]`** ✓ |
| **`hp_ratio_threshold`** | `BonusInitiativeHpRatio` | 10 | **`[]`** ✓ |
| **`last_skill`** | `BonusInitiativeLastSkill` | 8 | **`[]`** ✓ |

```
🔴 **而 `death` 的 6 个"未认"【不是缺陷】** —— 实测它的**全文只有 14 行**：
   ```csharp
   public sealed class BonusInitiativeDeath : BonusInitiativeDesire {
       public BonusInitiativeDeath(Dictionary<string, object> dataSet) { GenerateFromDataSet(dataSet); }
       public override bool CheckBonusInitiative(FormationUnit performer)
           => performer.Character.HasZeroHealth;      // 🔴 无条件：只要血为 0
   }
   ```
   ⇒ 📌 **它【不覆写 `GenerateFromDataSet`】** ⇒ 走**基类**那份 ⇒
      **基类认那 6 个键** ⇒ ✅ **其实全都认了** ✓
   🎖️ **而我的脚本判它"未认"的原因**：我只在"文件里有 `ProcessBaseDataToken(token)`"
      时才把基类键算进去 ⚠️ ⇒ **`death` 没写这句（它压根没覆写）** ⇒ **我漏算了** ✓
   🎖️ **判据（第 38 条）**：**"它【不覆写】那个方法 —— 是【没有这个能力】还是【直接用基类的】？"
      ⇒ 看类体：**没覆写 ⇒ 继承基类 ⇒ 基类的能力【照样有】"** ✓
```

## 3. 🔴 并**订正我上一句口误**：键名是 `health_ratio_threshold`

```
🔴 我在上一步的对比里把数据键写成 `health_ratio_threshold`，
   同时在"代码 case"里看到 `hp_ratio_threshold`（那是**技能侧**的键）⚠️
✅ **实测**：`BonusInitiativeHpRatio.cs:47` 的 case 是 **`"health_ratio_threshold"`** ✓
   ⇒ 📌 **即：两张表用了【相近但不同】的键名**：
      · **技能侧**（`SkillSelectionHeal`/`Specific`）⇒ **`hp_ratio_treshold`**（**还拼错了 `treshold`**）⚠️
      · **先手表侧**（`BonusInitiativeHpRatio`）⇒ **`health_ratio_threshold`**（拼对）✓
   🎖️ **判据（第 39 条）**：**"这两张表的同义键，名字【一样】吗？
      —— 不一样，且其中一个还拼错了 ⇒ ⚠️ 不能跨表复用键名"** ✓
   📌 而**技能侧的 `treshold` 是原版拼写错**（少一个 `h`）⇒ ✅ **采用时必须照抄那个错拼** ✓
      🎖️ 与 `NonDeathsDorrHeroesMin`（`Dorr`）**同族**：**原版有多处拼写错，且【数据键随代码】** ✓
```

## 4. 🎖️ 4 个时机开关 + 1 个**静默死键**

```
🎖️ **4 个时机**：`is_round_start` · `is_round_in_progress` · `is_round_finish` · `is_post_turn` ✓
   ⇒ 🎖️ **即：先手奖励可以挂在【回合的 4 个时点】上** ✓
      📌 而数据里 **6 种 type 全都带这 4 个 + `combat_skill_id_override`**
         ⇒ ✅ **即：整套先手机制是"时机 + 换技能 + 一个布尔条件"** ✓

🔴 **而 `is_pre_turn` 是【静默死键】**（基类原文）：
   ```csharp
   case "is_pre_turn":
       break;              // 🔴 【什么都不做】⚠️
   ```
   ⇒ 📌 **即：它被【认了】（不会触发 `default` 的 LogError），但【值被丢弃】** ✓
   ⇒ ✅ **而数据里 6 种 type【全都带 `is_pre_turn`】** ⚠️ ⇒ 它**永远是空操作** ✓
   🎖️ **判据（第 40 条）**：**"这个 case 有 `break` 但【没有赋值】—— 是【占位】还是【漏写】？"
      ⇒ 两种处置：**照抄（留个空 case）** 或 **不抄** ⇒ 但**必须知道它没作用**"** ✓
      📌 这是 `plot_quests`（6 字段没人读）· Effect（190 条没人引用）· 建筑解锁（3 字段）
         之后**第 4 类"数据有、代码不消费"** ✓
```

## 5. 🎖️ 而"换技能"是**这张表最特别的地方**

```
🎖️ **`CombatSkillOverride`** ⇒ 6 种 type **全都用它** ✓
   示例（`swine_prince` 的 `ally_last_damaged`）：
   `{ "type": "ally_last_damaged", "data": { "…": …, `
   `  "combat_skill_id_override": "**obliterate_enraged**", "ally_base_class_id": "swine_piglet" } }` ✓
   ＋ `BonusInitiativeAllyLastDamaged.CheckBonusInitiative`：
      `if (IgnoreIfStun && performer.Character[StatusType.Stun].IsApplied) return false;`
      `if (AllyBaseClass != null && RaidSceneManager.BattleGround.LastDamaged.Contains(AllyBaseClass)) {`
      `    RaidSceneManager.BattleGround.LastDamaged.Clear();`   // 🔴 **一次性：用完就清**
      `    return true; }` ✓
⇒ 🎖️ **即：条件【一次性消费】**（`LastDamaged.Clear()`）⇒ ⚠️ **不能重复触发** ✓
   📌 而**我方 `enemy_ai.json`【完全没有这一层】** ⇒ ✅ 与 `79_*.md` 的"缺两张表"一致 ✓
```

## 6. 诚实边界

```
✅ **能验**：**基类 46 行原文（无 `IProportionValue`）** · **6 子类的自有键与继承** ·
   **`death` 的 14 行全文（不覆写）** · **`health_ratio_threshold` 的确切 case** ·
   **`is_pre_turn: break;` 的原文** · **`LastDamaged.Clear()` 的一次性** ——
   **全部当场跑出** ✓
🎖️ 并**订正我自己两处**（把 `death` 判成"未认" · 键名口误）✓
🔴 **不能验**：**`is_pre_turn` 是【占位】还是【漏写】** ⇒ 无 commit 可查 ⇒ 记**未判** ✓
🔴 **不能验**：**先手奖励在【哪里被调用】**（谁执行 `CheckBonusInitiative`）⇒ 记**未查** ✓
   📌 那是"接上这一层"的前置 ⇒ ⚠️ **记为重点待办** ✓
🔴 **不能验**：**`monsters_size_limit` 的语义**（`Guaranteed`/`LastSkill` 用）⇒ 记**未读** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{ai_tree,bonus_keys,bonus_mismatch}.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **A5 三张表【全部读通】**：技能侧 14 阈值 · 目标侧 5 布尔 · 先手表侧 6 条件（换技能）✓
🆕 **新增一类"死数据"**：`is_pre_turn`（认了但丢弃）—— 本任务第 4 类
🆕 **可做**：① 🔴 **找先手奖励的调用点**（谁跑 `CheckBonusInitiative`）—— 接线前置
   ② 读 `monsters_size_limit` 的语义
   ③ 把第 37/38/39/40 条判据补进 `observe_list` D11 ✓
   ④ 把 A5 的完整机制写进 `PLAN_adoption`（含"三张表 + 原始拼写错"）✓
⏸️ **等策划**：九张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
