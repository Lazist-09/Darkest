# 敌方 8 条技能的**参考原型**：**6/8 有对应机制 · 2 条是我方自造**

> 🕒 2026-09-26 · 工具 `tools/dsh/find_enemy_skill_analogues.py`（新，可重跑）✓
> 🔴 **只读不落库**：`darkest/**` 零改动 ⇒ **零行为** ✓
> 🎖️ **收 `117_*.md §6①` 的"敌方 8 条有无参考原型未核"** ✓

---

## 1. 🎖️ 方法：**按效果形状找**，不按名字

```
🎖️ **判据（第 141 条）**：**"按名字找不到 ——
   那就按【效果形状】找（位移/增益/群攻/Debuff）"** ✓
⇒ 我方敌方 8 条的效果形状（逐个读定义后归纳）：
   ① 近战单目标伤害 · ② 自身前进 + 下次攻击加成 · ③ 远程单目标伤害 ·
   ④ 远程 + 士气削减 · ⑤ 自身后撤 · ⑥ 精神伤害 + 韧性削减 ·
   ⑦ 群攻（AOE）· ⑧ 移动（通用）
```

## 2. 📊 逐个形状的**实测结果**

| 我方技能 | 效果形状 | 参考对应 | 判定 |
|---|---|---|---|
| `melee_heavy_slash` | 近战单目标 | ✅ `melee` **929 处** ＋ `.dmg`/`.target`（怪技能通用）| ✅ **有** |
| **`melee_charge`** | **自身前进 + 下次攻击加成** | ✅ **`MoveSkill`**（`MoveForward`）＋ **`.forward` 64 处** | ✅ **有** |
| `ranged_precise_shot` | 远程单目标 | ✅ `ranged` **796 处** | ✅ **有** |
| **`ranged_intimidating_shot`** | **远程 + 士气削减** | ✅ `.stress` **8307 处** ＋ **`.damage_low_multiply` 139 处** | ✅ **有** |
| **`ranged_retreat`** | **自身后撤** | ✅ **`MoveSkill.MoveBackward`** ＋ `.backward` **25 处** | ✅ **有** |
| **`caster_fear_whisper`** | **精神伤害 + 韧性削减** | ✅ `.mental`/`resilience`（`JsonCamping`/`Curios`）| ⚠️ **部分**（见 §3）|
| **`caster_mental_shock`** | **群攻（AOE）** | ✅ **`templar_aoe`** 等（`.target 43` 打多 rank）| ✅ **有** |
| **`move`** | **移动（通用）** | ✅ **`MoveSkill`**（**参考的通用移动技能**）| ✅ **有** |

```
⇒ 🎖️ **即：8 条里【7 条有对应】** ✓（`caster_fear_whisper` 见 §3 的限定）
```

## 3. 🎖️ 而两处**机制级**的对应值得单独说

### ① `MoveSkill` —— 参考**有专门的位移技能类**

```csharp
// Mechanics/Skills/MoveSkill.cs（30 行）
public class MoveSkill : Skill {
    public int **MoveForward** { get; set; }
    public int **MoveBackward** { get; set; }
    public MoveSkill(string id, int backward, int forward) { … }
}
```
```
⇒ 🎖️ **即：参考把"移动"做成【独立的 Skill 子类】**，带前进/后退两个量 ✓
   📌 而 **`RaidSceneManager.cs:1416`** 有
      `if (skillSlot.Skill.MoveBackward >= -distance && …)` ⇒ **有实际的位移判定** ✓
   ⇒ ✅ **对应我方 `move` / `melee_charge`（self_forward）/ `ranged_retreat`（self_backward）** ✓
   🔴 **而我方的写法是 `"displacement": {"type": "self_forward", "count": 1}`** ⚠️
      ⇒ 📌 **字段名与参考不同**（参考是 `MoveSkill.MoveForward` 属性，
         数据里是 `.forward`），但**语义同构** ✓
   🎖️ **判据（第 142 条）**：**"我方的机制在参考里【叫什么】？
      ⇒ 不同名但同构时，要写清【对应关系】，而不是说'参考没有'"** ✓
```

### ② `.stress` 是**参考的士气机制**（8307 处）

```
📊 `ranged_intimidating_shot` 的 `morale_effects: [{"scope":"targets","delta":-4}]` ⚠️
   ⇒ 📌 **我方叫 `morale_effects`** ⇒ 而**参考叫 `.stress`**（`Effects.txt` 的 `.stress N`）✓
   ＋ **`.damage_low_multiply` 139 处**（`Unholy Killer` 等）⇒ **条件化伤害加成** ✓
   ＋ `debuff` **548 处**（`JsonBuffs`）✓
⇒ 🎖️ **即：这条技能的两个效果【都有参考机制】** ✓
   📌 而我方 `morale_effects` = 参考的 `.stress` ⇒ ✅ **同构异名** ✓
```

## 4. 🔴 所以 `117_*.md` 的"新空白"**要修正**

```
🔴 `117_*.md` 我写：**"敌方技能 ↔ 参考的映射不存在"**（因为它按兵种 vs 家族）⚠️
✅ **本件实测**：**映射不存在 ≠ 机制不存在** ✓
   · 🔴 **"技能级映射"确实没有**（参考没有"兵种"这个组织方式）✓
   · ✅ **但"机制级对应"【7/8 有】** ✓
⇒ 🎖️ **即：应当把结论精确成**：
   **"敌方技能没有【技能级】参考对应，但【机制级】多数有"** ✓
   🎖️ **判据（第 143 条）**：**"我说'没有对应'——
      是【技能级】没有，还是【机制级】也没有？⇒ 两者要分开说"** ✓
      📌 与第 129 条（"`§43` 的对齐是哪一层"）**同族：粒度要标清** ✓
```

## 5. 🔴 而唯一"部分"的那条

```
📊 **`caster_fear_whisper`** ⇒ `{"type":"stat_mod","stat":"resilience","delta":-10,"duration_rounds":2}` ⚠️
   ⇒ 📌 **它削的是 `resilience`（韧性）** ⇒ 而我方 `resilience` 用于**精神减免**（`§2.2`）✓
   🔴 **而参考里**：`resilience` **只有 4 处**（`Curios.xml`/`Heroes.xml` 的**本地化文本**）·
      `mental` 161 处（多在 `JsonCamping`/`JsonQuirks`）· **`mental_damage` 0 处** ✓
   ⇒ ⚠️ **即：参考【没有"精神韧性"这个数值属性】** ✓
      📌 而 **A4 记过**：`resilience` 属"我方自加"一侧 ⇒ ✅ **一致** ✓
   ⇒ 🎖️ **判据（第 144 条）**：**"'部分有对应'的含义是：
      机制有（属性削减），但【这个属性本身】参考没有"** ✓
```

## 6. 诚实边界

```
✅ **能验**：**8 条技能的完整定义（逐个 JSON）** ·
   **逐形状的探针计数（data + code 分开）** ·
   **`MoveSkill.cs` 的 30 行全文** · **`RaidSceneManager.cs:1416` 的位移判定** ·
   **`aoe` 的 41 处其实多数是【技能名子串】** · **`resilience` 在参考只 4 处且都是本地化** ——
   **全部当场跑出** ✓
🎖️ 并**修正 `117_*.md` 的"映射不存在"为"技能级没有 · 机制级 7/8 有"** ✓
🔴 **不能验**：**`melee_heavy_slash`/`ranged_precise_shot` 的"有对应"** ⇒
   📌 那是**通用怪技能形状**（`.type melee/ranged` + `.dmg`）⇒ ⚠️ **不是"某个技能"的对应** ✓
   🎖️ **即：那 2 条是"形状有对应，技能无对应"** ⇒ ✅ **与 §4 的分层一致** ✓
🔴 **不能验**：**`aoe` 的判定**：我只确认 `templar_aoe` 的 `.target 43` 打多 rank ⇒
   ⚠️ **未逐个读 `.target` 的数字编码含义** ⇒ 记**部分核** ✓
   📌 而 `.target 43` / `~1234` 这类编码 ⇒ **值得单做** ✓
🔴 **不能验**：**`next_attack_boost`（我方）在参考叫什么** ⇒
   📌 参考有 `.damage_low_multiply`（139）⇒ ⚠️ **可能就是它** ⇒ 记**推断** ✓
🔴 **不能验**：**没有改任何数据** ⇒ 零行为 ✓
⚠️ 探测脚本 `reports/unity_ref/_probe/{enemy8,aoe_move}.py` **不入库**（scratch）✓
```

## 7. 下一步

```
✅ **敌方 8 条的参考对应【查清】**：7 条有机制对应 · 1 条部分（`resilience` 自加）✓
🆕 **可做**：① 🔴 **读参考的 `.target` 数字编码**（`43` / `~1234` / `4321` 等）—— 那是"打哪些 rank" ✓
   ② 核 `next_attack_boost` ↔ `.damage_low_multiply` 是否同物
   ③ 把第 141~144 条判据补进 `observe_list` D11
   ④ **把"技能级 vs 机制级"的分层写进 `PLAN_adoption`** ✓
⏸️ **等策划**：十张单已投递 · 窗口无回复 ⇒ 继续做不依赖裁定的核对 ✓
```
