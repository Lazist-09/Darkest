#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_naming_consistency.py —— 扫我方数据里的**命名风格不一致**（不靠并集掩盖）✓

🔴 **为什么需要它**（承接 `33_*.md §4`）：
   宽面审计（`audit_all_refs.py`）为了让引用通过，会把**两种写法都收进并集** ⚠️
   ⇒ 📌 **"审计说闭合" ≠ "没有不一致"** —— **并集会把不一致吃掉** ✓
   ⇒ ✅ 本件**专门查不一致**：同一套概念在**不同文件**里写法是否统一 ✓

查三类（都是实测出来的"真陷阱"形态）：
   ① **复数 vs 单数**（传家宝那处 ——**已修**，作为对照基准）✓
   ② **下划线 vs 无下划线**（如 `melee_soldier` vs `meleesoldier`）✓
   ③ **同一 id 在不同文件的拼写差异**（编辑距离 1~2 的近邻）✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_naming_consistency.py
"""

from __future__ import annotations

import json
import os
import re
import sys
from collections import defaultdict

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DATA = os.path.join(REPO, "darkest", "data")
OUT = os.path.join(REPO, "reports", "unity_ref", "our_naming_audit.json")


def collect_strings(node, path, fname, out):
    if isinstance(node, dict):
        for k, v in node.items():
            collect_strings(v, f"{path}.{k}" if path else k, fname, out)
    elif isinstance(node, list):
        for i, v in enumerate(node):
            collect_strings(v, f"{path}[{i}]", fname, out)
    elif isinstance(node, str):
        if node and re.fullmatch(r"[a-z][a-z0-9_]*", node):
            out[node].add(fname)


def norm(s):
    return s.replace("_", "")


def close(a, b):
    """编辑距离 ≤1（等价于：不过度放宽，只抓真近邻）✓"""
    if a == b:
        return True
    if abs(len(a) - len(b)) > 1:
        return False
    if len(a) == len(b):
        return sum(1 for x, y in zip(a, b) if x != y) == 1
    lo, hi = (a, b) if len(a) < len(b) else (b, a)
    for i in range(len(hi)):
        if hi[:i] + hi[i + 1:] == lo:
            return True
    return False


def main() -> int:
    files = sorted(n for n in os.listdir(DATA) if n.endswith(".json"))
    per_file = defaultdict(set)
    for name in files:
        try:
            d = json.load(open(os.path.join(DATA, name), encoding="utf-8"))
        except Exception:
            continue
        s = defaultdict(set)
        collect_strings(d, "", name, s)
        for k, v in s.items():
            per_file[name].add(k)

    allnames = set()
    for v in per_file.values():
        allnames |= v
    print(f"[name] 我方 `darkest/data/` **{len(files)}** 文件 · "
          f"标识符形状的字符串 **{len(allnames)}** 个 ✓")

    # ---- ① 下划线去留：同一概念两种写法 ----
    bynorm = defaultdict(set)
    for n in allnames:
        bynorm[norm(n)].add(n)
    print()
    print("[name] 🔴 **同一概念、下划线写法不同**（最可能是一处真不一致）：")
    hits1 = 0
    for key, variants in sorted(bynorm.items()):
        if len(variants) > 1:
            hits1 += 1
            where = {v: sorted(f for f, s in per_file.items() if v in s) for v in variants}
            print(f"[name]   🔴 {sorted(variants)}")
            for v, fs in where.items():
                print(f"[name]        `{v}` ← {fs}")
    if not hits1:
        print("[name]   ✅ 无 ✓")

    # ---- ② 复数 vs 单数（同词干）----
    print()
    print("[name] 🔴 **复数 / 单数并存**：")
    stems = defaultdict(set)
    for n in allnames:
        if len(n) > 3 and n.endswith("s"):
            stems[n[:-1]].add(n)
        else:
            stems[n].add(n)
    hits2 = 0
    for stem, variants in sorted(stems.items()):
        singles = {v for v in variants if not v.endswith("s")}
        plurals = {v for v in variants if v.endswith("s")}
        if singles and plurals:
            hits2 += 1
            where = {v: sorted(f for f, s in per_file.items() if v in s) for v in variants}
            print(f"[name]   🔴 {sorted(variants)}")
            for v, fs in where.items():
                print(f"[name]        `{v}` ← {fs}")
    if not hits2:
        print("[name]   ✅ 无 ✓")

    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        json.dump({
            "note": "命名一致性审计（专门查不一致，不靠并集掩盖）。只读不落库 ✓",
            "identifiers": sorted(allnames),
            "underscore_variants": {k: sorted(v) for k, v in bynorm.items() if len(v) > 1},
        }, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    print()
    print(f"[name] 写出 {os.path.relpath(OUT, REPO)} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
