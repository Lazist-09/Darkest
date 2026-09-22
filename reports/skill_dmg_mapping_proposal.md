# 技能 `dmg%` 映射草案（主程序提案 · **未落库** · 待策划确认）

> 🔴 **为什么必须由你点名**：我们 44 个技能是**自研命名**，参考项目保留**原版技能名**
>   ⇒ 实测机械匹配只覆盖 **5/44** ⇒ 我不编映射 ✗，改为给你一张**可直接改的表** ✓
> 🔴 **本文件只是提案**：没有改任何数据 ✓；你确认/改写后我一次落库 + 附前后读数 ✓
> 📌 **两个判据（都透明可审）**：
>   ① **名字重叠度** = |交集| ÷ min(|我们|, |参考|) ⇒ ≥0.5 记「名字可信」
>   ② **类型一致性**：我们的 `range_axis`（melee/ranged）vs 参考的 `.type`
>   ⇒ 🔴 **名字过、但类型不符 ⇒ 降级为「可疑」**（实测例：`warrior_battle_fury` 名字匹到
>     `battle_ballad`＝小丑的歌，类型不符 ✗ —— 这就是为什么必须有第②条 ✓）

## 覆盖统计（当场实测）

· 我们 = **44** 个技能 · 参考项目 level-0 = **98** 个技能
· ✅ **名字+类型都过（可用）= 7** · ⚠️ **名字过但类型不符（可疑）= 5** · 名字弱 = **0** · **无候选（需点名）= 32**

## 逐技能提案表

| 我们的技能 | 我们类型 | 目标侧 | 建议参考技能 | 名字分 | 参考类型 | 参考 `dmg%` | 判定 | 备注 |
|---|---|---|---|---|---|---|---|---|
| `warrior_cleave` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `warrior_sweep` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `warrior_lunge` | melee | enemy | `lunge` | 1.0 | melee | 40% | ✅ 可用 |  |
| `warrior_javelin` | ranged | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `warrior_shield_bash` | melee | enemy | `mace_bash` | 0.5 | melee | 0% | ✅ 可用 |  |
| `warrior_war_cry` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `warrior_battle_fury` | none | — | `battle_ballad` | 0.5 | ranged | 0% | ⚠️ **可疑（类型不符）** | 我们 none vs 参考 ranged ⇒ **请核对** |
| `warrior_catch_breath` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `warrior_last_stand` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `tank_guard_wall` | none | — | `guard_dog` | 0.5 | melee | 0% | ⚠️ **可疑（类型不符）** | 我们 none vs 参考 melee ⇒ **请核对** |
| `tank_taunt` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `tank_shield_bash` | melee | enemy | `mace_bash` | 0.5 | melee | 0% | ✅ 可用 |  |
| `tank_heavy_ram` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `tank_iron_wall` | none | — | `iron_swan` | 0.5 | melee | 0% | ⚠️ **可疑（类型不符）** | 我们 none vs 参考 melee ⇒ **请核对** |
| `tank_war_cry` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `tank_hunker` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `tank_catch_breath` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `tank_selfless_charge` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `medic_scalpel` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `medic_cross_slash` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `medic_anesthetic` | ranged | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `medic_medicine_flask` | ranged | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `medic_double_hit` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `medic_field_strike` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `medic_lethal_injection` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `medic_first_aid` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `medic_group_bandage` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `commissar_command_blade` | melee | enemy | `command` | 1.0 | ranged | 0% | ⚠️ **可疑（类型不符）** | 我们 melee vs 参考 ranged ⇒ **请核对** |
| `commissar_charge_order` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `commissar_pistol_shot` | ranged | enemy | `pistol_shot` | 1.0 | ranged | -25% | ✅ 可用 |  |
| `commissar_supervise` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `commissar_battle_inspiration` | none | — | `battle_ballad` | 0.5 | ranged | 0% | ⚠️ **可疑（类型不符）** | 我们 none vs 参考 ranged ⇒ **请核对** |
| `commissar_mobilize` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `commissar_execution_order` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `commissar_burst_fire` | ranged | enemy | `suppressing_fire` | 0.5 | ranged | -80% | ✅ 可用 |  |
| `commissar_total_mobilization` | melee | enemy | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `melee_heavy_slash` | melee | player | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `melee_charge` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `ranged_precise_shot` | ranged | player | `sniper_shot` | 0.5 | ranged | 0% | ✅ 可用 |  |
| `ranged_intimidating_shot` | ranged | player | `sniper_shot` | 0.5 | ranged | 0% | ✅ 可用 |  |
| `ranged_retreat` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `caster_fear_whisper` | ranged | player | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `caster_mental_shock` | ranged | player | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |
| `move` | none | — | — | 0.0 | — | — | 🔴 需点名 | **参考项目无同名/近名技能 ⇒ 请直接给数** |

## 你确认后我怎么落库
```
① 你确认/改写本表（或只给「我们的技能 id = dmg%」44 行）
② 我把 `dmg_pct` 写进 darkest/data/skills.json（每行带来源标记 ✓）
③ 附【前后读数】：用已备好的对照夹具跑 旧/新 两列 + 全量测试读数 ✓
④ 写进 tools/dsh/reference_placeholders.md 替换清单（后续要改时一处可查 ✓）
```