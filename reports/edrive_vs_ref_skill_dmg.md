# 一手（E 盘）vs 第三方（参考项目）· 英雄技能 `.dmg` **对账报告**

> 🔴 口径：`doc/architecture/source_priority.md`（用户 2026-09-26 裁定 —— **一手优先**；
>   冲突时**以一手为准**，并**记录冲突**）· `#473`「一律采用参考」**已被取代** ✓
> 工具：`tools/dsh/reconcile_skill_dmg_edrive_vs_ref.py`（可复跑；**不写游戏数据** ✓）
> 一手读取器：`tools/dsh/extract_dd1_hero_skills.py`（同一份解析器 ⇒ 两表不会是两套解析结果 ✓）

## 读数（**技能等级 0** —— 我们与参考对齐的那一档）

| 项 | 值 |
|---|---|
| 一手 skill id | **105** |
| 参考 skill id | **105** |
| **两侧都有 ⇒ 逐条可对账** | **105** |
| **两侧值相同** | **82** |
| **两侧值不同** | **23** |
| 🔴 **真冲突（数值不同）** | **23** |
| ✅ 两侧都**没有** `.dmg` 字段（写法一致，不是冲突） | **0** |
| 只有一手有 | **0** |
| 只有参考有 | **0** |

## 🔴 冲突逐条（**以一手为准** —— 而改值 = 平衡数值改动 ⇒ 归 `dd1_baseline §39` 解冻清单）

| skill id | 一手 L0 | 参考 L0 | 一手 L0..L4 | 参考 L0..L4 | 一手证据 |
|---|---:|---:|---|---|---|
| `abyssal_artillery` | **-33** | -25 | [-33, -33, -33, -33, -33] | [-25, -25, -25, -25, -25] | `heroes/occultist/occultist.info.darkest:18` |
| `bellow` | **-100** | -90 | [-100, -100, -100, -100, -100] | [-90, -90, -90, -90, -90] | `heroes/man_at_arms/man_at_arms.info.darkest:23` |
| `blackjack` | **-65** | -60 | [-65, -65, -65, -65, -65] | [-60, -60, -60, -60, -60] | `heroes/houndmaster/houndmaster.info.darkest:44` |
| `bleed_out` | **20** | 15 | [20, 20, 20, 20, 20] | [15, 15, 15, 15, 15] | `heroes/hellion/hellion.info.darkest:44` |
| `breakthru` | **-50** | -55 | [-50, -50, -50, -50, -50] | [-55, -55, -55, -55, -55] | `heroes/hellion/hellion.info.darkest:34` |
| `come_hither` | **-80** | -67 | [-80, -80, -80, -80, -80] | [-67, -67, -67, -67, -67] | `heroes/bounty_hunter/bounty_hunter.info.darkest:23` |
| `flare` | **-100** | 0 | [-100, -100, -100, -100, -100] | [0, 0, 0, 0, 0] | `heroes/arbalest/arbalest.info.darkest:44` |
| `focus` | **-40** | -90 | [-40, -40, -40, -40, -40] | [-90, -90, -90, -90, -90] | `heroes/leper/leper.info.darkest:23` |
| `gods_illumination` | **-75** | -50 | [-75, -75, -75, -75, -75] | [-50, -50, -50, -50, -50] | `heroes/vestal/vestal.info.darkest:39` |
| `grape_shot_blast` | **-50** | -60 | [-50, -50, -50, -50, -50] | [-60, -60, -60, -60, -60] | `heroes/highwayman/highwayman.info.darkest:28` |
| `heroic_end` | **50** | 150 | [50, 50, 50, 50, 50] | [150, 150, 150, 150, 150] | `heroes/jester/jester.info.darkest:23` |
| `hew` | **-50** | -40 | [-50, -50, -50, -50, -50] | [-40, -40, -40, -40, -40] | `heroes/leper/leper.info.darkest:18` |
| `hook_and_slice` | **-95** | -85 | [-95, -95, -95, -95, -95] | [-85, -85, -85, -85, -85] | `heroes/bounty_hunter/bounty_hunter.info.darkest:44` |
| `hounds_harry` | **-75** | -80 | [-75, -75, -75, -75, -75] | [-80, -80, -80, -80, -80] | `heroes/houndmaster/houndmaster.info.darkest:18` |
| `intimidate` | **-85** | -75 | [-85, -85, -85, -80, -80] | [-75, -75, -75, -75, -75] | `heroes/leper/leper.info.darkest:44` |
| `judgement` | **-25** | -20 | [-25, -25, -25, -25, -25] | [-20, -20, -20, -20, -20] | `heroes/vestal/vestal.info.darkest:18` |
| `pick` | **-15** | 0 | [-15, -15, -15, -15, -15] | [0, 0, 0, 0, 0] | `heroes/grave_robber/grave_robber.info.darkest:13` |
| `pistol_shot` | **-15** | -25 | [-15, -15, -15, -15, -15] | [-25, -25, -25, -25, -25] | `heroes/highwayman/highwayman.info.darkest:18` |
| `poison_dart` | **-60** | -90 | [-60, -60, -60, -60, -60] | [-90, -90, -90, -90, -90] | `heroes/grave_robber/grave_robber.info.darkest:39` |
| `rake` | **-50** | -40 | [-50, -50, -50, -50, -50] | [-40, -40, -40, -40, -40] | `heroes/abomination/abomination.info.darkest:34` |
| `stunning_blow` | **-50** | -75 | [-50, -50, -50, -50, -50] | [-75, -75, -75, -75, -75] | `heroes/crusader/crusader.info.darkest:23` |
| `thrown_dagger` | **-10** | 0 | [-10, -10, -10, -10, -10] | [0, 0, 0, 0, 0] | `heroes/grave_robber/grave_robber.info.darkest:34` |
| `vomit` | **-90** | -100 | [-90, -90, -90, -90, -90] | [-100, -100, -100, -100, -100] | `heroes/abomination/abomination.info.darkest:23` |

🔴 **等级错位已被排除**（本表把 5 档全打出来就是为了这条）：
   · 一手侧 **22/23** 条在 L0..L4 上**全同值** · 参考侧 **23/23** 条同样全同值
   ⇒ 差异**不是**把档位读错造成的 —— 两条来源对同一个 id 写的**就是不同的数** ✓
## ✅ 值相同的（82 条）

`absolution` `adrenaline_rush` `barbaric_yawp` `battle_ballad` `battle_heal` `battlefield_bandage` `battlefield_medicine` `blindfire` `blinding_gas` `bloodlet` `bola` `bolster` `bulwark_of_faith` `chop` `collect_bounty` `command` `cower` `crush` `daemons_pull` `dazzling_light` `defender` `dirk_stab` `disorienting_blast` `disruptive_curse` `divine_grace` `duelist_advance` `emboldening_vapours` `festering_vapours` `finish_him` `flashbang` `flashing_daggers` `flashpowder` `fortifying_vapours` `gods_comfort` `gods_hand` `guard_dog` `hands_from_abyss` `harvest` `holy_lance` `hounds_rush` `howl` `if_it_bleeds` `incision` `inspiring_cry` `inspiring_tune` `invigorating_vapours` `iron_swan` `kris_stab` `lick_wounds` `lunge` `mace_bash` `manacles` `noxious_blast` `opened_vein` `plague_grenade` `point_blank_shot` `protect_me` `rage` `rampart` `retribution` `revenge` `shadow_fade` `slam` `slice_off` `smite` `sniper_mark` `sniper_shot` `solemnity` `solo` `suppressing_fire` `take_aim` `target_tag` `toxin_trickery` `transform` `uppercut` `weakening_curse` `whistle` `wicked_hack` `wicked_slice` `withstand` `wyrd_reconstruction` `zealous_accusation`

## 结论（对本次口径切换的意义）

```
· 值相同 ⇒ 已落库的参考值**同时就是一手值** ⇒ 出处可标【一手 = 第三方同值】✓
· 冲突 ⇒ **一手为准**；但把现有值改成一手值 = **平衡数值改动** ⇒
  入 `dd1_baseline §39` 解冻清单（**冻结期只登记不动**）✓
```

