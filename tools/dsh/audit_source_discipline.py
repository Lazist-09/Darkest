#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_source_discipline.py —— 核 **每层的"来源口径"到底是哪个源** ✓

🔴 **为什么做**（承接 `162_*.md` 的判据 319）：
   我用"参考"一词指两个源（**本地参考项目** vs **一手 E 盘**）⚠️
   ⇒ 📌 **而我 `162_*.md` 的采用率表【混用了两个源】** ⚠️
   ⇒ ✅ 本件**逐层判清：它到底抄自哪个源** ✓
   🎖️ **判据（第 319 条）**：**"我说'参考'——
      指【本地参考项目】还是【一手 E 盘】？（两者条数不同）"** ✓

🔴 **做法**：读每个数据文件的 `_source`/`_note` ⇒ 抽"从哪转写"⇒ 归类 ✓
🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_source_discipline.py
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
    print(f"[sd] `darkest/data` ⇒ **{len(files)}** 文件 ✓")
    print()
    ref, first, both, none = [], [], [], []
    for f in files:
        raw = io.open(os.path.join(D, f), encoding="utf-8", errors="replace").read()
        is_ref = bool(re.search(r"本地参考项目|参考项目|Darkest-Dungeon-Unity", raw))
        is_first = bool(re.search(r"SteamLibrary|一手|E 盘|E盘", raw))
        if is_ref and is_first:
            both.append(f)
        elif is_ref:
            ref.append(f)
        elif is_first:
            first.append(f)
        else:
            none.append(f)

    print(f"[sd] 🎖️ **只指【本地参考项目】⇒ {len(ref)}**")
    for f in ref:
        print(f"[sd]     {f}")
    print()
    print(f"[sd] 🎖️ **只指【一手 E 盘】⇒ {len(first)}**")
    for f in first:
        print(f"[sd]     {f}")
    print()
    print(f"[sd] ⚠️ **两个源【都提到】⇒ {len(both)}**（口径最易混）")
    for f in both:
        print(f"[sd]     {f}")
    print()
    print(f"[sd] ⚪ **都没提 ⇒ {len(none)}**")
    for f in none:
        print(f"[sd]     {f}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
