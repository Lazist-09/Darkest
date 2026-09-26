#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_buildings.py —— A9：抽取参考项目的**建筑 + 升级树**（8 + 8 个文件）✓

源（两处，缺一不可）：
   ① `…/Data/Buildings/*.building.json`（8 个）—— **活动（activities）** 的结构：
      `side_effects`（副作用概率表）· `quirk_library_names` · `caretaker_friendly` ·
      `cost_upgrades` / `slot_upgrades` / `stress_upgrades` / `affliction_cure_upgrades` …
      🆕 以及 **3 个建筑级 gate 字段**：`on_start_town_visit_priority` ·
         `number_of_quests_finished` · `highest_dungeon_level` ✓
   ② `…/Data/Upgrades/Building/*.upgrades.json`（8 个）—— **升级树**：
      `trees[] { id, is_instanced, tags, requirements[] { code, currency_cost[], prerequisite_requirements[] } }`
      ⇒ ✅ **形状与我方 `buildings.json` 的 `trees[].levels[]` 几乎同构** ✓
         （我方 `levels[] { code, currency_cost, prerequisites }` ↔ 参考 `requirements[] { code, currency_cost, prerequisite_requirements }`）✓

🔴 **4 个 `.building.json` 有尾随逗号**（`abbey`/`nomad_wagon`/`sanitarium`/`tavern`）⇒ 先清洗 ✓
🔴 **只抽不落库**（`darkest/**` 零改动）⇒ 零行为 ✓

用法：python tools/dsh/extract_ref_buildings.py
"""

from __future__ import annotations

import json
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DATA = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
OUT = os.path.join(REPO, "reports", "unity_ref", "buildings_from_ref.json")
TRIM = re.compile(r",\s*([\]}])")

# 3 个**建筑级 gate 字段** —— 参考解析了但实测【从不被读】（见 PLAN_adoption §10.1 ⑥）
GATE_KEYS = ("on_start_town_visit_priority", "number_of_quests_finished", "highest_dungeon_level")


def load(path: str):
    raw = open(path, encoding="utf-8-sig", errors="replace").read()
    try:
        return json.loads(raw), False
    except json.JSONDecodeError:
        return json.loads(TRIM.sub(r"\1", raw)), True


def strip_meta(names):
    return sorted(n for n in names if not n.endswith(".meta"))


def main() -> int:
    bdir = os.path.join(DATA, "Buildings")
    udir = os.path.join(DATA, "Upgrades", "Building")

    buildings = []
    trailing = []
    for name in strip_meta(os.listdir(bdir)):
        d, trimmed = load(os.path.join(bdir, name))
        if trimmed:
            trailing.append(name)
        bid = name.replace(".building.json", "")
        gates = {k: d.get(k) for k in GATE_KEYS}
        acts = {k: v for k, v in d.items() if k not in GATE_KEYS}
        # 把每个活动拆成：side_effects（摘要）+ 升级数组（原样）
        act_out = {}
        for aname, av in acts.items():
            if isinstance(av, dict):
                se = av.get("side_effects") or {}
                act_out[aname] = {
                    "kind": "activity",
                    "keys": sorted(av.keys()),
                    "side_effects_chance": se.get("chance"),
                    "side_effect_types": [r.get("type") for r in (se.get("results") or [])],
                    "side_effects": se,
                    "quirk_library_names": av.get("quirk_library_names"),
                    "caretaker_friendly": av.get("caretaker_friendly"),
                    "upgrade_arrays": {k: v for k, v in av.items()
                                       if isinstance(v, list)},
                }
            else:
                act_out[aname] = {"kind": "upgrade_array", "values": av}
        buildings.append({
            "id": bid,
            "gate": gates,
            "activities": act_out,
            "file": f"Buildings/{name}",
        })

    # ---- 升级树（来自 Upgrades/Building/）----
    trees = {}
    for name in strip_meta(os.listdir(udir)):
        d, _ = load(os.path.join(udir, name))
        bid = name.replace(".upgrades.json", "")
        ts = d.get("trees") or []
        trees[bid] = {
            "file": f"Upgrades/Building/{name}",
            "tree_count": len(ts),
            "level_count": sum(len(t.get("requirements") or []) for t in ts),
            "trees": ts,
        }

    print(f"[a9] `Buildings/` ⇒ **{len(buildings)} 个建筑** ✓"
          f"（尾随逗号 **{len(trailing)}** 个：{trailing}）")
    for b in buildings:
        na = len(b["activities"])
        nse = sum(1 for a in b["activities"].values()
                  if a["kind"] == "activity" and a.get("side_effects_chance") is not None)
        print(f"[a9]   {b['id']:18s} 活动/升级组 {na:2d} · 带 side_effects 的 {nse} · "
              f"gate={b['gate']}")
    print()
    print(f"[a9] `Upgrades/Building/` ⇒ **{len(trees)} 个文件的树** ✓")
    tot_t = tot_l = 0
    for bid, t in sorted(trees.items()):
        tot_t += t["tree_count"]
        tot_l += t["level_count"]
        print(f"[a9]   {bid:18s} 树 {t['tree_count']:2d} · 等级 {t['level_count']:3d}")
    print(f"[a9]   ⇒ 合计 **{tot_t} 树 / {tot_l} 等级** ✓")

    # ---- 完整性自检 ----
    print()
    print("[a9] 完整性自检（不许字段大面积为空）：")
    for bid, t in sorted(trees.items()):
        bad = [x["id"] for x in t["trees"] if not x.get("requirements")]
        if bad:
            print(f"[a9]   🔴 {bid}: {len(bad)} 棵树没有 requirements ⇒ {bad}")
    print(f"[a9]   ✅ 上面没打印 ⇒ 无空树 ✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": DATA,
            "note": "A9 抽取：参考项目 Buildings/*.building.json（8）+ Upgrades/Building/*.upgrades.json（8）。"
                    "🔴 4 个 building.json 有尾随逗号 ⇒ 已清洗。本件【只抽不落库】✓",
            "building_count": len(buildings),
            "upgrade_file_count": len(trees),
            "trailing_comma_files": trailing,
            "buildings": buildings,
            "upgrade_trees": trees,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[a9] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
