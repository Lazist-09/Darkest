#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""predict_rounding_test_impact.py —— 预演 **`Round`→`Ceil` + 下限 1→0** 会打破哪些测试 ✓

🔴 **为什么做**（承接 `107_*.md §6①`）：
   `107_*.md` 用**手算**判断 `M1cStage3DiffTableTests` 的 3 处**不会红**
   ⇒ 📌 但那是"推理"，不是"实测" ⚠️
   ⇒ ✅ 本件**拿真实数值跑一遍两种口径**，逐个断言比 ✓

🎖️ **判据（第 108 条）**：**"我推理说'不会红'—— 能不能【直接算出】两种口径下的值？
   ⇒ 能算就别推理"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/predict_rounding_test_impact.py
"""

from __future__ import annotations

import io
import json
import math
import os
import re
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def round_away(x):
    """我方现状：`Math.Round(x, AwayFromZero)`"""
    return math.floor(x + 0.5) if x >= 0 else math.ceil(x - 0.5)


def ceil_int(x):
    """参考：`CeilToInt`"""
    return math.ceil(x)


FLOOR_OLD, FLOOR_NEW = 1, 0


def old_impl(raw, fl=FLOOR_OLD):
    return max(fl, round_away(raw))


def new_impl(raw, fl=FLOOR_NEW):
    return max(fl, ceil_int(raw))


def main() -> int:
    # 🔴 只读 `FormulaTests` 的断言表（5 条）+ 实测两种口径
    cases = [
        (0.5, 1, "round(0.5)=1（AwayFromZero）"),
        (1.49, 1, ""),
        (1.5, 2, "round(1.5)=2"),
        (-3.2, 1, "任何来源伤害最低 1 点"),
        (7.68, 8, "7.68 四舍五入 → 8"),
    ]
    print("[pr] 🎖️ **`FormulaTests` 的 5 条断言：新旧口径逐个算**")
    print(f"[pr]   {'输入':>7s} {'旧(Round,floor1)':>17s} {'新(Ceil,floor0)':>16s} "
          f"{'断言期望':>9s} {'旧过':>5s} {'新过':>5s}")
    broken = []
    for raw, exp, note in cases:
        o, n = old_impl(raw), new_impl(raw)
        ok_o, ok_n = (o == exp), (n == exp)
        print(f"[pr]   {raw:>7} {o:>17} {n:>16} {exp:>9} "
              f"{'✅' if ok_o else '🔴':>5} {'✅' if ok_n else '🔴':>5}   {note}")
        if ok_o and not ok_n:
            broken.append((raw, o, n, exp))
    print()
    print(f"[pr] 🔴 **会红的断言：{len(broken)} 条**")
    for raw, o, n, exp in broken:
        print(f"[pr]     `ApplyDamageRounding({raw})` ⇒ 旧 {o}（期望 {exp}）· 新 **{n}**")
    print()

    # 🔴 `M1cStage3DiffTableTests` 的断言：counted>0 与 tier4Worse<=tier0Worse
    print("[pr] 🎖️ **`M1cStage3DiffTableTests`：断言是【相对比较】，不是具体值**")
    print("[pr]   `Assert.IsTrue(counted > 0)` ⇒ 与取整无关 ✓")
    print("[pr]   `Assert.IsTrue(tier4Worse <= tier0Worse)` ⇒")
    print("[pr]     · 它比的是 `new0 < now0` 与 `new4 < now0` 的【计数】")
    print("[pr]     · 而 `now0` 与 `new0`/`new4` **都走同一个函数** ⇒ 一起变 ✓")
    print("[pr]     · ⇒ 📌 **单调性（tier4 ≥ tier0）由【数据】决定，与取整方向无关** ✓")
    print("[pr]   ⇒ ✅ **该测试【不会红】**（但这是推理 —— 见 §下面实测）")
    print()

    # 🔴 实测：造一组数验证单调性在两种口径下都保持
    print("[pr] 🔴 **实测单调性**：对一组区间取两口径，比 `tier4 >= tier0` 是否都成立")
    import random
    random.seed(20260926)
    bad_old = bad_new = 0
    N = 20000
    for _ in range(N):
        lo = random.uniform(1, 20)
        hi = lo + random.uniform(0, 5)
        t0 = lo + (hi - lo) * 0.5
        t4 = lo + (hi - lo) * 0.9          # 高阶 ⇒ 值更大
        if old_impl(t4) < old_impl(t0):
            bad_old += 1
        if new_impl(t4) < new_impl(t0):
            bad_new += 1
    print(f"[pr]   {N} 组随机区间：旧口径违反 **{bad_old}** 次 · 新口径违反 **{bad_new}** 次 ✓")
    print(f"[pr]   ⇒ {'✅ 两口径都保持单调' if bad_old == 0 and bad_new == 0 else '🔴 有违反'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
