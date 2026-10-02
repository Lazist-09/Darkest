using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Darkest.Core.Contracts;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Board;
using Darkest.Gameplay.Sim.Skill;
using Godot;


namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓


/// <summary>
/// ① 从 `BattleUI.Dungeon.cs` 拆出（用户红线：程序文件 ≤600 行；架构要求**按相位切**）✓
/// ② 本文件 = **地图模式 · 扎营相位面板**（`HostDungeonCampPanel`：可见性**只读** `Session.CanShowCampUi`；
///    数据与 `ExpeditionRoot` 同源 = `camp_skills.json` ＋ 名册原型映射 ＋ `RespiteLeft` ＋ `Tuning.Camp`）✓
/// ③ 🔴 依赖主类私有成员/状态：本片持有 `_mapModeCamp`/`_campSkillsCfgForMap`/`_rosterCfgForMap`；
///    调核心片 `DungeonHost()`（宿主）与 `HostDungeonPanels()`（用后刷新）；其余读数走内核 `ExpeditionFlow` ✓
/// ④ **只搬家、零行为改动**（一字未改）✓
/// </summary>
public partial class BattleUI : Control
{

    /// <summary>🔴 `#327` 片 2 #6：**扎营**面板（B 类）—— 内容已独立化（`CampSkillPanel`），可见性**只读** `Session.CanShowCampUi` ✓</summary>
    private Darkest.UI.CampSkillPanel? _mapModeCamp;
    private Darkest.Data.CampSkillsConfig? _campSkillsCfgForMap;   // 懒解析（与 `ExpeditionRoot` 同一数据源）
    private Darkest.Data.RosterConfig? _rosterCfgForMap;           // 懒解析（英雄 id → 原型）

    /// <summary>
    /// 🔴 `#327` 片 2 #6：把 **【扎营】面板（B 类）**挂进地图模式。
    /// 铁律：**可见性只读 `Session.CanShowCampUi`**（`blueprint §9.17.0`：内核持相位、UI 只读谓词 ⇒ 绝不推断相位）✓
    /// 数据与 `ExpeditionRoot` **同源**（`camp_skills.json` + 名册原型映射 + `RespiteLeft` + `Tuning.Camp`），UI 不重算 ✓
    /// ⚠️ 诚实边界：**战斗相位下谓词为假 ⇒ 面板必然隐藏**；"该显示时显示"要等片 3 的行走相位进宿主后才能观察 ✓
    /// </summary>
    private void HostDungeonCampPanel(Darkest.Gameplay.Sim.Run.ExpeditionFlow flow)
    {
        if (_mapModeCamp is null || !GodotObject.IsInstanceValid(_mapModeCamp))
        {
            _mapModeCamp = new Darkest.UI.CampSkillPanel { Name = "MapModeCamp", CustomMinimumSize = new Vector2(210, 96) }; // 🔴 预留宽度+按 720 收高  // 🔴 相机 720 口径：96→64
            DungeonHost().AddChild(_mapModeCamp);
        }

        if (!flow.Session.CanShowCampUi) // 🔴 只读谓词
        {
            _mapModeCamp.Visible = false;
            GD.Print($"[UI 片2] 【扎营】面板：Phase={flow.Session.Phase} ⇒ `CanShowCampUi=False` ⇒ **隐藏**（只读谓词，不推断相位）✓");
            return;
        }

        // 🔴 片 4 收尾（主程序 ③）：**配置改读公共读处** `ExpeditionContext.CampSkills`（消掉"各解析一份"）——
        //    ⚠️ 为空时（单场战斗 / 未 `BindConfigs`）退回本地解析并**留痕**（不静默、不崩）✓
        if (_campSkillsCfgForMap is null)
        {
            _campSkillsCfgForMap = Darkest.Gameplay.Scene.ExpeditionContext.CampSkills;
            if (_campSkillsCfgForMap is null)
            {
                _campSkillsCfgForMap = Darkest.Data.CampSkillsConfig.Parse(
                    Godot.FileAccess.GetFileAsString(Darkest.Data.CampSkillsConfig.ResPath));
                GD.Print("[UI 片2] `ExpeditionContext.CampSkills` 为空 ⇒ 本地解析一份" +
                         "（留痕：单场战斗 / 未 `BindConfigs` 时会走这里）");
            }
        }

        // `RosterConfig`（英雄 id → 原型）：**你未开公共读处** ⇒ 仍本地懒解析（已投窗口问是否要开）✓
        _rosterCfgForMap ??= Darkest.Data.RosterConfig.Parse(
            Godot.FileAccess.GetFileAsString(Darkest.Data.RosterConfig.ResPath));

        var heroByArchetype = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string id, int _, int _, int _) in flow.Session.Roster())
        {
            string? archetype = _rosterCfgForMap.Heroes.FirstOrDefault(h => h.Id == id)?.Archetype;
            if (archetype is not null && !heroByArchetype.ContainsKey(archetype))
            {
                heroByArchetype[archetype] = id;
            }
        }

        Darkest.Data.CampSkillConfig[] usable = _campSkillsCfgForMap.Skills
            .Where(s => Darkest.Data.CampSkillsConfig.ConsumedEffectNames.Contains(s.Effect)) // 只列已接线（红线 21）
            .ToArray();
        Darkest.Core.Events.CombatLog? log = Darkest.Gameplay.Scene.ExpeditionContext.Log;

        _mapModeCamp.Visible = true;
        _mapModeCamp.Refresh(
            usable,
            heroOf: s => heroByArchetype.TryGetValue(s.OwnerUnit, out string? hero) ? hero : null,
            affordOf: s => flow.Session.RespiteLeft >= s.Cost,
            useOf: (s, target) =>
            {
                if (log is null)
                {
                    GD.Print("[UI 片2] 扎营：无 `ExpeditionContext.Log` ⇒ 不执行（如实拒绝）");
                    return;
                }

                bool used = flow.Session.UseCampSkill(log, s, Darkest.Core.Contracts.UnitId.Of(target), flow.Tuning.Camp!);
                GD.Print($"[UI 片2] 扎营技能 {s.Name}：{(used ? "已使用" : "拒绝")}　剩余 Respite {flow.Session.RespiteLeft}");
                HostDungeonPanels(); // 刷新（点数/可用性变化）
            },
            statusText: $"【扎营】Respite {flow.Session.RespiteLeft} 点　可用技能 {usable.Length} 个（只列已接线）");

        GD.Print($"[UI 片2] 【扎营】面板：Phase={flow.Session.Phase} ⇒ **显示**（可用 {usable.Length} 个技能）✓");
    }
}
