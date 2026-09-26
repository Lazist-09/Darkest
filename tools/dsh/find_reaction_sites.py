#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""find_reaction_sites.py —— 全盘找 **反应 act-out 的实现点**（含联机的别处）✓

🔴 **要关的口子**（`86_*.md §6`）：
   实测：`ReactionType.` **单机 21 处 · 联机 1 处**
   ⇒ 🔴 但我**只查了 `RaidSceneMultiplayerManager`** ⚠️
   ⇒ ✅ 本件**全盘搜**，看联机侧是否在【别的文件】实现了反应 ✓

🎖️ **判据（第 49 条）**：**"A 少 B 多 —— 是【B 真的残缺】还是【B 写在别处】？
   ⇒ 全盘搜【符号】而不是只看一个文件"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/find_reaction_sites.py
"""

from __future__ import annotations

import io
import os
import re
import sys
from collections import Counter, defaultdict

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

ROOT = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"


def main() -> int:
    per_file = defaultdict(Counter)
    for dirpath, _d, ns in os.walk(ROOT):
        for f in ns:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, ROOT)
            t = io.open(p, encoding="utf-8-sig", errors="replace").read()
            for m in re.finditer(r"ReactionType\.(\w+)", t):
                per_file[rel][m.group(1)] += 1
            # 也搜 string 形式的反应名
            for m in re.finditer(r'"(block_\w+|comment_\w+)"', t):
                per_file[rel]["<str:" + m.group(1) + ">"] += 1

    print(f"[r] 全盘 **{len(per_file)}** 个文件提到反应符号 ✓")
    print()
    print("[r] 🎖️ 按【文件】列出（降序）：")
    for rel, c in sorted(per_file.items(), key=lambda x: -sum(x[1].values())):
        tot = sum(c.values())
        kinds = {k: v for k, v in c.items()}
        print(f"[r]   **{rel}** ⇒ {tot} 处")
        print(f"[r]       {kinds}")
    print()
    # 汇总
    allk = Counter()
    for c in per_file.values():
        allk.update(c)
    print(f"[r] 🎖️ 全盘反应符号总表（{len(allk)} 种）：")
    for k, v in allk.most_common():
        print(f"[r]   `{k}` ×{v}")
    print()
    # 单机 vs 联机文件
    SPF = os.path.join("Managers", "RaidSceneManager.cs")
    MPF = os.path.join("Networking", "RaidSceneMultiplayerManager.cs")
    print(f"[r] 🔴 单机（`{SPF}`）：{sum(per_file.get(SPF, {}).values())} 处")
    print(f"[r] 🔴 联机（`{MPF}`）：{sum(per_file.get(MPF, {}).values())} 处")
    others = {k: v for k, v in per_file.items() if k not in (SPF, MPF)}
    print(f"[r] 🎖️ **别的文件**：{len(others)} 个 ⇒ "
          f"{ {k: sum(v.values()) for k, v in others.items()} }")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
