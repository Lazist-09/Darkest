using System.Collections.Generic;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// M8.0 ③（`#283`）：**回城场景 Hamlet**（局外养成层的入口）。
///
/// 🔴 职责边界（`blueprint §9.15`）：本类**只渲染 + 转发** —— 金钱来自**跨趟持有者**
/// （`ExpeditionContext.Gold`，与经济数据同源），减压/招募按钮在 ④/⑤ 落地前**置灰**
/// （**不假装可用**）。
/// 🔴 红线 18：Hamlet 必须**从启动场景可达**（`BattleRoot` 的按钮 / `--hamlet` CLI）。
/// </summary>
public partial class HamletRoot : Node2D
{
    private Label _status = null!;

    public override void _Ready()
    {
        // 跨趟经济：与地牢层共用同一实例（回城不重置金钱）
        EconomyConfig cfg = EconomyConfig.Parse(FileAccess.GetFileAsString(EconomyConfig.ResPath));
        Economy economy = ExpeditionContext.EnsureEconomy(cfg);

        _status = new Label
        {
            Name = "HamletStatus",
            Position = new Vector2(24, 24),
            Size = new Vector2(1200, 120),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_status);

        var backToDungeon = new Button
        {
            Name = "BackToDungeon",
            Text = "再出发（远征）",
            Position = new Vector2(24, 160),
            Size = new Vector2(240, 40),
        };
        backToDungeon.Pressed += () => GetTree().ChangeSceneToFile("res://scenes/expedition/Expedition.tscn");
        AddChild(backToDungeon);

        // ④ 减压 / ⑤ 招募：数据与校验未落地前置灰（不假装可用）
        var relief = new Button { Name = "StressRelief", Text = "减压（M8.0 ④ 未开放）", Position = new Vector2(280, 160), Size = new Vector2(240, 40), Disabled = true };
        AddChild(relief);
        var recruit = new Button { Name = "Recruit", Text = "招募（M8.0 ⑤ 未开放）", Position = new Vector2(536, 160), Size = new Vector2(240, 40), Disabled = true };
        AddChild(recruit);

        Refresh();
        GD.Print($"[HamletRoot] 回城就绪：金钱 {economy.Gold}（跨趟持有；减压一次 {economy.StressReliefCost}）");
    }

    /// <summary>刷新（只读跨趟状态，不自己算账）。</summary>
    public void Refresh()
    {
        int gold = ExpeditionContext.Gold?.Gold ?? 0;
        int cost = ExpeditionContext.Gold?.StressReliefCost ?? 0;
        int affordable = cost <= 0 ? 0 : gold / cost;
        _status.Text =
            $"【Hamlet 回城】金钱 {gold}　一次减压 {cost} ⇒ 现在能减 {affordable} 次\n" +
            "M8.0 ③ 只做入口接线：④ 减压（Tavern/Abbey 同价同效、风险不同）与 ⑤ 招募（免费 + 新兵 Lv1）随后落地。";
    }
}
