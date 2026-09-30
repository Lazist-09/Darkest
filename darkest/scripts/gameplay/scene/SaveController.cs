using System;
using System.IO;
using System.Text.Json;
using Darkest.Core.Events;
using Darkest.Data;
using Darkest.Gameplay.Sim.Run;
using Darkest.Gameplay.Sim.Save;

namespace Darkest.Gameplay.Scene;

/// <summary>一次存/读档的结果（`Ok=false` 时 `Message` 必须说清"为什么" —— 红线 21：不静默失败）✓</summary>
public sealed record SaveLoadResult(bool Ok, string Message)
{
    public static SaveLoadResult Success(string message) => new(true, message);

    public static SaveLoadResult Fail(string message) => new(false, message);
}

/// <summary>
/// 🔴 **存档编排**（`Phase 1` · `P4 ①` 起是**五个**持有者）—— 把「跨趟状态持有者」与「文件」缝在一起。
///
/// <para>**职责**：**存** = 五处 `CaptureSnapshot()` 拼成 `SaveSnapshot` → 序列化 → 落盘；
/// **读** = 读文本 → 反序列化 → 迁移 → 五处 `RestoreFrom(...)`。
/// **什么时候存**由调用方（组合根）决定，本类只提供 `Save`/`Load` ✓</para>
///
/// <para>🔴🔴 **硬纪律：读档失败绝不删档**（"版本不符即删档"是公认反模式）⇒
/// 迁移/反序列化失败时**原文件原样留在磁盘上**，只把原因回报给玩家 ✓</para>
/// </summary>
public sealed class SaveController
{
    private readonly SaveConfig _cfg;
    private readonly SaveFileGateway _gateway;
    private readonly Roster _roster;
    private readonly RunProgress _progress;
    private readonly HeirloomStock _heirlooms;
    private readonly Economy _economy;
    private readonly HeroGearState _gear;

    public SaveController(SaveConfig config, SaveFileGateway gateway,
        Roster roster, RunProgress progress, HeirloomStock heirlooms, Economy economy,
        HeroGearState gear)
    {
        _cfg = config ?? throw new ArgumentNullException(nameof(config));
        _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        _roster = roster ?? throw new ArgumentNullException(nameof(roster));
        _progress = progress ?? throw new ArgumentNullException(nameof(progress));
        _heirlooms = heirlooms ?? throw new ArgumentNullException(nameof(heirlooms));
        _economy = economy ?? throw new ArgumentNullException(nameof(economy));
        _gear = gear ?? throw new ArgumentNullException(nameof(gear));
    }

    /// <summary>可用槽位数（来自 `save.json`；数字不硬编码 —— #307 ✓）。</summary>
    public int SlotCount => _cfg.SlotCount;

    /// <summary>槽位上有没有存档。</summary>
    public bool HasSave(int slot) => _gateway.Exists(slot);

    /// <summary>
    /// 🔴 **存盘**：五处状态 → 快照 → JSON → 落盘。
    /// </summary>
    public SaveLoadResult Save(int slot)
    {
        try
        {
            var snapshot = new SaveSnapshot(
                SaveMigrator.CurrentVersion,
                _roster.CaptureSnapshot(),
                _progress.CaptureSnapshot(),
                _heirlooms.CaptureSnapshot(),
                _economy.CaptureSnapshot(),
                _gear.CaptureSnapshot());

            _gateway.WriteText(slot, SaveSerializer.Serialize(snapshot));
            return SaveLoadResult.Success(
                $"已存入槽位 {slot}：名册 {snapshot.Roster.Heroes.Count} 人 / " +
                $"金币 {snapshot.Economy.Gold} / 已完成 {snapshot.Progress.RunsFinished} 趟 / " +
                $"装备阶 {snapshot.Gear.Tiers.Count} 条 ✓");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                      or InvalidDataException or ArgumentOutOfRangeException)
        {
            // 🔴 存盘失败**必须看得见**（静默失败 = 玩家以为存了、实际没存）
            return SaveLoadResult.Fail($"存盘失败（槽位 {slot}）：{ex.Message}");
        }
    }

    /// <summary>
    /// 🔴 **读档**：文本 → 快照 → 迁移 → 恢复五处状态。
    /// <para>🔴 **失败时不动磁盘**：损坏档 / 未来版本档 ⇒ 只回报原因，**文件保留**（交给玩家决定）✓</para>
    /// </summary>
    public SaveLoadResult Load(int slot)
    {
        string? text = _gateway.ReadText(slot);
        if (text is null)
        {
            return SaveLoadResult.Success($"槽位 {slot} 还没有存档（新建一局）✓");
        }

        SaveSnapshot snapshot;
        try
        {
            snapshot = SaveSerializer.Deserialize(text);
        }
        catch (Exception ex) when (ex is InvalidDataException or JsonException)
        {
            // 🔴 损坏档：**回报 + 保留文件**（绝不删除）
            return SaveLoadResult.Fail(
                $"槽位 {slot} 的存档读不出来（损坏）：{ex.Message} —— **原档已保留**，未做任何修改 ✓");
        }

        MigrationResult migrated = SaveMigrator.Migrate(snapshot);
        if (!migrated.Ok)
        {
            return SaveLoadResult.Fail($"槽位 {slot}：{migrated.Message}");
        }

        // 🔴 恢复**不写事件**：读档不是游戏事件，写进 `CombatLog` 会污染事件流 ✓
        SaveSnapshot loaded = migrated.Snapshot!;
        try
        {
            _roster.RestoreFrom(loaded.Roster);
            _progress.RestoreFrom(loaded.Progress);
            _heirlooms.RestoreFrom(loaded.Heirlooms);
            _economy.RestoreFrom(loaded.Economy);
            _gear.RestoreFrom(loaded.Gear);
        }
        catch (InvalidDataException ex)
        {
            // 🔴 反序列化能过、但**语义越界**的档（例：装备阶 9 阶）⇒ 同样**只回报 + 保留原档**，
            //    绝不静默钳制（钳住 = 玩家看到的读数与档里写的不是一回事）✓
            return SaveLoadResult.Fail(
                $"槽位 {slot} 的存档内容越界：{ex.Message} —— **原档已保留**，未做任何修改 ✓");
        }

        return SaveLoadResult.Success(
            $"已读取槽位 {slot}：名册 {loaded.Roster.Heroes.Count} 人 / " +
            $"金币 {loaded.Economy.Gold} / 已完成 {loaded.Progress.RunsFinished} 趟 / " +
            $"装备阶 {loaded.Gear.Tiers.Count} 条 ✓");
    }
}
