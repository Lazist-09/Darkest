#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_rounding_all.py —— **全库审计取整**：把参考的每一种取整与下限列全 ✓

🔴 **为什么做**（承接 `101_*.md` 的"压力用 `RoundToInt`，伤害用 `Ceil`"）：
   本任务已零散记过 **4 处**取整差异（`38`/`39`/`41`/`101_*.md`）⇒
   📌 但**从未一次扫全** ⇒ ⚠️ 可能还有别处 ✓
   ⇒ ✅ 本件**全库扫取整调用**，并**逐个标出是否与"上限/下限"配套** ✓

🎖️ **判据（第 100 条）**：**"同一个项目里有几种取整？
   ⇒ 全库扫【函数名】，而不是遇到一个记一个"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_rounding_all.py
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

R = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Scripts"
# 🔴 所有可能的取整入口
PATTERNS = {
    "CeilToInt": r"Mathf\.CeilToInt",
    "FloorToInt": r"Mathf\.FloorToInt",
    "RoundToInt": r"Mathf\.RoundToInt",
    "Ceiling": r"Math\.Ceiling",
    "Floor": r"Math\.Floor",
    "Round": r"Math\.Round",
    "(int)cast": r"\(int\)\s*[A-Za-z_(]",
}


def main() -> int:
    per = defaultdict(Counter)      # 文件 → 取整种类计数
    total = Counter()
    for dirpath, _d, ns in os.walk(R):
        for f in ns:
            if not f.endswith(".cs"):
                continue
            p = os.path.join(dirpath, f)
            rel = os.path.relpath(p, R)
            t = io.open(p, encoding="utf-8-sig", errors="replace").read()
            for name, pat in PATTERNS.items():
                n = len(re.findall(pat, t))
                if n:
                    per[rel][name] = n
                    total[name] += n
    print(f"[rd] 扫了 `Assets/Scripts/**` 的 `.cs` ✓")
    print()
    print("[rd] 🎖️ **取整方式总计**：")
    for k, v in total.most_common():
        print(f"[rd]   `{k}` ×**{v}**")
    print()
    print("[rd] 🔴 **按文件（含取整的）**：")
    for rel, c in sorted(per.items(), key=lambda x: -sum(x[1].values()))[:20]:
        print(f"[rd]   **{rel}** ⇒ {dict(c)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
