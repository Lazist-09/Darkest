#!/usr/bin/env python3
"""audit_split_integrity.py -- prove a file split did NOT lose any member.

WHY this tool exists (two hard-won lessons, both from real incidents in this repo):

  Lesson 1: comparing against HEAD is USELESS after the split is committed
            (HEAD == the post-split state, so "the parts contain the main file"
            proves nothing).  ==> you MUST compare against a PRE-SPLIT snapshot.

  Lesson 2: for decompiled / machine-assembled sources, matching the *signature line*
            is unreliable (indentation/format differ) ==> compare by MEMBER NAME.

Usage:
  python tools/dsh/audit_split_integrity.py --baseline <pre-split.cs> --parts a.cs b.cs [...]
  python tools/dsh/audit_split_integrity.py --baseline ... --parts ... --json

Exit code: 0 = no member of the baseline is missing; 1 = missing member(s) found
           (fail-closed: a split that lost something must never look green).

Self-check (both directions):
  python tools/dsh/audit_split_integrity.py --selfcheck
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import tempfile
from pathlib import Path

# Member name = the identifier right after a member modifier (any indentation).
# Deliberately NAME-based, not signature-based (see Lesson 2 in the docstring).
NAME_RE = re.compile(
    r"(?m)^\s*(?:public|private|protected|internal)\s+"
    r"(?:static\s+|sealed\s+|override\s+|readonly\s+|async\s+|partial\s+)*"
    r"[\w<>\[\]\?,\s\.\(\)]+?"
    r"\b(\w+)\s*[\(=>\{]"
)

# Keywords that the loose pattern above could pick up by accident.
NOISE = {
    "if", "for", "while", "switch", "return", "new", "get", "set", "value",
    "using", "namespace", "class", "record", "struct", "enum", "is", "as",
}


def member_names(text: str) -> set[str]:
    """All member names declared in `text` (name-based, indentation-agnostic)."""
    names = {m.group(1) for m in NAME_RE.finditer(text)}
    return {n for n in names if n not in NOISE}


def audit(baseline: Path, parts: list[Path]) -> dict:
    if not baseline.exists():
        raise SystemExit(f"baseline not found: {baseline}")
    missing_parts = [str(p) for p in parts if not p.exists()]
    if missing_parts:
        raise SystemExit("part(s) not found: " + ", ".join(missing_parts))

    base = member_names(baseline.read_text(encoding="utf-8", errors="replace"))
    now: set[str] = set()
    for p in parts:
        now |= member_names(p.read_text(encoding="utf-8", errors="replace"))

    missing = sorted(base - now)
    return {
        "baseline": str(baseline),
        "baseline_members": len(base),
        "parts": [str(p) for p in parts],
        "part_members_union": len(now),
        "missing": missing,
        "ok": not missing,
    }


def selfcheck() -> int:
    """Both directions: (a) intact split => ok, (b) dropped member => NOT ok."""
    with tempfile.TemporaryDirectory() as d:
        root = Path(d)
        base = root / "base.cs"
        base.write_text(
            "public sealed class Probe\n{\n"
            "    public void KeepA() { }\n"
            "    public int KeepB() => 1;\n"
            "    private void GoneC() { }\n"
            "}\n",
            encoding="utf-8",
        )
        good = root / "good.cs"
        good.write_text(
            "public sealed partial class Probe\n{\n"
            "    public void KeepA() { }\n"
            "    public int KeepB() => 1;\n"
            "    private void GoneC() { }\n"
            "}\n",
            encoding="utf-8",
        )
        bad = root / "bad.cs"
        bad.write_text(
            "public sealed partial class Probe\n{\n"
            "    public void KeepA() { }\n"
            "    public int KeepB() => 1;\n"
            "}\n",
            encoding="utf-8",
        )

        ok_case = audit(base, [good])
        bad_case = audit(base, [bad])
        passed = ok_case["ok"] is True and bad_case["ok"] is False and bad_case["missing"] == ["GoneC"]
        print(f"[split-audit] selfcheck: intact={ok_case['ok']} dropped={bad_case['missing']} "
              f"=> {'PASS' if passed else 'FAIL'}")
        return 0 if passed else 1


def main() -> int:
    ap = argparse.ArgumentParser(description="prove a split lost no member (name-based)")
    ap.add_argument("--baseline", type=Path, help="pre-split snapshot (NOT HEAD)")
    ap.add_argument("--parts", type=Path, nargs="+", help="all parts after the split")
    ap.add_argument("--json", action="store_true", help="machine-readable output")
    ap.add_argument("--selfcheck", action="store_true", help="run the two-direction self check")
    args = ap.parse_args()

    if args.selfcheck:
        return selfcheck()

    if not args.baseline or not args.parts:
        ap.print_help()
        return 2

    result = audit(args.baseline, args.parts)
    if args.json:
        print(json.dumps(result, ensure_ascii=True, indent=2))
    else:
        print(f"[split-audit] baseline members = {result['baseline_members']} | "
              f"parts union = {result['part_members_union']}")
        if result["ok"]:
            print("[split-audit] RESULT: OK (no member lost)")
        else:
            print(f"[split-audit] RESULT: FAIL ({len(result['missing'])} missing)")
            for name in result["missing"]:
                print(f"[split-audit]   missing: {name}")

    return 0 if result["ok"] else 1


if __name__ == "__main__":
    sys.exit(main())
