using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Darkest.Gameplay.Sim.Run;

/// <summary>起点/终点坐标（P30 ③ 要求落在**可行走**格）✓</summary>
public sealed record DungeonGridPoint(
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y);

/// <summary>房间锚点（P30 ④：`content_ref` 引用必须存在，与 P26 同族）✓</summary>
public sealed record DungeonGridRoomAnchor(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("x")] int X,
    [property: JsonPropertyName("y")] int Y,
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("content_ref")] string? ContentRef);

/// <summary>
/// 🔴 **每格遭遇（`P30` ⑤ / 策划 `#338`⑤）：本次【只留字段、值必须为 `null`】**
/// ⇒ **禁止加载器填默认值**（否则就是"谁替策划定了一个 5%"的静默默认 ⚠️ —— 红线 21 / P29 ① 同族）✓
/// 用户原话：**每格都可能遇敌，但规则后续安排（暂留）** ✓
/// </summary>
public sealed record DungeonGridEncounter(
    [property: JsonPropertyName("per_tile_chance")] int? PerTileChance,
    [property: JsonPropertyName("cooldown_tiles")] int? CooldownTiles,
    [property: JsonPropertyName("note")] string? Note)
{
    /// <summary>全空 = "规则未定"（本次**唯一允许**的形态）✓</summary>
    public bool IsEmpty => PerTileChance is null && CooldownTiles is null;
}

/// <summary>
/// 视野（策划 `#338`②：进格揭示 + 半径以**格**为单位；侦察临时 +N ⇒ 数值 `placeholder`）✓
///
/// 🔴 `D-3`（2026-09-20）：本记录**原本只有加载期 `radius ≥ 0` 校验、全仓无消费点**
/// ⇒ 典型的"填了但没生效"（红线 21 / `P29` ① 静默默认）⚠️ 现已接线，消费点见：
///   · `DungeonWalker.StateAt(..., vision)`（格级视野 ⇒ `Visited`）
///   · `ExpeditionFlow.TileStateAt` / `TileVision`（流程读数）
///   · `WalkMapView.FromTileWalk(..., stateOf)`（表现层三态配色）
/// 注：`tuning.dungeon_layer.vision` 是**同一份契约的另一个载体**（见 `TuningGridVision`）——
///     两者**字段一致**，因为 `dungeon_grid.json` 尚不存在（仍在派生图阶段）⇒ 走 tuning 注入 ✓
/// </summary>
public sealed record DungeonGridVision(
    [property: JsonPropertyName("reveal_on_enter")] bool RevealOnEnter,
    [property: JsonPropertyName("radius")] int Radius,
    [property: JsonPropertyName("scout_bonus")] int ScoutBonus = 0);

/// <summary>
/// 🔴🔴 **回头代价（`D-1`，2026-09-20 新增）** —— 网格层的 DD 口径。
///
/// **为什么必须新开一段**：旧房间图层 `expedition_map.json` 的 `revisit_cost` 校验写着
/// 「**回头必须更便宜**」（`|revisit| &lt; |new|`，`P25 ④`）—— 那是"按段移动"的世界；
/// 而 DD 的真规则是**重走已探索区更便宜、但仍有代价**（wiki ⑦：新区域 −6 / 重走 −1）。
/// ⇒ `D-1` 裁决：**旧层不动**（免返工，且它的"回头更便宜"在按段世界里自洽），
///   **新网格层用 DD 口径**（本段）。
///
/// **数值全部 `placeholder: true`**（`#307`）：DD 的 `−6 / −1` 挂在它的 `light` 刻度上，
/// 我们的刻度不同（`tuning.light.node_step = −30`）⇒ **不能直接抄数字**，等实测校准。
/// </summary>
public sealed record DungeonGridBacktrack(
    /// <summary>进入**已访问**格（重走）时额外扣的光照（**正数** = 消耗量；`null` = 未定案）⚠️</summary>
    [property: JsonPropertyName("revisit_light_cost")] int? RevisitLightCost,

    /// <summary>进入**未访问**格时的光照（`null` = 沿用既有 `WalkLightCost` 守恒口径，不在此处重复取值）✓</summary>
    [property: JsonPropertyName("new_tile_light_cost")] int? NewTileLightCost = null,

    /// <summary>人类可读说明（**不参与行为**；`#408` 说明字段纪律）✓</summary>
    [property: JsonPropertyName("note")] string? Note = null);

/// <summary>
/// 移动（`P30` ⑥ / 架构附注④）：🔴 **`cost_light_per_tile` 量级未定案 ⇒ 本次只登记、不取值（必须 null）** ⚠️
/// 已定的机制是**总消耗守恒**（见 `WalkLightCost`）：用**现有** `tuning.move.new_area` 作为"每段消耗"折算，**不在此处取值** ✓
/// </summary>
public sealed record DungeonGridMove(
    [property: JsonPropertyName("cost_light_per_tile")] int? CostLightPerTile);

/// <summary>
/// 🔴 **`dungeon_grid.json`**（架构 `data_schema` **`P30`**；用户裁 (B) 瓷砖网格）✓
/// 加载期校验 = `P30` ①~⑦ **逐条**（缺一条都会让"图不可通关"变成玩家撞墙 ⚠️）✓
/// ⚠️ 本类**只管数据与校验**；"能不能走"的判定仍在 `DungeonWalker`（内核单一真值）✓
/// </summary>
public sealed record DungeonGridConfig(
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("tiles")] IReadOnlyList<string> Tiles,
    [property: JsonPropertyName("start")] DungeonGridPoint Start,
    [property: JsonPropertyName("goal")] DungeonGridPoint Goal,
    [property: JsonPropertyName("room_anchors")] IReadOnlyList<DungeonGridRoomAnchor>? RoomAnchors = null,
    [property: JsonPropertyName("encounter")] DungeonGridEncounter? Encounter = null,
    [property: JsonPropertyName("vision")] DungeonGridVision? Vision = null,
    [property: JsonPropertyName("move")] DungeonGridMove? Move = null,
    [property: JsonPropertyName("backtrack")] DungeonGridBacktrack? Backtrack = null)
{
    public const string ResPath = "res://data/dungeon_grid.json";

    /// <summary>转成不可变网格（校验通过后才会走到这里）✓</summary>
    public DungeonGrid ToGrid() => DungeonGrid.Parse(ResPath, Tiles);

    /// <summary>
    /// 解析 + **`P30` 七条校验**（全部在**加载期**；任何一条不满足 ⇒ 抛 `InvalidDataException`，绝不静默修数据）✓
    /// </summary>
    public static DungeonGridConfig Parse(string json, IReadOnlySet<string>? knownContentRefs = null)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDataException($"{ResPath}: 内容为空。");
        }

        DungeonGridConfig cfg;
        try
        {
            cfg = JsonSerializer.Deserialize<DungeonGridConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? throw new InvalidDataException($"{ResPath}: 反序列化得到 null。");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"{ResPath}: JSON 解析失败（{ex.Message}）。", ex);
        }

        // ① 形状：`width/height > 0`、行数 == height、每行长度 == width ✓
        if (cfg.Width <= 0 || cfg.Height <= 0)
        {
            throw new InvalidDataException($"{ResPath}: width/height 必须 > 0（实测 {cfg.Width}×{cfg.Height}）。");
        }

        if (cfg.Tiles is null || cfg.Tiles.Count != cfg.Height)
        {
            throw new InvalidDataException(
                $"{ResPath}: `tiles` 行数（{cfg.Tiles?.Count ?? 0}）必须等于 height（{cfg.Height}）。");
        }

        for (int y = 0; y < cfg.Tiles.Count; y++)
        {
            if (cfg.Tiles[y].Length != cfg.Width)
            {
                throw new InvalidDataException(
                    $"{ResPath}: 第 {y + 1} 行长度 {cfg.Tiles[y].Length} ≠ width {cfg.Width}（形状不符）✓");
            }
        }

        // ② 字符集：每个字符都必须**在字符表里**（用"字符 ⇔ 枚举"往返判定 ⇒ 未登记字符会被抓出）✓
        for (int y = 0; y < cfg.Tiles.Count; y++)
        {
            for (int x = 0; x < cfg.Tiles[y].Length; x++)
            {
                char c = cfg.Tiles[y][x];
                if (DungeonTileMap.ToChar(DungeonTileMap.FromChar(c)) != c)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: 字符 '{c}'（{x},{y}）不在字符表里 —— 新增字符必须**同时登记枚举**（P30 ②）✓");
                }
            }
        }

        DungeonGrid grid = cfg.ToGrid();

        // ③ `start`/`goal` 必须落在**可行走**格，且 **`goal` 必须从 `start` 可达**（BFS 校验）✓
        if (!grid.InBounds(cfg.Start.X, cfg.Start.Y) || !DungeonTileMap.IsWalkable(grid.TileAt(cfg.Start.X, cfg.Start.Y)))
        {
            throw new InvalidDataException($"{ResPath}: start ({cfg.Start.X},{cfg.Start.Y}) 越界或不可通行 ⇒ 拒绝加载。");
        }

        if (cfg.Goal.X != grid.Goal.X || cfg.Goal.Y != grid.Goal.Y)
        {
            throw new InvalidDataException(
                $"{ResPath}: `goal` ({cfg.Goal.X},{cfg.Goal.Y}) 与图上的 `G` 格 ({grid.Goal.X},{grid.Goal.Y}) 不一致 ⇒ 拒绝加载。");
        }

        if (new DungeonWalker(grid, (cfg.Start.X, cfg.Start.Y)).PathTo(grid.Goal.X, grid.Goal.Y).Count == 0)
        {
            throw new InvalidDataException(
                $"{ResPath}: **`goal` 从 `start` 不可达**（P30 ③：把「这张图是不是可通关」变成**启动期可判**，不让玩家撞墙）⚠️");
        }

        // ④ `room_anchors` 的 `content_ref` 引用必须存在（P26 同族）✓
        if (cfg.RoomAnchors is not null && knownContentRefs is not null)
        {
            foreach (DungeonGridRoomAnchor a in cfg.RoomAnchors)
            {
                if (a.ContentRef is { Length: > 0 } r && !knownContentRefs.Contains(r))
                {
                    throw new InvalidDataException(
                        $"{ResPath}: 房间 \"{a.Id}\" 的 content_ref \"{r}\" 不在内容表里（P30 ④ / P26）⇒ 拒绝加载。");
                }
            }
        }

        // ⑤ `encounter` 本次**只留字段、值必须为 null** ⇒ 非 null 一律拒绝（禁止加载器/任何人填默认值）⚠️
        if (cfg.Encounter is not null && !cfg.Encounter.IsEmpty)
        {
            throw new InvalidDataException(
                $"{ResPath}: `encounter` 本次**必须为 null**（用户：每格遭遇规则**暂留**）" +
                " —— 填任何值都等于'谁替策划定了一个数'（红線 21 / P29 ① 静默默认）⇒ 拒绝加载。");
        }

        // ⑥ `move.cost_light_per_tile` 量级**未定案** ⇒ 只登记、**不取值**（必须 null）⚠️
        if (cfg.Move?.CostLightPerTile is not null)
        {
            throw new InvalidDataException(
                $"{ResPath}: `move.cost_light_per_tile` **未定案** ⇒ 本次必须为 null（架构 P30 附注④）。" +
                "已定机制是【总消耗守恒】：用现有 `tuning.move.new_area` 折算（见 `WalkLightCost`）✓");
        }

        // ⑦ 视野（若给）：`radius ≥ 0`（数值 `placeholder`；"越暗侦察越差"等设计意图不在本类实现）✓
        //   🔴 `D-3`（2026-09-20）**补齐上界**：半径 ≥ 图的最长边 ⇒ 开局**整张图全亮**
        //      ⇒ 探索层被**静默架空**（玩家永远不用走）⚠️ —— 与 ③ 的"图必须可通关"同一族纪律：
        //      **把设计前提变成加载期可判**，而不是等策划填错了在游戏里才发现 ✓
        if (cfg.Vision is { } vs)
        {
            if (vs.Radius < 0)
            {
                throw new InvalidDataException($"{ResPath}: vision.radius 必须 ≥ 0（实测 {vs.Radius}）。");
            }

            if (vs.Radius >= Math.Max(cfg.Width, cfg.Height))
            {
                throw new InvalidDataException(
                    $"{ResPath}: vision.radius（{vs.Radius}）≥ 图的最长边（{Math.Max(cfg.Width, cfg.Height)}）" +
                    "⇒ **开局整张图全亮**，探索层被架空 ⇒ 拒绝加载（这是设计前提，不是可调数值）⚠️");
            }

            // 🔴 `scout_bonus < 0` ⇒ 侦察加成变**惩罚**；配 `base_pct` 低时会让侦察**永远失败**
            //    —— 又一个"填了但只静默失效"的坑（`D-2` 末档不覆盖满光照是同款）⚠️
            if (vs.ScoutBonus < 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: vision.scout_bonus 必须 ≥ 0（实测 {vs.ScoutBonus}）—— " +
                    "负加成会让侦察**永远失败**（静默失效）⚠️");
            }
        }

        // ⑧ 🔴 `D-1` 回头代价：**必须为正**（= 真正有代价），且**若同时给了新格代价则回头不得更便宜**
        //    —— 这是 DD 方向（wiki ⑦：新区域 −6 / 重走 −1，但**两者都要付**）。
        //    ⚠️ 与旧 `expedition_map.json` 的"回头必须更便宜"是**两套世界**（该文件注释已写明），
        //       此处**不是**笔误、**不是**矛盾：旧层是"按段移动"，本层是"瓷砖网格"。✓
        if (cfg.Backtrack is { } bt)
        {
            if (bt.RevisitLightCost is { } rv)
            {
                if (rv <= 0)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: backtrack.revisit_light_cost 必须 > 0（= 回头**真的**有代价；实测 {rv}）" +
                        " —— D-1 的判据就是「回头必付代价」（红线 21：不许留'填了但没生效'的数）⚠️");
                }

                if (bt.NewTileLightCost is { } nw && rv >= nw)
                {
                    throw new InvalidDataException(
                        $"{ResPath}: backtrack 中 **重走必须比新格便宜**（DD 方向：新 {nw} > 重走 {rv}）" +
                        " —— 否则「回头」反而更贵，玩家会拒绝探索（D-1 纠偏）⚠️");
                }
            }

            if (bt.NewTileLightCost is { } n2 && n2 <= 0)
            {
                throw new InvalidDataException(
                    $"{ResPath}: backtrack.new_tile_light_cost 必须 > 0（实测 {n2}）。");
            }
        }

        return cfg;
    }
}
