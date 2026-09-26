#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""read_event_queue.py —— 读 **`EventQueue`（延迟效果队列）** 的入口与消费 ✓

🔴 **为什么做**（承接 `99_*.md §6①`）：
   `99_*.md` 发现 `#region Effects` 处理 `eventUnit.EventQueue[0]` ⇒ `Execute()`
   ⇒ 📌 **那是"延迟效果"这一层，与 A6a 的 Effect 直接相关** ✓
   ⇒ ✅ 本件读出**入队点**（谁往里加）与**它的语义** ✓

🎖️ **判据（第 83 条）**：**"这个队列的【入队点】在哪？
   ⇒ 读【谁往里加】，而不是只看【谁取出来】"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/read_event_queue.py
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

R = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"


def main() -> int:
    print("=" * 78)
    print("① `EventQueue` 的全部引用（含 `EventQueue.Add` / `.Count` / `[0]`）")
    print("=" * 78)
    for dirpath, _d, ns in os.walk(R):
        for f in ns:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, R)
            t = io.open(p, encoding="utf-8-sig", errors="replace").read()
            for i, l in enumerate(t.splitlines(), 1):
                if re.search(r"EventQueue", l):
                    print(f"  {rel}:{i}")
                    print(f"      {l.strip()[:130]}")
    print()

    print("=" * 78)
    print("② `Effect.Execute()` 的定义（它做什么）")
    print("=" * 78)
    p = os.path.join(R, "Mechanics", "Skills", "Effect.cs")
    lines = io.open(p, encoding="utf-8-sig", errors="replace").read().splitlines()
    for i, l in enumerate(lines, 1):
        if re.search(r"(public|protected|private).*\bExecute\s*\(", l):
            print(f"  {i:4d}|{l.strip()[:130]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
