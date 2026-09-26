#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""reconcile_building_upgrades.py —— 逐值对账 **参考 vs 我方**的建筑升级消耗 ✓

🔴 **为什么单独做**（承接 A9）：
   A9 已测出「**我方 == 一手 E 盘 99/99；参考 == 一手 0/99**」⚠️
   ⇒ 📌 **即：每一级都不同** ⇒ 但**逐值差异从未列出** ⇒ 本件列出 ✓
   🎖️ **而"全部不同"这句话本身要能验** ⇒ 本件给**逐级逐项的表** ✓

形状（实测）：
   参考 `Data/Upgrades/Building/<id>.upgrades.json` ⇒ `{trees:[{id, is_instanced, tags, **requirements**:[…]}]}`
   我方 `darkest/data/buildings.json` ⇒ `{buildings:[{id, trees:[{id, …, **levels**:[…]}]}]}`
   🔴 **字段名不同**：参考 `requirements` ↔ 我方 `levels` ⚠️
   🔴 **前置字段不同**：参考 `prerequisite_requirements` ↔ 我方 `prerequisites` ⚠️
   ⇒ ✅ **两者形状【同构但不同名】** ⇒ 可逐值对账 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/reconcile_building_upgrades.py
"""

from __future__ import annotations

import io
import json
import os
import re
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Upgrades\Building"
MINE = os.path.join(REPO, "darkest", "data", "buildings.json")
OUT = os.path.join(REPO, "reports", "unity_ref", "building_upgrades_reconcile.json")


def load_ref(path):
    raw = io.open(path, encoding="utf-8-sig", errors="replace").read()
    # 🔴 参考的 `abbey`/`nomad_wagon`/`sanitarium`/`tavern` 有【尾随逗号】⚠️
    fixed = re.sub(r",\s*([\]}])", r"\1", raw)
    return json.loads(fixed)


def main() -> int:
    mine = json.load(open(MINE, encoding="utf-8"))
    mine_by = {b["id"]: b for b in mine["buildings"]}

    rows, summary = [], Counter()
    for fn in sorted(os.listdir(REF)):
        if not fn.endswith(".upgrades.json"):
            continue
        bid = fn.replace(".upgrades.json", "")
        ref = load_ref(os.path.join(REF, fn))
        ours = mine_by.get(bid)
        if ours is None:
            print(f"[bld] 🔴 `{bid}` **我方没有这个建筑** ⇒ 跳过（不静默）")
            summary["建筑缺失"] += 1
            continue
        ref_trees = {t["id"]: t for t in ref["trees"]}
        our_trees = {t["id"]: t for t in ours["trees"]}
        only_ref = sorted(set(ref_trees) - set(our_trees))
        only_our = sorted(set(our_trees) - set(ref_trees))
        if only_ref:
            print(f"[bld]   `{bid}` 🔴 参考独有的树：{only_ref}")
        if only_our:
            print(f"[bld]   `{bid}` 🔴 我方独有的树：{only_our}")

        for tid, rt in ref_trees.items():
            ot = our_trees.get(tid)
            if ot is None:
                continue
            rlv = {x["code"]: x for x in rt.get("requirements") or []}
            olv = {x["code"]: x for x in ot.get("levels") or []}
            if set(rlv) != set(olv):
                rows.append({"building": bid, "tree": tid, "kind": "等级集合不同",
                             "ref": sorted(rlv), "ours": sorted(olv)})
                summary["等级集合不同"] += 1
            for code in sorted(set(rlv) & set(olv)):
                rc = {c["type"]: c["amount"] for c in rlv[code]["currency_cost"]}
                oc = {c["type"]: c["amount"] for c in olv[code]["currency_cost"]}
                if rc != oc:
                    summary["消耗不同"] += 1
                    rows.append({"building": bid, "tree": tid, "code": code,
                                 "kind": "消耗不同", "ref": rc, "ours": oc})
                else:
                    summary["消耗相同"] += 1

    print()
    print(f"[bld] 🎖️ **逐值对账结果**（我方 ↔ 参考 · 建筑升级消耗）")
    print(f"[bld]   消耗【相同】**{summary['消耗相同']}** · 消耗【不同】**{summary['消耗不同']}**")
    print(f"[bld]   等级集合不同 **{summary['等级集合不同']}** · 建筑缺失 **{summary['建筑缺失']}**")
    print()
    if rows:
        print(f"[bld] 🔴 前 14 条差异：")
        for r in rows[:14]:
            print(f"[bld]   `{r['building']}.{r['tree']}` [{r.get('code', '-')}] {r['kind']}")
            print(f"[bld]       参考 {r['ref']}")
            print(f"[bld]       我方 {r['ours']}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "建筑升级消耗逐值对账（参考 vs 我方）。只读不落库 ✓",
                   "summary": dict(summary), "rows": rows},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[bld] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
