using Godot;

namespace Darkest.UI;

/// <summary>
/// BattleUi：四分区装配（blueprint §5d / ui_spec §1 / T-M5-05）——
/// A 顶栏（回合+行动序列+撤退按钮）、B 战场（槽视图）、C 角色卡、D 技能栏；
/// 只读 IBattleView 网关 + 命令门面，状态真值在内核。
/// MVP 最小集占位（位置编号/行动序列/撤退数字/技能灰显 tooltip/位移预览由后续分区控件实现）。
/// </summary>
public partial class BattleUi : CanvasLayer
{
    public override void _Ready()
    {
        // 四分区挂载占位：分区控件（TopBar/ActionOrderBar/RetreatButton/SkillBar/CharacterCard/BoardOverlay）
        // 在 Battle.tscn 中以子节点装配；本脚本仅持有 IBattleView 网关引用（M5 UI 薄层）。
    }
}