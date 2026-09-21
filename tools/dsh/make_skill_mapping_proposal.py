#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""make_skill_mapping_proposal.py -- draft OUR 44 skills -> reference-project skill dmg% mapping.

WHY
  The planner asked to substitute the reference project's values (F:\\GithubPro\\Darkest-Dungeon-Unity)
  for the pending numbers.  For skill `dmg%` a *mechanical* substitution is impossible: our 44 skills
  are self-authored names while the reference keeps the original's names (measured: 5/44 match by any
  name rule).  So instead of inventing a mapping, this tool produces a **reviewable draft table** with a
  transparent rule, for the planner to confirm or edit.  Nothing is written into the game data.

RULE (transparent, auditable)
  overlap(skill) = |tokens(ours) INTERSECT tokens(reference)| / min(|tokens(ours)|, |tokens(reference)|)
  confidence: >= 0.5 "credible" -- (0, 0.5) "weak" -- no overlap "needs a human pick".

USAGE
  python tools/dsh/extract_dd1_skills.py            # refresh the reference pool first (optional)
  python tools/dsh/make_skill_mapping_proposal.py   # writes reports/skill_dmg_mapping_proposal.md
EXIT: 0 always (it is a report generator); prints the coverage readout.
"""

from __future__ import annotations

import csv
import io
import json
import os
import re

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SKILLS = os.path.join(REPO, "darkest", "data", "skills.json")
POOL = os.path.join(os.environ.get("TEMP", "/tmp"), "ref_skills.csv")
OUT = os.path.join(REPO, "reports", "skill_dmg_mapping_proposal.md")

DASH = "\u2014"          # em dash, built from an escape so no shell/encoding mangles it
OK = "\u2705"            # check mark
WARN = "\u26a0\ufe0f"    # warning sign
BAD = "\U0001f534"       # red circle


def tokens(name: str) -> set[str]:
    return {t for t in name.lower().split("_") if t}


def load_ours() -> list[str]:
    obj = json.load(io.open(SKILLS, encoding="utf-8"))
    skills = obj["skills"] if isinstance(obj, dict) else obj
    return [s["id"] for s in skills]


def load_pool() -> tuple[dict[str, int], dict[str, str]]:
    dmg: dict[str, int] = {}
    typ: dict[str, str] = {}
    with io.open(POOL, encoding="utf-8") as fh:
        for row in csv.DictReader(fh):
            if int(row["Lv"]) == 0:
                dmg[row["Id"]] = int(row["Dmg"])
                typ[row["Id"]] = row["Type"]
    return dmg, typ


def main() -> int:
    ours = load_ours()
    dmg, typ = load_pool()

    rows = []
    for oid in ours:
        ot = tokens(oid)
        best, best_score = None, 0.0
        for rid in dmg:
            inter = ot & tokens(rid)
            if not inter:
                continue
            score = len(inter) / max(1, min(len(ot), len(tokens(rid))))
            if score > best_score:
                best_score, best = score, rid
        rows.append((oid, best, round(best_score, 2), dmg.get(best) if best else None,
                     typ.get(best) if best else None))

    strong = [r for r in rows if r[2] >= 0.5]
    weak = [r for r in rows if 0 < r[2] < 0.5]
    none = [r for r in rows if not r[1]]

    L: list[str] = []
    L.append("# 技能 `dmg%` 映射草案（主程序提案 · **未落库** · 待策划确认）")
    L.append("")
    L.append("> 🔴 **为什么必须由你点名**：我们 44 个技能是**自研命名**，参考项目保留**原版技能名**")
    L.append(">   ⇒ 实测机械匹配只覆盖 **5/44** ⇒ 我不编映射 ✗，改为给你一张**可直接改的表** ✓")
    L.append("> 🔴 **本文件只是提案**：我没有改任何数据 ✓；你确认/改写后我一次落库 + 附前后读数 ✓")
    L.append("> 📌 规则（透明可审）：重叠度 = |交集| ÷ min(|我们|, |参考|) ⇒ ≥0.5 记「较可信」，")
    L.append(">   0~0.5 记「弱」，无交集记「**需点名**」 ✓")
    L.append("")
    L.append("## 覆盖统计（当场实测）")
    L.append("")
    L.append(f"· 我们 = **{len(ours)}** 个技能 · 参考项目 level-0 = **{len(dmg)}** 个技能")
    L.append(f"· 较可信（≥0.5）= **{len(strong)}** · 弱匹配 = **{len(weak)}** · **无候选（需点名）= {len(none)}**")
    L.append("")
    L.append("## 逐技能提案表")
    L.append("")
    L.append("| 我们的技能 | 建议参考技能 | 置信度 | 参考 `dmg%` | 类型 | 备注 |")
    L.append("|---|---|---|---|---|---|")
    for oid, rid, score, d, t in rows:
        if score >= 0.5:
            conf = f"{OK} 较可信"
            note = ""
        elif rid:
            conf = f"{WARN} 弱"
            note = "请你确认或直接改"
        else:
            conf = f"{BAD} 需点名"
            note = "**参考项目无同名/近名技能 ⇒ 请直接给数**"
        refcell = f"`{rid}`" if rid else DASH
        dcell = f"{d}%" if d is not None else DASH
        L.append(f"| `{oid}` | {refcell} | {conf} {score} | {dcell} | {t or DASH} | {note} |")
    L.append("")
    L.append("## 你确认后我怎么落库")
    L.append("```")
    L.append("① 你确认/改写本表（或只给「我们的技能 id = dmg%」44 行）")
    L.append("② 我把 `dmg_pct` 写进 darkest/data/skills.json（每行带来源标记 ✓）")
    L.append("③ 附【前后读数】：用已备好的对照夹具跑 旧/新 两列 + 全量测试读数 ✓")
    L.append("④ 写进 tools/dsh/reference_placeholders.md 替换清单（后续要改时一处可查 ✓）")
    L.append("```")

    io.open(OUT, "w", encoding="utf-8").write("\n".join(L))
    print(f"[map] wrote {os.path.relpath(OUT, REPO)}")
    print(f"[map] credible {len(strong)} / weak {len(weak)} / needs-pick {len(none)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
