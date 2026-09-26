#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""crosscheck_curio_names.py —— 核 **A8 的 `curio_name`** 与 **A12 的 60 奇物** ✓

🔴 **为什么做**（承接 A8 记的"`curio_name` 11 个引用 ⇒ 10 个未匹配"）：
   A8 已判：那些是**任务专用交互道具**（只在 `JsonQuests` + 本地化里）⇒
   📌 **与 A12 的 60 个通用奇物是【不同命名空间】** ✓
   ⇒ 🎖️ **而本件用【实测】把这条判定坐实**：
      · 列出 A8 的 `curio_name` 全表
      · 与 A12 的 60 个求交/求差
      · 再核它们**各自被谁引用** ✓

🎖️ **判据**：**"这两个集合是【同一套】还是【两个命名空间】？"** ——
   看**交集**与**各自的引用方** ✓（与 A8 那次同法，但这次把两边都量全）

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/crosscheck_curio_names.py
"""

from __future__ import annotations

import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
R = os.path.join(REPO, "reports", "unity_ref")
OUT = os.path.join(R, "curio_names_crosscheck.json")
MINE = os.path.join(REPO, "darkest", "data", "curios.json")


def main() -> int:
    q = json.load(open(os.path.join(R, "quests_loot_narration_from_ref.json"),
                       encoding="utf-8"))
    goals = q["quests"]["goals"]
    print(f"[cx] A8 的 goals：**{len(goals)}** ✓")

    # ① A8 侧：`curio_name` 引用
    a8 = {}
    for g in goals:
        d = g.get("data") or {}
        cn = d.get("curio_name")
        if cn:
            a8.setdefault(cn, []).append(g["id"])
    print(f"[cx] A8 的 `curio_name`（去重）：**{len(a8)}** ✓")
    for k in sorted(a8):
        print(f"[cx]   `{k}` ← {a8[k]}")
    print()

    # ② A12 侧：参考的通用奇物
    #    🔴 **结构是嵌套的**：`{"curios": {"header": [...], "items": [...60...], "file": "…"}}` ⚠️
    #       我第一版只查顶层 list ⇒ **得 0** ⇒ ✅ 按实测结构调整 ✓
    #    🎖️ 判据：**"我按猜的结构取数 —— 得 0 ⇒ 先【打印结构】再取"** ✓
    c = json.load(open(os.path.join(R, "curios_from_ref.json"), encoding="utf-8"))
    items = ((c.get("curios") or {}).get("items")) or []
    ref_curios = {x["id"] for x in items if isinstance(x, dict) and x.get("id")}
    print(f"[cx] A12 的通用奇物：**{len(ref_curios)}** ✓（`curios.items`）")
    print()

    # ③ 我方 curios.json
    mine = json.load(open(MINE, encoding="utf-8"))
    mine_ids = {x["id"] for x in mine["curios"]}
    print(f"[cx] 我方 `curios.json`：**{len(mine_ids)}** ✓")
    print()

    A, Rc = set(a8), ref_curios
    print(f"[cx] 🎖️ **三方对照**")
    print(f"[cx]   A8 `curio_name` ∩ A12 通用奇物：**{len(A & Rc)}**  {sorted(A & Rc)}")
    print(f"[cx]   🔴 只在 A8：**{len(A - Rc)}**")
    print(f"[cx]   🔴 只在 A12：（{len(Rc - A)} 个 —— 太多不列全）")
    print(f"[cx]   A8 ∩ 我方：**{len(A & mine_ids)}**  {sorted(A & mine_ids)}")
    print(f"[cx]   A12 ∩ 我方：**{len(Rc & mine_ids)}**")
    print()

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "A8 curio_name vs A12 通用奇物。只读不落库 ✓",
                   "a8_curio_name": a8, "a12_count": len(Rc),
                   "intersection": sorted(A & Rc),
                   "only_a8": sorted(A - Rc),
                   "a8_and_mine": sorted(A & mine_ids),
                   "a12_and_mine": sorted(Rc & mine_ids)},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[cx] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
