using System;
using Darkest.Data;
using Godot;

namespace Darkest.Gameplay.Scene;

/// <summary>
/// 🔴 **存档的物理读写**（`Phase 1`）—— 整个项目**唯一**碰存档文件的类。
///
/// <para>**为什么它必须在 scene 层**：内核（`scripts/gameplay/sim/`）必须**零 `using Godot`**，
/// 而"把字节写到磁盘"只能用 Godot 的 `FileAccess` ⇒ 把 IO 单独关在这一层，
/// 内核只负责"**快照 ⇄ JSON 文本**"（`SaveSerializer`）⇒ 两边都不越界 ✓</para>
///
/// <para>🔴 **O-84 / 红线 26**：表现层**禁止 `System.IO`**（`File.Exists`/`Path.Combine` 等）——
/// 导出后 `res://data/*.json` 在 PCK 里、磁盘上根本不存在 ⇒ 只能用 `FileAccess`/`DirAccess` ✓</para>
///
/// <para>🔴 **为什么是 `user://` 而不是 `res://`**：`res://` 导出后是**只读**的；
/// 存档必须是**可写**目录 ⇒ `user://saves/`（Godot 会映射到各平台的 User Data 目录）✓</para>
///
/// <para>**不在这里的东西**：**读不懂/版本不符怎么办**归 `SaveMigrator`；
/// **什么时候存**归 `SaveController` ⇒ 本类只做"读得到/写得进" ✓</para>
/// </summary>
public sealed class SaveFileGateway
{
    /// <summary>存档目录（`user://` = 可写的 User Data 目录；`res://` 导出后只读，不能放存档）✓</summary>
    private const string SaveDir = "user://saves";

    private readonly SaveConfig _cfg;

    public SaveFileGateway(SaveConfig config)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>槽位 `slot` 上有没有存档。</summary>
    public bool Exists(int slot) => FileAccess.FileExists(PathFor(slot));

    /// <summary>
    /// 读回槽位文本；**没有该档 ⇒ 返回 `null`**（"还没存过"是正常状态，不是错误 ✓）。
    /// </summary>
    public string? ReadText(int slot)
    {
        string path = PathFor(slot);
        if (!FileAccess.FileExists(path))
        {
            return null;
        }

        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (file is null)
        {
            // 🔴 打不开**必须报出来**（静默返回 null 会让玩家以为"档没了"）✓
            throw new InvalidOperationException(
                $"存档 {path} 存在但打不开（错误码 {(int)FileAccess.GetOpenError()}）✓");
        }

        return file.GetAsText();
    }

    /// <summary>
    /// 写入槽位（覆盖写）。
    /// <para>🔴 **先建目录**：`user://saves` 在首次运行时还不存在 ⇒
    /// 不建就会 `Open` 失败（表现为"存了但没存上"—— 静默失败家族）✓</para>
    /// </summary>
    public void WriteText(int slot, string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            throw new ArgumentException("存档内容不得为空（拒绝把空文本写成「有效存档」）✓", nameof(json));
        }

        EnsureDirectory();
        string path = PathFor(slot);

        using FileAccess? file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (file is null)
        {
            throw new InvalidOperationException(
                $"无法写入存档 {path}（错误码 {(int)FileAccess.GetOpenError()}）✓");
        }

        file.StoreString(json);
    }

    /// <summary>🔴 槽位文件的完整路径（**不拼 `System.IO.Path`** —— O-84 红线 26 ✓）。</summary>
    private string PathFor(int slot) => $"{SaveDir}/{_cfg.FileNameFor(slot)}";

    /// <summary>确保存档目录存在（首次运行 / 玩家手删后都需要）✓</summary>
    private static void EnsureDirectory()
    {
        if (DirAccess.DirExistsAbsolute(SaveDir))
        {
            return;
        }

        Error err = DirAccess.MakeDirRecursiveAbsolute(SaveDir);
        if (err != Error.Ok)
        {
            throw new InvalidOperationException($"无法创建存档目录 {SaveDir}（错误码 {(int)err}）✓");
        }
    }
}
