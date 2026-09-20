#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_buff_primitives.py -- M2 (buff primitive layer) alignment table.

WHY (architect's M2 dispatch + the "no silent skipping" rule)
    The original's buff vocabulary is a set of PRIMITIVES:
        { id, stat_type, stat_sub_type, amount, rule_type, rule_data{float,string} }
    Our `buff_defs.json` instead carries `modifiers[].kind` + `hooks[].timing`.
    Before translating, M2 needs two countable things:
        (1) WHICH primitives exist and how often they are used
        (2) WHICH of ours have no counterpart -- and which of theirs are unused
    Red line (the architect's words): "every primitive must either be referenced or
    land in an explicit unmapped list" -- **no silent skipping**.

INPUTS (both read-only)
    reference: F:\\GithubPro\\Darkest-Dungeon-Unity\\Assets\\Resources\\Data\\JsonBuffs.json
    ours:      darkest/data/buff_defs.json
OUTPUTS (reports/; derived tables, not shipped content -- red line 29 / B6)
    reports/dd1_buff_primitives.md / .json

OUTPUT IS ASCII-ONLY: this console is GBK and printing CJK kills the tool.

USAGE
    python tools/dsh/extract_dd1_buff_primitives.py [--ref <path>] [--ours <path>]
EXIT: 0 = tables written, 1 = an input is missing/unreadable.
"""

import collections
import json
import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\JsonBuffs.json"
OURS = os.path.join(REPO, "darkest", "data", "buff_defs.json")
OUT_MD = os.path.join(REPO, "reports", "dd1_buff_primitives.md")
OUT_JSON = os.path.join(REPO, "reports", "dd1_buff_primitives.json")

# PROPOSED mapping: our modifier "kind" -> the original's stat_type family.
# 🔴 This is a *proposal for the planner to bless*, not a decision: the two schemas are
#    not 1:1 (ours is effect-oriented, theirs is stat-oriented).
PROPOSED = {
    "state_flag": "(no direct counterpart -- our engine-side flag, e.g. stunned)",
    "stat_add": "combat_stat_add",
    "stat_multiply": "combat_stat_multiply",
    "dot": "(original expresses DoT via rule/duration on stat types; needs cases)",
    "resist_add": "combat_stat_add (stat_sub_type = *_resist / *_chance)",
    "guard_redirect": "(no direct counterpart -- our displacement/guard layer)",
}


def load(path):
    with open(path, "r", encoding="utf-8") as fh:
        return json.load(fh)


def main(argv):
    ref = argv[argv.index("--ref") + 1] if "--ref" in argv else DEFAULT_REF
    ours = argv[argv.index("--ours") + 1] if "--ours" in argv else OURS
    for p in (ref, ours):
        if not os.path.isfile(p):
            print("[m2] input missing: %s" % p)
            return 1

    rb = load(ref).get("buffs", [])
    ob = load(ours).get("buffs", [])

    pairs = collections.Counter()
    stat_types = collections.Counter()
    rules = collections.Counter()
    for b in rb:
        st = b.get("stat_type", "?")
        sst = b.get("stat_sub_type", "?")
        pairs["%s / %s" % (st, sst)] += 1
        stat_types[st] += 1
        rules[b.get("rule_type", "?")] += 1

    kinds = collections.Counter()
    for b in ob:
        for m in b.get("modifiers", []):
            kinds[m.get("kind", "?")] += 1

    mapped = {k: PROPOSED.get(k, "**UNMAPPED**") for k in kinds}
    unmapped_ours = [k for k, v in mapped.items() if v == "**UNMAPPED**"]

    out = {
        "source": ref,
        "note": "REFERENCE EXTRACT (local Unity port of the original). Not shipped content. "
                "M2 alignment target; the mapping below is a PROPOSAL for the planner.",
        "reference_buff_count": len(rb),
        "reference_primitives": dict(pairs.most_common()),
        "reference_stat_types": dict(stat_types.most_common()),
        "reference_rule_types": dict(rules.most_common()),
        "our_buff_count": len(ob),
        "our_modifier_kinds": dict(kinds.most_common()),
        "proposed_kind_mapping": mapped,
        "ours_unmapped": unmapped_ours,
    }
    with open(OUT_JSON, "w", encoding="utf-8") as fh:
        json.dump(out, fh, ensure_ascii=False, indent=1)

    lines = [
        "# M2 buff 原语层：**参考项目 ↔ 我们**的映射表（含双向未映射清单）",
        "",
        "> 来源：`%s`（本地参考项目，用户指定可参考）" % ref,
        "> 🔴 **规则（架构）**：**每条原语要么被引用、要么进未映射清单** —— **不许静默跳过** ✓",
        "> ⚠️ 下面的映射是**提案**（两套 schema 不同源：他们是 stat 导向、我们是 effect 导向）⇒ **请策划/架构裁定** ✓",
        "",
        "## 1. 参考项目：原语用量（`stat_type / stat_sub_type`，共 %d 条 buff）" % len(rb),
        "",
        "| 原语 | 次数 |",
        "|---|---|",
    ]
    for k, v in pairs.most_common(40):
        lines.append("| `%s` | %d |" % (k, v))
    if len(pairs) > 40:
        lines.append("| …（其余 %d 种） | |" % (len(pairs) - 40))

    lines += ["", "### 规则类型分布（`rule_type`）", "", "| rule | 次数 |", "|---|---|"]
    for k, v in rules.most_common():
        lines.append("| `%s` | %d |" % (k, v))

    lines += ["", "## 2. 我们：`modifiers[].kind` 用量（共 %d 条 buff）" % len(ob), "",
              "| 我们的 kind | 次数 | 提案映射 |", "|---|---|---|"]
    for k, v in kinds.most_common():
        lines.append("| `%s` | %d | %s |" % (k, v, mapped.get(k, "?")))

    lines += ["", "## 3. 🔴 未映射清单（**必须可见**）", ""]
    lines.append("- **我们侧未映射的 kind**：%s" % (", ".join("`%s`" % k for k in unmapped_ours) or "（无）"))
    lines.append("- **参考侧未被提案引用的 `stat_type`**：%s"
                 % (", ".join("`%s`" % s for s in stat_types if s not in set(PROPOSED.values())) or "（无）"))
    lines += ["", "## 4. 数量核对（可测）", ""]
    lines.append("- 参考 buff 条数 = **%d** · 原语种类 = **%d** · rule_type 种类 = **%d**"
                 % (len(rb), len(pairs), len(rules)))
    lines.append("- 我们 buff 条数 = **%d** · modifier kind 种类 = **%d** · **未映射 = %d**"
                 % (len(ob), len(kinds), len(unmapped_ours)))

    with open(OUT_MD, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print("[m2] reference buffs=%d primitives=%d rule_types=%d"
          % (len(rb), len(pairs), len(rules)))
    print("[m2] ours buffs=%d kinds=%d unmapped=%d" % (len(ob), len(kinds), len(unmapped_ours)))
    top = pairs.most_common(1)[0][0] if pairs else "-"
    print("[m2] top primitive: %s" % top)
    print("[m2] wrote %s + %s" % (os.path.relpath(OUT_MD, REPO), os.path.relpath(OUT_JSON, REPO)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
