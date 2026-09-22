#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""make_skill_mapping_proposal.py -- draft OUR 44 skills -> reference-project skill dmg% mapping.

WHY
  The planner asked to substitute the reference project's values for the pending numbers.  For skill
  `dmg%` a *mechanical* substitution is impossible: our 44 skills are self-authored names while the
  reference keeps the original's names (measured: 5/44 match by any name rule).  So instead of
  inventing a mapping, this tool produces a **reviewable draft table** -- nothing is written into the
  game data.

TWO JUDGEMENTS PER ROW (both transparent, both auditable)
  1. NAME  overlap(skill) = |tokens(ours) INTERSECT tokens(reference)| / min(|ours|, |reference|)
     >= 0.5 "name-credible" -- (0, 0.5) "weak" -- no overlap "needs a human pick".
  2. TYPE  ours `range_axis` (melee/ranged) vs the reference's `.type` (melee/ranged).
     A name match whose TYPE differs is downgraded to **suspect** -- measured example:
     `warrior_battle_fury` name-matches `battle_ballad` (a Jester song) but the types disagree.

USAGE
  python tools/dsh/extract_dd1_skills.py            # refresh the reference pool first (writes %TEMP%/ref_skills.csv)
  python tools/dsh/make_skill_mapping_proposal.py   # writes reports/skill_dmg_mapping_proposal.md
EXIT: 0 always (report generator); prints the coverage readout.
"""

from __future__ import annotations

import csv
import io
import json
import os

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SKILLS = os.path.join(REPO, "darkest", "data", "skills.json")
POOL = os.path.join(os.environ.get("TEMP", "/tmp"), "ref_skills.csv")
OUT = os.path.join(REPO, "reports", "skill_dmg_mapping_proposal.md")

DASH = "\u2014"          # em dash (built from an escape so no shell/encoding can mangle it)
OK = "\u2705"            # check mark
WARN = "\u26a0\ufe0f"    # warning sign
BAD = "\U0001f534"       # red circle


def tokens(name: str) -> set[str]:
    return {t for t in name.lower().split("_") if t}


def load_ours() -> list[tuple[str, str, str]]:
    """-> [(id, range_axis, target_side)]"""
    obj = json.load(io.open(SKILLS, encoding="utf-8"))
    skills = obj["skills"] if isinstance(obj, dict) else obj
    out = []
    for s in skills:
        tgt = s.get("target") or {}
        out.append((s["id"], str(s.get("range_axis") or ""), str(tgt.get("side") or "")))
    return out


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
    for oid, orange, oside in ours:
        ot = tokens(oid)
        best, best_score = None, 0.0
        for rid in dmg:
            inter = ot & tokens(rid)
            if not inter:
                continue
            score = len(inter) / max(1, min(len(ot), len(tokens(rid))))
            if score > best_score:
                best_score, best = score, rid
        ref_type = typ.get(best, "") if best else ""
        type_ok = bool(best) and bool(orange) and bool(ref_type) and orange == ref_type
        rows.append((oid, orange, oside, best, round(best_score, 2), dmg.get(best) if best else None,
                     ref_type, type_ok))

    name_ok = [r for r in rows if r[4] >= 0.5]
    credible = [r for r in name_ok if r[7]]
    suspect = [r for r in name_ok if not r[7]]
    weak = [r for r in rows if 0 < r[4] < 0.5]
    none = [r for r in rows if not r[3]]

    L: list[str] = []
    L.append("# 技能 `dmg%` 映射草案（主程序提案 · **未落库** · 待策划确认）")
    L.append("")
    L.append("> 🔴 **为什么必须由你点名**：我们 44 个技能是**自研命名**，参考项目保留**原版技能名**")
    L.append(">   ⇒ 实测机械匹配只覆盖 **5/44** ⇒ 我不编映射 ✗，改为给你一张**可直接改的表** ✓")
    L.append("> 🔴 **本文件只是提案**：没有改任何数据 ✓；你确认/改写后我一次落库 + 附前后读数 ✓")
    L.append("> 📌 **两个判据（都透明可审）**：")
    L.append(">   ① **名字重叠度** = |交集| ÷ min(|我们|, |参考|) ⇒ ≥0.5 记「名字可信」")
    L.append(">   ② **类型一致性**：我们的 `range_axis`（melee/ranged）vs 参考的 `.type`")
    L.append(">   ⇒ 🔴 **名字过、但类型不符 ⇒ 降级为「可疑」**（实测例：`warrior_battle_fury` 名字匹到")
    L.append(">     `battle_ballad`＝小丑的歌，类型不符 ✗ —— 这就是为什么必须有第②条 ✓）")
    L.append("")
    L.append("## 覆盖统计（当场实测）")
    L.append("")
    L.append(f"· 我们 = **{len(ours)}** 个技能 · 参考项目 level-0 = **{len(dmg)}** 个技能")
    L.append(f"· ✅ **名字+类型都过（可用）= {len(credible)}** · "
             f"⚠️ **名字过但类型不符（可疑）= {len(suspect)}** · "
             f"名字弱 = **{len(weak)}** · **无候选（需点名）= {len(none)}**")
    L.append("")
    L.append("## 逐技能提案表")
    L.append("")
    L.append("| 我们的技能 | 我们类型 | 目标侧 | 建议参考技能 | 名字分 | 参考类型 | 参考 `dmg%` | 判定 | 备注 |")
    L.append("|---|---|---|---|---|---|---|---|---|")
    for oid, orange, oside, rid, score, d, rt, ok in rows:
        if not rid:
            verdict, note = f"{BAD} 需点名", "**参考项目无同名/近名技能 ⇒ 请直接给数**"
        elif score < 0.5:
            verdict, note = f"{WARN} 名字弱", "请你确认或直接改"
        elif not ok:
            verdict, note = f"{WARN} **可疑（类型不符）**", f"我们 {orange or '?'} vs 参考 {rt or '?'} ⇒ **请核对**"
        else:
            verdict, note = f"{OK} 可用", ""
        L.append(f"| `{oid}` | {orange or DASH} | {oside or DASH} | "
                 f"{('`' + rid + '`') if rid else DASH} | {score} | {rt or DASH} | "
                 f"{(str(d) + '%') if d is not None else DASH} | {verdict} | {note} |")
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
    print(f"[map] name>=0.5: {len(name_ok)}  => credible(type ok) {len(credible)} / suspect(type differs) {len(suspect)}")
    print(f"[map] weak {len(weak)} / needs-pick {len(none)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
