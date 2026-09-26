#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_adoption_scope.py —— 核 **"采用参考来源"这件事的覆盖清单** ✓

🔴 **为什么做**（回到本任务目标）：
   目标是"**把我方数值全面改用该项目的来源顶替**" ⇒
   📌 **那"全面"到什么程度？哪些【还没顶替】？** ⇒ ✅ 本件列出清单 ✓
   🎖️ **判据（第 300 条）**：**"我说'全面改用' ——
      '全面'的【清单】在哪？（没有清单 = 没法说全面）"** ✓

🔴 **做法**：对每个 `darkest/data/*.json` ⇒ 看它的
   `_source` / `origin` / `_note` 是否指向【参考项目】或【一手 E 盘】✓
   ⇒ 三分：✅ 参考来源 · 🎖️ 一手来源 · 🔴 **我方自造（无外部来源）**

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_adoption_scope.py
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
D = os.path.join(REPO, "darkest", "data")


def main() -> int:
    files = sorted(f for f in os.listdir(D) if f.endswith(".json"))
    print(f"[as] `darkest/data` ⇒ **{len(files)}** 个 `.json` ✓")
    print()

    ref, first, ours, unknown = [], [], [], []
    for f in files:
        p = os.path.join(D, f)
        raw = io.open(p, encoding="utf-8", errors="replace").read()
        has_ref = bool(re.search(r"本地参考项目|Darkest-Dungeon-Unity|参考项目", raw))
        has_first = bool(re.search(r"SteamLibrary|一手|E 盘|E盘", raw))
        has_ours = bool(re.search(r"ours|我方自|自造|我方新增", raw))
        if has_ref and not has_first:
            ref.append(f)
        elif has_first and not has_ref:
            first.append(f)
        elif has_ref and has_first:
            first.append(f + "（含参考字样）")
        elif has_ours:
            ours.append(f)
        else:
            unknown.append(f)

    print(f"[as] 🎖️ **指向【参考项目】的 ⇒ {len(ref)}** ✓")
    for f in ref:
        print(f"[as]     {f}")
    print()
    print(f"[as] 🎖️ **指向【一手 E 盘】的 ⇒ {len(first)}** ✓")
    for f in first:
        print(f"[as]     {f}")
    print()
    print(f"[as] 🔴 **无外部来源（我方自造）⇒ {len(ours)}** ⚠️")
    for f in ours:
        print(f"[as]     {f}")
    print()
    print(f"[as] ⚪ **未标注 ⇒ {len(unknown)}**")
    for f in unknown:
        print(f"[as]     {f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
