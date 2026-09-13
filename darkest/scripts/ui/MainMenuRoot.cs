using System;
using System.Linq;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Scene;
using Darkest.Gameplay.Sim.Run;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// M8.0 收尾 ①（`#288` / V8）：**主菜单 = 启动入口的正解**。
///
/// 背景：`project.godot` 的 main_scene 原本是 `Battle.tscn`（那是 `#279` 孤岛的 **(b) 权宜**），
/// 玩家启动即进单场战斗，要手动点按钮才能去远征/回城。本场景把入口摆正：
/// **启动 ⇒ 主菜单 ⇒ 三选一（单场战斗 ／ 出发远征 ／ 回城）**。
///
/// 🔴 三条硬要求：
/// · **三个入口都保留** —— **单场战斗不能删**（它是 **A1 判定闸**的基础）；
/// · **CLI 全部保留**（`--expedition` / `--hamlet` / `--hamlet-next` / `--e2e`，冒烟依赖它们）⇒ 本场景**转发 CLI**；
/// · **只渲染 + 转发**（本类不持游戏状态）。
/// 验收（红线 18）：三项**从启动场景可达**（不是场景直载）。
/// </summary>
public partial class MainMenuRoot : Node2D
{
    public const string BattleScene = "res://scenes/battle/Battle.tscn";
    public const string ExpeditionScene = "res://scenes/expedition/Expedition.tscn";
    public const string HamletScene = "res://scenes/hamlet/Hamlet.tscn";

    private Label _title = null!;
    private Label _status = null!;

    public override void _Ready()
    {
        _title = new Label
        {
            Name = "MenuTitle",
            Text = "【主菜单】选择去向（三选一；单场战斗保留 —— 它是 A1 判定闸的基础）",
            Position = new Vector2(24, 24),
            Size = new Vector2(1200, 40),
        };
        AddChild(_title);

        AddMenuButton("单场战斗（A1 判定闸）", BattleScene, 0);
        AddMenuButton("出发远征（地牢层）", ExpeditionScene, 1);
        AddMenuButton("回城（Hamlet）", HamletScene, 2);

        // 跨趟状态在这里就先确保（菜单要显示金钱/名册概况）
        Economy economy = ExpeditionContext.EnsureEconomy(
            EconomyConfig.Parse(FileAccess.GetFileAsString(EconomyConfig.ResPath)));
        Roster roster = ExpeditionContext.EnsureRoster(
            RosterConfig.Parse(FileAccess.GetFileAsString(RosterConfig.ResPath)));

        _status = new Label
        {
            Name = "MenuStatus",
            Position = new Vector2(24, 220),
            Size = new Vector2(1200, 80),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        AddChild(_status);
        _status.Text = $"跨趟状态：金钱 {economy.Gold}　名册 {roster.Heroes.Count}/{roster.Cap}　" +
                       $"最低士气 {roster.Heroes.Min(h => roster.MoraleOf(h.Id))}";

        GD.Print($"[MainMenuRoot] 主菜单就绪：三选一（单场战斗 ／ 出发远征 ／ 回城）　" +
                 $"金钱 {economy.Gold}　名册 {roster.Heroes.Count}");

        // 🔴 CLI 全保留：启动即按参数直达（冒烟依赖；这些参数此前挂在 BattleRoot 上）
        string[] args = OS.GetCmdlineArgs();
        if (Array.Exists(args, a => a == "--hamlet"))
        {
            GD.Print("[MainMenuRoot] --hamlet ⇒ 直达回城（冒烟路径：启动 到 回城）");
            GetTree().CallDeferred("change_scene_to_file", HamletScene);
        }
        else if (Array.Exists(args, a => a == "--expedition" || a == "--e2e" || a == "--hamlet-next"))
        {
            GD.Print("[MainMenuRoot] --expedition/--e2e/--hamlet-next ⇒ 直达地牢层（冒烟路径）");
            GetTree().CallDeferred("change_scene_to_file", ExpeditionScene);
        }
    }

    private void AddMenuButton(string text, string scenePath, int index)
    {
        var button = new Button
        {
            Name = $"Menu{index}",
            Text = text,
            Position = new Vector2(24, 80 + (index * 44)),
            Size = new Vector2(420, 38),
        };
        button.Pressed += () => GetTree().ChangeSceneToFile(scenePath);
        AddChild(button);
    }
}
