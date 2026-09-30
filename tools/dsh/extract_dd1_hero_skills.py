#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""extract_dd1_hero_skills.py -- read the PRIMARY (E-drive DD1 install) hero combat skills.

WHY (ruling, user 2026-09-26 -- doc/architecture/source_priority.md 1b)
    "PRIMARY E-drive first; the local Unity reference only when E-drive cannot answer."
    Every skill `.dmg` the team ships must therefore be checked against THIS tree first.

WHAT IT READS (read-only; nothing is written there)
    E:\\SteamLibrary\\steamapps\\common\\DarkestDungeon\\heroes\\<hero>\\<hero>.info.darkest
    Plain text (CRLF), one combat skill per line, e.g.
        combat_skill: .id "smite" .level 0 .type "melee" .atk 85% .dmg 0% .crit 0% ...
    Unless --no-shared: also shared\\**\\*.darkest, where common skills (e.g. `move`) live.

WHAT IT EMITS (reports/ only; NEITHER ships -- red line 29 / B6)
    reports/dd1_hero_skills_from_edrive.json   machine-readable, every row with file:line
    reports/dd1_hero_skills_from_edrive.md     human-readable id -> .dmg table

USAGE
    python tools/dsh/extract_dd1_hero_skills.py               # write both files
    python tools/dsh/extract_dd1_hero_skills.py --check        # print index, write nothing
    python tools/dsh/extract_dd1_hero_skills.py --root <dir> [--no-shared]
EXIT: 0 = ok, 1 = E-drive tree not found / unreadable.
"""

import argparse
import json
import os
import re
import sys

DEFAULT_ROOT = r"E:\SteamLibrary\steamapps\common\DarkestDungeon"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT_JSON = os.path.join(REPO, "reports", "dd1_hero_skills_from_edrive.json")
OUT_MD = os.path.join(REPO, "reports", "dd1_hero_skills_from_edrive.md")

RE_ID = re.compile(r'\.id\s+"([^"]*)"')
RE_LEVEL = re.compile(r'\.level\s+(\d+)')
RE_DMG = re.compile(r'\.dmg\s+(-?\d+)%')
RE_ATK = re.compile(r'\.atk\s+(-?\d+)%')
RE_TYPE = re.compile(r'\.type\s+"([^"]*)"')
RE_EFFECT = re.compile(r'\.effect\s+"([^"]*)"')

SHARED_FILE_CAP = 400
SHARED_SIZE_CAP = 4 * 1024 * 1024


def read_text(path):
    with open(path, "rb") as fh:
        return fh.read().decode("utf-8", "replace").replace("\r\n", "\n")


def parse_file(path, rel_root, group):
    rows = []
    for lineno, raw in enumerate(read_text(path).split("\n"), 1):
        if not raw.startswith("combat_skill:"):
            continue
        mid = RE_ID.search(raw)
        if not mid:
            continue
        mdmg = RE_DMG.search(raw)
        matk = RE_ATK.search(raw)
        mtype = RE_TYPE.search(raw)
        meff = RE_EFFECT.search(raw)
        rows.append({
            "id": mid.group(1),
            "level": int(RE_LEVEL.search(raw).group(1)) if RE_LEVEL.search(raw) else None,
            "dmg_pct": int(mdmg.group(1)) if mdmg else None,
            "atk_pct": int(matk.group(1)) if matk else None,
            "type": mtype.group(1) if mtype else None,
            "effect": meff.group(1) if meff else None,
            "group": group,
            "file": rel_root,
            "line": lineno,
        })
    return rows


def collect(root, with_shared):
    heroes_dir = os.path.join(root, "heroes")
    if not os.path.isdir(heroes_dir):
        return None
    rows = []
    for hero in sorted(os.listdir(heroes_dir)):
        path = os.path.join(heroes_dir, hero, hero + ".info.darkest")
        if os.path.isfile(path):
            rows += parse_file(path, "heroes/%s/%s.info.darkest" % (hero, hero), hero)
    shared_count = 0
    shared_dir = os.path.join(root, "shared")
    if with_shared and os.path.isdir(shared_dir):
        for base, _dirs, files in os.walk(shared_dir):
            for name in sorted(files):
                if not name.endswith(".darkest"):
                    continue
                path = os.path.join(base, name)
                if os.path.getsize(path) > SHARED_SIZE_CAP or shared_count >= SHARED_FILE_CAP:
                    continue
                shared_count += 1
                rel = os.path.relpath(path, root).replace(os.sep, "/")
                rows += parse_file(path, rel, "shared")
    return rows, shared_count


def level0(rows):
    """Index: skill id -> the level-0 row (the tier the team compares against)."""
    idx = {}
    for row in rows:
        if row["level"] == 0 and row["id"] not in idx:
            idx[row["id"]] = row
    return idx


def emit_md(rows, idx, count_heroes, count_shared, root):
    out = ["# PRIMARY (E-drive DD1) hero combat skills -- `extract_dd1_hero_skills.py`", "",
           "> Read-only extract of `%s`." % root,
           "> Derived table only -- no original asset bytes (red line 29 / B6).", "",
           "* hero files: **%d** -- shared files scanned: **%d** -- rows: **%d** -- distinct ids (level 0): **%d**"
           % (count_heroes, count_shared, len(rows), len(idx)), "",
           "## level-0 `.dmg` by skill id", "", "| skill id | group | .dmg | .atk | type | evidence |",
           "|---|---|---:|---:|---|---|"]
    for sid in sorted(idx):
        r = idx[sid]
        dmg = "--" if r["dmg_pct"] is None else "%d%%" % r["dmg_pct"]
        atk = "--" if r["atk_pct"] is None else "%d%%" % r["atk_pct"]
        out.append("| `%s` | %s | %s | %s | %s | `%s:%d` |"
                   % (sid, r["group"], dmg, atk, r["type"] or "--", r["file"], r["line"]))
    out += ["", "## every level (raw)", "", "| skill id | lvl | .dmg | file:line |", "|---|---:|---:|---|"]
    for r in sorted(rows, key=lambda x: (x["group"], x["id"], -1 if x["level"] is None else x["level"])):
        out.append("| `%s` | %s | %s | `%s:%d` |"
                   % (r["id"], r["level"], "--" if r["dmg_pct"] is None else "%d%%" % r["dmg_pct"],
                      r["file"], r["line"]))
    return "\n".join(out) + "\n"


def main(argv):
    ap = argparse.ArgumentParser(description="read PRIMARY (E-drive) hero combat skills")
    ap.add_argument("--root", default=DEFAULT_ROOT, help="DD1 install root (default: %s)" % DEFAULT_ROOT)
    ap.add_argument("--no-shared", action="store_true", help="skip the shared/ tree")
    ap.add_argument("--check", action="store_true", help="print the index, write nothing")
    args = ap.parse_args(argv)

    got = collect(args.root, not args.no_shared)
    if got is None:
        print("E-drive tree not found: %s" % args.root, file=sys.stderr)
        return 1
    rows, count_shared = got
    idx = level0(rows)
    count_heroes = len({r["group"] for r in rows if r["group"] != "shared"})

    if args.check:
        for sid in sorted(idx):
            r = idx[sid]
            print("%-28s %-14s dmg=%-5s atk=%-5s %s:%d"
                  % (sid, r["group"], "--" if r["dmg_pct"] is None else "%d%%" % r["dmg_pct"],
                     "--" if r["atk_pct"] is None else "%d%%" % r["atk_pct"], r["file"], r["line"]))
        print("# hero files=%d shared files=%d rows=%d ids(level0)=%d"
              % (count_heroes, count_shared, len(rows), len(idx)))
        return 0

    with open(OUT_JSON, "w", encoding="utf-8") as fh:
        json.dump({"root": args.root, "hero_files": count_heroes, "shared_files": count_shared,
                   "rows": rows}, fh, ensure_ascii=False, indent=1)
        fh.write("\n")
    with open(OUT_MD, "w", encoding="utf-8") as fh:
        fh.write(emit_md(rows, idx, count_heroes, count_shared, args.root))
    print("wrote %s" % os.path.relpath(OUT_JSON, REPO))
    print("wrote %s" % os.path.relpath(OUT_MD, REPO))
    print("hero files=%d shared files=%d rows=%d ids(level0)=%d" % (count_heroes, count_shared, len(rows), len(idx)))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
