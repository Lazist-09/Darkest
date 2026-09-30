# PRIMARY (E-drive DD1) hero combat skills -- `extract_dd1_hero_skills.py`

> Read-only extract of `E:\SteamLibrary\steamapps\common\DarkestDungeon`.
> Derived table only -- no original asset bytes (red line 29 / B6).

* hero files: **15** -- shared files scanned: **28** -- rows: **525** -- distinct ids (level 0): **105**

## level-0 `.dmg` by skill id

| skill id | group | .dmg | .atk | type | evidence |
|---|---|---:|---:|---|---|
| `absolution` | abomination | 0% | 0% | ranged | `heroes/abomination/abomination.info.darkest:28` |
| `abyssal_artillery` | occultist | -33% | 85% | ranged | `heroes/occultist/occultist.info.darkest:18` |
| `adrenaline_rush` | hellion | 0% | 0% | melee | `heroes/hellion/hellion.info.darkest:39` |
| `barbaric_yawp` | hellion | -100% | 95% | melee | `heroes/hellion/hellion.info.darkest:23` |
| `battle_ballad` | jester | 0% | 0% | ranged | `heroes/jester/jester.info.darkest:39` |
| `battle_heal` | crusader | -- | -- | -- | `heroes/crusader/crusader.info.darkest:34` |
| `battlefield_bandage` | arbalest | -- | -- | -- | `heroes/arbalest/arbalest.info.darkest:39` |
| `battlefield_medicine` | plague_doctor | -- | -- | -- | `heroes/plague_doctor/plague_doctor.info.darkest:34` |
| `bellow` | man_at_arms | -100% | 90% | ranged | `heroes/man_at_arms/man_at_arms.info.darkest:23` |
| `blackjack` | houndmaster | -65% | 95% | melee | `heroes/houndmaster/houndmaster.info.darkest:44` |
| `bleed_out` | hellion | 20% | 85% | melee | `heroes/hellion/hellion.info.darkest:44` |
| `blindfire` | arbalest | -10% | 75% | ranged | `heroes/arbalest/arbalest.info.darkest:34` |
| `blinding_gas` | plague_doctor | -100% | 95% | ranged | `heroes/plague_doctor/plague_doctor.info.darkest:23` |
| `bloodlet` | occultist | 0% | 80% | melee | `heroes/occultist/occultist.info.darkest:13` |
| `bola` | arbalest | -50% | 95% | ranged | `heroes/arbalest/arbalest.info.darkest:28` |
| `bolster` | man_at_arms | 0% | 0% | ranged | `heroes/man_at_arms/man_at_arms.info.darkest:44` |
| `breakthru` | hellion | -50% | 85% | melee | `heroes/hellion/hellion.info.darkest:34` |
| `bulwark_of_faith` | crusader | 0% | 0% | melee | `heroes/crusader/crusader.info.darkest:28` |
| `chop` | leper | 0% | 75% | melee | `heroes/leper/leper.info.darkest:13` |
| `collect_bounty` | bounty_hunter | 0% | 85% | melee | `heroes/bounty_hunter/bounty_hunter.info.darkest:13` |
| `come_hither` | bounty_hunter | -80% | 90% | ranged | `heroes/bounty_hunter/bounty_hunter.info.darkest:23` |
| `command` | man_at_arms | 0% | 0% | ranged | `heroes/man_at_arms/man_at_arms.info.darkest:39` |
| `cower` | antiquarian | 0% | 0% | ranged | `heroes/antiquarian/antiquarian.info.darkest:23` |
| `crush` | man_at_arms | 0% | 85% | melee | `heroes/man_at_arms/man_at_arms.info.darkest:13` |
| `daemons_pull` | occultist | -50% | 90% | ranged | `heroes/occultist/occultist.info.darkest:44` |
| `dazzling_light` | vestal | -75% | 90% | ranged | `heroes/vestal/vestal.info.darkest:23` |
| `defender` | man_at_arms | 0% | 0% | melee | `heroes/man_at_arms/man_at_arms.info.darkest:28` |
| `dirk_stab` | jester | 0% | 85% | melee | `heroes/jester/jester.info.darkest:13` |
| `disorienting_blast` | plague_doctor | -100% | 95% | ranged | `heroes/plague_doctor/plague_doctor.info.darkest:44` |
| `disruptive_curse` | occultist | -90% | 95% | ranged | `heroes/occultist/occultist.info.darkest:34` |
| `divine_grace` | vestal | -- | -- | -- | `heroes/vestal/vestal.info.darkest:28` |
| `duelist_advance` | highwayman | -20% | 90% | melee | `heroes/highwayman/highwayman.info.darkest:39` |
| `emboldening_vapours` | plague_doctor | 0% | 0% | melee | `heroes/plague_doctor/plague_doctor.info.darkest:39` |
| `festering_vapours` | antiquarian | -75% | 95% | ranged | `heroes/antiquarian/antiquarian.info.darkest:18` |
| `finish_him` | bounty_hunter | 0% | 85% | melee | `heroes/bounty_hunter/bounty_hunter.info.darkest:39` |
| `flare` | arbalest | -100% | 95% | ranged | `heroes/arbalest/arbalest.info.darkest:44` |
| `flashbang` | bounty_hunter | -100% | 95% | ranged | `heroes/bounty_hunter/bounty_hunter.info.darkest:34` |
| `flashing_daggers` | grave_robber | -33% | 90% | ranged | `heroes/grave_robber/grave_robber.info.darkest:23` |
| `flashpowder` | antiquarian | -100% | 95% | ranged | `heroes/antiquarian/antiquarian.info.darkest:28` |
| `focus` | leper | -40% | 85% | melee | `heroes/leper/leper.info.darkest:23` |
| `fortifying_vapours` | antiquarian | -- | -- | -- | `heroes/antiquarian/antiquarian.info.darkest:34` |
| `gods_comfort` | vestal | -- | -- | -- | `heroes/vestal/vestal.info.darkest:34` |
| `gods_hand` | vestal | -50% | 85% | ranged | `heroes/vestal/vestal.info.darkest:44` |
| `gods_illumination` | vestal | -75% | 90% | ranged | `heroes/vestal/vestal.info.darkest:39` |
| `grape_shot_blast` | highwayman | -50% | 75% | ranged | `heroes/highwayman/highwayman.info.darkest:28` |
| `guard_dog` | houndmaster | 0% | 0% | melee | `heroes/houndmaster/houndmaster.info.darkest:34` |
| `hands_from_abyss` | occultist | -50% | 90% | ranged | `heroes/occultist/occultist.info.darkest:39` |
| `harvest` | jester | -50% | 90% | melee | `heroes/jester/jester.info.darkest:18` |
| `heroic_end` | jester | 50% | 140% | melee | `heroes/jester/jester.info.darkest:23` |
| `hew` | leper | -50% | 75% | melee | `heroes/leper/leper.info.darkest:18` |
| `holy_lance` | crusader | 0% | 85% | melee | `heroes/crusader/crusader.info.darkest:39` |
| `hook_and_slice` | bounty_hunter | -95% | 90% | ranged | `heroes/bounty_hunter/bounty_hunter.info.darkest:44` |
| `hounds_harry` | houndmaster | -75% | 85% | ranged | `heroes/houndmaster/houndmaster.info.darkest:18` |
| `hounds_rush` | houndmaster | 0% | 85% | ranged | `heroes/houndmaster/houndmaster.info.darkest:13` |
| `howl` | houndmaster | 0% | 0% | ranged | `heroes/houndmaster/houndmaster.info.darkest:28` |
| `if_it_bleeds` | hellion | -35% | 85% | melee | `heroes/hellion/hellion.info.darkest:28` |
| `incision` | plague_doctor | 0% | 85% | melee | `heroes/plague_doctor/plague_doctor.info.darkest:28` |
| `inspiring_cry` | crusader | -- | -- | -- | `heroes/crusader/crusader.info.darkest:44` |
| `inspiring_tune` | jester | 0% | 0% | ranged | `heroes/jester/jester.info.darkest:44` |
| `intimidate` | leper | -85% | 95% | melee | `heroes/leper/leper.info.darkest:44` |
| `invigorating_vapours` | antiquarian | 0% | 0% | ranged | `heroes/antiquarian/antiquarian.info.darkest:39` |
| `iron_swan` | hellion | 0% | 85% | melee | `heroes/hellion/hellion.info.darkest:18` |
| `judgement` | vestal | -25% | 85% | ranged | `heroes/vestal/vestal.info.darkest:18` |
| `kris_stab` | antiquarian | 0% | 85% | melee | `heroes/antiquarian/antiquarian.info.darkest:13` |
| `lick_wounds` | houndmaster | 0% | 0% | melee | `heroes/houndmaster/houndmaster.info.darkest:39` |
| `lunge` | grave_robber | 40% | 95% | melee | `heroes/grave_robber/grave_robber.info.darkest:18` |
| `mace_bash` | vestal | 0% | 85% | melee | `heroes/vestal/vestal.info.darkest:13` |
| `manacles` | abomination | -60% | 95% | ranged | `heroes/abomination/abomination.info.darkest:18` |
| `noxious_blast` | plague_doctor | -80% | 95% | ranged | `heroes/plague_doctor/plague_doctor.info.darkest:13` |
| `opened_vein` | highwayman | -15% | 95% | melee | `heroes/highwayman/highwayman.info.darkest:44` |
| `pick` | grave_robber | -15% | 90% | melee | `heroes/grave_robber/grave_robber.info.darkest:13` |
| `pistol_shot` | highwayman | -15% | 85% | ranged | `heroes/highwayman/highwayman.info.darkest:18` |
| `plague_grenade` | plague_doctor | -90% | 95% | ranged | `heroes/plague_doctor/plague_doctor.info.darkest:18` |
| `point_blank_shot` | highwayman | 50% | 95% | ranged | `heroes/highwayman/highwayman.info.darkest:23` |
| `poison_dart` | grave_robber | -60% | 95% | ranged | `heroes/grave_robber/grave_robber.info.darkest:39` |
| `protect_me` | antiquarian | 0% | 0% | ranged | `heroes/antiquarian/antiquarian.info.darkest:44` |
| `rage` | abomination | 0% | 85% | melee | `heroes/abomination/abomination.info.darkest:39` |
| `rake` | abomination | -50% | 90% | melee | `heroes/abomination/abomination.info.darkest:34` |
| `rampart` | man_at_arms | -60% | 90% | melee | `heroes/man_at_arms/man_at_arms.info.darkest:18` |
| `retribution` | man_at_arms | -75% | 85% | melee | `heroes/man_at_arms/man_at_arms.info.darkest:34` |
| `revenge` | leper | 0% | 0% | melee | `heroes/leper/leper.info.darkest:28` |
| `shadow_fade` | grave_robber | -100% | 95% | melee | `heroes/grave_robber/grave_robber.info.darkest:28` |
| `slam` | abomination | -25% | 80% | melee | `heroes/abomination/abomination.info.darkest:44` |
| `slice_off` | jester | -33% | 95% | melee | `heroes/jester/jester.info.darkest:34` |
| `smite` | crusader | 0% | 85% | melee | `heroes/crusader/crusader.info.darkest:13` |
| `sniper_mark` | arbalest | -100% | 100% | ranged | `heroes/arbalest/arbalest.info.darkest:23` |
| `sniper_shot` | arbalest | 0% | 95% | ranged | `heroes/arbalest/arbalest.info.darkest:13` |
| `solemnity` | leper | 0% | 0% | melee | `heroes/leper/leper.info.darkest:39` |
| `solo` | jester | -100% | 125% | ranged | `heroes/jester/jester.info.darkest:28` |
| `stunning_blow` | crusader | -50% | 90% | melee | `heroes/crusader/crusader.info.darkest:23` |
| `suppressing_fire` | arbalest | -80% | 95% | ranged | `heroes/arbalest/arbalest.info.darkest:18` |
| `take_aim` | highwayman | -80% | 95% | ranged | `heroes/highwayman/highwayman.info.darkest:34` |
| `target_tag` | bounty_hunter | -100% | 100% | ranged | `heroes/bounty_hunter/bounty_hunter.info.darkest:18` |
| `thrown_dagger` | grave_robber | -10% | 90% | ranged | `heroes/grave_robber/grave_robber.info.darkest:34` |
| `toxin_trickery` | grave_robber | 0% | 0% | melee | `heroes/grave_robber/grave_robber.info.darkest:44` |
| `transform` | abomination | 0% | 0% | ranged | `heroes/abomination/abomination.info.darkest:13` |
| `uppercut` | bounty_hunter | -67% | 90% | melee | `heroes/bounty_hunter/bounty_hunter.info.darkest:28` |
| `vomit` | abomination | -90% | 95% | ranged | `heroes/abomination/abomination.info.darkest:23` |
| `weakening_curse` | occultist | -75% | 95% | ranged | `heroes/occultist/occultist.info.darkest:23` |
| `whistle` | houndmaster | -100% | 100% | ranged | `heroes/houndmaster/houndmaster.info.darkest:23` |
| `wicked_hack` | hellion | 0% | 85% | melee | `heroes/hellion/hellion.info.darkest:13` |
| `wicked_slice` | highwayman | 15% | 85% | melee | `heroes/highwayman/highwayman.info.darkest:13` |
| `withstand` | leper | 0% | 0% | melee | `heroes/leper/leper.info.darkest:34` |
| `wyrd_reconstruction` | occultist | -- | -- | -- | `heroes/occultist/occultist.info.darkest:28` |
| `zealous_accusation` | crusader | -40% | 85% | ranged | `heroes/crusader/crusader.info.darkest:18` |

## every level (raw)

| skill id | lvl | .dmg | file:line |
|---|---:|---:|---|
| `absolution` | 0 | 0% | `heroes/abomination/abomination.info.darkest:28` |
| `absolution` | 1 | 0% | `heroes/abomination/abomination.info.darkest:29` |
| `absolution` | 2 | 0% | `heroes/abomination/abomination.info.darkest:30` |
| `absolution` | 3 | 0% | `heroes/abomination/abomination.info.darkest:31` |
| `absolution` | 4 | 0% | `heroes/abomination/abomination.info.darkest:32` |
| `manacles` | 0 | -60% | `heroes/abomination/abomination.info.darkest:18` |
| `manacles` | 1 | -60% | `heroes/abomination/abomination.info.darkest:19` |
| `manacles` | 2 | -60% | `heroes/abomination/abomination.info.darkest:20` |
| `manacles` | 3 | -60% | `heroes/abomination/abomination.info.darkest:21` |
| `manacles` | 4 | -60% | `heroes/abomination/abomination.info.darkest:22` |
| `rage` | 0 | 0% | `heroes/abomination/abomination.info.darkest:39` |
| `rage` | 1 | 0% | `heroes/abomination/abomination.info.darkest:40` |
| `rage` | 2 | 0% | `heroes/abomination/abomination.info.darkest:41` |
| `rage` | 3 | 0% | `heroes/abomination/abomination.info.darkest:42` |
| `rage` | 4 | 0% | `heroes/abomination/abomination.info.darkest:43` |
| `rake` | 0 | -50% | `heroes/abomination/abomination.info.darkest:34` |
| `rake` | 1 | -50% | `heroes/abomination/abomination.info.darkest:35` |
| `rake` | 2 | -50% | `heroes/abomination/abomination.info.darkest:36` |
| `rake` | 3 | -50% | `heroes/abomination/abomination.info.darkest:37` |
| `rake` | 4 | -50% | `heroes/abomination/abomination.info.darkest:38` |
| `slam` | 0 | -25% | `heroes/abomination/abomination.info.darkest:44` |
| `slam` | 1 | -25% | `heroes/abomination/abomination.info.darkest:45` |
| `slam` | 2 | -25% | `heroes/abomination/abomination.info.darkest:46` |
| `slam` | 3 | -25% | `heroes/abomination/abomination.info.darkest:47` |
| `slam` | 4 | -25% | `heroes/abomination/abomination.info.darkest:48` |
| `transform` | 0 | 0% | `heroes/abomination/abomination.info.darkest:13` |
| `transform` | 1 | 0% | `heroes/abomination/abomination.info.darkest:14` |
| `transform` | 2 | 0% | `heroes/abomination/abomination.info.darkest:15` |
| `transform` | 3 | 0% | `heroes/abomination/abomination.info.darkest:16` |
| `transform` | 4 | 0% | `heroes/abomination/abomination.info.darkest:17` |
| `vomit` | 0 | -90% | `heroes/abomination/abomination.info.darkest:23` |
| `vomit` | 1 | -90% | `heroes/abomination/abomination.info.darkest:24` |
| `vomit` | 2 | -90% | `heroes/abomination/abomination.info.darkest:25` |
| `vomit` | 3 | -90% | `heroes/abomination/abomination.info.darkest:26` |
| `vomit` | 4 | -90% | `heroes/abomination/abomination.info.darkest:27` |
| `cower` | 0 | 0% | `heroes/antiquarian/antiquarian.info.darkest:23` |
| `cower` | 1 | 0% | `heroes/antiquarian/antiquarian.info.darkest:24` |
| `cower` | 2 | 0% | `heroes/antiquarian/antiquarian.info.darkest:25` |
| `cower` | 3 | 0% | `heroes/antiquarian/antiquarian.info.darkest:26` |
| `cower` | 4 | 0% | `heroes/antiquarian/antiquarian.info.darkest:27` |
| `festering_vapours` | 0 | -75% | `heroes/antiquarian/antiquarian.info.darkest:18` |
| `festering_vapours` | 1 | -75% | `heroes/antiquarian/antiquarian.info.darkest:19` |
| `festering_vapours` | 2 | -75% | `heroes/antiquarian/antiquarian.info.darkest:20` |
| `festering_vapours` | 3 | -75% | `heroes/antiquarian/antiquarian.info.darkest:21` |
| `festering_vapours` | 4 | -75% | `heroes/antiquarian/antiquarian.info.darkest:22` |
| `flashpowder` | 0 | -100% | `heroes/antiquarian/antiquarian.info.darkest:28` |
| `flashpowder` | 1 | -100% | `heroes/antiquarian/antiquarian.info.darkest:29` |
| `flashpowder` | 2 | -100% | `heroes/antiquarian/antiquarian.info.darkest:30` |
| `flashpowder` | 3 | -100% | `heroes/antiquarian/antiquarian.info.darkest:31` |
| `flashpowder` | 4 | -100% | `heroes/antiquarian/antiquarian.info.darkest:32` |
| `fortifying_vapours` | 0 | -- | `heroes/antiquarian/antiquarian.info.darkest:34` |
| `fortifying_vapours` | 1 | -- | `heroes/antiquarian/antiquarian.info.darkest:35` |
| `fortifying_vapours` | 2 | -- | `heroes/antiquarian/antiquarian.info.darkest:36` |
| `fortifying_vapours` | 3 | -- | `heroes/antiquarian/antiquarian.info.darkest:37` |
| `fortifying_vapours` | 4 | -- | `heroes/antiquarian/antiquarian.info.darkest:38` |
| `invigorating_vapours` | 0 | 0% | `heroes/antiquarian/antiquarian.info.darkest:39` |
| `invigorating_vapours` | 1 | 0% | `heroes/antiquarian/antiquarian.info.darkest:40` |
| `invigorating_vapours` | 2 | 0% | `heroes/antiquarian/antiquarian.info.darkest:41` |
| `invigorating_vapours` | 3 | 0% | `heroes/antiquarian/antiquarian.info.darkest:42` |
| `invigorating_vapours` | 4 | 0% | `heroes/antiquarian/antiquarian.info.darkest:43` |
| `kris_stab` | 0 | 0% | `heroes/antiquarian/antiquarian.info.darkest:13` |
| `kris_stab` | 1 | 0% | `heroes/antiquarian/antiquarian.info.darkest:14` |
| `kris_stab` | 2 | 0% | `heroes/antiquarian/antiquarian.info.darkest:15` |
| `kris_stab` | 3 | 0% | `heroes/antiquarian/antiquarian.info.darkest:16` |
| `kris_stab` | 4 | 0% | `heroes/antiquarian/antiquarian.info.darkest:17` |
| `protect_me` | 0 | 0% | `heroes/antiquarian/antiquarian.info.darkest:44` |
| `protect_me` | 1 | 0% | `heroes/antiquarian/antiquarian.info.darkest:45` |
| `protect_me` | 2 | 0% | `heroes/antiquarian/antiquarian.info.darkest:46` |
| `protect_me` | 3 | 0% | `heroes/antiquarian/antiquarian.info.darkest:47` |
| `protect_me` | 4 | 0% | `heroes/antiquarian/antiquarian.info.darkest:48` |
| `battlefield_bandage` | 0 | -- | `heroes/arbalest/arbalest.info.darkest:39` |
| `battlefield_bandage` | 1 | -- | `heroes/arbalest/arbalest.info.darkest:40` |
| `battlefield_bandage` | 2 | -- | `heroes/arbalest/arbalest.info.darkest:41` |
| `battlefield_bandage` | 3 | -- | `heroes/arbalest/arbalest.info.darkest:42` |
| `battlefield_bandage` | 4 | -- | `heroes/arbalest/arbalest.info.darkest:43` |
| `blindfire` | 0 | -10% | `heroes/arbalest/arbalest.info.darkest:34` |
| `blindfire` | 1 | -10% | `heroes/arbalest/arbalest.info.darkest:35` |
| `blindfire` | 2 | -10% | `heroes/arbalest/arbalest.info.darkest:36` |
| `blindfire` | 3 | -10% | `heroes/arbalest/arbalest.info.darkest:37` |
| `blindfire` | 4 | -10% | `heroes/arbalest/arbalest.info.darkest:38` |
| `bola` | 0 | -50% | `heroes/arbalest/arbalest.info.darkest:28` |
| `bola` | 1 | -50% | `heroes/arbalest/arbalest.info.darkest:29` |
| `bola` | 2 | -50% | `heroes/arbalest/arbalest.info.darkest:30` |
| `bola` | 3 | -50% | `heroes/arbalest/arbalest.info.darkest:31` |
| `bola` | 4 | -50% | `heroes/arbalest/arbalest.info.darkest:32` |
| `flare` | 0 | -100% | `heroes/arbalest/arbalest.info.darkest:44` |
| `flare` | 1 | -100% | `heroes/arbalest/arbalest.info.darkest:45` |
| `flare` | 2 | -100% | `heroes/arbalest/arbalest.info.darkest:46` |
| `flare` | 3 | -100% | `heroes/arbalest/arbalest.info.darkest:47` |
| `flare` | 4 | -100% | `heroes/arbalest/arbalest.info.darkest:48` |
| `sniper_mark` | 0 | -100% | `heroes/arbalest/arbalest.info.darkest:23` |
| `sniper_mark` | 1 | -100% | `heroes/arbalest/arbalest.info.darkest:24` |
| `sniper_mark` | 2 | -100% | `heroes/arbalest/arbalest.info.darkest:25` |
| `sniper_mark` | 3 | -100% | `heroes/arbalest/arbalest.info.darkest:26` |
| `sniper_mark` | 4 | -100% | `heroes/arbalest/arbalest.info.darkest:27` |
| `sniper_shot` | 0 | 0% | `heroes/arbalest/arbalest.info.darkest:13` |
| `sniper_shot` | 1 | 0% | `heroes/arbalest/arbalest.info.darkest:14` |
| `sniper_shot` | 2 | 0% | `heroes/arbalest/arbalest.info.darkest:15` |
| `sniper_shot` | 3 | 0% | `heroes/arbalest/arbalest.info.darkest:16` |
| `sniper_shot` | 4 | 0% | `heroes/arbalest/arbalest.info.darkest:17` |
| `suppressing_fire` | 0 | -80% | `heroes/arbalest/arbalest.info.darkest:18` |
| `suppressing_fire` | 1 | -80% | `heroes/arbalest/arbalest.info.darkest:19` |
| `suppressing_fire` | 2 | -80% | `heroes/arbalest/arbalest.info.darkest:20` |
| `suppressing_fire` | 3 | -80% | `heroes/arbalest/arbalest.info.darkest:21` |
| `suppressing_fire` | 4 | -80% | `heroes/arbalest/arbalest.info.darkest:22` |
| `collect_bounty` | 0 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:13` |
| `collect_bounty` | 1 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:14` |
| `collect_bounty` | 2 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:15` |
| `collect_bounty` | 3 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:16` |
| `collect_bounty` | 4 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:17` |
| `come_hither` | 0 | -80% | `heroes/bounty_hunter/bounty_hunter.info.darkest:23` |
| `come_hither` | 1 | -80% | `heroes/bounty_hunter/bounty_hunter.info.darkest:24` |
| `come_hither` | 2 | -80% | `heroes/bounty_hunter/bounty_hunter.info.darkest:25` |
| `come_hither` | 3 | -80% | `heroes/bounty_hunter/bounty_hunter.info.darkest:26` |
| `come_hither` | 4 | -80% | `heroes/bounty_hunter/bounty_hunter.info.darkest:27` |
| `finish_him` | 0 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:39` |
| `finish_him` | 1 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:40` |
| `finish_him` | 2 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:41` |
| `finish_him` | 3 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:42` |
| `finish_him` | 4 | 0% | `heroes/bounty_hunter/bounty_hunter.info.darkest:43` |
| `flashbang` | 0 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:34` |
| `flashbang` | 1 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:35` |
| `flashbang` | 2 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:36` |
| `flashbang` | 3 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:37` |
| `flashbang` | 4 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:38` |
| `hook_and_slice` | 0 | -95% | `heroes/bounty_hunter/bounty_hunter.info.darkest:44` |
| `hook_and_slice` | 1 | -95% | `heroes/bounty_hunter/bounty_hunter.info.darkest:45` |
| `hook_and_slice` | 2 | -95% | `heroes/bounty_hunter/bounty_hunter.info.darkest:46` |
| `hook_and_slice` | 3 | -95% | `heroes/bounty_hunter/bounty_hunter.info.darkest:47` |
| `hook_and_slice` | 4 | -95% | `heroes/bounty_hunter/bounty_hunter.info.darkest:48` |
| `target_tag` | 0 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:18` |
| `target_tag` | 1 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:19` |
| `target_tag` | 2 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:20` |
| `target_tag` | 3 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:21` |
| `target_tag` | 4 | -100% | `heroes/bounty_hunter/bounty_hunter.info.darkest:22` |
| `uppercut` | 0 | -67% | `heroes/bounty_hunter/bounty_hunter.info.darkest:28` |
| `uppercut` | 1 | -67% | `heroes/bounty_hunter/bounty_hunter.info.darkest:29` |
| `uppercut` | 2 | -67% | `heroes/bounty_hunter/bounty_hunter.info.darkest:30` |
| `uppercut` | 3 | -67% | `heroes/bounty_hunter/bounty_hunter.info.darkest:31` |
| `uppercut` | 4 | -67% | `heroes/bounty_hunter/bounty_hunter.info.darkest:32` |
| `battle_heal` | 0 | -- | `heroes/crusader/crusader.info.darkest:34` |
| `battle_heal` | 1 | -- | `heroes/crusader/crusader.info.darkest:35` |
| `battle_heal` | 2 | -- | `heroes/crusader/crusader.info.darkest:36` |
| `battle_heal` | 3 | -- | `heroes/crusader/crusader.info.darkest:37` |
| `battle_heal` | 4 | -- | `heroes/crusader/crusader.info.darkest:38` |
| `bulwark_of_faith` | 0 | 0% | `heroes/crusader/crusader.info.darkest:28` |
| `bulwark_of_faith` | 1 | 0% | `heroes/crusader/crusader.info.darkest:29` |
| `bulwark_of_faith` | 2 | 0% | `heroes/crusader/crusader.info.darkest:30` |
| `bulwark_of_faith` | 3 | 0% | `heroes/crusader/crusader.info.darkest:31` |
| `bulwark_of_faith` | 4 | 0% | `heroes/crusader/crusader.info.darkest:32` |
| `holy_lance` | 0 | 0% | `heroes/crusader/crusader.info.darkest:39` |
| `holy_lance` | 1 | 0% | `heroes/crusader/crusader.info.darkest:40` |
| `holy_lance` | 2 | 0% | `heroes/crusader/crusader.info.darkest:41` |
| `holy_lance` | 3 | 0% | `heroes/crusader/crusader.info.darkest:42` |
| `holy_lance` | 4 | 0% | `heroes/crusader/crusader.info.darkest:43` |
| `inspiring_cry` | 0 | -- | `heroes/crusader/crusader.info.darkest:44` |
| `inspiring_cry` | 1 | -- | `heroes/crusader/crusader.info.darkest:45` |
| `inspiring_cry` | 2 | -- | `heroes/crusader/crusader.info.darkest:46` |
| `inspiring_cry` | 3 | -- | `heroes/crusader/crusader.info.darkest:47` |
| `inspiring_cry` | 4 | -- | `heroes/crusader/crusader.info.darkest:48` |
| `smite` | 0 | 0% | `heroes/crusader/crusader.info.darkest:13` |
| `smite` | 1 | 0% | `heroes/crusader/crusader.info.darkest:14` |
| `smite` | 2 | 0% | `heroes/crusader/crusader.info.darkest:15` |
| `smite` | 3 | 0% | `heroes/crusader/crusader.info.darkest:16` |
| `smite` | 4 | 0% | `heroes/crusader/crusader.info.darkest:17` |
| `stunning_blow` | 0 | -50% | `heroes/crusader/crusader.info.darkest:23` |
| `stunning_blow` | 1 | -50% | `heroes/crusader/crusader.info.darkest:24` |
| `stunning_blow` | 2 | -50% | `heroes/crusader/crusader.info.darkest:25` |
| `stunning_blow` | 3 | -50% | `heroes/crusader/crusader.info.darkest:26` |
| `stunning_blow` | 4 | -50% | `heroes/crusader/crusader.info.darkest:27` |
| `zealous_accusation` | 0 | -40% | `heroes/crusader/crusader.info.darkest:18` |
| `zealous_accusation` | 1 | -40% | `heroes/crusader/crusader.info.darkest:19` |
| `zealous_accusation` | 2 | -40% | `heroes/crusader/crusader.info.darkest:20` |
| `zealous_accusation` | 3 | -40% | `heroes/crusader/crusader.info.darkest:21` |
| `zealous_accusation` | 4 | -40% | `heroes/crusader/crusader.info.darkest:22` |
| `flashing_daggers` | 0 | -33% | `heroes/grave_robber/grave_robber.info.darkest:23` |
| `flashing_daggers` | 1 | -33% | `heroes/grave_robber/grave_robber.info.darkest:24` |
| `flashing_daggers` | 2 | -33% | `heroes/grave_robber/grave_robber.info.darkest:25` |
| `flashing_daggers` | 3 | -33% | `heroes/grave_robber/grave_robber.info.darkest:26` |
| `flashing_daggers` | 4 | -33% | `heroes/grave_robber/grave_robber.info.darkest:27` |
| `lunge` | 0 | 40% | `heroes/grave_robber/grave_robber.info.darkest:18` |
| `lunge` | 1 | 40% | `heroes/grave_robber/grave_robber.info.darkest:19` |
| `lunge` | 2 | 40% | `heroes/grave_robber/grave_robber.info.darkest:20` |
| `lunge` | 3 | 40% | `heroes/grave_robber/grave_robber.info.darkest:21` |
| `lunge` | 4 | 40% | `heroes/grave_robber/grave_robber.info.darkest:22` |
| `pick` | 0 | -15% | `heroes/grave_robber/grave_robber.info.darkest:13` |
| `pick` | 1 | -15% | `heroes/grave_robber/grave_robber.info.darkest:14` |
| `pick` | 2 | -15% | `heroes/grave_robber/grave_robber.info.darkest:15` |
| `pick` | 3 | -15% | `heroes/grave_robber/grave_robber.info.darkest:16` |
| `pick` | 4 | -15% | `heroes/grave_robber/grave_robber.info.darkest:17` |
| `poison_dart` | 0 | -60% | `heroes/grave_robber/grave_robber.info.darkest:39` |
| `poison_dart` | 1 | -60% | `heroes/grave_robber/grave_robber.info.darkest:40` |
| `poison_dart` | 2 | -60% | `heroes/grave_robber/grave_robber.info.darkest:41` |
| `poison_dart` | 3 | -60% | `heroes/grave_robber/grave_robber.info.darkest:42` |
| `poison_dart` | 4 | -60% | `heroes/grave_robber/grave_robber.info.darkest:43` |
| `shadow_fade` | 0 | -100% | `heroes/grave_robber/grave_robber.info.darkest:28` |
| `shadow_fade` | 1 | -100% | `heroes/grave_robber/grave_robber.info.darkest:29` |
| `shadow_fade` | 2 | -100% | `heroes/grave_robber/grave_robber.info.darkest:30` |
| `shadow_fade` | 3 | -100% | `heroes/grave_robber/grave_robber.info.darkest:31` |
| `shadow_fade` | 4 | -100% | `heroes/grave_robber/grave_robber.info.darkest:32` |
| `thrown_dagger` | 0 | -10% | `heroes/grave_robber/grave_robber.info.darkest:34` |
| `thrown_dagger` | 1 | -10% | `heroes/grave_robber/grave_robber.info.darkest:35` |
| `thrown_dagger` | 2 | -10% | `heroes/grave_robber/grave_robber.info.darkest:36` |
| `thrown_dagger` | 3 | -10% | `heroes/grave_robber/grave_robber.info.darkest:37` |
| `thrown_dagger` | 4 | -10% | `heroes/grave_robber/grave_robber.info.darkest:38` |
| `toxin_trickery` | 0 | 0% | `heroes/grave_robber/grave_robber.info.darkest:44` |
| `toxin_trickery` | 1 | 0% | `heroes/grave_robber/grave_robber.info.darkest:45` |
| `toxin_trickery` | 2 | 0% | `heroes/grave_robber/grave_robber.info.darkest:46` |
| `toxin_trickery` | 3 | 0% | `heroes/grave_robber/grave_robber.info.darkest:47` |
| `toxin_trickery` | 4 | 0% | `heroes/grave_robber/grave_robber.info.darkest:48` |
| `adrenaline_rush` | 0 | 0% | `heroes/hellion/hellion.info.darkest:39` |
| `adrenaline_rush` | 1 | 0% | `heroes/hellion/hellion.info.darkest:40` |
| `adrenaline_rush` | 2 | 0% | `heroes/hellion/hellion.info.darkest:41` |
| `adrenaline_rush` | 3 | 0% | `heroes/hellion/hellion.info.darkest:42` |
| `adrenaline_rush` | 4 | 0% | `heroes/hellion/hellion.info.darkest:43` |
| `barbaric_yawp` | 0 | -100% | `heroes/hellion/hellion.info.darkest:23` |
| `barbaric_yawp` | 1 | -100% | `heroes/hellion/hellion.info.darkest:24` |
| `barbaric_yawp` | 2 | -100% | `heroes/hellion/hellion.info.darkest:25` |
| `barbaric_yawp` | 3 | -100% | `heroes/hellion/hellion.info.darkest:26` |
| `barbaric_yawp` | 4 | -100% | `heroes/hellion/hellion.info.darkest:27` |
| `bleed_out` | 0 | 20% | `heroes/hellion/hellion.info.darkest:44` |
| `bleed_out` | 1 | 20% | `heroes/hellion/hellion.info.darkest:45` |
| `bleed_out` | 2 | 20% | `heroes/hellion/hellion.info.darkest:46` |
| `bleed_out` | 3 | 20% | `heroes/hellion/hellion.info.darkest:47` |
| `bleed_out` | 4 | 20% | `heroes/hellion/hellion.info.darkest:48` |
| `breakthru` | 0 | -50% | `heroes/hellion/hellion.info.darkest:34` |
| `breakthru` | 1 | -50% | `heroes/hellion/hellion.info.darkest:35` |
| `breakthru` | 2 | -50% | `heroes/hellion/hellion.info.darkest:36` |
| `breakthru` | 3 | -50% | `heroes/hellion/hellion.info.darkest:37` |
| `breakthru` | 4 | -50% | `heroes/hellion/hellion.info.darkest:38` |
| `if_it_bleeds` | 0 | -35% | `heroes/hellion/hellion.info.darkest:28` |
| `if_it_bleeds` | 1 | -35% | `heroes/hellion/hellion.info.darkest:29` |
| `if_it_bleeds` | 2 | -35% | `heroes/hellion/hellion.info.darkest:30` |
| `if_it_bleeds` | 3 | -35% | `heroes/hellion/hellion.info.darkest:31` |
| `if_it_bleeds` | 4 | -35% | `heroes/hellion/hellion.info.darkest:32` |
| `iron_swan` | 0 | 0% | `heroes/hellion/hellion.info.darkest:18` |
| `iron_swan` | 1 | 0% | `heroes/hellion/hellion.info.darkest:19` |
| `iron_swan` | 2 | 0% | `heroes/hellion/hellion.info.darkest:20` |
| `iron_swan` | 3 | 0% | `heroes/hellion/hellion.info.darkest:21` |
| `iron_swan` | 4 | 0% | `heroes/hellion/hellion.info.darkest:22` |
| `wicked_hack` | 0 | 0% | `heroes/hellion/hellion.info.darkest:13` |
| `wicked_hack` | 1 | 0% | `heroes/hellion/hellion.info.darkest:14` |
| `wicked_hack` | 2 | 0% | `heroes/hellion/hellion.info.darkest:15` |
| `wicked_hack` | 3 | 0% | `heroes/hellion/hellion.info.darkest:16` |
| `wicked_hack` | 4 | 0% | `heroes/hellion/hellion.info.darkest:17` |
| `duelist_advance` | 0 | -20% | `heroes/highwayman/highwayman.info.darkest:39` |
| `duelist_advance` | 1 | -20% | `heroes/highwayman/highwayman.info.darkest:40` |
| `duelist_advance` | 2 | -20% | `heroes/highwayman/highwayman.info.darkest:41` |
| `duelist_advance` | 3 | -20% | `heroes/highwayman/highwayman.info.darkest:42` |
| `duelist_advance` | 4 | -20% | `heroes/highwayman/highwayman.info.darkest:43` |
| `grape_shot_blast` | 0 | -50% | `heroes/highwayman/highwayman.info.darkest:28` |
| `grape_shot_blast` | 1 | -50% | `heroes/highwayman/highwayman.info.darkest:29` |
| `grape_shot_blast` | 2 | -50% | `heroes/highwayman/highwayman.info.darkest:30` |
| `grape_shot_blast` | 3 | -50% | `heroes/highwayman/highwayman.info.darkest:31` |
| `grape_shot_blast` | 4 | -50% | `heroes/highwayman/highwayman.info.darkest:32` |
| `opened_vein` | 0 | -15% | `heroes/highwayman/highwayman.info.darkest:44` |
| `opened_vein` | 1 | -15% | `heroes/highwayman/highwayman.info.darkest:45` |
| `opened_vein` | 2 | -15% | `heroes/highwayman/highwayman.info.darkest:46` |
| `opened_vein` | 3 | -15% | `heroes/highwayman/highwayman.info.darkest:47` |
| `opened_vein` | 4 | -15% | `heroes/highwayman/highwayman.info.darkest:48` |
| `pistol_shot` | 0 | -15% | `heroes/highwayman/highwayman.info.darkest:18` |
| `pistol_shot` | 1 | -15% | `heroes/highwayman/highwayman.info.darkest:19` |
| `pistol_shot` | 2 | -15% | `heroes/highwayman/highwayman.info.darkest:20` |
| `pistol_shot` | 3 | -15% | `heroes/highwayman/highwayman.info.darkest:21` |
| `pistol_shot` | 4 | -15% | `heroes/highwayman/highwayman.info.darkest:22` |
| `point_blank_shot` | 0 | 50% | `heroes/highwayman/highwayman.info.darkest:23` |
| `point_blank_shot` | 1 | 50% | `heroes/highwayman/highwayman.info.darkest:24` |
| `point_blank_shot` | 2 | 50% | `heroes/highwayman/highwayman.info.darkest:25` |
| `point_blank_shot` | 3 | 50% | `heroes/highwayman/highwayman.info.darkest:26` |
| `point_blank_shot` | 4 | 50% | `heroes/highwayman/highwayman.info.darkest:27` |
| `take_aim` | 0 | -80% | `heroes/highwayman/highwayman.info.darkest:34` |
| `take_aim` | 1 | -80% | `heroes/highwayman/highwayman.info.darkest:35` |
| `take_aim` | 2 | -80% | `heroes/highwayman/highwayman.info.darkest:36` |
| `take_aim` | 3 | -80% | `heroes/highwayman/highwayman.info.darkest:37` |
| `take_aim` | 4 | -80% | `heroes/highwayman/highwayman.info.darkest:38` |
| `wicked_slice` | 0 | 15% | `heroes/highwayman/highwayman.info.darkest:13` |
| `wicked_slice` | 1 | 15% | `heroes/highwayman/highwayman.info.darkest:14` |
| `wicked_slice` | 2 | 15% | `heroes/highwayman/highwayman.info.darkest:15` |
| `wicked_slice` | 3 | 15% | `heroes/highwayman/highwayman.info.darkest:16` |
| `wicked_slice` | 4 | 15% | `heroes/highwayman/highwayman.info.darkest:17` |
| `blackjack` | 0 | -65% | `heroes/houndmaster/houndmaster.info.darkest:44` |
| `blackjack` | 1 | -65% | `heroes/houndmaster/houndmaster.info.darkest:45` |
| `blackjack` | 2 | -65% | `heroes/houndmaster/houndmaster.info.darkest:46` |
| `blackjack` | 3 | -65% | `heroes/houndmaster/houndmaster.info.darkest:47` |
| `blackjack` | 4 | -65% | `heroes/houndmaster/houndmaster.info.darkest:48` |
| `guard_dog` | 0 | 0% | `heroes/houndmaster/houndmaster.info.darkest:34` |
| `guard_dog` | 1 | 0% | `heroes/houndmaster/houndmaster.info.darkest:35` |
| `guard_dog` | 2 | 0% | `heroes/houndmaster/houndmaster.info.darkest:36` |
| `guard_dog` | 3 | 0% | `heroes/houndmaster/houndmaster.info.darkest:37` |
| `guard_dog` | 4 | 0% | `heroes/houndmaster/houndmaster.info.darkest:38` |
| `hounds_harry` | 0 | -75% | `heroes/houndmaster/houndmaster.info.darkest:18` |
| `hounds_harry` | 1 | -75% | `heroes/houndmaster/houndmaster.info.darkest:19` |
| `hounds_harry` | 2 | -75% | `heroes/houndmaster/houndmaster.info.darkest:20` |
| `hounds_harry` | 3 | -75% | `heroes/houndmaster/houndmaster.info.darkest:21` |
| `hounds_harry` | 4 | -75% | `heroes/houndmaster/houndmaster.info.darkest:22` |
| `hounds_rush` | 0 | 0% | `heroes/houndmaster/houndmaster.info.darkest:13` |
| `hounds_rush` | 1 | 0% | `heroes/houndmaster/houndmaster.info.darkest:14` |
| `hounds_rush` | 2 | 0% | `heroes/houndmaster/houndmaster.info.darkest:15` |
| `hounds_rush` | 3 | 0% | `heroes/houndmaster/houndmaster.info.darkest:16` |
| `hounds_rush` | 4 | 0% | `heroes/houndmaster/houndmaster.info.darkest:17` |
| `howl` | 0 | 0% | `heroes/houndmaster/houndmaster.info.darkest:28` |
| `howl` | 1 | 0% | `heroes/houndmaster/houndmaster.info.darkest:29` |
| `howl` | 2 | 0% | `heroes/houndmaster/houndmaster.info.darkest:30` |
| `howl` | 3 | 0% | `heroes/houndmaster/houndmaster.info.darkest:31` |
| `howl` | 4 | 0% | `heroes/houndmaster/houndmaster.info.darkest:32` |
| `lick_wounds` | 0 | 0% | `heroes/houndmaster/houndmaster.info.darkest:39` |
| `lick_wounds` | 1 | 0% | `heroes/houndmaster/houndmaster.info.darkest:40` |
| `lick_wounds` | 2 | 0% | `heroes/houndmaster/houndmaster.info.darkest:41` |
| `lick_wounds` | 3 | 0% | `heroes/houndmaster/houndmaster.info.darkest:42` |
| `lick_wounds` | 4 | 0% | `heroes/houndmaster/houndmaster.info.darkest:43` |
| `whistle` | 0 | -100% | `heroes/houndmaster/houndmaster.info.darkest:23` |
| `whistle` | 1 | -100% | `heroes/houndmaster/houndmaster.info.darkest:24` |
| `whistle` | 2 | -100% | `heroes/houndmaster/houndmaster.info.darkest:25` |
| `whistle` | 3 | -100% | `heroes/houndmaster/houndmaster.info.darkest:26` |
| `whistle` | 4 | -100% | `heroes/houndmaster/houndmaster.info.darkest:27` |
| `battle_ballad` | 0 | 0% | `heroes/jester/jester.info.darkest:39` |
| `battle_ballad` | 1 | 0% | `heroes/jester/jester.info.darkest:40` |
| `battle_ballad` | 2 | 0% | `heroes/jester/jester.info.darkest:41` |
| `battle_ballad` | 3 | 0% | `heroes/jester/jester.info.darkest:42` |
| `battle_ballad` | 4 | 0% | `heroes/jester/jester.info.darkest:43` |
| `dirk_stab` | 0 | 0% | `heroes/jester/jester.info.darkest:13` |
| `dirk_stab` | 1 | 0% | `heroes/jester/jester.info.darkest:14` |
| `dirk_stab` | 2 | 0% | `heroes/jester/jester.info.darkest:15` |
| `dirk_stab` | 3 | 0% | `heroes/jester/jester.info.darkest:16` |
| `dirk_stab` | 4 | 0% | `heroes/jester/jester.info.darkest:17` |
| `harvest` | 0 | -50% | `heroes/jester/jester.info.darkest:18` |
| `harvest` | 1 | -50% | `heroes/jester/jester.info.darkest:19` |
| `harvest` | 2 | -50% | `heroes/jester/jester.info.darkest:20` |
| `harvest` | 3 | -50% | `heroes/jester/jester.info.darkest:21` |
| `harvest` | 4 | -50% | `heroes/jester/jester.info.darkest:22` |
| `heroic_end` | 0 | 50% | `heroes/jester/jester.info.darkest:23` |
| `heroic_end` | 1 | 50% | `heroes/jester/jester.info.darkest:24` |
| `heroic_end` | 2 | 50% | `heroes/jester/jester.info.darkest:25` |
| `heroic_end` | 3 | 50% | `heroes/jester/jester.info.darkest:26` |
| `heroic_end` | 4 | 50% | `heroes/jester/jester.info.darkest:27` |
| `inspiring_tune` | 0 | 0% | `heroes/jester/jester.info.darkest:44` |
| `inspiring_tune` | 1 | 0% | `heroes/jester/jester.info.darkest:45` |
| `inspiring_tune` | 2 | 0% | `heroes/jester/jester.info.darkest:46` |
| `inspiring_tune` | 3 | 0% | `heroes/jester/jester.info.darkest:47` |
| `inspiring_tune` | 4 | 0% | `heroes/jester/jester.info.darkest:48` |
| `slice_off` | 0 | -33% | `heroes/jester/jester.info.darkest:34` |
| `slice_off` | 1 | -33% | `heroes/jester/jester.info.darkest:35` |
| `slice_off` | 2 | -33% | `heroes/jester/jester.info.darkest:36` |
| `slice_off` | 3 | -33% | `heroes/jester/jester.info.darkest:37` |
| `slice_off` | 4 | -33% | `heroes/jester/jester.info.darkest:38` |
| `solo` | 0 | -100% | `heroes/jester/jester.info.darkest:28` |
| `solo` | 1 | -100% | `heroes/jester/jester.info.darkest:29` |
| `solo` | 2 | -100% | `heroes/jester/jester.info.darkest:30` |
| `solo` | 3 | -100% | `heroes/jester/jester.info.darkest:31` |
| `solo` | 4 | -100% | `heroes/jester/jester.info.darkest:32` |
| `chop` | 0 | 0% | `heroes/leper/leper.info.darkest:13` |
| `chop` | 1 | 0% | `heroes/leper/leper.info.darkest:14` |
| `chop` | 2 | 0% | `heroes/leper/leper.info.darkest:15` |
| `chop` | 3 | 0% | `heroes/leper/leper.info.darkest:16` |
| `chop` | 4 | 0% | `heroes/leper/leper.info.darkest:17` |
| `focus` | 0 | -40% | `heroes/leper/leper.info.darkest:23` |
| `focus` | 1 | -40% | `heroes/leper/leper.info.darkest:24` |
| `focus` | 2 | -40% | `heroes/leper/leper.info.darkest:25` |
| `focus` | 3 | -40% | `heroes/leper/leper.info.darkest:26` |
| `focus` | 4 | -40% | `heroes/leper/leper.info.darkest:27` |
| `hew` | 0 | -50% | `heroes/leper/leper.info.darkest:18` |
| `hew` | 1 | -50% | `heroes/leper/leper.info.darkest:19` |
| `hew` | 2 | -50% | `heroes/leper/leper.info.darkest:20` |
| `hew` | 3 | -50% | `heroes/leper/leper.info.darkest:21` |
| `hew` | 4 | -50% | `heroes/leper/leper.info.darkest:22` |
| `intimidate` | 0 | -85% | `heroes/leper/leper.info.darkest:44` |
| `intimidate` | 1 | -85% | `heroes/leper/leper.info.darkest:45` |
| `intimidate` | 2 | -85% | `heroes/leper/leper.info.darkest:46` |
| `intimidate` | 3 | -80% | `heroes/leper/leper.info.darkest:47` |
| `intimidate` | 4 | -80% | `heroes/leper/leper.info.darkest:48` |
| `revenge` | 0 | 0% | `heroes/leper/leper.info.darkest:28` |
| `revenge` | 1 | 0% | `heroes/leper/leper.info.darkest:29` |
| `revenge` | 2 | 0% | `heroes/leper/leper.info.darkest:30` |
| `revenge` | 3 | 0% | `heroes/leper/leper.info.darkest:31` |
| `revenge` | 4 | 0% | `heroes/leper/leper.info.darkest:32` |
| `solemnity` | 0 | 0% | `heroes/leper/leper.info.darkest:39` |
| `solemnity` | 1 | 0% | `heroes/leper/leper.info.darkest:40` |
| `solemnity` | 2 | 0% | `heroes/leper/leper.info.darkest:41` |
| `solemnity` | 3 | 0% | `heroes/leper/leper.info.darkest:42` |
| `solemnity` | 4 | 0% | `heroes/leper/leper.info.darkest:43` |
| `withstand` | 0 | 0% | `heroes/leper/leper.info.darkest:34` |
| `withstand` | 1 | 0% | `heroes/leper/leper.info.darkest:35` |
| `withstand` | 2 | 0% | `heroes/leper/leper.info.darkest:36` |
| `withstand` | 3 | 0% | `heroes/leper/leper.info.darkest:37` |
| `withstand` | 4 | 0% | `heroes/leper/leper.info.darkest:38` |
| `bellow` | 0 | -100% | `heroes/man_at_arms/man_at_arms.info.darkest:23` |
| `bellow` | 1 | -100% | `heroes/man_at_arms/man_at_arms.info.darkest:24` |
| `bellow` | 2 | -100% | `heroes/man_at_arms/man_at_arms.info.darkest:25` |
| `bellow` | 3 | -100% | `heroes/man_at_arms/man_at_arms.info.darkest:26` |
| `bellow` | 4 | -100% | `heroes/man_at_arms/man_at_arms.info.darkest:27` |
| `bolster` | 0 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:44` |
| `bolster` | 1 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:45` |
| `bolster` | 2 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:46` |
| `bolster` | 3 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:47` |
| `bolster` | 4 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:48` |
| `command` | 0 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:39` |
| `command` | 1 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:40` |
| `command` | 2 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:41` |
| `command` | 3 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:42` |
| `command` | 4 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:43` |
| `crush` | 0 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:13` |
| `crush` | 1 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:14` |
| `crush` | 2 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:15` |
| `crush` | 3 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:16` |
| `crush` | 4 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:17` |
| `defender` | 0 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:28` |
| `defender` | 1 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:29` |
| `defender` | 2 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:30` |
| `defender` | 3 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:31` |
| `defender` | 4 | 0% | `heroes/man_at_arms/man_at_arms.info.darkest:32` |
| `rampart` | 0 | -60% | `heroes/man_at_arms/man_at_arms.info.darkest:18` |
| `rampart` | 1 | -60% | `heroes/man_at_arms/man_at_arms.info.darkest:19` |
| `rampart` | 2 | -60% | `heroes/man_at_arms/man_at_arms.info.darkest:20` |
| `rampart` | 3 | -60% | `heroes/man_at_arms/man_at_arms.info.darkest:21` |
| `rampart` | 4 | -60% | `heroes/man_at_arms/man_at_arms.info.darkest:22` |
| `retribution` | 0 | -75% | `heroes/man_at_arms/man_at_arms.info.darkest:34` |
| `retribution` | 1 | -75% | `heroes/man_at_arms/man_at_arms.info.darkest:35` |
| `retribution` | 2 | -75% | `heroes/man_at_arms/man_at_arms.info.darkest:36` |
| `retribution` | 3 | -75% | `heroes/man_at_arms/man_at_arms.info.darkest:37` |
| `retribution` | 4 | -75% | `heroes/man_at_arms/man_at_arms.info.darkest:38` |
| `abyssal_artillery` | 0 | -33% | `heroes/occultist/occultist.info.darkest:18` |
| `abyssal_artillery` | 1 | -33% | `heroes/occultist/occultist.info.darkest:19` |
| `abyssal_artillery` | 2 | -33% | `heroes/occultist/occultist.info.darkest:20` |
| `abyssal_artillery` | 3 | -33% | `heroes/occultist/occultist.info.darkest:21` |
| `abyssal_artillery` | 4 | -33% | `heroes/occultist/occultist.info.darkest:22` |
| `bloodlet` | 0 | 0% | `heroes/occultist/occultist.info.darkest:13` |
| `bloodlet` | 1 | 0% | `heroes/occultist/occultist.info.darkest:14` |
| `bloodlet` | 2 | 0% | `heroes/occultist/occultist.info.darkest:15` |
| `bloodlet` | 3 | 0% | `heroes/occultist/occultist.info.darkest:16` |
| `bloodlet` | 4 | 0% | `heroes/occultist/occultist.info.darkest:17` |
| `daemons_pull` | 0 | -50% | `heroes/occultist/occultist.info.darkest:44` |
| `daemons_pull` | 1 | -50% | `heroes/occultist/occultist.info.darkest:45` |
| `daemons_pull` | 2 | -50% | `heroes/occultist/occultist.info.darkest:46` |
| `daemons_pull` | 3 | -50% | `heroes/occultist/occultist.info.darkest:47` |
| `daemons_pull` | 4 | -50% | `heroes/occultist/occultist.info.darkest:48` |
| `disruptive_curse` | 0 | -90% | `heroes/occultist/occultist.info.darkest:34` |
| `disruptive_curse` | 1 | -90% | `heroes/occultist/occultist.info.darkest:35` |
| `disruptive_curse` | 2 | -90% | `heroes/occultist/occultist.info.darkest:36` |
| `disruptive_curse` | 3 | -90% | `heroes/occultist/occultist.info.darkest:37` |
| `disruptive_curse` | 4 | -90% | `heroes/occultist/occultist.info.darkest:38` |
| `hands_from_abyss` | 0 | -50% | `heroes/occultist/occultist.info.darkest:39` |
| `hands_from_abyss` | 1 | -50% | `heroes/occultist/occultist.info.darkest:40` |
| `hands_from_abyss` | 2 | -50% | `heroes/occultist/occultist.info.darkest:41` |
| `hands_from_abyss` | 3 | -50% | `heroes/occultist/occultist.info.darkest:42` |
| `hands_from_abyss` | 4 | -50% | `heroes/occultist/occultist.info.darkest:43` |
| `weakening_curse` | 0 | -75% | `heroes/occultist/occultist.info.darkest:23` |
| `weakening_curse` | 1 | -75% | `heroes/occultist/occultist.info.darkest:24` |
| `weakening_curse` | 2 | -75% | `heroes/occultist/occultist.info.darkest:25` |
| `weakening_curse` | 3 | -75% | `heroes/occultist/occultist.info.darkest:26` |
| `weakening_curse` | 4 | -75% | `heroes/occultist/occultist.info.darkest:27` |
| `wyrd_reconstruction` | 0 | -- | `heroes/occultist/occultist.info.darkest:28` |
| `wyrd_reconstruction` | 1 | -- | `heroes/occultist/occultist.info.darkest:29` |
| `wyrd_reconstruction` | 2 | -- | `heroes/occultist/occultist.info.darkest:30` |
| `wyrd_reconstruction` | 3 | -- | `heroes/occultist/occultist.info.darkest:31` |
| `wyrd_reconstruction` | 4 | -- | `heroes/occultist/occultist.info.darkest:32` |
| `battlefield_medicine` | 0 | -- | `heroes/plague_doctor/plague_doctor.info.darkest:34` |
| `battlefield_medicine` | 1 | -- | `heroes/plague_doctor/plague_doctor.info.darkest:35` |
| `battlefield_medicine` | 2 | -- | `heroes/plague_doctor/plague_doctor.info.darkest:36` |
| `battlefield_medicine` | 3 | -- | `heroes/plague_doctor/plague_doctor.info.darkest:37` |
| `battlefield_medicine` | 4 | -- | `heroes/plague_doctor/plague_doctor.info.darkest:38` |
| `blinding_gas` | 0 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:23` |
| `blinding_gas` | 1 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:24` |
| `blinding_gas` | 2 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:25` |
| `blinding_gas` | 3 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:26` |
| `blinding_gas` | 4 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:27` |
| `disorienting_blast` | 0 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:44` |
| `disorienting_blast` | 1 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:45` |
| `disorienting_blast` | 2 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:46` |
| `disorienting_blast` | 3 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:47` |
| `disorienting_blast` | 4 | -100% | `heroes/plague_doctor/plague_doctor.info.darkest:48` |
| `emboldening_vapours` | 0 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:39` |
| `emboldening_vapours` | 1 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:40` |
| `emboldening_vapours` | 2 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:41` |
| `emboldening_vapours` | 3 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:42` |
| `emboldening_vapours` | 4 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:43` |
| `incision` | 0 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:28` |
| `incision` | 1 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:29` |
| `incision` | 2 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:30` |
| `incision` | 3 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:31` |
| `incision` | 4 | 0% | `heroes/plague_doctor/plague_doctor.info.darkest:32` |
| `noxious_blast` | 0 | -80% | `heroes/plague_doctor/plague_doctor.info.darkest:13` |
| `noxious_blast` | 1 | -80% | `heroes/plague_doctor/plague_doctor.info.darkest:14` |
| `noxious_blast` | 2 | -80% | `heroes/plague_doctor/plague_doctor.info.darkest:15` |
| `noxious_blast` | 3 | -80% | `heroes/plague_doctor/plague_doctor.info.darkest:16` |
| `noxious_blast` | 4 | -80% | `heroes/plague_doctor/plague_doctor.info.darkest:17` |
| `plague_grenade` | 0 | -90% | `heroes/plague_doctor/plague_doctor.info.darkest:18` |
| `plague_grenade` | 1 | -90% | `heroes/plague_doctor/plague_doctor.info.darkest:19` |
| `plague_grenade` | 2 | -90% | `heroes/plague_doctor/plague_doctor.info.darkest:20` |
| `plague_grenade` | 3 | -90% | `heroes/plague_doctor/plague_doctor.info.darkest:21` |
| `plague_grenade` | 4 | -90% | `heroes/plague_doctor/plague_doctor.info.darkest:22` |
| `dazzling_light` | 0 | -75% | `heroes/vestal/vestal.info.darkest:23` |
| `dazzling_light` | 1 | -75% | `heroes/vestal/vestal.info.darkest:24` |
| `dazzling_light` | 2 | -75% | `heroes/vestal/vestal.info.darkest:25` |
| `dazzling_light` | 3 | -75% | `heroes/vestal/vestal.info.darkest:26` |
| `dazzling_light` | 4 | -75% | `heroes/vestal/vestal.info.darkest:27` |
| `divine_grace` | 0 | -- | `heroes/vestal/vestal.info.darkest:28` |
| `divine_grace` | 1 | -- | `heroes/vestal/vestal.info.darkest:29` |
| `divine_grace` | 2 | -- | `heroes/vestal/vestal.info.darkest:30` |
| `divine_grace` | 3 | -- | `heroes/vestal/vestal.info.darkest:31` |
| `divine_grace` | 4 | -- | `heroes/vestal/vestal.info.darkest:32` |
| `gods_comfort` | 0 | -- | `heroes/vestal/vestal.info.darkest:34` |
| `gods_comfort` | 1 | -- | `heroes/vestal/vestal.info.darkest:35` |
| `gods_comfort` | 2 | -- | `heroes/vestal/vestal.info.darkest:36` |
| `gods_comfort` | 3 | -- | `heroes/vestal/vestal.info.darkest:37` |
| `gods_comfort` | 4 | -- | `heroes/vestal/vestal.info.darkest:38` |
| `gods_hand` | 0 | -50% | `heroes/vestal/vestal.info.darkest:44` |
| `gods_hand` | 1 | -50% | `heroes/vestal/vestal.info.darkest:45` |
| `gods_hand` | 2 | -50% | `heroes/vestal/vestal.info.darkest:46` |
| `gods_hand` | 3 | -50% | `heroes/vestal/vestal.info.darkest:47` |
| `gods_hand` | 4 | -50% | `heroes/vestal/vestal.info.darkest:48` |
| `gods_illumination` | 0 | -75% | `heroes/vestal/vestal.info.darkest:39` |
| `gods_illumination` | 1 | -75% | `heroes/vestal/vestal.info.darkest:40` |
| `gods_illumination` | 2 | -75% | `heroes/vestal/vestal.info.darkest:41` |
| `gods_illumination` | 3 | -75% | `heroes/vestal/vestal.info.darkest:42` |
| `gods_illumination` | 4 | -75% | `heroes/vestal/vestal.info.darkest:43` |
| `judgement` | 0 | -25% | `heroes/vestal/vestal.info.darkest:18` |
| `judgement` | 1 | -25% | `heroes/vestal/vestal.info.darkest:19` |
| `judgement` | 2 | -25% | `heroes/vestal/vestal.info.darkest:20` |
| `judgement` | 3 | -25% | `heroes/vestal/vestal.info.darkest:21` |
| `judgement` | 4 | -25% | `heroes/vestal/vestal.info.darkest:22` |
| `mace_bash` | 0 | 0% | `heroes/vestal/vestal.info.darkest:13` |
| `mace_bash` | 1 | 0% | `heroes/vestal/vestal.info.darkest:14` |
| `mace_bash` | 2 | 0% | `heroes/vestal/vestal.info.darkest:15` |
| `mace_bash` | 3 | 0% | `heroes/vestal/vestal.info.darkest:16` |
| `mace_bash` | 4 | 0% | `heroes/vestal/vestal.info.darkest:17` |
