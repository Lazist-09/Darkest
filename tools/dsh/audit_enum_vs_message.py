#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_enum_vs_message.py —— 核 **"枚举式措辞"与"实际分支"是否一致** ✓

🔴 **为什么做**（承接 `152_*.md` 偶然发现的那 3 处）：
   我在 `UnlocksConfig.cs` **偶然**发现：**新增了一支分支，而注释/报错里的
   "合法：A／B／C"清单【没跟着加】** ⚠️
   ⇒ 📌 **那不是一处，是一类** ⇒ ✅ 本件**全仓扫** ✓
   🎖️ **判据（第 267 条）**：**"同一文件里【新增了一支】——
      那同一文件里的【清单式措辞】（注释/报错）跟着改了吗？"** ✓

🔴 **做法**：找形如「合法：A ／ B ／ C」的清单 ⇒ 抽出列出的项 ⇒
   与**同文件里 `StartsWith("…")` / `switch` 的 case** 对照 ⇒ 看有没有漏 ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_enum_vs_message.py
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
    print(f"[ev] 扫了 **{len(files)}** 个 `.cs` ✓")
    print()

    # 找「合法：」/「合法:」清单
    pat = re.compile(r"合法[：:]\s*([^\n\"]{0,200})")
    hits = 0
    for rel, t in sorted(files.items()):
        for m in pat.finditer(t):
            hits += 1
            listed = set(re.findall(r"`([A-Za-z_0-9:]+)`", m.group(1)))
            if not listed:
                listed = set(re.findall(r"([a-z_]+:)", m.group(1)))
            ln = t[:m.start()].count("\n") + 1
            # 同文件里 StartsWith 的实际分支
            actual = set(re.findall(r'StartsWith\(\s*"([^"]+)"', t))
            actual |= set(re.findall(r'case\s+"([^"]+)"', t))
            missing = {a for a in actual if any(a.startswith(x) or x.startswith(a)
                                                for x in listed)} - listed
            base = {x for x in listed if x.endswith(":")}
            extra = {a for a in actual if a.endswith(":")} - base
            tag = "🔴" if extra else "✅"
            print(f"[ev] {tag} {rel}:{ln}")
            print(f"[ev]      清单：{sorted(listed)}")
            print(f"[ev]      实际：{sorted(a for a in actual if a.endswith(':'))}")
            if extra:
                print(f"[ev]      ⇒ 🔴 **清单漏了：{sorted(extra)}**")
            print()
    print(f"[ev] 共找到 **{hits}** 处「合法：」清单 ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
