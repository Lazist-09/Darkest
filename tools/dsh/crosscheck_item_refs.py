#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""crosscheck_item_refs.py —— 核 **A8 的 `item` 引用** × **A10 的 57 个物品** ✓

🔴 **为什么做**（同 `74`/`75_*.md` 的奇物那件，换个类别）：
   A8 记过它做过"**按 `entry.type` 分别查引用**"⇒ 表引用 8/9 · **item 引用 22/22** ✓
   ⇒ 📌 而**那个 22/22 是"参考内部"的闭合** ✓
   ⇒ 🎖️ **本件问的是另一件事**：**A8 的那些 item 名，与 A10 抽的 57 个物品【对得上吗】？** ✓

🎖️ **判据**：**"两个采集项之间，名字对得上吗？"** ——
   对不上 ⇒ 要么**不同命名空间**，要么**其中一个漏了** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/crosscheck_item_refs.py
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
R = os.path.join(REPO, "reports", "unity_ref")
OUT = os.path.join(R, "item_refs_crosscheck.json")


def main() -> int:
    a10 = json.load(open(os.path.join(R, "items_and_provisions_from_ref.json"),
                         encoding="utf-8"))
    items = a10["items"]
    # 🔴 **口径（读原文后定）**：
    #    · `id` 有【两类】：**名字**（`portrait`/`ruby`/`shovel`…）与**数字**
    #      （`journal_page` 的 `"0"`~`"21"` —— **那是【页码】，不是名字**）⚠️
    #    · 还有 **2 条 `id` 为空**（`provision` 与 `gold` —— 它们是【类别】不是具体物品）✓
    #    ⇒ 🎖️ **判据**：**"这个 `id` 是【名字】还是【序号】？"** ⇒ 混在一起统计会失真 ✓
    named = {x["id"] for x in items if x.get("id") and not str(x["id"]).isdigit()}
    numeric = {x["id"] for x in items if x.get("id") and str(x["id"]).isdigit()}
    empty = [x for x in items if not x.get("id")]
    ids = named
    types = Counter(x.get("type") for x in items)
    print(f"[ix] A10：**{a10['item_count']}** 个物品")
    print(f"[ix]   🎖️ **有【名字】的：{len(named)}** · 🔴 **数字 id（页码）：{len(numeric)}** · "
          f"🔴 **id 为空：{len(empty)}**")
    print(f"[ix]   按 `type`：{dict(types)}")
    print(f"[ix]   🔴 id 为空的类型：{[x.get('type') for x in empty]}")
    print()

    # A8 的 loot 表里的 item 引用
    a8 = json.load(open(os.path.join(R, "quests_loot_narration_from_ref.json"),
                        encoding="utf-8"))
    tables = a8["loot"]["loot_tables"]
    print(f"[ix] A8 的 loot 表：**{len(tables)}** ✓")
    refs = Counter()
    kinds = Counter()
    for t in tables:
        for e in t.get("entries") or []:
            kinds[e.get("type")] += 1
            # 🔴 **item 类引用在 `data` 里**（实测：`{"type":"table","data":{"table":"C"}}`）
            #    而 `journal_page` 用的是 `min_page_index`/`max_page_index`（**不是具体 id**）⚠️
            d = e.get("data") or {}
            if e.get("type") in ("item", "journal_page"):
                for kk in ("item", "id", "name"):
                    if d.get(kk):
                        refs[d[kk]] += 1
    print(f"[ix] A8 的 loot 条目按 `type`：{dict(kinds)}")
    print(f"[ix] A8 里 item 类引用（去重）：**{len(refs)}** ✓ {sorted(refs)[:10]}")
    print()

    A, B = set(refs), ids
    print(f"[ix] 🎖️ **两方对照**")
    print(f"[ix]   A8 item 引用 ∩ A10 物品 id：**{len(A & B)}**")
    print(f"[ix]   🔴 只在 A8：**{len(A - B)}**  {sorted(A - B)[:12]}")
    print(f"[ix]   🔴 只在 A10：**{len(B - A)}**  {sorted(B - A)[:12]}")
    print()

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "A8 item 引用 × A10 物品 id。只读不落库 ✓",
                   "a10_ids": sorted(ids), "a10_types": dict(types),
                   "a8_item_refs": dict(refs),
                   "intersection": sorted(A & B),
                   "only_a8": sorted(A - B), "only_a10": sorted(B - A)},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[ix] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
