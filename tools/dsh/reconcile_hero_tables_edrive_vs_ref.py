#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""reconcile_hero_tables_edrive_vs_ref.py -- primary (E-drive) vs third-party (reference) reconciliation.

WHY (the objective's hard requirement (a))
  Our unit tables were copied from the REFERENCE project (third-party, level 3).
  Our own precedence rule (dd1_baseline section 32.3) says: primary (E-drive) > second-hand > third-party,
  and "when primary and third-party conflict, PRIMARY WINS -- and record the conflict".
  => So before substituting any more values we must know where the two sources AGREE and where they differ.

WHAT IT DOES
  1. reads the primary's hero info files:  E:/SteamLibrary/.../heroes/<name>/<name>.info.darkest
  2. reads the derived reference table:    reports/dd1_hero_tables_from_unity_ref.json
  3. diffs every hero x tier x field (weapon: atk/dmg_min/dmg_max/crit/spd -- armour: def/prot/hp/spd)
  4. writes reports/edrive_vs_reference_hero_tables.md   (agreements + every conflict, quoted)
  It NEVER writes game data: this is a report tool.

NOTE the reference may have edited/deleted things (measured: stage_coach primary 12322 B vs reference 8610 B),
  so a conflict here is expected to be possible -- that is exactly why we look.

USAGE
  python tools/dsh/reconcile_hero_tables_edrive_vs_ref.py [--edrive <path>] [--ref <json>]
EXIT: 0 = report written (0 conflicts or not), 1 = could not read the primary.
"""

from __future__ import annotations

import io
import json
import os
import re
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
EDRIVE = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
REF = os.path.join(REPO, "reports", "dd1_hero_tables_from_unity_ref.json")
OUT = os.path.join(REPO, "reports", "edrive_vs_reference_hero_tables.md")

# weapon: .name "crusader_weapon_0" .atk 0% .dmg 6 12 .crit 5% .spd 1
WPN_RE = re.compile(
    r'\.name\s+"(?P<name>[a-z_]+_weapon_\d+)"\s+\.atk\s+(?P<atk>-?\d+(?:\.\d+)?)%\s+'
    r"\.dmg\s+(?P<dmin>-?\d+)\s+(?P<dmax>-?\d+)\s+\.crit\s+(?P<crit>-?\d+(?:\.\d+)?)%\s+"
    r"\.spd\s+(?P<spd>-?\d+)")
# armour: .name "crusader_armour_0" .def 10% .prot 0 .hp 26 .spd 0
ARM_RE = re.compile(
    r'\.name\s+"(?P<name>[a-z_]+_armour_\d+)"\s+\.def\s+(?P<def>-?\d+(?:\.\d+)?)%\s+'
    r"\.prot\s+(?P<prot>-?\d+)\s+\.hp\s+(?P<hp>-?\d+)\s+\.spd\s+(?P<spd>-?\d+)")


def read_primary(edrive: str) -> dict:
    """-> {hero_lower: {'weapon': [...], 'armour': [...]}} from the PRIMARY."""
    out: dict = {}
    root = os.path.join(edrive, "heroes")
    if not os.path.isdir(root):
        return out
    for d in sorted(os.listdir(root)):
        p = os.path.join(root, d, "%s.info.darkest" % d)
        if not os.path.isfile(p):
            continue
        txt = io.open(p, encoding="utf-8", errors="replace").read()
        wpn = [{
            "name": m.group("name"), "atk": float(m.group("atk")), "dmg_min": int(m.group("dmin")),
            "dmg_max": int(m.group("dmax")), "crit": float(m.group("crit")), "spd": int(m.group("spd")),
        } for m in WPN_RE.finditer(txt)]
        arm = [{
            "name": m.group("name"), "def": float(m.group("def")), "prot": int(m.group("prot")),
            "hp": int(m.group("hp")), "spd": int(m.group("spd")),
        } for m in ARM_RE.finditer(txt)]
        if wpn or arm:
            out[d] = {"weapon": wpn, "armour": arm}
    return out


def main() -> int:
    argv = sys.argv[1:]
    edrive = argv[argv.index("--edrive") + 1] if "--edrive" in argv else EDRIVE
    refp = argv[argv.index("--ref") + 1] if "--ref" in argv else REF

    primary = read_primary(edrive)
    if not primary:
        print("[reconcile] PRIMARY not readable at %s -- nothing compared" % edrive)
        return 1
    ref = json.load(io.open(refp, encoding="utf-8"))["heroes"]

    W_FIELDS = ["atk", "dmg_min", "dmg_max", "crit", "spd"]
    A_FIELDS = ["def", "prot", "hp", "spd"]

    lines: list[str] = []
    conflicts: list[str] = []
    compared = 0
    agree = 0
    heroes_matched = 0

    for hlower, pv in sorted(primary.items()):
        rkey = next((k for k in ref if k.lower() == hlower), None)
        if rkey is None:
            conflicts.append("hero `%s`: 参考项目**没有**这个英雄 ⇒ 无法对账" % hlower)
            continue
        heroes_matched += 1
        rv = ref[rkey]
        for kind, fields in (("weapon", W_FIELDS), ("armour", A_FIELDS)):
            ptiers, rtiers = pv.get(kind, []), rv.get(kind, [])
            if len(ptiers) != len(rtiers):
                conflicts.append("`%s.%s`: **阶数不同** 一手 %d vs 参考 %d"
                                 % (hlower, kind, len(ptiers), len(rtiers)))
            for i in range(min(len(ptiers), len(rtiers))):
                for f in fields:
                    pval, rval = ptiers[i].get(f), rtiers[i].get(f)
                    if pval is None or rval is None:
                        continue
                    compared += 1
                    if abs(float(pval) - float(rval)) < 1e-9:
                        agree += 1
                    else:
                        conflicts.append("`%s.%s[%d].%s`: 一手 **%s** vs 参考 %s"
                                         % (hlower, kind, i, f, pval, rval))

    lines.append("# 一手（E 盘）vs 第三方（参考项目）· 英雄武器/护甲表 **对账报告**")
    lines.append("")
    lines.append("> 🔴 依据：我们自己的 `dd1_baseline` §32.3 —— **一手 > 二手 > 第三方**；")
    lines.append(">   **冲突时以一手为准，并记录冲突** ✓")
    lines.append("> 工具：`tools/dsh/reconcile_hero_tables_edrive_vs_ref.py`（可复跑；**不写游戏数据** ✓）")
    lines.append("")
    lines.append("## 读数")
    lines.append("")
    lines.append("| 项 | 值 |")
    lines.append("|---|---|")
    lines.append("| 一手可读英雄 | **%d** |" % len(primary))
    lines.append("| 与参考项目配对上的英雄 | **%d** |" % heroes_matched)
    lines.append("| 逐字段比较次数 | **%d** |" % compared)
    lines.append("| **一致** | **%d** |" % agree)
    lines.append("| **冲突** | **%d** |" % len(conflicts))
    if compared:
        lines.append("| 一致率 | **%.1f%%** |" % (100.0 * agree / compared))
    lines.append("")
    if conflicts:
        lines.append("## 🔴 冲突逐条（按 §32.3：**以一手为准**）")
        lines.append("")
        for c in conflicts:
            lines.append("- " + c)
    else:
        lines.append("## ✅ 无冲突")
        lines.append("")
        lines.append("⇒ **两个来源逐字段一致** ⇒ 即：我们已落库的那批参考项目值**同时就是一手值** ✓")
        lines.append("（⇒ 目标硬要求 (a) 的\"能取一手则优先一手\"在此**已被满足**：两源同值 ✓）")
    lines.append("")
    lines.append("## 结论（对本次顶替的意义）")
    lines.append("")
    lines.append("```")
    lines.append("· 若一致 ⇒ 已落库的表**不需要**因\"换一手\"而改动 ⇒ 顶替的出处等级可标为【一手=第三方同值】✓")
    lines.append("· 若有冲突 ⇒ **冲突字段逐条以一手为准**（本报告已列），并写进替换清单与 assets_credits ✓")
    lines.append("```")

    io.open(OUT, "w", encoding="utf-8").write("\n".join(lines))
    print("[reconcile] wrote %s" % os.path.relpath(OUT, REPO))
    print("[reconcile] heroes=%d compared=%d agree=%d conflicts=%d"
          % (len(primary), compared, agree, len(conflicts)))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
