#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_pending_landed.py —— 核 **`Pending` 24 个原语「接上了没有」** ✓

🔴 **为什么做**（承接 `136_*.md §6` 的判据 207）：
   `ValidateAgainst` 保证的是 **"名字在参考里存在"** ⇒
   📌 **而"接上了没有"是【另一层】** ⚠️
   ⇒ ✅ 本件核：那 24 个 `Pending` 名字**在消费侧有没有对应** ✓
   🎖️ **判据（第 207 条）**：**"这个校验保证的是【名字存在】——
      那【接上了没有】是另一层，谁保证？"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/check_pending_landed.py
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
SC = os.path.join(REPO, "darkest", "scripts")


def main() -> int:
    t = io.open(SRC, encoding="utf-8", errors="replace").read()
    act = re.search(r"Activated \{ get; \} = new\[\] \{(.*?)\};", t, re.S)
    pen = re.search(r"Pending \{ get; \} = new\[\]\s*\{(.*?)\};", t, re.S)
    activated = re.findall(r'"([a-z_0-9]+)"', act.group(1)) if act else []
    pending = re.findall(r'"([a-z_0-9]+)"', pen.group(1)) if pen else []
    print(f"[pl] `Activated` ⇒ **{len(activated)}**：{activated}")
    print(f"[pl] `Pending`   ⇒ **{len(pending)}** ✓")
    print()

    # 读全部脚本
    files = {}
    for dp, _d, ns in os.walk(SC):
        for f in ns:
            if f.endswith(".cs"):
                p = os.path.join(dp, f)
                files[os.path.relpath(p, REPO)] = io.open(
                    p, encoding="utf-8", errors="replace").read()

    print("[pl] 🔴 **每个 `Pending` 名字在【消费侧】出现几次**（排除本文件）：")
    for n in pending:
        hits = []
        for rel, txt in files.items():
            if "BuffPrimitiveTranslation" in rel:
                continue
            for i, l in enumerate(txt.splitlines(), 1):
                if re.search(r"\b" + re.escape(n) + r"\b", l):
                    hits.append((rel, i, l.strip()[:88]))
        tag = "✅" if hits else "🔴"
        print(f"[pl]   {tag} `{n}` ⇒ **{len(hits)}** 处")
        for h in hits[:1]:
            print(f"[pl]         {h[0]}:{h[1]}  {h[2]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
