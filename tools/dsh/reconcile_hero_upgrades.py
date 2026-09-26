#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""reconcile_hero_upgrades.py —— 逐值对账 **参考 vs 我方**的英雄升级树消耗 ✓

🔴 **为什么还要做**（承接 A3 的局限）：
   A3 只比了 **39 个字段的武器/防具档**（`crit_pct` 那 13 个非整数）⚠️
   ⇒ 📌 **而【升级树的消耗】从未逐值比过** ✓
   🎖️ 形状与建筑那件同法（建筑已查出 **99/99 不同且集中在 `crest`**）⇒ 本件同问 ✓

形状（实测）：
   参考 `Data/Upgrades/Heroes/<hero>.upgrades.json`（**16 个**）
      ⇒ `{trees:[{id, is_instanced, tags, **requirements**:[{code, currency_cost[], prerequisite_requirements[]}]}]}`
   我方 `darkest/data/hero_upgrades.json`
      ⇒ `{heroes: {<hero>: …}}`
   🔴 **等级 code 从 `"0"` 开始**（`first_level_not_upgrade` 标签）⚠️ —— 与建筑从 `"a"` 不同 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/reconcile_hero_upgrades.py
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
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Upgrades\Heroes"
MINE = os.path.join(REPO, "darkest", "data", "hero_upgrades.json")
OUT = os.path.join(REPO, "reports", "unity_ref", "hero_upgrades_reconcile.json")


def load_ref(path):
    raw = io.open(path, encoding="utf-8-sig", errors="replace").read()
    return json.loads(re.sub(r",\s*([\]}])", r"\1", raw))


def main() -> int:
    mine = json.load(open(MINE, encoding="utf-8"))["heroes"]
    print(f"[hero] 我方 heroes：**{len(mine)}** 个 ⇒ {sorted(mine)[:6]} …")

    # 🔴 先探我方的形状（不假设）
    h0 = sorted(mine)[0]
    print(f"[hero] 我方 `{h0}` 的键：{list(mine[h0])}")
    trees = mine[h0].get("trees") or []
    if trees:
        print(f"[hero] 我方 tree 键：{list(trees[0])}")
        lv = trees[0].get("levels") or trees[0].get("requirements") or []
        if lv:
            print(f"[hero] 我方 等级键：{list(lv[0])}")
            print(f"[hero] 样例：{json.dumps(lv[0], ensure_ascii=False)[:220]}")
    print()

    rows, summ = [], Counter()
    for fn in sorted(os.listdir(REF)):
        if not fn.endswith(".upgrades.json"):
            continue
        hero = fn.replace(".upgrades.json", "")
        ref = load_ref(os.path.join(REF, fn))
        ours = mine.get(hero)
        if ours is None:
            print(f"[hero] 🔴 `{hero}` 我方没有 ⇒ 跳过（不静默）")
            summ["英雄缺失"] += 1
            continue
        our_trees = {t["id"]: t for t in (ours.get("trees") or [])}
        for rt in ref["trees"]:
            tid = rt["id"]
            ot = our_trees.get(tid)
            if ot is None:
                summ["树缺失"] += 1
                rows.append({"hero": hero, "tree": tid, "kind": "我方缺这棵树"})
                continue
            olv_list = ot.get("levels") or ot.get("requirements") or []
            rlv = {x["code"]: x for x in rt.get("requirements") or []}
            olv = {x["code"]: x for x in olv_list}
            if set(rlv) != set(olv):
                summ["等级集合不同"] += 1
                rows.append({"hero": hero, "tree": tid, "kind": "等级集合不同",
                             "ref": sorted(rlv), "ours": sorted(olv)})
            for code in sorted(set(rlv) & set(olv)):
                rc = {c["type"]: c["amount"] for c in rlv[code].get("currency_cost") or []}
                oc = {c["type"]: c["amount"] for c in olv[code].get("currency_cost") or []}
                if rc == oc:
                    summ["消耗相同"] += 1
                else:
                    summ["消耗不同"] += 1
                    rows.append({"hero": hero, "tree": tid, "code": code,
                                 "kind": "消耗不同", "ref": rc, "ours": oc})

    print(f"[hero] 🎖️ **逐值对账（英雄升级消耗）**")
    print(f"[hero]   消耗【相同】{summ['消耗相同']} · 【不同】{summ['消耗不同']}")
    print(f"[hero]   等级集合不同 {summ['等级集合不同']} · 树缺失 {summ['树缺失']} · "
          f"英雄缺失 {summ['英雄缺失']}")
    print()
    diffkeys = Counter()
    for r in rows:
        if r["kind"] != "消耗不同":
            continue
        for k in set(r["ref"]) | set(r["ours"]):
            if r["ref"].get(k) != r["ours"].get(k):
                diffkeys[k] += 1
    print(f"[hero] 🔴 差异货币分布：{dict(diffkeys)}")
    print()
    print(f"[hero] 前 12 条差异：")
    for r in rows[:12]:
        if r["kind"] == "消耗不同":
            print(f"[hero]   {r['hero']}.{r['tree']} [{r['code']}]")
            print(f"[hero]       参考 {r['ref']}")
            print(f"[hero]       我方 {r['ours']}")
        else:
            print(f"[hero]   {r['hero']}.{r['tree']} ⇒ {r['kind']} {r.get('ref','')} {r.get('ours','')}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "英雄升级消耗逐值对账（参考 vs 我方）。只读不落库 ✓",
                   "summary": dict(summ), "diff_currency": dict(diffkeys), "rows": rows},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[hero] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
