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
/// ② 本文件 = **地图层 · 放弃远征入口**（`retreat.md §8`：`SetAbandonAction` 注入 ＋ `PressAbandon` 二次确认；
///    不可逆 ⇒ 与战斗内【撤退】不得同屏）✓
/// ③ 🔴 依赖主类私有成员/状态：`_abandonExpedition`/`_abandonButton`/`_abandonConfirm`/`_abandonWarned`/
///    `_uiRoot`/`_overlay` ＋ 模态工厂 `MakeOpaqueModal`（`PopupLayout.Confirm`）✓
/// ④ **只搬家、零行为改动**（一字未改；含 `[UI-TRACE] abandon` ASCII 留痕，供 `ui_sweep` 断言）✓
/// </summary>
public partial class BattleUI : Control
{

    /// <summary>
    /// 🔴 `retreat.md §8`：宿主把【放弃远征】动作交给我（**不破 `Bind` 签名**：`Bind` 之后调一次即可）✓
    /// ⚠️ **未调用 ⇒ 按钮不显示**（红线 21：不留"点了没用"的控件；也避免把"结束一趟"错标成"退一场"）✓
    /// </summary>
    public void SetAbandonAction(Action? abandon)
    {
        _abandonExpedition = abandon;
        if (_abandonButton is not null)
        {
            _abandonButton.Visible = abandon is not null;
        }

        GD.Print(abandon is null
            ? "[UI 撤退/放弃] 宿主**未提供**放弃远征动作 ⇒ 按钮不显示（不假装可用，红线 21）✓"
            : "[UI 撤退/放弃] 已接入【放弃远征】入口（行走模式可见·二次确认·tooltip 写清后果）✓");
    }

    /// <summary>按下【放弃远征】⇒ **二次确认**（不可逆；`§8` 硬要求②）✓</summary>
    public void PressAbandon()   // 🔴 主程序 2026-09-21 请求：retreat 冒烟需公共入口（发真实 Pressed）✓
    {
        GD.Print("[UI-TRACE] abandon");   // ASCII 留痕（供 ui_sweep 断言：避免 PS5.1 读中文的编码坑）
        if (_abandonExpedition is null)
        {
            if (!_abandonWarned)
            {
                _abandonWarned = true;
                GD.Print("[UI 撤退/放弃] 放弃远征：**没有动作可调**（宿主未注入）⇒ 什么也不做（不静默假装）✓");
            }

            return;
        }

        if (_uiRoot is null || !GodotObject.IsInstanceValid(_uiRoot))
        {
            GD.Print("[UI 撤退/放弃] ⚠️ _uiRoot 未就绪（未 Build）⇒ 不执行放弃（不静默、不半执行）✓");
            return;
        }

        if (_abandonConfirm is null)
        {
            // ⚠️ 战斗屏的模态工厂是 `MakeOpaqueModal`（返回正文 Label + out 面板）；按钮挂在**正文的父容器**（col）上 ✓
            // 🔴 DD `shared/confirm_dialog` ⇒ 840×600 居中（二次确认框比普通模态高）✓
            Label abandonText = MakeOpaqueModal("AbandonConfirm", out PanelContainer abandonPanel,
                Darkest.UI.PopupLayout.Confirm);
            _abandonConfirm = abandonPanel;
            abandonText.Text = "放弃远征 = **结束本次远征、回城**（本趟未完成）。\n此操作**不可逆**；若只是想退出本场战斗，请用【撤退】。";
            var row = new HBoxContainer { Name = "AbandonConfirmRow" };
            row.AddThemeConstantOverride("separation", 8);
            ((Control)abandonText).GetParent().AddChild(row);

            var yes = new Button { Name = "AbandonYes", Text = "确认放弃远征", CustomMinimumSize = new Vector2(180, 34) };
            yes.Pressed += () =>
            {
                GD.Print("[UI 撤退/放弃] ✅ 二次确认通过 ⇒ 调宿主【放弃远征】动作 ✓");
                _abandonConfirm!.Visible = false;
                _overlay?.CloseModal(_abandonConfirm!);
                _abandonExpedition?.Invoke();
            };
            row.AddChild(yes);

            var no = new Button { Name = "AbandonNo", Text = "取消（继续走）", CustomMinimumSize = new Vector2(160, 34) };
            no.Pressed += () =>
            {
                GD.Print("[UI 撤退/放弃] 取消放弃远征 ⇒ 继续走 ✓");
                _abandonConfirm!.Visible = false;
                _overlay?.CloseModal(_abandonConfirm!);
            };
            row.AddChild(no);
        }

        _overlay?.OpenModal(_abandonConfirm);   // 🔴 入栈 ⇒ 遮罩出现 + Esc 可关 ✓
        GD.Print("[UI 撤退/放弃] 弹出【放弃远征】二次确认（不可逆；取消 ⇒ 继续走）✓");
    }
}
