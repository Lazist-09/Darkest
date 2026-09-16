using System;
using Godot;

namespace Darkest.Ui;

/// <summary>
/// 🔴 策划 `#348`③ / `#347`：**占位英雄美术的读取入口**（战斗单帧 + 名册头像）——**只从
/// `HeroAssets.PlaceholderRoot` 读**（拷进 `resources/` **不会被加载**，那是 `§8` 的设计）✓
///
/// ⚠️ **关键坑（我实测踩过）**：占位 png 在 `res://assets/` 下且**没有导入产物**（`.import`）
///    ⇒ 走 `ResourceLoader` 会报 `No loader found for resource`（引擎错误 4）⚠️
///    ⇒ 正解 = **直接解码文件**：`Godot.Image.LoadFromFile`（**静态**）+ `ImageTexture.CreateFromImage` ✓
///
/// 🔴 **V6 纪律**：这里只证明"**接口能装下 + UI 能显示**"；**不证明**"动画能播"（需 Spine，本阶段裁掉）✓
/// </summary>
public static class HeroArt
{
    private static bool _probed;
    private static Texture2D? _combat;
    private static Texture2D? _portrait;

    /// <summary>战斗单帧（`actions.combat.frames[0]`）；取不到 ⇒ null（调用方回落色块+首字，并留痕）✓</summary>
    public static Texture2D? CombatTexture() => Load("combat", ref _combat);

    /// <summary>名册头像（`portrait` 字段；`P31` 契约）；取不到 ⇒ null（调用方回落色块+首字）✓</summary>
    public static Texture2D? PortraitTexture() => Load("portrait", ref _portrait);

    private static Texture2D? Load(string which, ref Texture2D? cache)
    {
        if (cache is not null)
        {
            return cache;
        }

        string jsonPath = $"{Darkest.Data.HeroAssets.PlaceholderRoot}/hero.json";
        if (!Godot.FileAccess.FileExists(jsonPath))
        {
            if (!_probed)
            {
                _probed = true;
                GD.Print($"[UI 占位英雄] `{jsonPath}` 不存在 ⇒ 回落色块+首字（正式路径不受影响）✓");
            }

            return null;
        }

        try
        {
            Darkest.Data.HeroAssetsConfig cfg = Darkest.Data.HeroAssets.Parse(
                Godot.FileAccess.GetFileAsString(jsonPath));

            string? rel = which switch
            {
                "combat" => cfg.Actions.TryGetValue("combat", out Darkest.Data.HeroActionSlot? slot) && slot.Frames.Count > 0
                    ? slot.Frames[0]
                    : null,
                "portrait" => cfg.Portrait, // 🔴 `P31` 的 `portrait` 字段（引用；允许缺失 ⇒ 缺就回落）✓
                _ => null,
            };

            if (string.IsNullOrEmpty(rel))
            {
                GD.Print($"[UI 占位英雄] 占位 `hero.json` 里没有 `{which}` 引用 ⇒ 回落色块+首字（不静默）✓");
                return null;
            }

            string full = $"{Darkest.Data.HeroAssets.PlaceholderRoot}/{rel}";
            Godot.Image? img = Godot.Image.LoadFromFile(full); // 🔴 静态；绕过导入系统 ✓
            cache = img is null ? null : Godot.ImageTexture.CreateFromImage(img);
            GD.Print(cache is null
                ? $"[UI 占位英雄] 载入失败 `{full}` ⇒ 回落色块+首字（不静默）✓"
                : $"[UI 占位英雄] ✅ {which} 用占位：`{full}`（**只从 PlaceholderRoot 读**；`placeholder={cfg.Placeholder}`）✓");
            return cache;
        }
        catch (Exception ex)
        {
            GD.Print($"[UI 占位英雄] 占位配置解析失败：{ex.Message} ⇒ 回落色块+首字（不静默）✓");
            return null;
        }
    }
}
