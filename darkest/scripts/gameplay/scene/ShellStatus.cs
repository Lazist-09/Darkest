using System;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 **形态 B 的【一键状态】**（把三条判据串起来 ⇒ UI/架构一个命令看全）✓
///
/// 三条判据（都在我域 ✓）：
///   · **B-2 绘层**：外壳是否在**当前场景之上**（`ShellLayerAudit` ✓ 否则"冒烟绿=假绿"）
///   · **B-3 回落留痕**：`UiShellLedger` ⇒ "**还差哪几屏没面板化**"可数 ✓
///   · **C4 终态**：外壳**实例数必须 = 1** + `main_scene`/`autoload` 三者齐看（`ShellInstanceAudit` ✓）
///
/// 🔴 **零行为**：只在显式 `--shell-status` 时运行（我域旗标 ✓）
/// 用法：`Godot --path darkest --headless --smoke=main:1 --shell-status --quit-after 400` ✓
/// </summary>
public static class ShellStatus
{
    public static void Run(Node anyNode)
    {
        GD.Print("════════ 形态 B · 一键状态（主程序侧三条判据）════════");
        GD.Print("【① B-2 绘层】外壳 vs 当前场景");
        bool layerOk = ShellLayerAudit.Run(anyNode);

        GD.Print("【② B-3 回落留痕】还差哪几屏没面板化");
        // 🔴 架构回执点名：**"未触发"不该打印成 `0`** ✗（否则读的人会以为"已经 0 屏 = 都面板化了"）
        GD.Print(Darkest.Data.UiShellLedger.Count == 0
            ? "   （**本趟未触发**：没有发生「回城/回落」 ⇒ 账本为空 —— **这不是「还差 0 屏」** ✓）"
            : "   " + Darkest.Data.UiShellLedger.Report());

        GD.Print("【③ C4 终态】外壳实例数 / main_scene / autoload");
        bool terminalOk = ShellInstanceAudit.Run(anyNode);

        GD.Print($"════════ 小结：B-2 = {(layerOk ? "✅" : "🔴")} · C4 终态 = {(terminalOk ? "✅" : "🔴")} "
            + $"· B-3 见上（账本每次回落都会自动更新 ✓）════════");
        if (!layerOk || !terminalOk)
        {
            GD.Print("⇒ 剩下的都**不需要主程序再动代码**：① 外壳加 `CanvasLayer` 抬层（UI）"
                + " ② hamlet 等屏面板化（UI）③ `main_scene` → `ui_root.tscn` + 撤 autoload（UI，**同批做**）✓");
        }
    }
}
