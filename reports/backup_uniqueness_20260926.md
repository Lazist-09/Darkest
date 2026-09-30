# 备份目录可删性取证（只读 · 对照活仓库 object store）

- 活仓库：`F:\GithubPro\Darkest`
- 时间：2026-09-26 23:25
- 判据：**提交/内容在活仓库 object store 里存在 ⇒ 备份里那份不是唯一来源**

## Darkest.verify

- refs 指向的提交：**3** 个 · 活仓库里**缺 0** 个 ✅
- HEAD = `09301c9a`
- 相对自身 HEAD 有改动/未跟踪：**35** 个文件（>50MB 跳过 0 个）
- 🔴 **内容只在备份里的：20 个 / 407 KB** ⇒ 删前必须先救出来：
  - `darkest/data/buildings.json`  (64.5 KB)
  - `darkest/scripts/data/HeroAssets.cs`  (18.3 KB)
  - `darkest/tests/WeaponDamageModelStage1Tests.cs`  (7 KB)
  - `darkest/scripts/gameplay/scene/ExpeditionComposition.cs`  (9.4 KB)
  - `darkest/scripts/gameplay/scene/BattleRoot.PlayerActions.cs`  (12.7 KB)
  - `darkest/tests/UnitTierStage1Tests.cs`  (8.8 KB)
  - `darkest/scripts/gameplay/scene/BattleRoot.cs`  (34.9 KB)
  - `darkest/scripts/gameplay/sim/pipeline/EffectsStep.cs`  (4.8 KB)
  - `darkest/project.godot`  (1.7 KB)
  - `darkest/data/quirks.json`  (102.6 KB)
  - `tools/dsh/verify_hero_placeholders.py`  (6.6 KB)
  - `darkest/scripts/gameplay/sim/board/UnitStatsMapper.cs`  (1 KB)
  - `darkest/scripts/data/UnitsConfig.cs`  (9.7 KB)
  - `darkest/data/units.json`  (7.1 KB)
  - `darkest/scripts/gameplay/sim/run/RunStartSnapshot.cs`  (11.6 KB)
  - `darkest/scripts/core/contracts/UnitStats.cs`  (3 KB)
  - `darkest/data/trinkets.json`  (75.9 KB)
  - `darkest/scripts/data/EconomyConfig.cs`  (14.7 KB)
  - `darkest/scripts/core/math/BattleMath.cs`  (10.7 KB)
  - `darkest/data/economy.json`  (2.1 KB)

## Darkest-backup-20260921_000640

- refs 指向的提交：**3** 个 · 活仓库里**缺 0** 个 ✅
- HEAD = `71a63051`
- 相对自身 HEAD 有改动/未跟踪：**213** 个文件（>50MB 跳过 0 个）
- 🔴 **内容只在备份里的：36 个 / 1940.8 KB** ⇒ 删前必须先救出来：
  - `.workbuddy/_tmp_append.py`  (0 KB)
  - `darkest/scripts/gameplay/sim/run/TrapDefs.cs`  (19.3 KB)
  - `reports/LATEST.md`  (0.2 KB)
  - `darkest/scripts/gameplay/sim/survival/LightMeter.cs`  (7.2 KB)
  - `skills/godot-ui-designer/SKILL.md`  (54.6 KB)
  - `darkest/tests/TrapFlowTests.cs`  (18.4 KB)
  - `doc/architecture/goal2_stealth.md`  (12.3 KB)
  - `doc/windows/架构窗口.txt`  (4.1 KB)
  - `doc/modules/dd1_baseline.md`  (91.5 KB)
  - `doc/modules/trinkets.md`  (8.2 KB)
  - `darkest/scripts/gameplay/sim/morale/MoraleLedger.cs`  (14.4 KB)
  - `darkest/tests/TrapTests.cs`  (19.9 KB)
  - `.workbuddy/memory/2026-09-20.md`  (45.6 KB)
  - `skills/darkest-game-designer/SKILL.md`  (76.3 KB)
  - `skills/darkest-architect/SKILL.md`  (54.5 KB)
  - `.workbuddy/memory/2026-09-19.md`  (40.6 KB)
  - `doc/modules/dd_reference.md`  (28.1 KB)
  - `doc/architecture/data_schema.md`  (179.2 KB)
  - `doc/windows/主程序窗口.txt`  (423.2 KB)
  - `skills/darkest-lead-programmer/SKILL.md`  (70.5 KB)
  - `_GIT_STATUS_AT_BACKUP.txt`  (9 KB)
  - `darkest/tests/SecretFlowTests.cs`  (20.9 KB)
  - `.workbuddy/memory/MEMORY.md`  (12.5 KB)
  - `tools/check_no_external_assets.py`  (6.6 KB)
  - `darkest/tests/GridVisionFlowTests.cs`  (15.7 KB)
  - `doc/state.md`  (520.7 KB)
  - `darkest/tests/RevisitTests.cs`  (27.6 KB)
  - `doc/architecture/goal1_combat_relocation.md`  (7.6 KB)
  - `tools/deadkey_allowlist.txt`  (3.8 KB)
  - `darkest/tests/StealthTests.cs`  (15.4 KB)
  - `tools/check_data_discipline.py`  (17.7 KB)
  - `.workbuddy/memory/2026-09-18.md`  (20.6 KB)
  - `darkest/tests/GridVisionTests.cs`  (15 KB)
  - `darkest/tests/StealthFollowUpTests.cs`  (14 KB)
  - `doc/modules/ui_spec.md`  (64 KB)
  - `tools/deadfunc_allowlist.txt`  (1.6 KB)

## Darkest-backup-20260921_011818

- refs 指向的提交：**3** 个 · 活仓库里**缺 0** 个 ✅
- HEAD = `af05680f`
- 相对自身 HEAD 有改动/未跟踪：**212** 个文件（>50MB 跳过 0 个）
- 🔴 **内容只在备份里的：34 个 / 1747.9 KB** ⇒ 删前必须先救出来：
  - `.workbuddy/_tmp_append.py`  (0 KB)
  - `darkest/scripts/gameplay/sim/run/TrapDefs.cs`  (19.3 KB)
  - `reports/LATEST.md`  (0.2 KB)
  - `darkest/scripts/gameplay/sim/survival/LightMeter.cs`  (7.2 KB)
  - `skills/godot-ui-designer/SKILL.md`  (58.6 KB)
  - `darkest/tests/TrapFlowTests.cs`  (18.4 KB)
  - `doc/architecture/goal2_stealth.md`  (12.3 KB)
  - `doc/modules/dd1_baseline.md`  (108.6 KB)
  - `darkest/scripts/gameplay/sim/morale/MoraleLedger.cs`  (14.4 KB)
  - `darkest/tests/TrapTests.cs`  (19.9 KB)
  - `.workbuddy/memory/2026-09-20.md`  (45.6 KB)
  - `skills/darkest-game-designer/SKILL.md`  (80.3 KB)
  - `skills/darkest-architect/SKILL.md`  (58.4 KB)
  - `.workbuddy/memory/2026-09-19.md`  (40.6 KB)
  - `doc/modules/dd_reference.md`  (28.1 KB)
  - `doc/architecture/data_schema.md`  (179.2 KB)
  - `doc/modules/dd1_baseline.md.bak.20260921_003517`  (108.1 KB)
  - `skills/darkest-lead-programmer/SKILL.md`  (74.4 KB)
  - `_GIT_STATUS_AT_BACKUP.txt`  (9 KB)
  - `darkest/tests/SecretFlowTests.cs`  (20.9 KB)
  - `.workbuddy/memory/MEMORY.md`  (12.5 KB)
  - `doc/modules/dd1_baseline.md.bak2.20260921_003809`  (108 KB)
  - `darkest/tests/GridVisionFlowTests.cs`  (15.7 KB)
  - `doc/state.md`  (520.7 KB)
  - `darkest/tests/RevisitTests.cs`  (27.6 KB)
  - `doc/architecture/goal1_combat_relocation.md`  (7.6 KB)
  - `tools/deadkey_allowlist.txt`  (3.8 KB)
  - `darkest/tests/StealthTests.cs`  (15.4 KB)
  - `tools/check_data_discipline.py`  (17.7 KB)
  - `.workbuddy/memory/2026-09-18.md`  (20.6 KB)
  - `darkest/tests/GridVisionTests.cs`  (15 KB)
  - `darkest/tests/StealthFollowUpTests.cs`  (14 KB)
  - `doc/modules/ui_spec.md`  (64 KB)
  - `tools/deadfunc_allowlist.txt`  (1.6 KB)

## Darkest-backup-20260921_124311

- refs 指向的提交：**3** 个 · 活仓库里**缺 0** 个 ✅
- HEAD = `98dd674d`
- 相对自身 HEAD 有改动/未跟踪：**253** 个文件（>50MB 跳过 0 个）
- 🔴 **内容只在备份里的：34 个 / 1749.9 KB** ⇒ 删前必须先救出来：
  - `darkest/scripts/gameplay/sim/run/TrapDefs.cs`  (19.3 KB)
  - `reports/LATEST.md`  (0.2 KB)
  - `darkest/scripts/gameplay/sim/survival/LightMeter.cs`  (7.2 KB)
  - `skills/godot-ui-designer/SKILL.md`  (58.6 KB)
  - `doc/state.md`  (520.7 KB)
  - `darkest/tests/TrapFlowTests.cs`  (18.4 KB)
  - `doc/architecture/goal2_stealth.md`  (12.3 KB)
  - `doc/modules/dd1_baseline.md`  (108.6 KB)
  - `darkest/scripts/gameplay/sim/morale/MoraleLedger.cs`  (14.4 KB)
  - `darkest/tests/TrapTests.cs`  (19.9 KB)
  - `skills/darkest-game-designer/SKILL.md`  (80.3 KB)
  - `.workbuddy/memory/2026-09-19.md`  (40.6 KB)
  - `doc/modules/dd_reference.md`  (28.1 KB)
  - `doc/architecture/data_schema.md`  (179.2 KB)
  - `doc/modules/dd1_baseline.md.bak.20260921_003517`  (108.1 KB)
  - `skills/darkest-lead-programmer/SKILL.md`  (74.4 KB)
  - `_GIT_STATUS_AT_BACKUP.txt`  (11 KB)
  - `doc/modules/dd1_baseline.md.bak2.20260921_003809`  (108 KB)
  - `.workbuddy/_tmp_append.py`  (0 KB)
  - `darkest/tests/SecretFlowTests.cs`  (20.9 KB)
  - `.workbuddy/memory/MEMORY.md`  (12.5 KB)
  - `darkest/tests/RevisitTests.cs`  (27.6 KB)
  - `doc/architecture/goal1_combat_relocation.md`  (7.6 KB)
  - `tools/deadkey_allowlist.txt`  (3.8 KB)
  - `darkest/tests/StealthTests.cs`  (15.4 KB)
  - `darkest/tests/GridVisionFlowTests.cs`  (15.7 KB)
  - `.workbuddy/memory/2026-09-18.md`  (20.6 KB)
  - `darkest/tests/GridVisionTests.cs`  (15 KB)
  - `tools/check_data_discipline.py`  (17.7 KB)
  - `darkest/tests/StealthFollowUpTests.cs`  (14 KB)
  - `doc/modules/ui_spec.md`  (64 KB)
  - `tools/deadfunc_allowlist.txt`  (1.6 KB)
  - `skills/darkest-architect/SKILL.md`  (58.4 KB)
  - `.workbuddy/memory/2026-09-20.md`  (45.6 KB)

## Darkest-backup-20260921_124836

- refs 指向的提交：**3** 个 · 活仓库里**缺 0** 个 ✅
- HEAD = `87793b1d`
- 相对自身 HEAD 有改动/未跟踪：**253** 个文件（>50MB 跳过 0 个）
- 🔴 **内容只在备份里的：34 个 / 1749.9 KB** ⇒ 删前必须先救出来：
  - `darkest/scripts/gameplay/sim/run/TrapDefs.cs`  (19.3 KB)
  - `reports/LATEST.md`  (0.2 KB)
  - `darkest/scripts/gameplay/sim/survival/LightMeter.cs`  (7.2 KB)
  - `skills/godot-ui-designer/SKILL.md`  (58.6 KB)
  - `doc/state.md`  (520.7 KB)
  - `darkest/tests/TrapFlowTests.cs`  (18.4 KB)
  - `doc/architecture/goal2_stealth.md`  (12.3 KB)
  - `doc/modules/dd1_baseline.md`  (108.6 KB)
  - `darkest/scripts/gameplay/sim/morale/MoraleLedger.cs`  (14.4 KB)
  - `darkest/tests/TrapTests.cs`  (19.9 KB)
  - `skills/darkest-game-designer/SKILL.md`  (80.3 KB)
  - `.workbuddy/memory/2026-09-19.md`  (40.6 KB)
  - `doc/modules/dd_reference.md`  (28.1 KB)
  - `doc/architecture/data_schema.md`  (179.2 KB)
  - `doc/modules/dd1_baseline.md.bak.20260921_003517`  (108.1 KB)
  - `skills/darkest-lead-programmer/SKILL.md`  (74.4 KB)
  - `_GIT_STATUS_AT_BACKUP.txt`  (11 KB)
  - `doc/modules/dd1_baseline.md.bak2.20260921_003809`  (108 KB)
  - `.workbuddy/_tmp_append.py`  (0 KB)
  - `darkest/tests/SecretFlowTests.cs`  (20.9 KB)
  - `.workbuddy/memory/MEMORY.md`  (12.5 KB)
  - `darkest/tests/RevisitTests.cs`  (27.6 KB)
  - `doc/architecture/goal1_combat_relocation.md`  (7.6 KB)
  - `tools/deadkey_allowlist.txt`  (3.8 KB)
  - `darkest/tests/StealthTests.cs`  (15.4 KB)
  - `darkest/tests/GridVisionFlowTests.cs`  (15.7 KB)
  - `.workbuddy/memory/2026-09-18.md`  (20.6 KB)
  - `darkest/tests/GridVisionTests.cs`  (15 KB)
  - `tools/check_data_discipline.py`  (17.7 KB)
  - `darkest/tests/StealthFollowUpTests.cs`  (14 KB)
  - `doc/modules/ui_spec.md`  (64 KB)
  - `tools/deadfunc_allowlist.txt`  (1.6 KB)
  - `skills/darkest-architect/SKILL.md`  (58.4 KB)
  - `.workbuddy/memory/2026-09-20.md`  (45.6 KB)

## Darkest-backup-20260921_235637

- refs 指向的提交：**3** 个 · 活仓库里**缺 0** 个 ✅
- HEAD = `5a2c231b`
- 相对自身 HEAD 有改动/未跟踪：**9** 个文件（>50MB 跳过 0 个）
- 🔴 **内容只在备份里的：9 个 / 457.8 KB** ⇒ 删前必须先救出来：
  - `BACKUP_INFO.txt`  (0.2 KB)
  - `doc/modules/dd1_baseline.md`  (127.9 KB)
  - `reports/smoke_summary_20260921_2315.txt`  (1.1 KB)
  - `skills/godot-ui-designer/SKILL.md`  (67.5 KB)
  - `reports/INDEX_lead_programmer.md`  (8.9 KB)
  - `skills/darkest-game-designer/SKILL.md`  (89.2 KB)
  - `skills/darkest-lead-programmer/SKILL.md`  (83.4 KB)
  - `darkest/scripts/data/BuffDefsConfig.cs`  (12.1 KB)
  - `skills/darkest-architect/SKILL.md`  (67.4 KB)

## 合计

- 只在备份里的提交：**0** 个
- 只在备份里的文件内容：**167** 个
