#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_zero_initiative.py —— 查**数据里有没有 `NumberOfTurns == 0` 的怪** ✓

🔴 **为什么做**（承接 `96_*.md §5`）：
   `idleUnit` 分支（`TotalInitiatives == 0`）**成立的前提**是
   **某怪的 `Initiative.NumberOfTurns == 0`** ✓
   ⇒ 📌 若**实际数据里没有这种怪** ⇒ 那个分支是**死代码**（不用实现）✓
   ⇒ 📌 若**有** ⇒ ⚠️ **必须实现**（否则那些怪的 DoT 永不结算）✓
   🎖️ **判据（第 76 条）**：**"这个分支的【触发条件】在数据里出现过吗？
      ⇒ 没出现过 ⇒ 死代码；出现过 ⇒ 必须实现"** ✓

数据来源：`Initiative.cs:20` ⇒ `NumberOfTurns = int.Parse(data[++i])`
   ⇒ 从怪的 `.info.darkest` 里读 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_zero_initiative.py
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

ED = r"E:\SteamLibrary\steamapps\common\DarkestDungeon\monsters"
DLC = r"E:\SteamLibrary\steamapps\common\DarkestDungeon\dlc"


def scan_monster_dir(root, label):
    vals = Counter()
    zero = []
    if not os.path.isdir(root):
        return vals, zero
    for d in sorted(os.listdir(root)):
        dp = os.path.join(root, d)
        if not os.path.isdir(dp):
            continue
        for sub in sorted(os.listdir(dp)):
            sp = os.path.join(dp, sub)
            if not os.path.isdir(sp):
                continue
            for f in os.listdir(sp):
                if not f.endswith(".info.darkest"):
                    continue
                t = io.open(os.path.join(sp, f), encoding="utf-8-sig",
                            errors="replace").read()
                m = re.search(r"initiative:\s*(.*)", t)
                if not m:
                    continue
                # 🔴 **实测格式**：`initiative: .number_of_turns_per_round 1` ⚠️
                #    我第一版搜 `.number_of_turns` / `.turns` ⇒ **得 0 个变体** ⇒
                #    ✅ **看原文才知道真名是 `number_of_turns_per_round`** ✓
                #    🎖️ 判据（第 26 条复用）：**按猜的格式解析得 0 ⇒ 先看一行原文** ✓
                mm = re.search(r"\.number_of_turns_per_round\s+(\d+)", m.group(1))
                if not mm:
                    mm = re.search(r"\.number_of_turns\s+(\d+)", m.group(1))
                if not mm:
                    mm = re.search(r"\.turns\s+(\d+)", m.group(1))
                if mm:
                    v = int(mm.group(1))
                    vals[v] += 1
                    if v == 0:
                        zero.append(f"{label}/{d}/{sub}")
    return vals, zero


def main() -> int:
    total = Counter()
    allzero = []
    v, z = scan_monster_dir(ED, "base")
    total.update(v)
    allzero += z
    print(f"[zi] 一手本体：**{sum(v.values())}** 个变体 ⇒ 取值分布 {dict(sorted(v.items()))}")
    print(f"[zi]   🔴 `== 0` 的：**{len(z)}** {z[:8]}")
    print()
    if os.path.isdir(DLC):
        for d in sorted(os.listdir(DLC)):
            mp = os.path.join(DLC, d, "monsters")
            v2, z2 = scan_monster_dir(mp, d)
            if v2:
                total.update(v2)
                allzero += z2
                print(f"[zi] DLC **{d}**：{sum(v2.values())} 变体 ⇒ "
                      f"{dict(sorted(v2.items()))} · 🔴 0 的 {len(z2)}")
    # 还有 features 下的
    for d in sorted(os.listdir(DLC)) if os.path.isdir(DLC) else []:
        base = os.path.join(DLC, d)
        for dirpath, _dd, _fs in os.walk(base):
            if os.path.basename(dirpath) == "monsters":
                v3, z3 = scan_monster_dir(dirpath, os.path.relpath(dirpath, DLC))
                if v3:
                    total.update(v3)
                    allzero += z3
    print()
    print(f"[zi] 🎖️ **全盘合计**：**{sum(total.values())}** 个变体 ⇒ "
          f"取值分布 **{dict(sorted(total.items()))}**")
    print(f"[zi] 🎖️ **`NumberOfTurns == 0` 的变体数：{len(allzero)}** ✓")
    if allzero:
        print("[zi]   ⇒ 🔴 **该分支【不是死代码】⇒ 必须实现** ✓")
        for x in allzero[:12]:
            print(f"[zi]       {x}")
    else:
        print("[zi]   ⇒ ✅ **数据里没有 ⇒ 该分支是【死代码】**（可不实现，但要记下）✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
