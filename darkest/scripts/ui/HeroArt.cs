using System;
using System.Linq;
using Godot;

namespace Darkest.UI;

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
    private static bool _combatDone;     // 🔴 成功/失败都置位 ⇒ **绝不每帧重试**（主程序 2026-09-16 报告）✓
    private static bool _portraitDone;

    /// <summary>战斗单帧（`actions.combat.frames[0]`）；取不到 ⇒ null（调用方回落色块+首字，并留痕）✓</summary>
    public static Texture2D? CombatTexture() => Load("combat", ref _combat, ref _combatDone);

    /// <summary>名册头像（`portrait` 字段；`P31` 契约）；取不到 ⇒ null（调用方回落色块+首字）✓</summary>
    public static Texture2D? PortraitTexture() => Load("portrait", ref _portrait, ref _portraitDone);

    private static Texture2D? Load(string which, ref Texture2D? cache, ref bool done)
    {
        if (done)
        {
            return cache;   // 🔴 已处理过（成功或失败）⇒ **不再重试**（防止失败时每帧刷 ERROR）✓
        }

        string jsonPath = $"{Darkest.Data.HeroAssets.PlaceholderRoot}/hero.json";
        if (!Godot.FileAccess.FileExists(jsonPath))
        {
            done = true;
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

            // 🔴 **改用 `HeroAssets` 的现成访问器**（2026-09-20）——
            //    此前这里**内联重写**了"取帧 / 取头像引用"的逻辑 ⇒ 那三个纯函数（`FramesOf`/`PortraitRef`/`RootFor`）
            //    **无人调用**、成了疑似死代码 ⚠️；而且这里还把根**硬编码**成 `PlaceholderRoot`（绕过了 `RootFor`）⚠️
            //    ⇒ 现在：取引用 / 取根 / 拼路径 **全部走 `HeroAssets`**（单一真值、不再重复实现）✓
            string? rel = which switch
            {
                "combat" => Darkest.Data.HeroAssets.FramesOf(cfg, "combat").FirstOrDefault(),
                "portrait" => Darkest.Data.HeroAssets.PortraitRef(cfg) is { Length: > 0 } p ? p : null,
                _ => null,
            };

            if (string.IsNullOrEmpty(rel))
            {
                done = true;
                GD.Print($"[UI 占位英雄] 占位 `hero.json` 里没有 `{which}` 引用 ⇒ 回落色块+首字（不静默）✓");
                return null;
            }

            string full = Darkest.Data.HeroAssets.Resolve(cfg, rel!); // 🔴 根 + 引用（`RootFor` 内部判占位/正式）✓

            // 🔴 **关键修正**（主程序 2026-09-16 抓到的异常正文）：
            //    `Image.LoadFromFile` 只接受**文件系统路径** ⇒ 传 `res://…` **必失败**，
            //    且旧代码失败不缓存 ⇒ **每帧重试、每帧刷一条 ERROR** ⚠️
            //    ⇒ 正解：`FileAccess.GetFileAsBytes`（**支持 `res://`**）+ `Image.LoadPngFromBuffer` ✓
            Godot.Image? img = null;
            byte[] bytes = Godot.FileAccess.GetFileAsBytes(full);
            if (bytes.Length > 0)
            {
                var decoded = new Godot.Image();
                Godot.Error err = decoded.LoadPngFromBuffer(bytes);
                img = err == Godot.Error.Ok ? decoded : null;
            }

            done = true;   // 🔴 无论成败都置位 ⇒ 不再重试 ✓
            cache = img is null ? null : Godot.ImageTexture.CreateFromImage(img);
            GD.Print(cache is null
                ? $"[UI 占位英雄] 载入失败 `{full}` ⇒ 回落色块+首字（**已缓存失败、不再重试**）✓"
                : $"[UI 占位英雄] ✅ {which} 用占位：`{full}`（**只从 PlaceholderRoot 读**；`placeholder={cfg.Placeholder}`）✓");
            return cache;
        }
        catch (Exception ex)
        {
            done = true;
            GD.Print($"[UI 占位英雄] 占位配置解析失败：{ex.Message} ⇒ 回落色块+首字（不静默）✓");
            return null;
        }
    }
}
