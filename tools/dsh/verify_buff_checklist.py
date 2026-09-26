#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""verify_buff_checklist.py —— 独立复核 **`Checklist` 的每个名字都在参考里** ✓

🔴 **为什么做**（承接 `135_*.md §6①`）：
   `ValidateAgainst` 的注释说它是 **"加载即校验（红线的防火墙）"**，
   且**历史上踩过 3 个名字**（`resolve_xp_percent` 等）⇒
   📌 **那就该独立验一遍**：**清单里现在还有没有对不上的** ✓
   ⇒ ✅ 本件**不靠代码**，直接拿参考的 `stat_type` 全集对清单 ✓

🎖️ **判据（第 203 条）**：**"这个校验的目标是什么？
   ⇒ 拿【参考的真实全集】独立复算一遍，别只信校验通过"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/verify_buff_checklist.py
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
SRC = os.path.join(REPO, "darkest", "scripts", "data", "BuffPrimitiveTranslation.cs")
REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"


def main() -> int:
    # ① 抽 Checklist 的全部名字（Activated + Pending）
    t = io.open(SRC, encoding="utf-8", errors="replace").read()
    act = re.search(r"Activated \{ get; \} = new\[\] \{(.*?)\};", t, re.S)
    pen = re.search(r"Pending \{ get; \} = new\[\]\s*\{(.*?)\};", t, re.S)
    names = []
    for m in (act, pen):
        if m:
            names += re.findall(r'"([a-z_0-9]+)"', m.group(1))
    print(f"[bc] 🎖️ `Checklist`（Activated + Pending）⇒ **{len(names)}** 个名字 ✓")
    print(f"[bc]   Activated: {re.findall(chr(34) + '([a-z_0-9]+)' + chr(34), act.group(1)) if act else '?'}")
    print()

    # ② 抽参考的 stat_type 全集
    stats = set()
    for dp, _d, ns in os.walk(REF):
        for f in ns:
            if f.endswith(".meta"):
                continue
            p = os.path.join(dp, f)
            try:
                txt = io.open(p, encoding="utf-8-sig", errors="replace").read()
            except Exception:
                continue
            for m in re.finditer(r'"stat_type"\s*:\s*"([^"]+)"', txt):
                stats.add(m.group(1))
            for m in re.finditer(r"\.stat_type\s+\"?([a-z_0-9]+)\"?", txt):
                stats.add(m.group(1))
    print(f"[bc] 🎖️ 参考 `stat_type` 全集 ⇒ **{len(stats)}** 个 ✓")
    print()

    miss = sorted(set(names) - stats)
    print(f"[bc] 🔴 **不在参考里的：{len(miss)}** ✓")
    for m in miss:
        print(f"[bc]     `{m}`")
    if not miss:
        print("[bc]   ⇒ ✅ **全部命中** ⇒ 防火墙目标达成 ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
