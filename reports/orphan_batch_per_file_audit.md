# 孤儿批「逐文件核准表」（架构 ⑨ 裁 (乙) ⇒ **降级形态 = 逐文件标注** ✓）

> 🔴 **架构裁定原文**：「⑨ 误提处置 ⇒ **取 (乙) 逐文件摘出**（**但若"摘出"成本高 ⇒ 允许先做【逐文件标注】**）」✓

> 🔴 **本表能担保什么、不能担保什么（如实分开写 ✓）**：
> · ✅ **能担保**：这些文件**在树上能编译**（构建 0 错）· **全量 809/809 绿** · **门禁全绿**（文件规模/B6/数据纪律/godot 引用）·
>   **每批都写了归属**（提交信息里标"内容非我所写" ✓）· **用的都是显式路径**（不是通配符 ✓）
> · 🔴 **不能担保**：**内容对不对** —— 这些**不是我写的** ✗ ⇒ 我不能替作者担保其正确性 ✓
> · ✅ **可追溯**：每个文件都能定位到**提交号 + 批次 + 归属域**（下表 ✓）

## 批次总览（237 个文件）

| 批次 | 提交 | 文件数 | 归属 | 我核过的 |
|---|---|---|---|---|
| 工程/卫生 | `b5ef094` | 8 | 混合 | 构建+全量+门禁 ✓ |
| B1 数据/解析/core | `427e2ef` | 19 | 主程序+策划 | 构建+全量+门禁 ✓ |
| B2 sim 层 | `cb6c918` | 47 | 主程序 | 构建+全量+门禁 ✓ |
| B3 scene 层 | `2e7ea63` | 5 | 主程序 | 构建+全量+门禁 ✓ |
| B4 tests 层 | `2656bd1` | 80 | 主程序 | 构建+全量+门禁 ✓ |
| B5 UI 域 | `2ef9bba` | 40 | UI 设计师 | 构建+全量+门禁 ✓ |
| B6 doc 契约 | `3e20915` | 30 | 策划+架构 | 构建+全量+门禁 ✓ |
| B7 其余 | `2ef77b4` | 3 | 混合 | 构建+全量+门禁 ✓ |
| B8 收尾 | `7fff261` | 5 | 混合 | 构建+全量+门禁 ✓ |

**合计 = 237 个文件**（9 批 ✓ 每批均附显式清单 ✓）

## 逐文件明细

### 工程/卫生 · `b5ef094` · 混合 · 8 个文件

- `.gitignore`
- `darkest/project.godot`
- `skills/darkest-architect/SKILL.md`
- `skills/darkest-game-designer/SKILL.md`
- `skills/darkest-lead-programmer/SKILL.md`
- `skills/godot-ui-designer/SKILL.md`
- `tools/deadfunc_allowlist.txt`
- `tools/deadkey_allowlist.txt`

### B1 数据/解析/core · `427e2ef` · 主程序+策划 · 19 个文件

- `darkest/data/buff_defs.json`
- `darkest/data/skills.json`
- `darkest/data/trap_defs.json`
- `darkest/data/tuning.json`
- `darkest/scripts/core/contracts/ISkillUseResolver.cs`
- `darkest/scripts/data/BuffDefsConfig.cs`
- `darkest/scripts/data/BuffPrimitiveTranslation.cs.uid`
- `darkest/scripts/data/BuildingsConfig.cs.uid`
- `darkest/scripts/data/CampSkillsConfig.cs`
- `darkest/scripts/data/CuriosConfig.cs`
- `darkest/scripts/data/HeroArtResolver.cs.uid`
- `darkest/scripts/data/QuirksConfig.cs.uid`
- `darkest/scripts/data/SkillsConfig.cs`
- `darkest/scripts/data/SmokeStepSpec.cs.uid`
- `darkest/scripts/data/TrinketsConfig.cs.uid`
- `darkest/scripts/data/TuningConfig.ExpeditionAndCombat.cs`
- `darkest/scripts/data/TuningConfig.ExpeditionAndCombat.cs.uid`
- `darkest/scripts/data/TuningConfig.cs`
- `darkest/scripts/data/UiShellLedger.cs.uid`

### B2 sim 层 · `cb6c918` · 主程序 · 47 个文件

- `darkest/scripts/gameplay/sim/board/FormationBoard.cs`
- `darkest/scripts/gameplay/sim/board/ObstacleRuntime.cs`
- `darkest/scripts/gameplay/sim/board/UnitRuntime.cs`
- `darkest/scripts/gameplay/sim/director/BattleDirector.OvertimeAndRetreat.cs`
- `darkest/scripts/gameplay/sim/director/BattleDirector.OvertimeAndRetreat.cs.uid`
- `darkest/scripts/gameplay/sim/director/BattleDirector.Types.cs.uid`
- `darkest/scripts/gameplay/sim/director/BattleDirector.cs`
- `darkest/scripts/gameplay/sim/director/BattleProjector.cs`
- `darkest/scripts/gameplay/sim/enemy/EnemyAi.cs`
- `darkest/scripts/gameplay/sim/morale/MoraleLedger.cs`
- `darkest/scripts/gameplay/sim/morale/MoraleLedger.cs.uid`
- `darkest/scripts/gameplay/sim/pipeline/DamagePipeline.cs`
- `darkest/scripts/gameplay/sim/run/DungeonGrid.cs`
- `darkest/scripts/gameplay/sim/run/DungeonGridConfig.cs`
- `darkest/scripts/gameplay/sim/run/DungeonGridDeriver.cs`
- `darkest/scripts/gameplay/sim/run/Economy.cs`
- `darkest/scripts/gameplay/sim/run/ExpeditionFlow.BattleReturn.cs.uid`
- `darkest/scripts/gameplay/sim/run/ExpeditionFlow.Hunger.cs.uid`
- `darkest/scripts/gameplay/sim/run/ExpeditionFlow.Outcome.cs.uid`
- `darkest/scripts/gameplay/sim/run/ExpeditionFlow.Reveal.cs.uid`
- `darkest/scripts/gameplay/sim/run/ExpeditionFlow.RoomInteractions.cs.uid`
- `darkest/scripts/gameplay/sim/run/ExpeditionFlow.TileWalk.cs.uid`
- `darkest/scripts/gameplay/sim/run/ExpeditionFlow.Topology.cs.uid`
- `darkest/scripts/gameplay/sim/run/ExpeditionFlow.Traps.cs.uid`
- `darkest/scripts/gameplay/sim/run/ExpeditionProjector.cs`
- `darkest/scripts/gameplay/sim/run/ExpeditionSession.CampAndBonuses.cs.uid`
- `darkest/scripts/gameplay/sim/run/ExpeditionSession.cs`
- `darkest/scripts/gameplay/sim/run/ExplorationActOut.cs`
- `darkest/scripts/gameplay/sim/run/ExplorationActOut.cs.uid`
- `darkest/scripts/gameplay/sim/run/HungerSpawner.cs`
- `darkest/scripts/gameplay/sim/run/HungerSpawner.cs.uid`
- `darkest/scripts/gameplay/sim/run/MapScouting.cs`
- `darkest/scripts/gameplay/sim/run/MapTraversal.cs`
- `darkest/scripts/gameplay/sim/run/RevisitSpawner.cs`
- `darkest/scripts/gameplay/sim/run/RevisitSpawner.cs.uid`
- `darkest/scripts/gameplay/sim/run/Roster.cs`
- `darkest/scripts/gameplay/sim/run/RosterComposition.cs.uid`
- `darkest/scripts/gameplay/sim/run/Scouting.cs`
- `darkest/scripts/gameplay/sim/run/SimplePlayerAuto.cs`
- `darkest/scripts/gameplay/sim/run/TrapDefs.cs`
- `darkest/scripts/gameplay/sim/run/TrapDefs.cs.uid`
- `darkest/scripts/gameplay/sim/skill/SkillExecutor.cs`
- `darkest/scripts/gameplay/sim/skill/SkillTargetResolver.cs`
- `darkest/scripts/gameplay/sim/skill/SkillUseResolver.cs`
- `darkest/scripts/gameplay/sim/survival/LightMeter.cs`
- `darkest/scripts/gameplay/sim/survival/LightMeter.cs.uid`
- `darkest/scripts/gameplay/sim/survival/WeakDeathsDoor.cs`

### B3 scene 层 · `2e7ea63` · 主程序 · 5 个文件

- `darkest/scripts/gameplay/scene/BattleRoot.PlayerActions.cs`
- `darkest/scripts/gameplay/scene/BattleRoot.PlayerActions.cs.uid`
- `darkest/scripts/gameplay/scene/BattleRoot.cs`
- `darkest/scripts/gameplay/scene/ExpeditionComposition.cs`
- `darkest/scripts/gameplay/scene/ExpeditionContext.cs`

### B4 tests 层 · `2656bd1` · 主程序 · 80 个文件

- `darkest/tests/BoardTests.Fixtures.cs.uid`
- `darkest/tests/BoardTests.cs`
- `darkest/tests/BuffPrimitiveTranslationTests.cs.uid`
- `darkest/tests/BuildingsConfigTests.cs.uid`
- `darkest/tests/CampSkillRunLedgerTests.cs`
- `darkest/tests/CurioBlessingTests.cs`
- `darkest/tests/CurioDiseaseTests.cs`
- `darkest/tests/CurioPoolReferenceTests.cs`
- `darkest/tests/CurioTraitTests.cs`
- `darkest/tests/CurioUnlockGateWiringTests.cs`
- `darkest/tests/DefMergeBaselineTests.cs.uid`
- `darkest/tests/ExpeditionAmbushTests.cs`
- `darkest/tests/ExpeditionFlowStateMachineTests.cs`
- `darkest/tests/ExpeditionTopologyTests.cs`
- `darkest/tests/ExplorationActOutFlowTests.cs`
- `darkest/tests/ExplorationActOutFlowTests.cs.uid`
- `darkest/tests/ExplorationActOutTests.cs`
- `darkest/tests/ExplorationActOutTests.cs.uid`
- `darkest/tests/FlowTileWalkTests.cs`
- `darkest/tests/GridVisionFlowTests.cs`
- `darkest/tests/GridVisionFlowTests.cs.uid`
- `darkest/tests/GridVisionTests.cs`
- `darkest/tests/GridVisionTests.cs.uid`
- `darkest/tests/HamletLoopIntegrationTests.cs.uid`
- `darkest/tests/HeroArtResolverTests.cs.uid`
- `darkest/tests/HeroPlaceholderPacksTests.cs.uid`
- `darkest/tests/HungerTests.cs`
- `darkest/tests/HungerTests.cs.uid`
- `darkest/tests/LevelUpChannelTests.cs.uid`
- `darkest/tests/LightBoundaryConsistencyTests.cs`
- `darkest/tests/LightEffectWiringTests.cs`
- `darkest/tests/LightMeterTests.cs`
- `darkest/tests/M1cPilotComparisonTests.cs.uid`
- `darkest/tests/M75VerificationPackTests.PolicyAndSelfCheck.cs.uid`
- `darkest/tests/M75VerificationPackTests.cs`
- `darkest/tests/M76TopologyProbeTests.BranchProbes.cs`
- `darkest/tests/M76TopologyProbeTests.BranchProbes.cs.uid`
- `darkest/tests/M76TopologyProbeTests.PolicySweeps.cs`
- `darkest/tests/M76TopologyProbeTests.PolicySweeps.cs.uid`
- `darkest/tests/M76TopologyProbeTests.cs`
- `darkest/tests/MapScoutingTests.cs`
- `darkest/tests/MapTraversalTests.cs`
- `darkest/tests/MoraleTests.cs`
- `darkest/tests/NextUnlockProgressionTests.cs.uid`
- `darkest/tests/P2BattleLayerExpeditionReadingsTests.cs.uid`
- `darkest/tests/P2BattleLayerReadingsTests.cs.uid`
- `darkest/tests/PassPenaltyTests.cs`
- `darkest/tests/QuirksConfigTests.cs.uid`
- `darkest/tests/RecruitGrowthTests.cs.uid`
- `darkest/tests/ReliefEffectiveValueTests.cs.uid`
- `darkest/tests/RevisitTests.cs`
- `darkest/tests/RevisitTests.cs.uid`
- `darkest/tests/RoomContentDistributionProbeTests.cs`
- `darkest/tests/RoomContentSelectionTests.cs`
- `darkest/tests/RosterCapGrowthTests.cs.uid`
- `darkest/tests/RosterDeathTests.cs.uid`
- `darkest/tests/RosterRotationTests.cs.uid`
- `darkest/tests/RunBuffTests.cs`
- `darkest/tests/SecretFlowTests.cs`
- `darkest/tests/SecretFlowTests.cs.uid`
- `darkest/tests/SecretTests.cs`
- `darkest/tests/SecretTests.cs.uid`
- `darkest/tests/SmokeStepKindTests.cs.uid`
- `darkest/tests/SnapshotHpPercentTests.cs.uid`
- `darkest/tests/StagecoachCurvesTests.cs.uid`
- `darkest/tests/StateMachineTests.cs`
- `darkest/tests/StealthFollowUpTests.cs`
- `darkest/tests/StealthFollowUpTests.cs.uid`
- `darkest/tests/StealthTests.cs`
- `darkest/tests/StealthTests.cs.uid`
- `darkest/tests/TrapFlowTests.cs`
- `darkest/tests/TrapFlowTests.cs.uid`
- `darkest/tests/TrapTests.cs`
- `darkest/tests/TrapTests.cs.uid`
- `darkest/tests/TrinketsConfigTests.cs.uid`
- `darkest/tests/UiShellLedgerTests.cs.uid`
- `darkest/tests/UnitTierStage1Tests.cs.uid`
- `darkest/tests/UnlockConsumptionTests.cs`
- `darkest/tests/WalkingReadoutsTests.cs`
- `darkest/tests/WeaponDamageModelStage1Tests.cs.uid`

### B5 UI 域 · `2ef9bba` · UI 设计师 · 40 个文件

- `darkest/resources/theme/ui_palette.tres`
- `darkest/scenes/battle/Battle.tscn`
- `darkest/scenes/ui/building_popup.tscn`
- `darkest/scenes/ui/heirloom_exchange_skeleton.tscn`
- `darkest/scenes/ui/overlay_layer.tscn`
- `darkest/scripts/ui/BattleUI.Dungeon.cs`
- `darkest/scripts/ui/BattleUI.Modals.cs`
- `darkest/scripts/ui/BattleUI.MultiFunction.cs`
- `darkest/scripts/ui/BattleUI.PanelBanner.cs.uid`
- `darkest/scripts/ui/BattleUI.Refresh.cs`
- `darkest/scripts/ui/BattleUI.RoomInteractions.cs`
- `darkest/scripts/ui/BattleUI.RoomInteractions.cs.uid`
- `darkest/scripts/ui/BattleUI.cs`
- `darkest/scripts/ui/BuildingPopupSkeleton.cs`
- `darkest/scripts/ui/ControlsSkeleton.cs.uid`
- `darkest/scripts/ui/CreditsSkeleton.cs.uid`
- `darkest/scripts/ui/DdTheme.cs`
- `darkest/scripts/ui/FeFlowSkeleton.cs.uid`
- `darkest/scripts/ui/HamletRoot.BuildingPopup.cs`
- `darkest/scripts/ui/HamletRoot.Controls.cs`
- `darkest/scripts/ui/HamletRoot.Controls.cs.uid`
- `darkest/scripts/ui/HamletRoot.HeirloomExchange.cs`
- `darkest/scripts/ui/HamletRoot.HeroDetail.cs`
- `darkest/scripts/ui/HamletRoot.LootOverlay.cs`
- `darkest/scripts/ui/HamletRoot.PopupMenu.cs`
- `darkest/scripts/ui/HamletRoot.Progression.cs`
- `darkest/scripts/ui/HamletRoot.Provision.cs`
- `darkest/scripts/ui/HamletRoot.QuestSelect.cs`
- `darkest/scripts/ui/HamletRoot.cs`
- `darkest/scripts/ui/HeirloomExchangeSkeleton.cs`
- `darkest/scripts/ui/HeroArt.cs`
- `darkest/scripts/ui/LightBarPanel.cs`
- `darkest/scripts/ui/OverlayLayer.cs`
- `darkest/scripts/ui/PanelBannerSkeleton.cs.uid`
- `darkest/scripts/ui/RaidResultsSkeleton.cs.uid`
- `darkest/scripts/ui/UILayoutSpec.cs`
- `darkest/scripts/ui/UILayoutSpec.cs.uid`
- `darkest/scripts/ui/UiPalette.cs`
- `darkest/scripts/ui/WalkMapSkeleton.cs`
- `darkest/scripts/ui/WalkMapView.cs`

### B6 doc 契约 · `3e20915` · 策划+架构 · 30 个文件

- `doc/README.md`
- `doc/UI_ASSET_SIZE_PLAN.md`
- `doc/UI_STRUCTURE_DECISIONS.md`
- `doc/architecture/data_schema.md`
- `doc/architecture/dd_replication_roadmap.md`
- `doc/architecture/dungeon_layer_design.md`
- `doc/architecture/goal1_combat_relocation.md`
- `doc/architecture/goal2_stealth.md`
- `doc/dd_original_vs_clone_crossref.md`
- `doc/dd_ui_vs_clone.md`
- `doc/modules/dd1_baseline.md`
- `doc/modules/dd1_baseline.md.bak2.20260921_003809`
- `doc/modules/dd_reference.md`
- `doc/modules/encounters.md`
- `doc/modules/hamlet_loop.md`
- `doc/modules/morale_costs.md`
- `doc/modules/observe_list.md`
- `doc/modules/retreat.md`
- `doc/modules/trinkets.md`
- `doc/modules/ui_spec.md`
- `doc/state.md`
- `doc/state.md.bak`
- `doc/state.md.bak2`
- `doc/state.md.bak3`
- `doc/state.md.bak4`
- `doc/state.md.bak5`
- `doc/state.md.bak6`
- `doc/state.md.bak7`
- `doc/state.md.bak8`
- `doc/windows/主程序窗口.txt`

### B7 其余 · `2ef77b4` · 混合 · 3 个文件

- `.gitignore`
- `reports/LATEST.md`
- `tools/check_data_discipline.py`

### B8 收尾 · `7fff261` · 混合 · 5 个文件

- `darkest/scripts/core/contracts/UnitTiers.cs.uid`
- `reports/_denv.sh`
- `reports/_move_c2c3.py`
- `reports/goal8_secret.md`
- `reports/goal9_act_out.md`

## 处置建议（我的判断，供你复核）
```
① **不需要"摘出"**：这些文件**已经在树上工作**（809 全绿 + 冒烟 13/13）⇒ 摘出反而是**回退已工作代码** ✗
② **需要的是"认领"**：各域作者看一眼自己那批（表里已按域分好 ✓）⇒ 认领即闭环 ✓
③ 🔴 **唯一仍存疑的那一处**早前已如实登记：`ExpeditionFlow` 的**整类体注释丢失**（反编译回填 ✓）
   ⇒ 已交给架构（"无注释整类体清单" ✓）⇒ 与本表无关，但同属"来源可核对"这一族 ✓
```
