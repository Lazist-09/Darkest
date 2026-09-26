#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""compare_bonus_mp.py —— 比 **单机 vs 联机** 的 `BonusTurn` / `MonsterTurn` ✓

🔴 **要验的怀疑**（`83_*.md §5`）：
   联机侧也有 3 处 `CombatSkillOverride`（`RaidSceneMultiplayerManager` 646/955/987）
   ⇒ 📌 **怀疑：又是"同一逻辑的第二份实现"**（与 `36_actout_multiplayer.md` 同族）✓
   ⇒ ✅ 本件把两边的 `BonusTurn` 与调用点**逐项比** ✓

🎖️ **判据（第 43 条）**：**"这两份实现，是【逐字复制】还是【有差异】？
   ⇒ 差异要在【守卫条件】与【跳过项】上找"** ✓

🔴 **只读不落库** ⇒ 零行为 ✓
用法：python tools/dsh/compare_bonus_mp.py
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
SP = os.path.join(R, "Managers", "RaidSceneManager.cs")
MP = os.path.join(R, "Networking", "RaidSceneMultiplayerManager.cs")


def show(path, pat, ctx=2, limit=20):
    lines = io.open(path, encoding="utf-8-sig", errors="replace").read().splitlines()
    out = []
    for i, l in enumerate(lines, 1):
        if re.search(pat, l):
            out.append((i, "\n".join(lines[max(0, i - 1 - ctx):i + ctx])))
            if len(out) >= limit:
                break
    return out


def main() -> int:
    print("=== 🔴 单机 `RaidSceneManager`：BonusTurn 相关 ===")
    for i, seg in show(SP, r"BonusTurn|fromBonusTurn|CombatSkillOverride"):
        print(f"--- L{i} ---")
        for s in seg.splitlines():
            print(f"    {s.rstrip()[:135]}")
    print()

    print("=== 🔴 联机 `RaidSceneMultiplayerManager`：BonusTurn 相关 ===")
    for i, seg in show(MP, r"BonusTurn|fromBonusTurn|CombatSkillOverride"):
        print(f"--- L{i} ---")
        for s in seg.splitlines():
            print(f"    {s.rstrip()[:135]}")
    print()

    # 计数字段
    for name, p in (("单机", SP), ("联机", MP)):
        t = io.open(p, encoding="utf-8-sig", errors="replace").read()
        print(f"[cmp] **{name}**：`BonusTurn` ×{t.count('BonusTurn')} · "
              f"`fromBonusTurn` ×{t.count('fromBonusTurn')} · "
              f"`CombatSkillOverride` ×{t.count('CombatSkillOverride')} · "
              f"`MonsterTurn(` ×{t.count('MonsterTurn(')}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
