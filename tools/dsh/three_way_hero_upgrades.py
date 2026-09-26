#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""three_way_hero_upgrades.py —— **三方对账**：一手 E 盘 vs 参考 vs 我方 ✓

🔴 **为什么做**（承接 `51_*.md §7` 的"我方 vs 一手未比"）：
   `51_*.md` 量出：**我方 ↔ 参考** 645 处里 **300 处不同（全是 `gold`）** ✓
   而我方 `_source` 写的是**一手 E 盘**
      `E:\\SteamLibrary\\steamapps\\common\\DarkestDungeon\\upgrades\\heroes` ✓
   ⇒ 🎖️ **而一手【可访问】** ⇒ 可以问 A9 那次问过的问题：
      **"到底是谁改了？我方==一手？还是参考==一手？"** ✓
   📌 A9（建筑）的结果是：**我方 == 一手 99/99；参考 == 一手 0/99** ⚠️
       ⇒ 本件问：**英雄升级是不是同样？** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/three_way_hero_upgrades.py
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
EDRIVE = r"E:\SteamLibrary\steamapps\common\DarkestDungeon\upgrades\heroes"
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Upgrades\Heroes"
MINE = os.path.join(REPO, "darkest", "data", "hero_upgrades.json")
OUT = os.path.join(REPO, "reports", "unity_ref", "hero_upgrades_three_way.json")


def load_json_tolerant(p):
    raw = io.open(p, encoding="utf-8-sig", errors="replace").read()
    return json.loads(re.sub(r",\s*([\]}])", r"\1", raw))


def costs_of(trees, key_levels):
    """⇒ {(tree_id, code): {currency: amount}}"""
    out = {}
    for t in trees:
        tid = t.get("id")
        lv = t.get(key_levels) or []
        for x in lv:
            cc = {c["type"]: c["amount"] for c in (x.get("currency_cost") or [])}
            out[(tid, x["code"])] = cc
    return out


def main() -> int:
    print(f"[3w] 一手 E 盘：{os.path.isdir(EDRIVE)} ⇒ {EDRIVE}")
    if not os.path.isdir(EDRIVE):
        print("[3w] 🔴 一手不可访问 ⇒ 本件无法做（不静默）")
        return 2

    mine = json.load(open(MINE, encoding="utf-8"))["heroes"]
    files = sorted(f for f in os.listdir(EDRIVE) if f.endswith(".upgrades.json"))
    print(f"[3w] 一手 **{len(files)}** 个英雄文件 ✓")

    same_e_m = same_e_r = same_m_r = 0
    diff_all = 0
    rows = []
    for fn in files:
        hero = fn.replace(".upgrades.json", "")
        e = load_json_tolerant(os.path.join(EDRIVE, fn))
        # 参考的文件名可能不同（musketeer 只在参考里有）
        rp = os.path.join(REF, fn)
        r = load_json_tolerant(rp) if os.path.isfile(rp) else None
        mh = mine.get(hero)
        ec = costs_of(e["trees"], "requirements")
        mc = costs_of(mh["trees"], "levels") if mh else {}
        rc = costs_of(r["trees"], "requirements") if r else {}
        for k in sorted(set(ec) | set(mc)):
            a, b = ec.get(k), mc.get(k)
            if a == b:
                same_e_m += 1
            else:
                diff_all += 1
                rows.append({"hero": hero, "key": list(k), "e": a, "mine": b,
                             "ref": rc.get(k)})
        for k in sorted(set(ec) | set(rc)):
            if ec.get(k) == rc.get(k):
                same_e_r += 1
        for k in sorted(set(rc) | set(mc)):
            if rc.get(k) == mc.get(k):
                same_m_r += 1

    print()
    print(f"[3w] 🎖️🎖️ **三方对账（英雄升级消耗）**")
    print(f"[3w]   一手 == 我方：**{same_e_m}** ✓")
    print(f"[3w]   一手 == 参考：**{same_e_r}**")
    print(f"[3w]   参考 == 我方：**{same_m_r}**")
    print(f"[3w]   一手 ↔ 我方【不同】：**{diff_all}**")
    print()
    print(f"[3w] 前 10 条「一手 ≠ 我方」的（带参考列）：")
    for r in rows[:10]:
        print(f"[3w]   {r['hero']}.{r['key'][0]} [{r['key'][1]}]")
        print(f"[3w]       一手 {r['e']}  ← 我方（我方抄的是一手？）")
        print(f"[3w]       我方 {r['mine']}")
        print(f"[3w]       参考 {r['ref']}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "英雄升级消耗三方对账（一手 E 盘 / 参考 / 我方）。只读不落库 ✓",
                   "same_e_mine": same_e_m, "same_e_ref": same_e_r, "same_mine_ref": same_m_r,
                   "diff_e_mine": diff_all, "rows": rows},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[3w] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
