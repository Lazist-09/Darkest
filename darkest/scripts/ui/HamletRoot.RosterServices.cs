using System;
using System.Collections.Generic;
using System.Linq;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.UI;   // 🔴 命名纪律：一律 Darkest.UI（大写 UI）✓

/// <summary>
/// ① 从 `HamletRoot.cs` 拆出（用户红线：程序文件 ≤600 行）✓
/// ② 本文件 = **城池 · 名册交互与服务族**（再出发/名册行点击/等级与防御读数/右键头像/服务与疗养）✓
/// ③ 🔴 依赖主类私有成员：`_embark` · `_heroButtons` · `_selectedHero` · `_saniButtons` · `_rosterList` · `_rosterCfgForDetail` · `_cfg` · `_log` · `_rng`✓
/// ④ **只搬家、零行为改动**✓
/// </summary>
public partial class HamletRoot : Control
{
    /// <summary>🔴 片① ⑥（冒烟）：**真实点击 Embark（再出发）** ⇒ 切 `Expedition.tscn`（红线 18）。</summary>
    public void PressEmbark()
    {
        GD.Print("[HamletRoot] PressEmbark：发出真实 Pressed（再出发 · EMBARK）");
        _embark.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>🔴 片① ④（冒烟）：**真实点击右列第 i 行名册**。</summary>
    public bool PressRosterRow(int index)
    {
        if (index < 0 || index >= _heroButtons.Count)
        {
            GD.Print($"[HamletRoot] PressRosterRow({index})：没有这一行（当前 {_heroButtons.Count} 行）");
            return false;
        }

        GD.Print($"[HamletRoot] PressRosterRow({index})：发出真实 Pressed（「{(_heroButtons[index].Text is { Length: > 0 } t ? t : _heroButtons[index].TooltipText)}」）");
        _heroButtons[index].EmitSignal(BaseButton.SignalName.Pressed);
        return true;
    }

    /// <summary>DD 名册行：**等级** —— 来自 `roster.json`（取不到 ⇒ 0，不编数字）✓</summary>
    private int LevelOfHero(string heroId)
        => _rosterCfgForDetail?.Heroes.FirstOrDefault(x => x.Id == heroId)?.Level ?? 0;

    /// <summary>DD 名册行：**防御等级** —— 来自 `units.json` 的该原型 `dodge`（取不到 ⇒ "?"，不编数字）✓</summary>
    private string DodgeOfHero(HeroConfig h)
    {
        int? dodge = _unitsCfg?.Units.FirstOrDefault(u => u.Id == h.Archetype)?.Dodge;
        return dodge?.ToString() ?? "?";
    }

    /// <summary>🔴 冒烟：**右键头像**（真实走 `GuiInput` 处理器 ⇒ 与玩家同一条路径）⇒ 打开角色详情 ✓</summary>
    public bool PressPortraitRightClick(int index)
    {
        if (index < 0 || index >= _heroButtons.Count)
        {
            GD.Print($"[HamletRoot] 右键头像({index})：没有这一行（当前 {_heroButtons.Count} 行）");
            return false;
        }

        Control? frame = _heroButtons[index].GetNodeOrNull<Control>("RosterRowBody/PortraitFrame");
        if (frame is null)
        {
            GD.Print("[HamletRoot] 右键头像：**找不到头像框**（红线 21：控件没挂上 ⇒ 如实报，不静默）");
            return false;
        }

        var ev = new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true };
        GD.Print($"[HamletRoot] 右键头像({index})：发出真实 `GuiInput`（右键按下）⇒ 打开角色详情");
        frame.EmitSignal(Control.SignalName.GuiInput, ev);
        return true;
    }

    /// <summary>🔴 用户规则②：**保留色相、只把 α 换成调色板里的占位透明度**（空闲位半透明 ⇒ 一眼看出"待填"）✓</summary>
    private static Color WithPlaceholderAlpha(Color hue)
    {
        hue.A = Darkest.UI.DdTheme.PlaceholderFill.A;
        return hue;
    }
    /// <summary>供冒烟：名册竖列的行数（应等于名册人数）。</summary>
    public int RosterRowCount => _heroButtons.Count;

    /// <summary>
    /// 🔴 M8.2 / V16：**Sanitarium 服务的真实点击路径**（发真实 `Pressed` 信号，不直接调业务方法）。
    /// </summary>
    public void PressService(string serviceName)
    {
        if (!_saniButtons.TryGetValue(serviceName, out Button? btn))
        {
            GD.Print($"[HamletRoot] PressService({serviceName})：找不到按钮（红线 21）");
            return;
        }

        GD.Print($"[HamletRoot] PressService({serviceName})：发出真实 Pressed 信号（按钮「{btn.Text}」，置灰={btn.Disabled}）");
        btn.EmitSignal(BaseButton.SignalName.Pressed);
    }

    /// <summary>**执行一项 Sanitarium 服务**：挑对象（优先玩家选中的、否则找有病/可改的人）→ 调用内核 → 打印结果。</summary>
    public void DoService(string serviceName)
    {
        Roster? roster = ExpeditionContext.Roster;
        Economy? economy = ExpeditionContext.Gold;
        HeirloomStock? heirlooms = ExpeditionContext.Heirlooms;
        if (roster is null || economy is null || heirlooms is null || _saniCfg is null)
        {
            return;
        }

        // 选对象：优先玩家选中的人；否则按服务挑一个"有事可做"的（有病 / 有负面特质 / 有正面特质）
        string? hero = _selectedHero;
        hero ??= serviceName switch
        {
            "cure_disease" => roster.Heroes.FirstOrDefault(h => roster.DiseasesOf(h.Id).Count > 0)?.Id,
            "remove_negative_trait" => roster.Heroes.FirstOrDefault(h => roster.FindRemovableNegativeTrait(h.Id) is not null)?.Id,
            _ => roster.Heroes.FirstOrDefault(h => roster.FindLockablePositiveTrait(h.Id) is not null)?.Id,
        };

        if (hero is null)
        {
            GD.Print($"[HamletRoot] Sanitarium·{serviceName}：**没有可用对象**（拒绝对空做事）");
            Refresh();
            return;
        }

        CureOutcome o = serviceName switch
        {
            "cure_disease" => CureFirstDisease(roster, economy, heirlooms, hero),
            "remove_negative_trait" => Sanitarium.RemoveNegativeTrait(_log, _saniCfg, economy, heirlooms, roster, hero),
            _ => Sanitarium.LockPositiveTrait(_log, _saniCfg, economy, heirlooms, roster, hero),
        };

        GD.Print($"[HamletRoot] Sanitarium·{serviceName}：{(o.Paid ? "成交" : "拒绝（钱/传家宝不足，或无事可做）")}" +
                 $"　对象 {hero}　花 金钱{o.GoldSpent} 加 传家宝[{o.HeirloomSpent}]");
        Refresh();
        AutoSave("疗养院");
    }

    private CureOutcome CureFirstDisease(Roster roster, Economy economy, HeirloomStock heirlooms, string heroId)
    {
        foreach (string d in roster.DiseasesOf(heroId).ToArray())
        {
            return Sanitarium.CureDisease(_log, _saniCfg!, economy, heirlooms, roster, heroId, d);
        }

        return new CureOutcome(false, 0, string.Empty);
    }
}
