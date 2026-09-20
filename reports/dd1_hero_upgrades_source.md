# M8 职业对齐：一手结构测量（**只测量、不落库**）

> 源：`E:\SteamLibrary\steamapps\common\DarkestDungeon\upgrades\heroes`（**一手** · 15 个文件）
> 🔴 **为什么不落库**：卡 M8 明写依赖 **M1（属性）/ M2（buff 原语层）** ⇒ 两者未到位时落库会产生
>    **没有消费者的数据**（阶段 A 的纪律：先有消费点再落）✓

## 实测

- 职业数 = **15**：`abomination`, `antiquarian`, `arbalest`, `bounty_hunter`, `crusader`, `grave_robber`, `hellion`, `highwayman`, `houndmaster`, `jester`, `leper`, `man_at_arms`, `occultist`, `plague_doctor`, `vestal`
- 升级树 = **135** 棵 · 等级条目 = **645** 条
- 树种类（按 id 后缀）：`weapon`=15, `armour`=15, `transform`=1, `manacles`=1, `vomit`=1, `absolution`=1, `rake`=1, `rage`=1, `slam`=1, `kris_stab`=1, `festering_vapours`=1, `cower`=1, `flashpowder`=1, `fortifying_vapours`=1, `invigorating_vapours`=1, `protect_me`=1, `sniper_shot`=1, `suppressing_fire`=1, `sniper_mark`=1, `bola`=1, `blindfire`=1, `battlefield_bandage`=1, `flare`=1, `collect_bounty`=1, `target_tag`=1, `come_hither`=1, `uppercut`=1, `flashbang`=1, `finish_him`=1, `hook_and_slice`=1, `smite`=1, `zealous_accusation`=1, `stunning_blow`=1, `bulwark_of_faith`=1, `battle_heal`=1, `holy_lance`=1, `inspiring_cry`=1, `pick`=1, `lunge`=1, `flashing_daggers`=1, `shadow_fade`=1, `thrown_dagger`=1, `poison_dart`=1, `toxin_trickery`=1, `wicked_hack`=1, `iron_swan`=1, `barbaric_yawp`=1, `if_it_bleeds`=1, `breakthru`=1, `adrenaline_rush`=1, `bleed_out`=1, `wicked_slice`=1, `pistol_shot`=1, `point_blank_shot`=1, `grape_shot_blast`=1, `take_aim`=1, `duelist_advance`=1, `opened_vein`=1, `hounds_rush`=1, `hounds_harry`=1, `whistle`=1, `howl`=1, `guard_dog`=1, `lick_wounds`=1, `blackjack`=1, `dirk_stab`=1, `harvest`=1, `heroic_end`=1, `solo`=1, `slice_off`=1, `battle_ballad`=1, `inspiring_tune`=1, `chop`=1, `hew`=1, `focus`=1, `revenge`=1, `withstand`=1, `solemnity`=1, `intimidate`=1, `crush`=1, `rampart`=1, `bellow`=1, `defender`=1, `retribution`=1, `command`=1, `bolster`=1, `bloodlet`=1, `abyssal_artillery`=1, `weakening_curse`=1, `wyrd_reconstruction`=1, `disruptive_curse`=1, `hands_from_abyss`=1, `daemons_pull`=1, `noxious_blast`=1, `plague_grenade`=1, `blinding_gas`=1, `incision`=1, `battlefield_medicine`=1, `emboldening_vapours`=1, `disorienting_blast`=1, `mace_bash`=1, `judgement`=1, `dazzling_light`=1, `divine_grace`=1, `gods_comfort`=1, `gods_illumination`=1, `gods_hand`=1
- `currency_cost` 资源类型：`gold`=645
- 🔴 **`prerequisite_resolve_level` 出现 645 次** ⇒ 这是**建筑文件里没有**的字段（决心等级门槛）
- 不含决心门槛的职业 = **0** ✓

## 每职业一览（树 · 档位数）

| 职业 | 树（档位数） |
|---|---|
| `abomination` | abomination.weapon(4) · abomination.armour(4) · abomination.transform(5) · abomination.manacles(5) · abomination.vomit(5) · abomination.absolution(5) · abomination.rake(5) · abomination.rage(5) · abomination.slam(5) |
| `antiquarian` | antiquarian.weapon(4) · antiquarian.armour(4) · antiquarian.kris_stab(5) · antiquarian.festering_vapours(5) · antiquarian.cower(5) · antiquarian.flashpowder(5) · antiquarian.fortifying_vapours(5) · antiquarian.invigorating_vapours(5) · antiquarian.protect_me(5) |
| `arbalest` | arbalest.weapon(4) · arbalest.armour(4) · arbalest.sniper_shot(5) · arbalest.suppressing_fire(5) · arbalest.sniper_mark(5) · arbalest.bola(5) · arbalest.blindfire(5) · arbalest.battlefield_bandage(5) · arbalest.flare(5) |
| `bounty_hunter` | bounty_hunter.weapon(4) · bounty_hunter.armour(4) · bounty_hunter.collect_bounty(5) · bounty_hunter.target_tag(5) · bounty_hunter.come_hither(5) · bounty_hunter.uppercut(5) · bounty_hunter.flashbang(5) · bounty_hunter.finish_him(5) · bounty_hunter.hook_and_slice(5) |
| `crusader` | crusader.weapon(4) · crusader.armour(4) · crusader.smite(5) · crusader.zealous_accusation(5) · crusader.stunning_blow(5) · crusader.bulwark_of_faith(5) · crusader.battle_heal(5) · crusader.holy_lance(5) · crusader.inspiring_cry(5) |
| `grave_robber` | grave_robber.weapon(4) · grave_robber.armour(4) · grave_robber.pick(5) · grave_robber.lunge(5) · grave_robber.flashing_daggers(5) · grave_robber.shadow_fade(5) · grave_robber.thrown_dagger(5) · grave_robber.poison_dart(5) · grave_robber.toxin_trickery(5) |
| `hellion` | hellion.weapon(4) · hellion.armour(4) · hellion.wicked_hack(5) · hellion.iron_swan(5) · hellion.barbaric_yawp(5) · hellion.if_it_bleeds(5) · hellion.breakthru(5) · hellion.adrenaline_rush(5) · hellion.bleed_out(5) |
| `highwayman` | highwayman.weapon(4) · highwayman.armour(4) · highwayman.wicked_slice(5) · highwayman.pistol_shot(5) · highwayman.point_blank_shot(5) · highwayman.grape_shot_blast(5) · highwayman.take_aim(5) · highwayman.duelist_advance(5) · highwayman.opened_vein(5) |
| `houndmaster` | houndmaster.weapon(4) · houndmaster.armour(4) · houndmaster.hounds_rush(5) · houndmaster.hounds_harry(5) · houndmaster.whistle(5) · houndmaster.howl(5) · houndmaster.guard_dog(5) · houndmaster.lick_wounds(5) · houndmaster.blackjack(5) |
| `jester` | jester.weapon(4) · jester.armour(4) · jester.dirk_stab(5) · jester.harvest(5) · jester.heroic_end(5) · jester.solo(5) · jester.slice_off(5) · jester.battle_ballad(5) · jester.inspiring_tune(5) |
| `leper` | leper.weapon(4) · leper.armour(4) · leper.chop(5) · leper.hew(5) · leper.focus(5) · leper.revenge(5) · leper.withstand(5) · leper.solemnity(5) · leper.intimidate(5) |
| `man_at_arms` | man_at_arms.weapon(4) · man_at_arms.armour(4) · man_at_arms.crush(5) · man_at_arms.rampart(5) · man_at_arms.bellow(5) · man_at_arms.defender(5) · man_at_arms.retribution(5) · man_at_arms.command(5) · man_at_arms.bolster(5) |
| `occultist` | occultist.weapon(4) · occultist.armour(4) · occultist.bloodlet(5) · occultist.abyssal_artillery(5) · occultist.weakening_curse(5) · occultist.wyrd_reconstruction(5) · occultist.disruptive_curse(5) · occultist.hands_from_abyss(5) · occultist.daemons_pull(5) |
| `plague_doctor` | plague_doctor.weapon(4) · plague_doctor.armour(4) · plague_doctor.noxious_blast(5) · plague_doctor.plague_grenade(5) · plague_doctor.blinding_gas(5) · plague_doctor.incision(5) · plague_doctor.battlefield_medicine(5) · plague_doctor.emboldening_vapours(5) · plague_doctor.disorienting_blast(5) |
| `vestal` | vestal.weapon(4) · vestal.armour(4) · vestal.mace_bash(5) · vestal.judgement(5) · vestal.dazzling_light(5) · vestal.divine_grace(5) · vestal.gods_comfort(5) · vestal.gods_illumination(5) · vestal.gods_hand(5) |

## 落库前必须先解决（如实列出）

```
① 依赖 M1（属性：weapon/armour 5 阶已在 units.json，但 M1c 伤害模型未切）+ M2（buff 原语层未落）
② 落库口径要定：15 职业是【全部落】还是像 M7 那样分批（卡说起点 crusader/vestal/plague_doctor）
③ 我方 4 职业/36 技能要【保留】并标 origin:ours；原版 15 职业标 origin:dd1 ⇒ 命名冲突要先查
④ `prerequisite_resolve_level` 是新字段：M6 的建筑形状没有它 ⇒ units.json/skills.json 的形状要一并定
```
