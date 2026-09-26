#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""audit_target_enum_axes.py —— 核 **`Target` 枚举的 9 个取值是否都混了「轴/层」** ✓

🔴 **为什么做**（承接 `146_*.md §6` 的判据 242）：
   本件只核了 `MoraleMod`（发现"枚举按轴、落点在层"）⇒
   📌 **而 `Target` 共 9 个取值** ⇒ ✅ **其余 7 个也要看** ✓
   🎖️ **判据（第 242 条）**：**"一个枚举里混了维度 —— 那【其它取值】也可能混；
      要看的是【枚举本身】，不是逐个取值"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/audit_target_enum_axes.py
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

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(REPO, "darkest", "scripts", "data", "BuffPrimitiveTranslation.cs")


def main() -> int:
    t = io.open(SRC, encoding="utf-8", errors="replace").read()

    # ① 抽枚举的 9 个取值
    m = re.search(r"public enum Target\s*\{(.*?)\n    \}", t, re.S)
    members = re.findall(r"^\s*(\w+),", m.group(1), re.M) if m else []
    print(f"[ta] `Target` 枚举 ⇒ **{len(members)}** 个取值：{members}")
    print()

    # ② 抽 ByStatType 的分布
    pairs = re.findall(r'\["([a-z_0-9]+)"\]\s*=\s*Target\.(\w+)', t)
    dist = Counter(v for _, v in pairs)
    print("[ta] 🎖️ **各取值下的原语数**：")
    for k in members:
        print(f"[ta]   `{k}` ⇒ **{dist.get(k, 0)}**")
    print()

    # ③ 抽每个成员的定义注释（判它说的是"轴"还是"层"）
    print("[ta] 🔴 **逐取值看它的【定义注释】（判是轴还是层）**：")
    for k in members:
        mm = re.search(r"///\s*<summary>(.*?)</summary>\s*" + k + r",", t, re.S)
        doc = ""
        if mm:
            doc = re.sub(r"\s+", " ", mm.group(1)).strip()
        else:
            # 可能注释在枚举值前一行
            mm2 = re.search(r"///\s*(.*?)\n\s*" + k + r",", t)
            doc = mm2.group(1).strip() if mm2 else "**(无注释)**"
        print(f"[ta]   **`{k}`** ⇒ {doc[:130]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
