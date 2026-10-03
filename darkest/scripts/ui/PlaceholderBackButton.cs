using System;
using Godot;

namespace Darkest.UI;

/// <summary>
/// 🔴 2026-10-03（与「主菜单满屏点不动」同批）：阶段2 占位骨架的「返回」块**真能点**。
///
/// 背景：占位骨架里的「返回」是 `PanelContainer` 色块（`mouse_filter = 2` ⇒ 不接输入）⇒
/// 骨架一旦**能打开**就**关不掉**（违反红线 21「能开必须能关」；模态栈 + Esc 只兜住键盘出口）⚠️
/// 本类只做一件事：把该色块**在运行时**升级成真按钮（`Stop` + 左键单击 ⇒ 回调）✓
/// ⚠️ **不换不删**（`§14.0.68`）：不动场景结构、不动美术占位，只改一个 `MouseFilter` ✓
/// </summary>
public static class PlaceholderBackButton
{
    /// <summary>接线成功 ⇒ true；色块缺失 ⇒ false（调用方仍留着 Esc 第二出口，且**打印**不留白）✓</summary>
    public static bool Wire(Control? block, Action onBack)
    {
        if (block is null)
        {
            GD.Print("[UI 占位返回] 色块缺失 ⇒ 只留 Esc 出口（不静默）✓");
            return false;
        }

        block.MouseFilter = Control.MouseFilterEnum.Stop;
        block.GuiInput += @event =>
        {
            if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            {
                GD.Print($"[UI 占位返回] 点击「{block.Name}」⇒ 关闭占位层 ✓");
                onBack();
            }
        };
        return true;
    }
}
