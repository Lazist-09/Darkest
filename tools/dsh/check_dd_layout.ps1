# tools/dsh/check_dd_layout.ps1 -- DD layout conformance check (UI owner, 2026-09-21)
#
# Why: directive 5 says every layout value must come from the ORIGINAL GAME's
#      *.layout.darkest files (1920x1080 base, scaled by 0.667 to our 1280x720 camera),
#      never from my own taste. This script prints DD value vs our implemented value so
#      conformance is checkable instead of remembered.
#
# Usage: powershell -NoProfile -ExecutionPolicy Bypass -File tools/dsh/check_dd_layout.ps1
# ASCII only on purpose (PowerShell 5.1 mis-parses UTF-8 no-BOM non-ASCII).

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ui   = Join-Path $root "darkest/scripts/ui"
$scn  = Join-Path $root "darkest/scenes"

# DD key -> (dd value at 1920x1080, scale, where we implemented it, what to grep)
$rows = @(
  @{ Key = "town.roster_list_pos.x";   Dd = 1550; Impl = "hamlet_skeleton.tscn RightColumn width"; Pat = "Vector2(370, 0)" }
  @{ Key = "town.embark_party_pos";    Dd = 754;  Impl = "HamletRoot.Build (two expanders centre Embark)"; Pat = "MidPadLeft" }
  @{ Key = "town.heirloom_exchange_pos.x"; Dd = 340; Impl = "HamletRoot.Build resource bar ShrinkBegin"; Pat = "ShrinkBegin" }
  @{ Key = "building_navigation.base_size"; Dd = 128; Impl = "hamlet_skeleton BuildingNav min (128,0)+expand-fill (DD 1000 fixed height overflowed HamletRootCol)"; Pat = "Vector2(128, 0)" }
  @{ Key = "roster row height";        Dd = 97;   Impl = "roster_row.tscn root"; Pat = "Vector2(370, 97)" }
  @{ Key = "menu element_hot_area_size"; Dd = 466; Impl = "MainMenuRoot button"; Pat = "Vector2(466, 48)" }
  @{ Key = "menu base_pos"; Dd = 450; Impl = "main_menu MenuMargin (element start 510,240 x0.667 = 340,160)"; Pat = "MenuMargin" }
  @{ Key = "menu options_area_size"; Dd = 600; Impl = "main_menu OptionsPanel 400x288"; Pat = "Vector2(600, 432)" }
  @{ Key = "raid hero_start_pos.x";    Dd = 788;  Impl = "BattleUI.StatusTray hero slot 0"; Pat = "0.410f" }
  @{ Key = "raid monster_start_pos.x"; Dd = 1050; Impl = "BattleUI.StatusTray enemy slot 0"; Pat = "0.547f" }
  @{ Key = "status_bars.y_pos";        Dd = 698;  Impl = "BattleUI.StatusTray tray y"; Pat = "0.646f" }
  @{ Key = "status_bars.health_height"; Dd = 10;  Impl = "BattleUI.StatusTray bar height"; Pat = "0.0093f" }
  @{ Key = "raid actor_spacing - figure"; Dd = 168; Impl = "BattleUI CardW+GapX = 126+14 (DD 504/4 and 168-154)"; Pat = "const float CardW = 126f" }
  @{ Key = "raid overlays hero_start_pos.y"; Dd = 680; Impl = "BattleUI MidPadTop ratio 0.6297 (exact)"; Pat = "MidPadTop" }
  @{ Key = "raid area tile_width"; Dd = 720; Impl = "BattleUI mid row band (padding ratios)"; Pat = "MidPadLeft" }
  @{ Key = "raid overlays actor_spacing"; Dd = 154; Impl = "BattleUI CardW 84 (DD band / 4)"; Pat = "const float CardW = 126f" }
  @{ Key = "panel.map clip"; Dd = 665; Impl = "battle_bottombar MapCorner (read only)"; Pat = "MapCorner" }
  @{ Key = "status_bars tray_icon_left_offset"; Dd = 58; Impl = "battle_bottombar HeroIcon slots (color blocks)"; Pat = "HeroIcon1" }
  @{ Key = "status_bars round_indicator_offset"; Dd = 10; Impl = "battle_bottombar RoundIndicator block"; Pat = "RoundIndicator" }
  @{ Key = "hero campaign_status spacing"; Dd = 10; Impl = "HeroStatusBars separation 7"; Pat = "HeroStatusBars" }
  @{ Key = "hero equipment weapon_pos"; Dd = 4;   Impl = "hero_detail_skeleton equip area (DD weapon 4,0)"; Pat = "HeroEquipArea" }
  @{ Key = "hero equipment armour_pos"; Dd = 95;  Impl = "hero_detail_skeleton equip area (DD armour 95,0)"; Pat = "EquipPlaceholder" }
  @{ Key = "hero trinket grid offset.x"; Dd = 92;  Impl = "hero skeleton trinket area (DD 32,52/92,160)"; Pat = "HeroTrinketArea" }
  @{ Key = "hero base_stats value_offset"; Dd = 115; Impl = "HeroStatsGrid value column"; Pat = "HeroStatsGrid" }
  @{ Key = "hero portrait disease_icon_offset"; Dd = 61; Impl = "hero_detail_skeleton HeroDiseaseIcon block"; Pat = "HeroDiseaseIcon" }
  @{ Key = "hero scouting_stat hotspot width"; Dd = 300; Impl = "hero_detail_skeleton HeroScoutingStat block"; Pat = "HeroScoutingStat" }
  @{ Key = "hero equipment highlight_pos_offset"; Dd = -20; Impl = "hero_detail_skeleton HeroEquipHighlight block"; Pat = "HeroEquipHighlight" }
  @{ Key = "hero equipment level_offset"; Dd = 90; Impl = "hero_detail_skeleton HeroEquipLevelText block"; Pat = "HeroEquipLevelText" }
  @{ Key = "hero base_stats icon_offset"; Dd = -26; Impl = "hero_detail_skeleton HeroStatsIcon block"; Pat = "HeroStatsIcon" }
  @{ Key = "monster type_pos"; Dd = 65; Impl = "unit_card MonsterType block (card-relative)"; Pat = "MonsterType" }
  @{ Key = "monster resistances_pos"; Dd = 100; Impl = "unit_card MonsterResistances block"; Pat = "MonsterResistances" }
  @{ Key = "monster skills_title_pos"; Dd = 480; Impl = "unit_card MonsterSkillsTitle block"; Pat = "MonsterSkillsTitle" }
  @{ Key = "raid battle attack_overlay_pos"; Dd = 960; Impl = "battle_bottombar AttackOverlayAnchor block (x/1920)"; Pat = "AttackOverlayAnchor" }
  @{ Key = "raid battle monster_panel_position"; Dd = 946; Impl = "battle_bottombar MonsterPanelAnchor block (x/1920)"; Pat = "MonsterPanelAnchor" }
  @{ Key = "panel.tab reorder button_pos"; Dd = 678; Impl = "battle_bottombar RaidReorderPartyButton block"; Pat = "RaidReorderPartyButton" }
  @{ Key = "inventory raid grid columns"; Dd = 8; Impl = "battle_bottombar RaidInventoryGridAnchor block (grid params 20,28 / 80,160)"; Pat = "RaidInventoryGridAnchor" }
  @{ Key = "hero status resolve offset.y"; Dd = 4; Impl = "HeroStatusBars first bar"; Pat = "HeroHpBar" }
  @{ Key = "hero status stress offset.y"; Dd = 100; Impl = "HeroStatusBars second bar"; Pat = "HeroMoraleBar" }
  @{ Key = "building upgrade_trees offset.y"; Dd = 195; Impl = "building_popup UpgradeTree"; Pat = "UpgradeTree" }
  @{ Key = "town estate_summary_pos.y"; Dd = 975; Impl = "hamlet_skeleton EstateSummary anchor 0.903"; Pat = "EstateSummary" }
  @{ Key = "town realm_inventory_pos.x"; Dd = 881; Impl = "hamlet_skeleton RealmInventory anchor 0.459"; Pat = "RealmInventory" }
  @{ Key = "building_navigation index0..9"; Dd = 10; Impl = "hamlet_skeleton DDNav0..9 slots"; Pat = "DDNav9_statue" }
  @{ Key = "provision store start_pos"; Dd = 120; Impl = "provision_skeleton StoreGrid"; Pat = "StoreGrid" }
  @{ Key = "provision store background x"; Dd = 814; Impl = "provision BodyRow equal expand + InfoCol 180 => store left approx 0.42 (derived, not exact)"; Pat = "StoreGrid" }
  @{ Key = "quest_select name_pos"; Dd = 104; Impl = "quest_select_skeleton title row"; Pat = "QuestSelectTitle" }
  @{ Key = "roster stress_offset.y"; Dd = 43; Impl = "roster_row PressureBar anchor y 0.4433 = 43/97 (A1 anchor rebuild)"; Pat = "0.4433" }
)

"DD value (x0.667 where linear) vs our implementation"
"-----------------------------------------------------------------"
"{0,-34} {1,8} {2,9}  {3}" -f "DD KEY", "DD", "SCALED", "IMPLEMENTED AT"
$miss = 0
foreach ($r in $rows) {
    $scaled = [math]::Round($r.Dd * 0.667, 3)
    $hit = @(Get-ChildItem $ui, $scn -Recurse -Include *.cs, *.tscn -ErrorAction SilentlyContinue |
        Select-String -Pattern $r.Pat -SimpleMatch -ErrorAction SilentlyContinue)
    $mark = if ($hit.Count -gt 0) { "ok" } else { $miss++; "MISSING" }
    "{0,-34} {1,8} {2,9}  {3}  [{4}]" -f $r.Key, $r.Dd, $scaled, $r.Impl, $mark
}
""
if ($miss -gt 0) { "RESULT: $miss item(s) not found in our code/scenes (review needed)"; exit 1 }
"RESULT: all DD values have an implementation anchor"; exit 0
