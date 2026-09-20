#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_hero_tables.py -- read the LOCAL Unity reference and emit an alignment table.

WHAT IT READS (local reference project; the user pointed the team at it)
    F:\\GithubPro\\Darkest-Dungeon-Unity\\Assets\\Resources\\Data\\Heroes\\Info\\<Hero>.bytes
    These are plain-text original data tables (Unity TextAsset), e.g.:
        resistances: .stun 40% .poison 30% .bleed 30% .disease 30% .move 40% .debuff 30% .death_blow 67% .trap 10%
        weapon: .name "crusader_weapon_0" .atk 0% .dmg 6 12 .crit 5% .spd 1
        armour: .name "crusader_armour_4" .def 25% .prot 0 .hp 61 .spd 0

WHAT IT EMITS (both go to reports/; NEITHER ships -- red line 29 / B6)
    reports/dd1_hero_tables_from_unity_ref.json   machine-readable (for the planner to bless)
    reports/dd1_hero_tables_from_unity_ref.md     human-readable table + provenance

RULES IT FOLLOWS
    * READ-ONLY on the reference; it never writes there.
    * Numbers are NOT invented: every value is transcribed with its source hero file.
    * The output is a DERIVED table (no original asset bytes) and is labelled as a reference
      extract, not as shipped content.

USAGE
    python tools/dsh/extract_dd1_hero_tables.py
    python tools/dsh/extract_dd1_hero_tables.py --root <path-to-Darkest-Dungeon-Unity>
EXIT: 0 = table written, 1 = reference not found / unreadable.
"""

import json
import os
import re
import sys

DEFAULT_ROOT = r"F:\GithubPro\Darkest-Dungeon-Unity"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_JSON = os.path.join(REPO, "reports", "dd1_hero_tables_from_unity_ref.json")
OUT_MD = os.path.join(REPO, "reports", "dd1_hero_tables_from_unity_ref.md")

RESIST_KEYS = ["stun", "poison", "bleed", "disease", "move", "debuff", "death_blow", "trap"]


def parse_hero(text):
    out = {"resistances": {}, "weapon": [], "armour": [], "skill_levels": {}}
    for raw in text.splitlines():
        line = raw.strip()
        if line.startswith("resistances:"):
            for key, val in re.findall(r"\.(\w+)\s+(-?\d+)%", line):
                out["resistances"][key] = int(val)
        elif line.startswith("weapon:"):
            m = {
                "name": (re.search(r'\.name\s+"([^"]+)"', line) or [None, None])[1],
                "atk": int((re.search(r"\.atk\s+(-?\d+)%", line) or [0, 0])[1]),
                "dmg_min": int((re.search(r"\.dmg\s+(-?\d+)\s+(-?\d+)", line) or [0, 0, 0])[1]),
                "dmg_max": int((re.search(r"\.dmg\s+(-?\d+)\s+(-?\d+)", line) or [0, 0, 0])[2]),
                "crit": float((re.search(r"\.crit\s+(-?[\d.]+)%", line) or [0, 0])[1]),
                "spd": int((re.search(r"\.spd\s+(-?\d+)", line) or [0, 0])[1]),
                "req": (re.search(r"\.upgradeRequirementCode\s+(\d+)", line) or [None, None])[1],
            }
            out["weapon"].append(m)
        elif line.startswith("armour:"):
            m = {
                "name": (re.search(r'\.name\s+"([^"]+)"', line) or [None, None])[1],
                "def": int((re.search(r"\.def\s+(-?\d+)%", line) or [0, 0])[1]),
                "prot": int((re.search(r"\.prot\s+(-?\d+)", line) or [0, 0])[1]),
                "hp": int((re.search(r"\.hp\s+(-?\d+)", line) or [0, 0])[1]),
                "spd": int((re.search(r"\.spd\s+(-?\d+)", line) or [0, 0])[1]),
                "req": (re.search(r"\.upgradeRequirementCode\s+(\d+)", line) or [None, None])[1],
            }
            out["armour"].append(m)
        elif line.startswith("combat_skill:"):
            sid = (re.search(r'\.id\s+"([^"]+)"', line) or [None, None])[1]
            lvl = (re.search(r"\.level\s+(\d+)", line) or [None, None])[1]
            if sid and lvl is not None:
                out["skill_levels"].setdefault(sid, []).append(int(lvl))
    return out


def main(argv):
    root = argv[argv.index("--root") + 1] if "--root" in argv else DEFAULT_ROOT
    info = os.path.join(root, "Assets", "Resources", "Data", "Heroes", "Info")
    if not os.path.isdir(info):
        print("[dd1] reference not found (expected %s) -- nothing extracted" % info)
        return 1

    heroes = {}
    for name in sorted(os.listdir(info)):
        if not name.endswith(".bytes"):
            continue
        hero = name[: -len(".bytes")]
        with open(os.path.join(info, name), "r", encoding="utf-8", errors="replace") as fh:
            heroes[hero] = parse_hero(fh.read())

    with open(OUT_JSON, "w", encoding="utf-8") as fh:
        json.dump({
            "source": os.path.join(root, "Assets/Resources/Data/Heroes/Info/*.bytes"),
            "note": "REFERENCE EXTRACT (local Unity port of the original). Not shipped content. "
                    "Alignment target for DD1 phase A; numbers must be blessed by the planner.",
            "resist_keys": RESIST_KEYS,
            "heroes": heroes,
        }, fh, ensure_ascii=False, indent=1)

    lines = [
        "# DD1 英雄对齐表（**参考项目提取** · 非发行内容）",
        "",
        "> 来源：`%s`（本地参考项目，用户指定可参考）" % os.path.join(root, "Assets", "Resources", "Data", "Heroes", "Info"),
        "> 🔴 **用途**：给策划**逐一核对/裁定**用的对齐目标（`dd1_baseline` 阶段 A：尽量对齐、不发明）。",
        "> ⚠️ **未获裁定前不进 `darkest/data/**`**（纪律：数值只在策划给数时改）✓",
        "> 📌 同源事实（参考项目 `Character.cs`）：`Protection = Clamp(prot, -1, max(0.85, raw))` ⇒ **prot 是 0~0.85 的比例** ✓",
        "",
        "| 英雄 | 8 抗性（stun/poison/bleed/disease/move/debuff/death_blow/trap） | weapon 5 阶 dmg | armour 5 阶 def%/hp | prot |",
        "|---|---|---|---|---|",
    ]
    for hero, d in heroes.items():
        r = d["resistances"]
        rtxt = "/".join(str(r.get(k, "-")) for k in RESIST_KEYS)
        w = d["weapon"]
        wtxt = " → ".join("%s-%s" % (x["dmg_min"], x["dmg_max"]) for x in w) if w else "-"
        a = d["armour"]
        atxt = " → ".join("%s%%/%s" % (x["def"], x["hp"]) for x in a) if a else "-"
        ptxt = "/".join(str(x["prot"]) for x in a) if a else "-"
        lines.append("| %s | %s | %s | %s | %s |" % (hero, rtxt, wtxt, atxt, ptxt))

    lines += ["", "## 数量核对（可测）", ""]
    lines.append("- 英雄数：**%d**" % len(heroes))
    lines.append("- 有 8 项抗性的英雄：**%d**" % sum(1 for d in heroes.values() if len(d["resistances"]) == 8))
    lines.append("- 有 5 阶 weapon 的：**%d**" % sum(1 for d in heroes.values() if len(d["weapon"]) == 5))
    lines.append("- 有 5 阶 armour 的：**%d**" % sum(1 for d in heroes.values() if len(d["armour"]) == 5))
    with open(OUT_MD, "w", encoding="utf-8") as fh:
        fh.write("\n".join(lines) + "\n")

    print("[dd1] heroes=%d  8-resist=%d  5-tier weapon=%d  5-tier armour=%d"
          % (len(heroes),
             sum(1 for d in heroes.values() if len(d["resistances"]) == 8),
             sum(1 for d in heroes.values() if len(d["weapon"]) == 5),
             sum(1 for d in heroes.values() if len(d["armour"]) == 5)))
    print("[dd1] wrote %s" % os.path.relpath(OUT_JSON, REPO))
    print("[dd1] wrote %s" % os.path.relpath(OUT_MD, REPO))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
