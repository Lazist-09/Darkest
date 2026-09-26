#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_damage_shape_vs_ref.py —— 核 **我方每一条技能的"伤害形状"参考里有没有** ✓

🔴 **为什么做**（承接 `114_*.md §6`）：
   `§43` 把技能映射到参考时，**只核了 `type`/语义/`dmg%`**，
   ⚠️ **没核【伤害公式的形状】** ⇒ 那是"技能级对齐"，不是"公式级对齐" ✓
   ⇒ 📌 本件**逐条**核：我方的 `damage.segments[].type`
      在参考里**有没有对应的机制** ⇒ ✅ 找出**全部**形状偏离 ✓

🎖️ **判据（第 130 条）**：**"这个'对齐'覆盖了【几层】？
   ⇒ 逐层核一遍，别停在最外层"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_damage_shape_vs_ref.py
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
REFDATA = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
REFCODE = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"


def main() -> int:
    sk = json.load(open(os.path.join(REPO, "darkest", "data", "skills.json"),
                        encoding="utf-8"))
    skills = sk["skills"]

    # ① 我方用了哪些 segment type
    kinds = Counter()
    users = {}
    for s in skills:
        d = s.get("damage")
        if not d:
            continue
        for g in d.get("segments") or []:
            t = g.get("type")
            kinds[t] += 1
            users.setdefault(t, []).append(s["id"])
    print("[ds] 🎖️ **我方 `damage.segments[].type` 的取值**：")
    for k, v in kinds.most_common():
        print(f"[ds]   `{k}` ×**{v}** ⇒ {users[k][:4]}")
    print()

    # ② 逐个 type 去参考里找
    PROBE = {
        "flat": ["Lerp", "DamageMin", "DamageMax", "DamageLow", "DamageHigh"],
        "missing_hp": ["missing_hp", "MissingHealth", "coefficient"],
    }
    print("[ds] 🔴 **逐个 type 在参考里的对应**：")
    for t in kinds:
        pats = PROBE.get(t, [t])
        found = {}
        for pat in pats:
            n = 0
            for root in (REFDATA, REFCODE):
                for dirpath, _d, ns in os.walk(root):
                    for f in ns:
                        if f.endswith(".meta") or not (f.endswith(".cs")
                                                       or f.endswith(".json")
                                                       or f.endswith(".txt")):
                            continue
                        p = os.path.join(dirpath, f)
                        try:
                            txt = io.open(p, encoding="utf-8-sig",
                                          errors="replace").read()
                        except Exception:
                            continue
                        n += txt.count(pat)
            found[pat] = n
        verdict = "✅ 有对应" if any(v > 0 for v in found.values()) else "🔴 无对应"
        print(f"[ds]   `{t}` ⇒ {verdict}  {found}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
