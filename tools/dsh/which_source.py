#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""which_source.py —— 对未标注文件：判它的对应物在【哪个源】里 ✓

🔴 **为什么做**（承接 `163_*.md` 的判据 324）：
   我说"参考里有对应物" —— 📌 **而"参考"指两个源**（本地参考项目 / 一手 E 盘）⚠️
   ⇒ 🔴 **这正是我上一件误判 `quirks` 的根源** ⇒ ✅ 本件**逐层分清** ✓
   🎖️ **判据（第 324 条）**：**"我说'参考里有对应物'——
      是哪个参考？（两个源都有不同的东西）"** ✓

🔴 **做法**：对每个层 ⇒ 在**两个源里各搜一次** ⇒ 报"哪个源有" ✓
🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/which_source.py
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

REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
# 🔴 一手只在【数据子目录】搜 —— 全盘 walk 会超时（实测 >600s）✓
FIRST = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
FIRST_SUBDIRS = ["shared", "campaign", "upgrades", "dungeons", "heroes", "monsters",
                 "scripts", "panels", "raid"]

# 我方未标注（或含对应物）的层 ⇒ 探针词 ⇒ 两源各搜
LAYERS = {
    "camp_skills": ["camping_skill", "camp_skill"],
    "curios": ["curio"],
    "enemy_ai": ["brains", "ai_", "taunt"],
    "expedition_nodes": ["expedition", "node_type"],
    "formation": ["formation", "party"],
    "morale_events": ["stress", "affliction"],
    "tuning": ["physical_mitigation", "morale"],
    "economy": ["gold", "heirloom"],
    "roster": ["roster"],
    "sanitarium": ["sanitarium", "disease"],
    "unlocks": ["unlock"],
    "encounters": ["encounter"],
    "room_contents": ["room_contents", "weight"],
    "traits": ["quirk", "affliction", "virtue"],
    "trap_defs": ["trap"],
}


def scan(root, needles):
    hits = {}
    if not os.path.isdir(root):
        return hits
    for dp, _d, ns in os.walk(root):
        # 🔴 只在浅层（≤4 层）找 —— 避免全盘 walk 超时 ✓
        if dp[len(root):].count(os.sep) > 4:
            _d[:] = []
            continue
        for f in ns:
            if f.endswith(".meta") or f.endswith(".dll") or f.endswith(".pck"):
                continue
            p = os.path.join(dp, f)
            try:
                if os.path.getsize(p) > 4_000_000:
                    continue
                t = io.open(p, encoding="utf-8-sig", errors="replace").read()
            except Exception:
                continue
            for nd in needles:
                if re.search(nd, t, re.I):
                    hits.setdefault(nd, []).append(os.path.relpath(p, root))
    return hits


def scan_first(needles):
    """一手：只扫 FIRST_SUBDIRS 下的浅层 ✓"""
    hits = {}
    for sub in FIRST_SUBDIRS:
        root = os.path.join(FIRST, sub)
        if not os.path.isdir(root):
            continue
        for k, v in scan(root, needles).items():
            hits.setdefault(k, []).extend(f"{sub}/{x}" for x in v)
    return hits


def main() -> int:
    print(f"[ws] 参考项目：`{REF}` 存在={os.path.isdir(REF)}")
    print(f"[ws] 一手 E 盘：`{FIRST}` 存在={os.path.isdir(FIRST)}")
    print()
    print("[ws] 🔴 **逐层的对应物在哪个源**：")
    print()
    for layer, needles in LAYERS.items():
        r = scan(REF, needles)
        f = scan_first(needles)
        rn = sum(len(v) for v in r.values())
        fn = sum(len(v) for v in f.values())
        if rn and not fn:
            tag, who = "🎖️", "**只在【参考项目】**"
        elif fn and not rn:
            tag, who = "⚠️", "**只在【一手 E 盘】**"
        elif rn and fn:
            tag, who = "🔴", "**两源都有**"
        else:
            tag, who = "⚪", "**两源都无**（我方自造）"
        print(f"[ws]   {tag} `{layer}` ⇒ {who}　（参考 {rn} 命中 / 一手 {fn} 命中）")
        if rn and not fn:
            for nd, fl in r.items():
                print(f"[ws]         `{nd}` ⇒ {fl[:2]}")
        elif fn and not rn:
            for nd, fl in f.items():
                print(f"[ws]         `{nd}` ⇒ {fl[:2]}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
