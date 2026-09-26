#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_missing_hp_in_ref.py —— 核 **`missing_hp` 段在参考里有无对应概念** ✓

🔴 **为什么做**（承接 `112_*.md §6`）：
   我方有 **2 条 `missing_hp` 段**（`medic_lethal_injection` · `commissar_execution_order`）
   ⇒ 结构是 `{"type":"missing_hp","base":1.0,"coefficient":0.6/0.7}` ✓
   ⇒ 📌 **而参考项目【有】对应吗？** ⇒ ✅ 本件诸参考数据/代码里找 ✓
   🎖️ **判据（第 124 条）**：**"我方这个结构 ——
      参考里有对应吗？⇒ 有 ⇒ 对齐；没有 ⇒ 那是【我方自加】⇒ 归解冻清单"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_missing_hp_in_ref.py
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

REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
REFCODE = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"


def main() -> int:
    print("=" * 76)
    print("① 参考【数据】里找 `missing_hp` / 类似概念")
    print("=" * 76)
    pats = ["missing_hp", "missing_health", "hp_missing", "MissingHp", "missingHp",
            "coefficient", "base_value"]
    for pat in pats:
        hits = []
        for dirpath, _d, ns in os.walk(REF):
            for f in ns:
                if f.endswith(".meta"):
                    continue
                p = os.path.join(dirpath, f)
                try:
                    t = io.open(p, encoding="utf-8-sig", errors="replace").read()
                except Exception:
                    continue
                for i, l in enumerate(t.splitlines(), 1):
                    if pat in l:
                        hits.append((os.path.relpath(p, REF), i, l.strip()[:110]))
        print(f"  `{pat}` ⇒ **{len(hits)}** 处")
        for h in hits[:4]:
            print(f"      {h[0]}:{h[1]}  {h[2]}")
    print()

    print("=" * 76)
    print("② 参考【代码】里找（有没有「缺生命值加成」这种机制）")
    print("=" * 76)
    for pat in ["MissingHealth", "missingHealth", "HealthLost", "MissingHp"]:
        hits = []
        for dirpath, _d, ns in os.walk(REFCODE):
            for f in ns:
                if not f.endswith(".cs"):
                    continue
                p = os.path.join(dirpath, f)
                t = io.open(p, encoding="utf-8-sig", errors="replace").read()
                for i, l in enumerate(t.splitlines(), 1):
                    if pat in l:
                        hits.append((os.path.relpath(p, REFCODE), i, l.strip()[:110]))
        print(f"  `{pat}` ⇒ **{len(hits)}** 处")
        for h in hits[:4]:
            print(f"      {h[0]}:{h[1]}  {h[2]}")
    print()

    print("=" * 76)
    print("③ 🎖️ 而参考的 `Lerp` 伤害结构（`BattleSolver`）里有没有等价物")
    print("=" * 76)
    p = os.path.join(REFCODE, "Mechanics", "Battle", "BattleSolver.cs")
    lines = io.open(p, encoding="utf-8-sig", errors="replace").read().splitlines()
    for i in range(382, 390):
        if i < len(lines):
            print(f"  {i+1:5d}|{lines[i].rstrip()[:130]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
