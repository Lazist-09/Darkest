#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_sigma_normalization.py —— 核 **`Σ段倍率` 归为 1** 是否已落库 ✓

🔴 **为什么做**（承接 `111_*.md §5`）：
   `skills.json` 的 `_sigma_note` 写着**策划 `#475` 裁定**：
   **`Σ段倍率` 归一为 1** —— 原版没这概念 ⇒ 若 ≠ 1 就不是对齐；
   做法：**多段攻击每段 1/段数**（Σ=1）；多段收益靠**其他机制** ✓
   🔴 **口径**：**只对 `flat` 段成立** —— `missing_hp` 段没有 `multiplier` 键
   （走 base+coefficient，不参与 Σ）；`damage = null` 的 16 条亦无 Σ ✓
   ⇒ 📌 但**从未实测复核** ⇒ ✅ 本件数一遍 ✓

🎖️ **判据（第 120 条）**：**"这条裁定说'每段 1/段数'——
   数据里【真的是】1/段数 吗？还是近似值（如 0.33）？"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_sigma_normalization.py
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
SK = os.path.join(REPO, "darkest", "data", "skills.json")


def main() -> int:
    sk = json.load(open(SK, encoding="utf-8"))
    skills = sk["skills"]
    print(f"[sg] 技能 **{len(skills)}** 条 ✓")
    print()

    stats = Counter()
    sums = Counter()
    details = []
    for s in skills:
        dmg = s.get("damage")
        if dmg is None:
            stats["damage=null ⇒ 无 Σ"] += 1
            continue
        segs = dmg.get("segments") or []
        if not segs:
            stats["segments 空 ⇒ 无 Σ"] += 1
            continue
        # 🔴 只对 flat 段算 Σ
        flats = [g for g in segs if g.get("kind") == "flat" or "multiplier" in g]
        noflat = [g for g in segs if "multiplier" not in g]
        if not flats:
            stats["无 flat 段 ⇒ 不参与 Σ"] += 1
            continue
        total = sum(g.get("multiplier", 0) for g in flats)
        sums[round(total, 6)] += 1
        details.append((s["id"], len(flats), len(noflat), total))

    print("[sg] 🎖️ **分类计数**：")
    for k, v in stats.most_common():
        print(f"[sg]   {k} ⇒ **{v}**")
    print()
    print("[sg] 🎖️ **flat 段的 Σ 取值分布**（按段数分组）：")
    byn = {}
    for sid, nf, nn, tot in details:
        byn.setdefault(nf, []).append((sid, nn, tot))
    for nf in sorted(byn):
        vals = Counter(round(t, 6) for _, _, t in byn[nf])
        print(f"[sg]   **{nf} 段**（{len(byn[nf])} 条）⇒ Σ 分布 {dict(vals)}")
        print(f"[sg]       1/{nf} = **{1.0/nf:.6f}**")
    print()
    bad = [(sid, nf, nn, tot) for sid, nf, nn, tot in details
           if abs(tot - 1.0) > 1e-6]
    print(f"[sg] 🔴 **Σ ≠ 1 的：{len(bad)}** ✓")
    for sid, nf, nn, tot in bad[:12]:
        print(f"[sg]     `{sid}` ⇒ {nf} 段 · Σ = **{tot}**")
    print()
    if not bad:
        print("[sg] ⇒ ✅ **全部归一到 1** ⇒ 裁定已落库 ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
