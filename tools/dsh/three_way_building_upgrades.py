#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""three_way_building_upgrades.py —— **三方对账**：一手 E 盘 vs 参考 vs 我方 ✓

🔴 **为什么做**：`50_*.md` 只比了**两方**（参考 ↔ 我方），
   得「**99/99 不同，差异集中在 `crest`**」⇒ 但**没回答【谁改了】** ⚠️
   ⇒ 🎖️ 而 A9 记过「我方 == 一手 99/99；参考 == 一手 0/99」，**但那是旧读数** ✓
   📌 本件用**同一把尺子**（`currency_cost` 逐值）重做三方 ✓

路径（实测）：
   一手：`E:\\SteamLibrary\\steamapps\\common\\DarkestDungeon\\upgrades\\**building**\\`（**单数**）✓
   参考：`Assets\\Resources\\Data\\Upgrades\\**Building**\\` ✓
   我方：`darkest/data/buildings.json` ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/three_way_building_upgrades.py
"""

from __future__ import annotations

import io
import json
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EDRIVE = r"E:\SteamLibrary\steamapps\common\DarkestDungeon\upgrades\building"
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Upgrades\Building"
MINE = os.path.join(REPO, "darkest", "data", "buildings.json")
OUT = os.path.join(REPO, "reports", "unity_ref", "building_upgrades_three_way.json")


def load_t(p):
    return json.loads(re.sub(r",\s*([\]}])", r"\1",
                             io.open(p, encoding="utf-8-sig", errors="replace").read()))


def costs(trees, key):
    out = {}
    for t in trees:
        for x in t.get(key) or []:
            out[(t["id"], x["code"])] = {c["type"]: c["amount"]
                                         for c in (x.get("currency_cost") or [])}
    return out


def main() -> int:
    if not os.path.isdir(EDRIVE):
        print("[3w] 🔴 一手不可访问 ⇒ 退出（不静默）")
        return 2
    mine = {b["id"]: b for b in json.load(open(MINE, encoding="utf-8"))["buildings"]}
    files = sorted(f for f in os.listdir(EDRIVE) if f.endswith(".upgrades.json"))
    print(f"[3w] 一手 **{len(files)}** 个建筑文件 ✓")

    sem = ser = smr = 0
    dem = 0
    rows = []
    for fn in files:
        bid = fn.replace(".upgrades.json", "")
        e = costs(load_t(os.path.join(EDRIVE, fn))["trees"], "requirements")
        rp = os.path.join(REF, fn)
        r = costs(load_t(rp)["trees"], "requirements") if os.path.isfile(rp) else {}
        m = costs(mine[bid]["trees"], "levels") if bid in mine else {}

        for k in sorted(set(e) | set(m)):
            if e.get(k) == m.get(k):
                sem += 1
            else:
                dem += 1
                rows.append({"building": bid, "key": list(k),
                             "e": e.get(k), "mine": m.get(k), "ref": r.get(k)})
        for k in sorted(set(e) | set(r)):
            if e.get(k) == r.get(k):
                ser += 1
        for k in sorted(set(r) | set(m)):
            if r.get(k) == m.get(k):
                smr += 1

    print()
    print(f"[3w] 🎖️🎖️ **三方对账（建筑升级消耗）**")
    print(f"[3w]   一手 == 我方：**{sem}**")
    print(f"[3w]   一手 == 参考：**{ser}**")
    print(f"[3w]   参考 == 我方：**{smr}**")
    print(f"[3w]   一手 ↔ 我方【不同】：**{dem}**")
    print()
    if rows:
        print(f"[3w] 前 8 条差异：")
        for x in rows[:8]:
            print(f"[3w]   {x['building']}.{x['key'][0]} [{x['key'][1]}]")
            print(f"[3w]       一手 {x['e']}")
            print(f"[3w]       我方 {x['mine']}")
            print(f"[3w]       参考 {x['ref']}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "建筑升级消耗三方对账。只读不落库 ✓",
                   "same_e_mine": sem, "same_e_ref": ser, "same_mine_ref": smr,
                   "diff_e_mine": dem, "rows": rows}, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[3w] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
