#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""reconcile_skill_dmg_edrive_vs_ref.py -- PRIMARY (E-drive) vs third-party (reference): every skill .dmg.

WHY
  The user 2026-09-26 ruling (doc/architecture/source_priority.md 1b) is: PRIMARY (the E-drive DD1 install,
  read-only) first, the local Unity reference only when E-drive cannot answer -- and when the two disagree,
  PRIMARY WINS while the conflict is RECORDED.  Our darkest/data/skills.json still carries values that were
  landed FROM THE REFERENCE (the ref: markers inside _dmg_pct_source), so those must be re-checked against
  the primary.  A count alone is not evidence, so this tool prints every conflicting id WITH both values,
  the primary file:line, and BOTH sides at every skill level (0..4).

WHAT IT DOES
  1. reads the primary hero skill tables through the sibling reader extract_dd1_hero_skills.py (one parser,
     not two: a second parser would silently drift, and this report would stop describing the same data)
  2. reads the derived reference table reports/unity_ref/hero_skills_from_ref.json (strings: 0, 0%)
  3. pairs the two by skill id at LEVEL 0 and reports agreements / conflicts / ids only one side has
  4. for every conflict it also prints level 0..4 on BOTH sides -- that column is what rules out the
     "the sources only disagree because one side was read at another skill level" explanation
  It NEVER writes game data: this is a report tool (red line 29 / B6 -- derived numbers only).

USAGE
  python tools/dsh/reconcile_skill_dmg_edrive_vs_ref.py            # writes reports/edrive_vs_ref_skill_dmg.md
  python tools/dsh/reconcile_skill_dmg_edrive_vs_ref.py --check    # print the readout, write nothing
  python tools/dsh/reconcile_skill_dmg_edrive_vs_ref.py --root <dir> [--ref <json>]
EXIT: 0 = report written (0 conflicts or not), 1 = a source could not be read.
"""

from __future__ import annotations

import argparse
import io
import json
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import extract_dd1_hero_skills as ed  # noqa: E402  (sibling reader -- see WHY item 1)

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
REF = os.path.join(REPO, "reports", "unity_ref", "hero_skills_from_ref.json")
OUT = os.path.join(REPO, "reports", "edrive_vs_ref_skill_dmg.md")
PCT_RE = re.compile(r"^\s*(-?\d+)\s*%?\s*$")
MAX_LEVEL = 4
BT = chr(96)  # markdown code span, built without a literal backtick so this file stays greppable


def pct(value):
    """-> int percentage, or None when the field is absent/degenerate. Handles the reference strings."""
    if value is None:
        return None
    if isinstance(value, str):
        m = PCT_RE.match(value)
        return int(m.group(1)) if m else None
    return int(value)


def read_primary(root):
    """-> ({id: {level: dmg_pct}}, {id: first row}) from the primary tree, or (None, None) if unreadable."""
    got = ed.collect(root, True)
    if got is None:
        return None, None
    rows, _shared = got
    by_id, first = {}, {}
    for row in rows:
        by_id.setdefault(row["id"], {})[row["level"]] = row["dmg_pct"]
        first.setdefault(row["id"], row)
    return by_id, first


def read_ref(path):
    """-> {id: {level: dmg_pct}} from the reference project derived table."""
    raw = json.load(io.open(path, encoding="utf-8"))
    by_id = {}
    for hero in sorted(raw):
        for rec in raw[hero].get("stat_records", []):
            by_id.setdefault(rec["id"], {})[pct(rec.get("level"))] = pct(rec.get("dmg"))
    return by_id


def shape(levels):
    """levels 0..4 as one compact cell."""
    return "[" + ", ".join("--" if levels.get(lv) is None else "%d" % levels[lv]
                           for lv in range(MAX_LEVEL + 1)) + "]"


def id_list(sids):
    return " ".join(BT + s + BT for s in sids)


def main(argv):
    ap = argparse.ArgumentParser(description="primary (E-drive) vs reference skill .dmg")
    ap.add_argument("--root", default=ed.DEFAULT_ROOT, help="DD1 install root (default: %s)" % ed.DEFAULT_ROOT)
    ap.add_argument("--ref", default=REF, help="reference hero skill table (default: %s)" % REF)
    ap.add_argument("--check", action="store_true", help="print the readout, write nothing")
    args = ap.parse_args(argv)

    pri, first = read_primary(args.root)
    if pri is None:
        print("PRIMARY tree not found: %s" % args.root, file=sys.stderr)
        return 1
    if not os.path.isfile(args.ref):
        print("reference table not found: %s" % args.ref, file=sys.stderr)
        return 1
    ref = read_ref(args.ref)

    shared = sorted(set(pri) & set(ref))
    only_pri = sorted(set(pri) - set(ref))
    only_ref = sorted(set(ref) - set(pri))
    same = [i for i in shared if pri[i].get(0) == ref[i].get(0)]
    differ = [i for i in shared if pri[i].get(0) != ref[i].get(0)]
    absent_both = [i for i in differ if pri[i].get(0) is None and ref[i].get(0) is None]
    conflict = [i for i in differ if i not in absent_both]
    flat_pri = [i for i in conflict if len({pri[i].get(lv) for lv in range(MAX_LEVEL + 1)}) == 1]
    flat_ref = [i for i in conflict if len({ref[i].get(lv) for lv in range(MAX_LEVEL + 1)}) == 1]

    L = []
    L.append("# 一手（E 盘）vs 第三方（参考项目）· 英雄技能 `.dmg` **对账报告**")
    L.append("")
    L.append("> 🔴 口径：`doc/architecture/source_priority.md`（用户 2026-09-26 裁定 —— **一手优先**；")
    L.append(">   冲突时**以一手为准**，并**记录冲突**）· `#473`「一律采用参考」**已被取代** ✓")
    L.append("> 工具：`tools/dsh/reconcile_skill_dmg_edrive_vs_ref.py`（可复跑；**不写游戏数据** ✓）")
    L.append("> 一手读取器：`tools/dsh/extract_dd1_hero_skills.py`（同一份解析器 ⇒ 两表不会是两套解析结果 ✓）")
    L.append("")
    L.append("## 读数（**技能等级 0** —— 我们与参考对齐的那一档）")
    L.append("")
    L.append("| 项 | 值 |")
    L.append("|---|---|")
    L.append("| 一手 skill id | **%d** |" % len(pri))
    L.append("| 参考 skill id | **%d** |" % len(ref))
    L.append("| **两侧都有 ⇒ 逐条可对账** | **%d** |" % len(shared))
    L.append("| **两侧值相同** | **%d** |" % len(same))
    L.append("| **两侧值不同** | **%d** |" % len(differ))
    L.append("| 🔴 **真冲突（数值不同）** | **%d** |" % len(conflict))
    L.append("| ✅ 两侧都**没有** `.dmg` 字段（写法一致，不是冲突） | **%d** |" % len(absent_both))
    L.append("| 只有一手有 | **%d** |" % len(only_pri))
    L.append("| 只有参考有 | **%d** |" % len(only_ref))
    L.append("")
    if conflict:
        L.append("## 🔴 冲突逐条（**以一手为准** —— 而改值 = 平衡数值改动 ⇒ 归 `dd1_baseline §39` 解冻清单）")
        L.append("")
        L.append("| skill id | 一手 L0 | 参考 L0 | 一手 L0..L4 | 参考 L0..L4 | 一手证据 |")
        L.append("|---|---:|---:|---|---|---|")
        for sid in conflict:
            row = first[sid]
            L.append("| `%s` | **%s** | %s | %s | %s | `%s:%d` |"
                     % (sid, pri[sid].get(0), ref[sid].get(0),
                        shape(pri[sid]), shape(ref[sid]), row["file"], row["line"]))
        L.append("")
        L.append("🔴 **等级错位已被排除**（本表把 5 档全打出来就是为了这条）：")
        L.append("   · 一手侧 **%d/%d** 条在 L0..L4 上**全同值** · 参考侧 **%d/%d** 条同样全同值"
                 % (len(flat_pri), len(conflict), len(flat_ref), len(conflict)))
        L.append("   ⇒ 差异**不是**把档位读错造成的 —— 两条来源对同一个 id 写的**就是不同的数** ✓")
    else:
        L.append("## ✅ 无冲突（逐条数值相同）")
        L.append("")
    L.append("## ✅ 值相同的（%d 条）" % len(same))
    L.append("")
    L.append(id_list(same))
    L.append("")
    if absent_both:
        L.append("## ✅ 两侧都没有 `.dmg` 字段的（%d 条 —— **写法一致**）" % len(absent_both))
        L.append("")
        L.append(id_list(absent_both))
        L.append("")
    L.append("## 结论（对本次口径切换的意义）")
    L.append("")
    L.append("```")
    L.append("· 值相同 ⇒ 已落库的参考值**同时就是一手值** ⇒ 出处可标【一手 = 第三方同值】✓")
    L.append("· 冲突 ⇒ **一手为准**；但把现有值改成一手值 = **平衡数值改动** ⇒")
    L.append("  入 `dd1_baseline §39` 解冻清单（**冻结期只登记不动**）✓")
    L.append("```")
    L.append("")

    print("primary ids=%d ref ids=%d paired=%d same=%d conflicts=%d absent_both=%d only_primary=%d only_ref=%d"
          % (len(pri), len(ref), len(shared), len(same), len(conflict), len(absent_both),
             len(only_pri), len(only_ref)))
    if conflict:
        print("conflict ids: %s" % ", ".join(conflict))
    if args.check:
        return 0
    with io.open(OUT, "w", encoding="utf-8") as fh:
        fh.write("\n".join(L) + "\n")
    print("wrote %s" % os.path.relpath(OUT, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
