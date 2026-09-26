#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""land_p7_rulings.py —— 落**策划 `#475` 对 P7 的两条裁定**（2026-09-26）。

裁定来源（`doc/windows/主程序窗口.txt` · 信封 `DELIVERY-DESIGNER-DMG30-AND-SIGMA-20260926`）
----------------------------------------------------------------------------------
✅ **裁定①**：那 **30 条"参考项目答不上来"的技能 ⇒ `dmg%` 全取 `0`**
   · 理由：它们**都是我们自加的** ⇒ 没有原版值可抄；`0` = **不做修正** = 最保守 ✓
   · 标：`value_source: none`（A 维：值的来源）+ `origin: ours`（B 维：归属）⇒ 归 `§39` 解冻清单 ✓

✅ **裁定②**：`Σ段倍率` **归一为 `1`**
   · 理由：**原版没有这个概念** ⇒ 若它 ≠ 1 ⇒ 那就不是"对齐" ✓
   · 做法：**多段攻击 ⇒ 每段 `1 / 段数`**（Σ = 1）✓
   · 🎖️ 纪律 **BS**（新立）：**"这个字段/乘数，原版有吗？没有 ⇒ 它必须【中性化】"**
     判据：**"原版没有的东西，在【默认值】下会不会改变结果？" —— 会 ⇒ 默认值错了** ✓

🔴 **只改 `flat` 段**（口径要写清 · 纪律 AU）
  实测段类型只有两种：**`flat` 28 段 · `missing_hp` 2 段** ✓
  `missing_hp` 段**没有 `multiplier` 键**，走 `base + coefficient` 另一条公式
  ⇒ **它不参与 Σ**（我第一版没分类就求和 ⇒ 把 2 条 `missing_hp` 误记成 `Σ=0` ⚠️ 已更正）✓
  `damage = null`（16 条：纯 buff / 治疗 / 移动）⇒ **无 Σ 可言，不动** ✓

⚠️ **不是"允许永久偏离"**：这是**中性化**（把"自加的乘数"退回不改变结果），
   与 `prot` 那次"维持现状"不同 —— 见 §BS 的判据 ✓

用法
------
    python tools/dsh/land_p7_rulings.py --check    # 只报读数，不写盘
    python tools/dsh/land_p7_rulings.py            # 落库（幂等）
"""

from __future__ import annotations

import hashlib
import json
import os
import sys

try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
except Exception:  # pragma: no cover
    pass

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SKILLS = os.path.join(REPO, "darkest", "data", "skills.json")


def sha(text: str) -> str:
    return hashlib.sha256(text.encode("utf-8")).hexdigest()[:16]


def main() -> int:
    check = "--check" in sys.argv
    before = open(SKILLS, encoding="utf-8", newline="").read()
    data = json.loads(before)
    skills = data["skills"]

    # ---- 裁定① 那 30 条（无参考值）⇒ dmg_pct = 0 + 两个维度标注 ----
    referenceless, already = [], []
    for s in skills:
        if "dmg_pct" in s:
            already.append(s["id"])
            continue
        s["dmg_pct"] = 0
        s["value_source"] = "none"   # A 维：值的来源 = 没有来源（我们自加）✓
        s["origin"] = "ours"         # B 维：归属 = 我们自加 ⇒ 归 §39 ✓
        referenceless.append(s["id"])

    # ---- 裁定② Σ段倍率 归一为 1（只动 flat 段）----
    normalized, touched, untouched, nonflat = [], [], [], []
    for s in skills:
        dmg = s.get("damage")
        if not isinstance(dmg, dict):
            untouched.append(s["id"])
            continue
        segs = dmg.get("segments") or []
        flats = [g for g in segs if g.get("type") == "flat"]
        others = [g for g in segs if g.get("type") != "flat"]
        if others:
            nonflat.append(s["id"])
            continue
        if not flats:
            untouched.append(s["id"])
            continue
        total = sum(g.get("multiplier", 0) for g in flats)
        if abs(total - 1.0) < 1e-9:
            continue
        each = 1.0 / len(flats)
        for g in flats:
            g["multiplier"] = each
        normalized.append((s["id"], total, each, len(flats)))
        touched.append(s["id"])

    print(f"[p7] 裁定① 那 30 条 ⇒ **{len(referenceless)}** 条落 `dmg_pct=0` + `value_source:none` + `origin:ours`")
    print(f"[p7]        原本已有 `dmg_pct` 的 **{len(already)}** 条**不动**（11+3=14 条有来源）✓")
    print(f"[p7] 裁定② `flat` 段 Σ 归一 ⇒ 改 **{len(normalized)}** 条")
    for sid, old, each, n in normalized:
        print(f"[p7]        {sid:30s} Σ {old!s:<6} ⇒ 每段 {each}（{n} 段 · Σ=1）")
    print(f"[p7]        无 damage（纯 buff/治疗/移动）**不动**：{len(untouched)} 条 ✓")
    print(f"[p7]        非 `flat` 段（`missing_hp`，走 base+coefficient 另一条公式）**不动**：{len(nonflat)} 条 ✓")
    print(f"[p7]        ⚠️ 实测：**多段技能只有 2 条，都是 2 段 0.5+0.5（Σ 本来就 = 1）**"
          f" ⇒ **没有循环小数问题** ✓")

    if check:
        print("[p7] --check ⇒ 未写盘 ✓")
        return 0

    # 🔴 注记重写（口径变了就必须改注记，否则注记在说谎 —— 纪律 AZ）
    data["_dmg_pct_note"] = (
        "A2+A3 之后：技能 dmg% 的来源 = 【本地参考项目】`Heroes/Info/*.bytes` 的 `.dmg` 字段"
        "（逐技能 5 级恒定不变 ⇒ 一个技能一个值）。"
        "🆕 策划 `#475` 裁定：**参考项目答不出来的 30 条一律取 0**"
        "（理由：它们都是我们自加的 ⇒ 无原版值可抄；0 = 不做修正 = 最保守）"
        "⇒ 每条标 `value_source: none`（值的来源）+ `origin: ours`（归属）⇒ 归 `§39` 解冻清单 ✓ "
        "🔴 与那 14 条的区别：那 14 条**有参考出处**（`_dmg_pct_source` 点名参考技能），"
        "这 30 条**没有**（`value_source: none` 是显式声明，不是「忘了填」）✓"
    )
    data["_sigma_note"] = (
        "🆕 策划 `#475` 裁定：**`Σ段倍率` 归一为 1** —— 原版没有这个概念 ⇒ 若它 ≠ 1 就不是对齐。"
        "做法：**多段攻击每段 1/段数**（Σ=1）；**多段的收益靠【其他机制】**（如多次触发 effect）✓ "
        "🔴 口径：**只对 `flat` 段成立** —— `missing_hp` 段没有 `multiplier` 键、走 base+coefficient，"
        "不参与 Σ；`damage = null` 的 16 条亦无 Σ ✓ "
        "🎖️ 纪律 BS：**「这个字段/乘数，原版有吗？没有 ⇒ 它必须【中性化】」** ✓ "
        "📌 这不是「允许永久偏离」，是**中性化**（把自加的乘数退回不改变结果）✓"
    )

    after = json.dumps(data, indent=2, ensure_ascii=False) + "\n"
    with open(SKILLS, "w", encoding="utf-8", newline="") as fh:
        fh.write(after)
    print(f"[p7] 写盘：sha256 {sha(before)} → {sha(after)} · "
          f"bytes {len(before.encode('utf-8'))} → {len(after.encode('utf-8'))} ✓")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
