#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_ref_trinkets_quirks.py —— A7：饰品 488 / 怪癖 163（**A 线最后一项**）✓

两个源：
   ① `JsonTrinkets.json`（**199,039 B** · 无尾随逗号）—— `rarities`(12) + **`trinkets`(488)**
      每条：`id` · **`buffs[]`** · `hero_class_requirements[]` · `rarity` · `price` · `limit` ·
      `origin_dungeon` ✓
   ② `JsonQuirks.json`（**72,454 B**）—— **`quirks`(163)**
      每条：`id` · `show_explicit_description` · `is_positive` · `is_disease` · `classification` ·
      **`incompatible_quirks[]`** · `curio_tag` · `curio_tag_chance` · `keep_loot` · **`buffs[]`** ✓

🔴 **A7 为什么重要**（`PLAN_adoption §2.1`）：我方 `quirks.json`(170) + `trinkets.json`(196)
   引用 **556 个不同 buff id**，而 `buff_defs.json` **只定义 22 个** ⇒ **引用链 100% 断裂** ⚠️
   A1 已把参考的 **1801 条**原语池落库（`c7494e`）⇒ ✅ **本件量"换源后还剩多少悬空"** ✓

🔴 **只抽不落库**（`darkest/**` 零改动）⇒ 零行为 ✓
   📌 **抽取是纯读** ⇒ **不必等"buff amount 是分数"的舍入口径**（那只挡"落库"）✓

用法：python tools/dsh/extract_ref_trinkets_quirks.py
"""

from __future__ import annotations

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
DATA = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
OUT = os.path.join(REPO, "reports", "unity_ref", "trinkets_quirks_from_ref.json")
POOL = os.path.join(REPO, "darkest", "data", "buff_primitives.json")
TRIM = re.compile(r",\s*([\]}])")


def load(name):
    raw = open(os.path.join(DATA, name), encoding="utf-8-sig", errors="replace").read()
    try:
        return json.loads(raw), len(raw), False
    except json.JSONDecodeError:
        return json.loads(TRIM.sub(r"\1", raw)), len(raw), True


def main() -> int:
    tk, tkb, tk_tr = load("JsonTrinkets.json")
    qk, qkb, qk_tr = load("JsonQuirks.json")
    trinkets = tk["trinkets"]
    quirks = qk["quirks"]
    print(f"[a7] `JsonTrinkets.json`（{tkb} B）⇒ rarities **{len(tk['rarities'])}** · "
          f"trinkets **{len(trinkets)}** ✓")
    print(f"[a7] `JsonQuirks.json`（{qkb} B）⇒ quirks **{len(quirks)}** ✓")

    # ---- 完整性自检 ----
    print()
    print("[a7] 完整性自检：")
    print(f"[a7]   trinket id 有值 **{sum(1 for t in trinkets if t.get('id'))}/{len(trinkets)}** ✓")
    print(f"[a7]   quirk id 有值 **{sum(1 for q in quirks if q.get('id'))}/{len(quirks)}** ✓")
    tk_nb = sum(1 for t in trinkets if not t.get("buffs"))
    qk_nb = sum(1 for q in quirks if not q.get("buffs"))
    print(f"[a7]   🔴 **没有 `buffs` 的**：trinket **{tk_nb}** · quirk **{qk_nb}** ⚠️")

    # ---- 字段全集 ----
    print(f"[a7] trinket 字段：{sorted({k for t in trinkets for k in t})}")
    print(f"[a7] quirk 字段：{sorted({k for q in quirks for k in q})}")
    print(f"[a7] rarity 分布：{dict(Counter(t.get('rarity') for t in trinkets))}")
    print(f"[a7] quirk classification：{dict(Counter(q.get('classification') for q in quirks))}")
    print(f"[a7] quirk is_positive：{dict(Counter(q.get('is_positive') for q in quirks))}")
    print(f"[a7] quirk is_disease：{dict(Counter(q.get('is_disease') for q in quirks))}")

    # ---- 🎖️ 关键：buff 引用 vs A1 落库的 1801 池 ----
    pool = json.load(open(POOL, encoding="utf-8"))
    pids = {p["id"] for p in pool["primitives"]}
    print()
    print(f"[a7] 🎖️ A1 落库的原语池：**{len(pids)}** 条 ✓")
    for label, refs in (("trinket", [b for t in trinkets for b in (t.get("buffs") or [])]),
                        ("quirk", [b for q in quirks for b in (q.get("buffs") or [])])):
        uniq = sorted(set(refs))
        miss = [b for b in uniq if b not in pids]
        print(f"[a7]   **{label}**：引用 {len(refs)} 次 · 去重 **{len(uniq)}** ⇒ "
              f"**未在池里的 {len(miss)}**")
        if miss:
            print(f"[a7]      🔴 样例：{miss[:12]}")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "source": DATA,
            "note": "A7 抽取：JsonTrinkets.json（488）+ JsonQuirks.json（163）。"
                    "🔴 本件【只抽不落库】—— 「buff amount 是分数」的舍入口径只挡落库，不挡抽取 ✓",
            "trinket_count": len(trinkets),
            "quirk_count": len(quirks),
            "rarities": tk["rarities"],
            "trinkets": trinkets,
            "quirks": quirks,
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[a7] 写出 {os.path.relpath(OUT, REPO)}（{os.path.getsize(OUT)} bytes）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
