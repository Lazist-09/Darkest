#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""crosscheck_trinket_refs.py —— 核 **A8 的 `trinket` 引用** × **A7 的 488 饰品** ✓

🔴 **为什么做**（同 `76_*.md` 的物品那件，第三类）：
   `76_*.md` 做了 item（**22/22 全中**）· `74`/`75_*.md` 做了奇物（**交集 1**）✓
   ⇒ 📌 A8 的 loot 表里还有 **51 个 `trinket` 引用** ⇒ ✅ 本件补上 ✓
   ⇒ 🎖️ **三类引用齐了，A8 的 loot 表就完整对账了** ✓

结构（**实测，不猜**）：
   · A7 `trinkets` 是 **list[488]**，首条：
     `{"id": "ancestors_coat", "buffs": [...], "rarity": "ancestral", "price": 50000, "limit": 1, …}` ✓
     ⇒ 🎖️ **`id` 全是名字**（无数字 id）✓
   · A8 的 loot 条目：`{"type": "trinket", "chances": N, "data": {...}}` ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/crosscheck_trinket_refs.py
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
OUT = os.path.join(R, "trinket_refs_crosscheck.json")


def main() -> int:
    a7 = json.load(open(os.path.join(R, "trinkets_quirks_from_ref.json"), encoding="utf-8"))
    tr = a7["trinkets"]
    ids = {x["id"] for x in tr if x.get("id")}
    print(f"[tx] A7：**{a7['trinket_count']}** 饰品（有 id 的 **{len(ids)}**）✓")
    rar = Counter(x.get("rarity") for x in tr)
    print(f"[tx]   按 rarity：{dict(rar.most_common())}")
    print()

    a8 = json.load(open(os.path.join(R, "quests_loot_narration_from_ref.json"),
                        encoding="utf-8"))
    refs = Counter()
    for t in a8["loot"]["loot_tables"]:
        for e in t.get("entries") or []:
            if e.get("type") != "trinket":
                continue
            d = e.get("data") or {}
            for kk in ("trinket", "id", "name"):
                if d.get(kk):
                    refs[d[kk]] += 1
            # 🔴 也可能用 rarity 而不是名字
            if d.get("rarity"):
                refs[f"<rarity:{d['rarity']}>"] += 1
    print(f"[tx] A8 的 `trinket` 引用（去重）：**{len(refs)}** ✓")
    for k in sorted(refs):
        print(f"[tx]     `{k}`  ×{refs[k]}")
    print()

    A, B = set(refs), ids
    real_a = {x for x in A if not x.startswith("<rarity:")}
    print(f"[tx] 🎖️ **对照**（排除 rarity 形式）")
    print(f"[tx]   A8 名字引用 ∩ A7 id：**{len(real_a & B)}** / {len(real_a)}")
    print(f"[tx]   🔴 只在 A8：**{len(real_a - B)}**  {sorted(real_a - B)}")
    print(f"[tx]   🔴 只在 A7：（{len(B - real_a)} 个 —— 绝大多数饰品不在 loot 表里，正常）")
    print()
    if refs and any(k.startswith("<rarity:") for k in refs):
        print(f"[tx] 🎖️ **A8 用了 `rarity` 形式**："
              f"{sorted(k for k in refs if k.startswith('<rarity:'))} ✓")
        print(f"[tx]   ⇒ 📌 即：**饰品掉落是【按稀有度】而不是按名字** ✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "A8 trinket 引用 × A7 488 饰品。只读不落库 ✓",
                   "a7_ids": sorted(ids), "a7_rarities": dict(rar),
                   "a8_refs": dict(refs),
                   "intersection": sorted(real_a & B),
                   "only_a8": sorted(real_a - B)},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[tx] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
