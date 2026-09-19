using System;

namespace Darkest.Data;

/// <summary>
/// 这份英雄美术描述符是【从哪来的】（决定"它是不是正式资产"）✓
/// </summary>
public enum HeroArtSource
{
    /// <summary>正式资产：`assets/heroes/&lt;archetype&gt;/hero.json`（原创，进 git）✓</summary>
    Formal,

    /// <summary>占位（按原型）：`assets/heroes_placeholder/&lt;archetype&gt;/hero.json`（gitignored · **不发布**）✓</summary>
    Placeholder,

    /// <summary>占位（旧单包）：`assets/heroes_placeholder/hero.json`（早期"1 个英雄验证接口"的形态 ⇒ 兼容保留）✓</summary>
    PlaceholderLegacy,

    /// <summary>都没有 ⇒ 调用方回落"色块 + 首字"（**不静默**：必须留痕 ✓）</summary>
    None,
}

/// <summary>
/// 解析结果。`ConfigPath` 为**描述符路径**，`Root` 为其所在目录（帧引用相对 <c>Root</c> 解析）✓
/// </summary>
public sealed record HeroArtResolution(
    HeroArtSource Source,
    string? ConfigPath,
    string? Root,
    string Note)
{
    public bool Found => Source != HeroArtSource.None;

    /// <summary>是否是"占位"（两种占位都算）⇒ 调用方**必须**把它当占位展示/留痕 ✓</summary>
    public bool IsPlaceholder => Source is HeroArtSource.Placeholder or HeroArtSource.PlaceholderLegacy;
}

/// <summary>
/// 🔴 **英雄美术资产的运行时解析**（用户 2026-09-19 拍板：把 mod 英雄接进来**替换现有内容**）。
///
/// 为什么要有它（问题）：`HeroAssets.RootFor/FramesOf/Resolve` 是**纯函数**，能力齐全，
///   但**游戏里没人调** —— `HeroArt`（表现层）此前**硬编码** `PlaceholderRoot` + `combat.frames[0]`，
///   于是 `warrior`/`tank`/`medic`/`commissar` **四个原型共用同一张占位图** ⚠️。
///   本类补的就是这条**接缝**：给定原型 ⇒ 决定用【哪一份】描述符 ✓
///
/// 位置优先级（**读到的**形状：契约 §3.2 正式按原型分目录；`PlaceholderRoot` 只装占位）：
///   ① `assets/heroes/&lt;archetype&gt;/hero.json`             ⇒ 正式（原创）✓
///   ② `assets/heroes_placeholder/&lt;archetype&gt;/hero.json` ⇒ 占位（按原型，**新增**：让四个原型各自一张）✓
///   ③ `assets/heroes_placeholder/hero.json`              ⇒ 占位（旧单包 ⇒ **兼容**，不破坏既有验证）✓
///   ④ 都没有 ⇒ `None` + **点名**（调用方回落色块 + 首字，并留痕 ✓）
///
/// 🔴 **零 Godot**：文件存在性由调用方注入（`exists`）⇒ 内核可单测、可 headless ✓
/// 🔴 **不静默**：占位与"都没有"都带 `Note`（红线 21：不给不可解释的默认）✓
/// </summary>
public static class HeroArtResolver
{
    /// <summary>正式资产的根（与 `HeroAssets.HeroesRoot` 同源，不另造 ✓）</summary>
    public const string FormalRoot = HeroAssets.HeroesRoot;

    /// <summary>占位的根（与 `HeroAssets.PlaceholderRoot` 同源 ✓）</summary>
    public const string PlaceholderRoot = HeroAssets.PlaceholderRoot;

    public const string DescriptorName = "hero.json";

    /// <param name="archetype">我们的原型 id（`units.json` 的 `warrior`/`tank`/`medic`/`commissar` ✓）</param>
    /// <param name="exists">文件是否存在（调用方注入：Godot `FileAccess.FileExists` / 测试里给假实现 ✓）</param>
    public static HeroArtResolution Resolve(string archetype, Func<string, bool> exists)
    {
        if (exists is null)
        {
            throw new ArgumentNullException(nameof(exists));
        }

        if (string.IsNullOrWhiteSpace(archetype))
        {
            return new HeroArtResolution(HeroArtSource.None, null, null,
                "未给原型 id ⇒ 无法解析英雄美术（调用方应回落色块+首字）✓");
        }

        string formal = $"{FormalRoot}/{archetype}/{DescriptorName}";
        if (exists(formal))
        {
            return new HeroArtResolution(HeroArtSource.Formal, formal, $"{FormalRoot}/{archetype}",
                $"正式资产：{formal} ✓");
        }

        string perArchetype = $"{PlaceholderRoot}/{archetype}/{DescriptorName}";
        if (exists(perArchetype))
        {
            return new HeroArtResolution(HeroArtSource.Placeholder, perArchetype, $"{PlaceholderRoot}/{archetype}",
                $"🔴 占位（按原型）：{perArchetype} —— **不是** `{archetype}` 的正式资产；来源见其 `source` 字段，**不发布** ✓");
        }

        string legacy = $"{PlaceholderRoot}/{DescriptorName}";
        if (exists(legacy))
        {
            return new HeroArtResolution(HeroArtSource.PlaceholderLegacy, legacy, PlaceholderRoot,
                $"🔴 占位（旧单包）：{legacy} —— 多原型共用一份 ⇒ 建议按原型补 `{PlaceholderRoot}/<archetype>/{DescriptorName}` ✓");
        }

        return new HeroArtResolution(HeroArtSource.None, null, null,
            $"未找到英雄美术描述符：`{formal}` ／ `{perArchetype}` ／ `{legacy}` 都不存在 ⇒ 回落色块+首字 ✓");
    }
}
