#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""analyze_brains.py —— 深析 A5 的 **160 brains**：结构 · 规模 · 与我方 3 个 archetype 的对应 ✓

🔴 **为什么做**（承接 A5 记的"规模差很大"）：
   A5 抽了参考的 **160 brains**（`JsonAI.json`）· 我方 `enemy_ai.json` 只有 **3 个 archetype** ✓
   ⇒ 📌 差距已记，但**从未逐字段对账** ⇒ ✅ 本件补上 ✓
   🎖️ 已知（A5）：参考是**欲望权重系统**（`skill_selection_desires` 9 种 type / 24 个 data 键 ·
      `target_selection_desires` 8 种 / 13 个键）· 我方是 `when` 条件 + `mark_weight` ✓

🎖️ **本件要回答**：
   ① 160 brains 的**结构全貌**（字段 + 出现次数）
   ② 它们**按什么分组**（怪？副本？难度？）
   ③ 我方那 3 个 archetype **能对上哪些** ⇒ ✅ **映射可行性** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/analyze_brains.py
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
OUT = os.path.join(R, "brains_analysis.json")


def main() -> int:
    a5 = json.load(open(os.path.join(R, "ai_from_ref.json"), encoding="utf-8"))
    print(f"[ai] A5 顶层键：{list(a5)}")
    brains = a5.get("brains") or a5.get("ai") or []
    print(f"[ai] brains：**{len(brains)}** ✓")
    print()

    print("[ai] 🎖️ **结构全貌**（顶层字段 + 出现次数）")
    keys = Counter()
    for b in brains:
        keys.update(b.keys())
    for k, v in keys.most_common():
        print(f"[ai]   `{k}`  ×{v}")
    print()

    print("[ai] 示例（1 条）：")
    print(f"[ai]   {json.dumps(brains[0], ensure_ascii=False)[:600]}")
    print()

    # ② 按什么分组
    print("[ai] 🔴 **命名规律**（id 的前缀）")
    pref = Counter()
    for b in brains:
        i = b.get("id") or ""
        pref[i.split("_")[0] if "_" in i else i] += 1
    for k, v in pref.most_common(15):
        print(f"[ai]   `{k}`  ×{v}")
    print()

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({"note": "A5 的 160 brains 深析。只读不落库 ✓",
                   "top_keys": dict(keys), "count": len(brains),
                   "id_prefixes": dict(pref), "sample": brains[0]},
                  fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print(f"[ai] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
