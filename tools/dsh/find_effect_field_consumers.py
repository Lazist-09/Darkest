#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""find_effect_field_consumers.py —— 找 A6a 的 **4 个驼峰字段**在代码里的消费点 ✓

🔴 **为什么做**（承接 `92_*.md §6①`）：
   `dotPoison` ×51 · `dotBleed` ×44 · `keyStatus` ×27 · `monsterType` ×20
   ⇒ 📌 前两个是 **DoT 的伤害数值**，丢了它们 A6a **不能用于实现 DoT** ✓
   ⇒ ✅ 本件读出**它们的代码消费点** —— 这是"采用 A6a"的硬前置 ✓

🎖️ **判据（第 67 条）**：**"这个数据字段的【消费点】在哪？
   ⇒ 按【数据键的驼峰名】去搜代码（`dotPoison` → `DotPoison`）"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/find_effect_field_consumers.py
"""

from __future__ import annotations

import io
import os
import re
import sys
from collections import Counter

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

ROOT = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"
# 🔴 数据键 → 可能的代码名（驼峰 / 帕斯卡 / 枚举）
CANDIDATES = {
    "dotPoison": ["dotPoison", "DotPoison", "DOTPoison", "Poison"],
    "dotBleed": ["dotBleed", "DotBleed", "DOTBleed", "Bleed"],
    "keyStatus": ["keyStatus", "KeyStatus"],
    "monsterType": ["monsterType", "MonsterType"],
}


def main() -> int:
    files = {}
    for dirpath, _d, ns in os.walk(ROOT):
        for f in ns:
            if f.endswith(".cs"):
                p = os.path.join(dirpath, f)
                files[os.path.relpath(p, ROOT)] = io.open(
                    p, encoding="utf-8-sig", errors="replace").read()
    print(f"[ec] 扫了 **{len(files)}** 个 `.cs` ✓")
    print()
    for key, cands in CANDIDATES.items():
        print(f"[ec] 🔴 数据键 `{key}` ⇒ 按 {cands} 搜：")
        total = 0
        hits = Counter()
        for name in cands:
            for rel, t in files.items():
                for i, l in enumerate(t.splitlines(), 1):
                    if re.search(r"\b" + re.escape(name) + r"\b", l):
                        hits[(name, rel)] += 1
                        total += 1
        if not hits:
            print(f"[ec]     🔴 **0 处** ⇒ 该字段【无代码消费】")
        else:
            print(f"[ec]     **{total}** 处：")
            for (name, rel), n in sorted(hits.items(), key=lambda x: -x[1])[:10]:
                print(f"[ec]       `{name}` @ **{rel}** ×{n}")
        print()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
