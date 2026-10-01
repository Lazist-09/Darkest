using System;

namespace Darkest.UI;

/// <summary>
/// 🔴 **拖放载荷协议【只有这一份】**（红线 21 (b)：一条规则一个落点）—— Godot 内建 drag-and-drop 的载荷
/// 就是一个 <c>Variant</c> 字符串；本类只做**编解码 ＋ 族校验**，不碰任何控件（因此也不依赖场景）✓
///
/// <para>格式：<c>&lt;tag&gt;:&lt;id&gt;</c>（例 <c>hero:hero_warrior_1</c> ／ <c>trinket:crow_wingfeather</c>）——
/// **tag 是必须的**：孔收错族必须**当场拒绝**（<c>_CanDropData</c> ⇒ false）。否则玩家把英雄方块拖进饰品格，
/// 英雄 id 会被拿去查饰品库 ⇒ <c>TrinketsConfig.Get</c> 当场抛（拖一下就把游戏炸了）✓</para>
///
/// <para>🆕 2026-10-02 M4u：英雄孔（铁匠铺 ／ 供应队形）与饰品孔（角色详情 2 格）**共用本协议** ——
/// 不复制第二套拖放代码（用户 2026-10-01：「不要自己造轮子」）✓</para>
/// </summary>
public static class DragPayload
{
    /// <summary>英雄族（载荷 = 英雄 id）✓</summary>
    public const string HeroTag = "hero";

    /// <summary>饰品族（载荷 = 饰品 id）✓</summary>
    public const string TrinketTag = "trinket";

    /// <summary>编成载荷串；<paramref name="id"/> 为空 ⇒ 空串（调用方据此**不起拖**，不静默拖一个空载荷）✓</summary>
    public static string Encode(string tag, string id)
        => string.IsNullOrEmpty(id) ? string.Empty : $"{tag}:{id}";

    /// <summary>
    /// 解载荷：**tag 必须逐字等于 <paramref name="acceptTag"/> 且 id 非空**，否则 false。
    /// <para>🔴 **不抛** —— 拖放是玩家输入，不能因为拖错东西就崩；调用方把 false 如实表达为「这个孔不收它」✓</para>
    /// </summary>
    public static bool TryDecode(string? raw, string acceptTag, out string id)
    {
        id = string.Empty;
        if (string.IsNullOrEmpty(raw))
        {
            return false;
        }

        int sep = raw.IndexOf(':', StringComparison.Ordinal);
        if (sep <= 0 || sep >= raw.Length - 1)
        {
            return false;
        }

        if (!string.Equals(raw[..sep], acceptTag, StringComparison.Ordinal))
        {
            return false;
        }

        id = raw[(sep + 1)..];
        return true;
    }
}
