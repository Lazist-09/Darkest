#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_hero_skills_ref.py —— 数 **参考英雄技能总数** + 核 `protect_me` 的完整 effect ✓

🔴 **为什么做**（承接 `122_*.md §5`）：
   ① `protect_me` 的**完整 effect 列表未列全** ⚠️
   ② **参考英雄技能总数未全数** ⚠️
   ⇒ ✅ 本件一并做 ✓
   🎖️ **判据（第 160 条）**：**"我说'N 未全数'—— 那就数一遍；
      半数的结论不能当基数用"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_hero_skills_ref.py
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

ID = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info"


def main() -> int:
    files = sorted(f for f in os.listdir(ID) if f.endswith(".bytes"))
    print(f"[hs] 参考英雄文件：**{len(files)}** 个 ✓")
    print()

    total = 0
    per = {}
    allids = set()
    for f in files:
        t = io.open(os.path.join(ID, f), encoding="utf-8-sig",
                    errors="replace").read()
        # 🔴 只数 `.level 0`（每技能一行 per level）
        ids = set(re.findall(r'combat_skill:\s*\.id\s+"([^"]+)"\s*\.level\s+0\b', t))
        per[f.replace(".bytes", "")] = len(ids)
        allids |= ids
        total += len(ids)
    print(f"[hs] 🎖️ **技能总数（各职业 level 0 之和）：{total}** ✓")
    print(f"[hs] 🎖️ **去重后的技能 id：{len(allids)}** ✓")
    print()
    for k, v in sorted(per.items(), key=lambda x: -x[1]):
        print(f"[hs]   {k:22s} ⇒ **{v}**")
    print()

    # protect_me 的完整 effect
    print("[hs] 🔴 **`protect_me` 的全部 level 0 行（含所有 effect）**：")
    t = io.open(os.path.join(ID, "Antiquarian.bytes"), encoding="utf-8-sig",
                errors="replace").read()
    for l in t.splitlines():
        if '.id "protect_me"' in l and ".level 0" in l:
            effs = re.findall(r'\.effect\s+"([^"]+)"', l)
            print(f"[hs]   `.effect` = **{effs}**")
            print(f"[hs]   `{l.strip()[:200]}`")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
