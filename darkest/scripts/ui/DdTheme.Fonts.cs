using Godot;

namespace Darkest.UI;

// ① 来源：从 `DdTheme.cs` 拆出（用户红线：程序文件 ≤600 行 · 目标 ≤400 · 架构 `file_size_split.md` §3 四条）——
//    本片 = **字体解析族**（原 `:194-287` 逐字节；M11 ① 预警面第二十二件·第一片）✓
// ② 职责：字体目录常量（`FontDir`）／`ResolveFonts`（拉丁→CJK fallback 链 ＋ `HasChar` 中文覆盖断言）／
//    `FirstExisting`（候选名按优先级试）／`ScanFontDir`（目录扫描兜底）／`Describe`／`TryLoadFont` ✓
// ③ 🔴 依赖（实测扫描本片 · 代码区 67 行）：**零跨片依赖** —— 仅用自身成员 `FontDir` x12 ／ `FirstExisting` x3 ／
//    `ScanFontDir` x3 ／ `Describe` x3 ／ `TryLoadFont` x3 ／ `ResolveFonts` x1；引擎口 `ResourceLoader` x2 ／ `DirAccess` x2 ✓
// ④ 只搬家、零行为改动（逐字节原样；using 按需裁剪 ⇒ 1 条；构建 RC=0 验证）✓
// ─────────────────────────────────────────────────────────────
public static partial class DdTheme
{
    /// <summary>字体目录（**放进去即生效**；`§13.4①` 的实施落点）。</summary>
    public const string FontDir = "res://resources/theme/fonts/";

    /// <summary>
    /// 🔴 解析字体（`§13.4①` / 策划 `#321`④）：**优先拉丁 `EB Garamond` → CJK `Noto Serif SC` 的 fallback 链**。
    /// ⚠️ **不要写死单一文件名**（实测教训）：用户实际放进来的是 `NotoSerifSC-Regular.otf`（Noto 官方**静态字重**命名），
    ///    而不是我原先假定的 `NotoSerifSC-Subset.ttf` ⇒ 写死名字会"文件明明在却认不出"（且只报"未找到"，很误导）⚠️
    /// ⇒ 正解 = **候选名按优先级试 + 目录扫描兜底**，并在读数里**打印真正用的是哪个文件** ✓
    /// </summary>
    private static (Font? Main, Font? Cjk, string Note) ResolveFonts()
    {
        Font? latin = FirstExisting(
            $"{FontDir}EBGaramond.ttf",
            $"{FontDir}EBGaramond-VF.ttf",
            $"{FontDir}EBGaramond-Regular.ttf");

        // CJK 候选：正则权重（正文）优先；子集/可变字重次之
        Font? cjk = FirstExisting(
            $"{FontDir}NotoSerifSC-Subset.ttf",
            $"{FontDir}NotoSerifSC-Regular.otf",
            $"{FontDir}NotoSerifSC-Regular.ttf",
            $"{FontDir}NotoSerifSC-VF.ttf",
            $"{FontDir}NotoSerifSC-Subset.otf");

        cjk ??= ScanFontDir("NotoSerifSC");   // 兜底：目录里任何 NotoSerifSC* 字体（按名字排序取第一个）
        latin ??= ScanFontDir("EBGaramond");

        Font? main = latin ?? cjk;
        if (main is null)
        {
            return (null, null, $"🔴 未找到字体文件（{FontDir} 下应有 EBGaramond*.ttf ／ NotoSerifSC*.otf|ttf）" +
                          " ⇒ **用引擎默认字体（占位）**；把 OFL 字体放进该目录即自动生效");
        }

        // 🔴 fallback 链：拉丁字体在前、CJK 在后（缺字形时逐级回退）✓
        if (latin is not null && cjk is not null)
        {
            latin.Fallbacks = new Godot.Collections.Array<Font> { cjk };
        }

        // 🔴 可断言：**字体是否真的覆盖中文**（不是"看起来像换了字体"）——
        //    `Font.HasChar` 是引擎内置查询 ⇒ 拿一个中文常用字直接问它 ✓（红线 25：能断言才算）
        const char Probe = '黑';
        bool cjkCovered = cjk is not null && cjk.HasChar(Probe);
        string cover = cjkCovered
            ? $"✅ 中文覆盖（`HasChar('{Probe}')` = true）"
            : "🔴 **中文未覆盖**（CJK 字体缺失或缺字形 ⇒ 中文会掉字）";

        return (main, cjk, $"{Describe(main)}（fallback {(cjk is null ? "无" : Describe(cjk))}）⇒ 已接（`§13.4①`）　{cover}");
    }

    /// <summary>按顺序取第一个存在的字体。</summary>
    private static Font? FirstExisting(params string[] paths)
    {
        foreach (string p in paths)
        {
            Font? f = TryLoadFont(p);
            if (f is not null)
            {
                return f;
            }
        }

        return null;
    }

    /// <summary>兜底：扫 `resources/theme/fonts/` 里名字含关键字的字体（按名字排序，结果确定）✓</summary>
    private static Font? ScanFontDir(string keyword)
    {
        using DirAccess? dir = DirAccess.Open(FontDir.TrimEnd('/'));
        if (dir is null)
        {
            return null;
        }

        var names = new System.Collections.Generic.List<string>();
        foreach (string file in dir.GetFiles())
        {
            if (file.Contains(keyword, System.StringComparison.OrdinalIgnoreCase) &&
                (file.EndsWith(".ttf", System.StringComparison.OrdinalIgnoreCase) ||
                 file.EndsWith(".otf", System.StringComparison.OrdinalIgnoreCase)))
            {
                names.Add(file);
            }
        }

        names.Sort(System.StringComparer.Ordinal);
        return names.Count == 0 ? null : TryLoadFont($"{FontDir}{names[0]}");
    }

    private static string Describe(Font f) => $"{f.ResourcePath.GetFile()}";

    private static Font? TryLoadFont(string resPath)
        => ResourceLoader.Exists(resPath) ? ResourceLoader.Load<Font>(resPath) : null;

}
