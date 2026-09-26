#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""find_validate_callers.py —— 找 **校验函数【被谁调用】** ✓

🔴 **为什么做**（承接 `133_*.md §5` 的判据 196）：
   我找到了校验函数（`Validate.ExpeditionSide.cs:550` 的 `throw`）⇒
   📌 **但"函数存在" ≠ "它被调用"** ⚠️
   ⇒ ✅ 本件找调用链，确认【启动时真跑】 ✓
   🎖️ **判据（第 196 条）**：**"校验函数存在 ≠ 它被调用；
      要再看【谁调它】"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/find_validate_callers.py
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
SC = os.path.join(REPO, "darkest", "scripts")


def main() -> int:
    files = {}
    for dp, _d, ns in os.walk(SC):
        for f in ns:
            if f.endswith(".cs"):
                p = os.path.join(dp, f)
                files[os.path.relpath(p, REPO)] = io.open(
                    p, encoding="utf-8", errors="replace").read()
    # ① 找校验类的方法名
    print("=== 🔴 `Validate.ExpeditionSide` 的方法签名 ===")
    for rel, t in sorted(files.items()):
        if "ExpeditionSide" not in rel:
            continue
        for i, l in enumerate(t.splitlines(), 1):
            if re.search(r"(public|internal|private|static).*\bValidate", l):
                print(f"  {rel}:{i}")
                print(f"      {l.strip()[:130]}")
    print()
    # ② 找谁调它
    print("=== 🔴 谁调用 `ExpeditionSide` 的校验 ===")
    for rel, t in sorted(files.items()):
        for i, l in enumerate(t.splitlines(), 1):
            if re.search(r"ValidateExpeditionSide|ExpeditionSide\.", l):
                print(f"  {rel}:{i}")
                print(f"      {l.strip()[:130]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
