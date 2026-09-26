#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""crosscheck_obstacles_traps.py —— 核参考的 **5 障碍 / 4 陷阱** 与我方 `trap_defs.json`(4) ✓

🔴 **为什么做**：
   A12 抽了参考的 `Obstacles.json`(5) 与 `Traps.json`(4) ✓
   而**我方 `trap_defs.json` 只有 4 条** ⇒ 📌 **两边规模相近** ⇒ ✅ 可逐条对账 ✓
   ＋ 🎖️ 参考的 `Obstacles` 有 `health`/`torchlight`/`ancestor_talk` 字段
      ⇒ ⚠️ **我方有没有这三样是【另一回事】** ⇒ 本件一并核 ✓

🎖️ **判据**：**"两边的规模相近时，最容易直接逐条比 —— 而【字段】往往对不上"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/crosscheck_obstacles_traps.py
"""

from __future__ import annotations

import json
import os
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
R = os.path.join(REPO, "reports", "unity_ref")
OUT = os.path.join(R, "obstacles_traps_crosscheck.json")


def main() -> int:
    a12 = json.load(open(os.path.join(R, "curios_from_ref.json"), encoding="utf-8"))
    obs = a12["obstacles"]["items"]
    trp = a12["traps"]["items"]
    print(f"[ot] 参考：**{len(obs)}** 障碍 · **{len(trp)}** 陷阱 ✓")
    print()

    print("[ot] 🎖️ **障碍**（逐个 + 字段）：")
    ok = Counter()
    for o in obs:
        print(f"[ot]   `{o.get('name')}`")
        for k, v in o.items():
            if k != "name":
                print(f"[ot]       `{k}` = {json.dumps(v, ensure_ascii=False)[:90]}")
        ok.update(o.keys())
    print(f"[ot]   字段全集：{sorted(ok)}")
    print()

    print("[ot] 🎖️ **陷阱**（逐个 + 字段）：")
    tk = Counter()
    for t in trp:
        print(f"[ot]   `{t.get('name')}`  {json.dumps({k: v for k, v in t.items() if k != 'name'}, ensure_ascii=False)[:160]}")
        tk.update(t.keys())
    print(f"[ot]   字段全集：{sorted(tk)}")
    print()

    # 我方
    mine = json.load(open(os.path.join(REPO, "darkest", "data", "trap_defs.json"),
                          encoding="utf-8"))
    print(f"[ot] 🔴 我方 `trap_defs.json` 顶层键：{list(mine)}")
    traps = mine.get("traps") or []
    print(f"[ot]   条目：**{len(traps)}** ✓")
    mk = Counter()
    for t in traps:
        print(f"[ot]   `{t.get('id')}`  {json.dumps({k: v for k, v in t.items() if k != 'id'}, ensure_ascii=False)[:150]}")
        mk.update(t.keys())
    print(f"[ot]   字段全集：{sorted(mk)}")
    print()

    A = {o.get("name") for o in obs}
    B = {t.get("name") for t in trp}
    M = {t.get("id") for t in traps}
    print(f"[ot] 🎖️ **名字对照**")
    print(f"[ot]   参考障碍 ∩ 我方：**{len(A & M)}**  {sorted(A & M)}")
    print(f"[ot]   参考陷阱 ∩ 我方：**{len(B & M)}**  {sorted(B & M)}")
    print(f"[ot]   🔴 参考障碍：{sorted(A)}")
    print(f"[ot]   🔴 参考陷阱：{sorted(B)}")
    print(f"[ot]   🔴 我方：{sorted(M)}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "参考 5 障碍/4 陷阱 × 我方 trap_defs(4)。只读不落库 ✓",
                   "ref_obstacles": obs, "ref_traps": trp, "mine": traps,
                   "obs_intersect": sorted(A & M), "trap_intersect": sorted(B & M)},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[ot] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
