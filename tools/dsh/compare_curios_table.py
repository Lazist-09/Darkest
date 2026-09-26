#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""compare_curios_table.py —— **逐条比对** 我方 6 条 curio vs 参考 `Curios.csv` ✓

🔴 **为什么做**（承接 `160_*.md` 的判据 308 与 `161` 的发现）：
   我已发现：**只 `sconce` 在参考里** · **我方第 7 条是元数据 `__source__`**
   ＋ 我方 note 说「**6 个 Curio；数值为占位**」⇒
   📌 **那"占位"是【我方自造】还是【抄了没记】？** ⇒ ✅ 本件逐条比 ✓
   🎖️ **判据（第 308 条）**：**"'可能有对应物'要变成'确认抄了'——
      要【逐条比对】，不能停在'词命中'"** ✓

🔴 **参考 CSV 结构**（实测）：
   每条约 15 行 · 首行 `,N,<Name>,,<ResultType>,…` · 次行 `,,ID STRING,…` ·
   再下行**第 3 列 = id 小写** ⇒ 以此抽 id ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/compare_curios_table.py
"""

from __future__ import annotations

import io
import json
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Curios\Curios.csv"
OURS = os.path.join(REPO, "darkest", "data", "curios.json")


def main() -> int:
    t = io.open(REF, encoding="utf-8-sig", errors="replace").read()
    lines = t.splitlines()
    ref_ids, ref_names = [], {}
    for i, l in enumerate(lines):
        p = l.split(",")
        # `,N,<Name>,` 形态 ⇒ p[1]=N, p[2]=Name
        if len(p) > 2 and re.fullmatch(r"\d+", p[1].strip()) and p[2].strip():
            name = p[2].strip()
            # 往下找 `,,<id>,` 行
            for j in range(i + 1, min(i + 4, len(lines))):
                q = lines[j].split(",")
                if len(q) > 2 and q[2].strip() and re.fullmatch(r"[a-z0-9_]+", q[2].strip()):
                    ref_ids.append(q[2].strip())
                    ref_names[q[2].strip()] = name
                    break
    print(f"[ct] 参考 `Curios.csv` ⇒ **{len(ref_ids)}** 条 curio ✓")
    print(f"[ct]   前 12：{ref_ids[:12]}")
    print()

    ours = [c for c in json.load(open(OURS, encoding="utf-8"))["curios"]
            if c["id"] != "__source__"]
    print(f"[ct] 我方 ⇒ **{len(ours)}** 条 ✓")
    print()
    print("[ct] 🔴 **逐条比**：")
    for c in ours:
        cid = c["id"]
        stem = cid.replace("cur_", "")
        hit = stem if stem in ref_ids else None
        if hit is None:
            cand = [r for r in ref_ids if stem in r or r in stem]
            hit = cand[0] if cand else None
        if hit:
            print(f"[ct]   ⚠️ `{cid}`（{c.get('name')}）⇒ 参考有 `{hit}`（{ref_names[hit]}）"
                  f"　🔴 **需比对数值**")
        else:
            print(f"[ct]   ✅ `{cid}`（{c.get('name')}）⇒ 参考**无同名**")
    print()
    print("[ct] 🎖️ 参考里【我方没抄】的（前 20）：")
    mine = {c["id"].replace("cur_", "") for c in ours}
    missing = [r for r in ref_ids if r not in mine]
    print(f"[ct]   合计 **{len(missing)}** 条，前 20：{missing[:20]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
