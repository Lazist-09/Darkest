using System;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 `ui_spec §12.4` **i18n 的"布局先对"验收**（策划 `#315`⑩ / `#321` 口径）：
/// "i18n 的【文本】可以后补，但【布局方式】必须现在对" —— 那到底怎么**断言**？
///
/// ⇒ 本探针：`--ui-longtext` 把当前场景里所有可见 `Label` 的文本**膨胀约 40%**（附加上限 40 字符的宽字 `W`），
///   然后**照跑同一套判据**（`--ui-audit`：可见 Label 两两不相交 ／ Panel 不透明）。
///   若膨胀后**重叠仍为 0** ⇒ 布局真的"可 i18n"（容器在兜底）；
///   若出现重叠 ⇒ **那就是 i18n 缺口清单**（哪对 Label 压了，一眼可指认）✓
///
/// ⚠️ 纪律：它**只改 `.Text`**（纯表现、冒烟专用，带 `--ui-longtext` 才生效）；**不碰玩法状态**、不进正式路径。
/// </summary>
public static class LongTextProbe
{
    public const string Flag = "--ui-longtext";

    /// <summary>膨胀比例（`§12.4` 的"多语言文本更长"压力；40% 是常见中↔英/德法落差的下界）。</summary>
    private const double Factor = 0.4;

    /// <summary>附加宽度上限（防个别长文本把测试变成噪声）。</summary>
    private const int MaxPad = 40;

    public static bool Requested => Array.Exists(OS.GetCmdlineArgs(), a => a == Flag);

    /// <summary>把子树里所有可见 `Label` 的文本膨胀（跳过瞬态特效层）。返回膨胀个数（取证）。</summary>
    public static int Inflate(Node root)
    {
        int n = 0;
        foreach (Node child in root.GetChildren())
        {
            if (child.Name == LayoutAudit.MotionLayerName)
            {
                continue;
            }

            if (child is Label label && label.IsVisibleInTree() && !string.IsNullOrWhiteSpace(label.Text))
            {
                int pad = Math.Clamp((int)(label.Text.Length * Factor), 4, MaxPad);
                label.Text += new string('W', pad);
                n++;
            }

            n += Inflate(child);
        }

        return n;
    }
}
