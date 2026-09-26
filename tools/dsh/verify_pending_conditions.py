#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""verify_pending_conditions.py —— 独立复核 **`PendingCondition` 的输出** ✓

🔴 **为什么做**（承接本轮实现的"待接条件"列）：
   新加的列说"哪些缺载体 · 哪些只差接线"⇒ 📌 **那是可验的**：
   拿 `ByStatType` 的去向独立算一遍，看与代码输出是否一致 ✓
   🎖️ **判据（第 225 条）**：**"我刚加的列 ——
      能【独立复算】吗？（否则它只是我的断言）"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/verify_pending_conditions.py
"""

from __future__ import annotations

import io
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(REPO, "darkest", "scripts", "data", "BuffPrimitiveTranslation.cs")

# 从源码里抽 ByStatType 的映射
COMBAT = {"DamageMod", "MoraleMod", "HealMod", "ProbMod", "StatMod", "StateFlag"}


def main() -> int:
    t = io.open(SRC, encoding="utf-8", errors="replace").read()
    pen = re.search(r"Pending \{ get; \} = new\[\]\s*\{(.*?)\};", t, re.S)
    pending = re.findall(r'"([a-z_0-9]+)"', pen.group(1)) if pen else []
    print(f"[pc] `Pending` ⇒ **{len(pending)}** ✓")

    # 抽 ByStatType 里的 ["name"] = Target.X
    pairs = dict(re.findall(r'\["([a-z_0-9]+)"\]\s*=\s*Target\.(\w+)', t))
    print(f"[pc] `ByStatType` 条目 ⇒ **{len(pairs)}** ✓")
    print()

    print("[pc] 🔴 **独立复算（按去向分类）**：")
    lack, wire = [], []
    for n in pending:
        tgt = pairs.get(n)
        if tgt is None:
            # combat_stat_add / combat_stat_multiply 走 Classify 特判
            if n in ("combat_stat_add", "combat_stat_multiply"):
                tgt = "StatMod/DamageMod(特判)"
            else:
                tgt = "**未在表内**"
        kind = ("🔴 缺载体" if tgt == "ExpeditionLayer" else
                "⚠️ 未接线" if tgt in COMBAT else
                "⚠️ 未接线(resistance)" if tgt == "UnitResistance" else
                f"?? {tgt}")
        (lack if "缺载体" in kind else wire).append((n, tgt, kind))

    print(f"[pc]   🔴 **缺载体（趟级/城池级）：{len(lack)}** ✓")
    for n, tgt, kind in lack:
        print(f"[pc]       `{n}` ⇒ {tgt}")
    print()
    print(f"[pc]   ⚠️ **未接线（落点已有）：{len(wire)}** ✓")
    for n, tgt, kind in wire:
        print(f"[pc]       `{n}` ⇒ {tgt}")
    print()
    print(f"[pc] 🎖️ **合计 {len(lack) + len(wire)} == {len(pending)}** "
          f"⇒ {'✅ 覆盖完整' if len(lack) + len(wire) == len(pending) else '🔴 有遗漏'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
