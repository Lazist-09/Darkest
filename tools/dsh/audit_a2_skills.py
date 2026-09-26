#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_a2_skills.py —— 核 **A2（技能 `dmg%`）** 的当前落库状态 ✓

🔴 **为什么做**：
   PLAN 记：**只落 14/44**（30 条参考答不上来 ⇒ 卡在策划）⚠️
   ⇒ 📌 但**从未独立复核**这个 14/44 ⇒ ✅ 本件用我方数据 + 参考数据**重算** ✓

🎖️ **判据（第 116 条）**：**"报告里说'落了 N/M'——
   N 与 M 能不能从【数据】里重新数出来？"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_a2_skills.py
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
OURS = os.path.join(REPO, "darkest", "data", "skills.json")
MAPPING = os.path.join(REPO, "reports", "unity_ref", "skill_dmg_mapping.json")


def main() -> int:
    sk = json.load(open(OURS, encoding="utf-8"))
    print(f"[a2] 我方 `skills.json` 顶层键：{list(sk)}")
    skills = sk.get("skills") or []
    print(f"[a2] 技能数：**{len(skills)}** ✓")
    print()

    have = sum(1 for s in skills if "dmg_pct" in s)
    print(f"[a2] 🎖️ **有 `dmg_pct` 的：{have} / {len(skills)}** ✓")
    miss = [s for s in skills if "dmg_pct" not in s]
    if miss:
        print(f"[a2] 🔴 **没有的 {len(miss)} 条**：")
        for s in miss[:12]:
            print(f"[a2]     `{s.get('id')}`"
                  f"  {json.dumps({k: v for k, v in s.items() if k != 'id'}, ensure_ascii=False)[:90]}")
    print()

    # 有 _dmg_pct_source 的
    src = sum(1 for s in skills if "_dmg_pct_source" in s)
    print(f"[a2] 🎖️ **带 `_dmg_pct_source`（来源标注）的：{src}** ✓")
    kinds = Counter()
    for s in skills:
        v = s.get("_dmg_pct_source")
        if v is None:
            kinds["无"] += 1
        elif isinstance(v, str):
            kinds["参考" if "Darkest-Dungeon-Unity" in v else "其它"] += 1
        else:
            kinds["非字符串"] += 1
    print(f"[a2]   溯源分类：{dict(kinds)}")
    print()

    # 与映射表对照
    if os.path.isfile(MAPPING):
        m = json.load(open(MAPPING, encoding="utf-8"))
        print(f"[a2] `skill_dmg_mapping.json` 顶层键：{list(m)}")
        for k, v in m.items():
            if isinstance(v, list):
                print(f"[a2]   `{k}`：list[{len(v)}]")
            elif isinstance(v, dict):
                print(f"[a2]   `{k}`：dict({len(v)})")
            else:
                print(f"[a2]   `{k}`：{str(v)[:80]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
