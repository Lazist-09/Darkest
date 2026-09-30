#!/usr/bin/env python3
"""check_file_budget.py — per-file line budget check (architecture plan §3.1).

Rule (plan §3.1; user decision 2026-09-27):
  Every `.cs` file under res:// must stay SMALL:
    * <= 400 lines : healthy (soft target, leaves 200 lines of headroom)
    * 401..600     : warning (approaching the ceiling — split before it grows)
    * >  600       : VIOLATION (hard red line, exit 1)

Why this exists: the repo has 407 .cs files and currently none exceeds 600,
but 10 files sit in the 512..581 band. Without an automated check, the next
feature added to any of them silently crosses the line. The check keys on
measured line counts only — never on file-name heuristics.

Usage:
  python tools/check_file_budget.py [--root DARKEST_DIR] [--warn N] [--hard N]
  python tools/check_file_budget.py --top 20        # only show the biggest N
  python tools/check_file_budget.py --selfcheck     # negative self-test
Exit codes: 0 = within budget; 1 = at least one file over the hard limit.
Pure stdlib, cross-platform (runs on CI without Godot).
"""

from __future__ import annotations

import argparse
import pathlib
import sys
import tempfile

# Default budget: soft 400, hard 600 (plan §3.1).
DEFAULT_WARN = 400
DEFAULT_HARD = 600

# Generated / vendored trees are not hand-maintained; counting them would
# drown the report in noise (and they can never be "split" meaningfully).
EXCLUDED_DIRS = {"obj", "bin", ".git"}


def default_root() -> pathlib.Path:
    """tools/ lives at the repo root, darkest/ is its sibling."""
    return pathlib.Path(__file__).resolve().parent.parent / "darkest"


def iter_sources(root: pathlib.Path):
    """Yield every hand-maintained .cs under root, sorted for stable output."""
    for file in sorted(root.rglob("*.cs")):
        if any(part in EXCLUDED_DIRS for part in file.parts):
            continue
        yield file


def count_lines(file: pathlib.Path) -> int:
    """Physical line count (matches `wc -l` semantics closely enough)."""
    with file.open("r", encoding="utf-8", errors="replace") as handle:
        return sum(1 for _ in handle)


def scan(root: pathlib.Path, warn: int, hard: int):
    """Return (over_hard, over_warn) as lists of (path, lines)."""
    over_hard: list[tuple[str, int]] = []
    over_warn: list[tuple[str, int]] = []
    for file in iter_sources(root):
        lines = count_lines(file)
        rel = str(file.relative_to(root))
        if lines > hard:
            over_hard.append((rel, lines))
        elif lines > warn:
            over_warn.append((rel, lines))
    # Biggest first: the files closest to the ceiling are the ones to act on.
    over_hard.sort(key=lambda item: -item[1])
    over_warn.sort(key=lambda item: -item[1])
    return over_hard, over_warn


def run_selfcheck() -> int:
    """Negative self-test: build a throwaway tree OUTSIDE the real darkest/ (so a
    concurrent build can never compile the probe) and assert a 601-line file is
    caught as a hard violation. Guards the checker against silently rotting."""
    with tempfile.TemporaryDirectory() as tmp:
        root = pathlib.Path(tmp)
        (root / "scripts" / "core").mkdir(parents=True)
        (root / "project.godot").write_text("[application]\n", encoding="utf-8")
        probe = root / "scripts" / "core" / "TooBig.cs"
        # 601 lines: one past the hard limit.
        probe.write_text("// probe\n" * 601, encoding="utf-8")

        over_hard, over_warn = scan(root, DEFAULT_WARN, DEFAULT_HARD)
        if not over_hard:
            print("[check_file_budget] SELFTEST FAIL: 601-line probe was NOT caught")
            return 1
        print(f"[check_file_budget] SELFTEST PASS: probe caught -> "
              f"{over_hard[0][0]} ({over_hard[0][1]} lines)")

        # A 450-line file must be a warning, not a hard violation.
        probe.write_text("// probe\n" * 450, encoding="utf-8")
        over_hard, over_warn = scan(root, DEFAULT_WARN, DEFAULT_HARD)
        if over_hard or not over_warn:
            print("[check_file_budget] SELFTEST FAIL: 450-line probe misclassified")
            return 1
        print(f"[check_file_budget] SELFTEST PASS: warning band works -> "
              f"{over_warn[0][0]} ({over_warn[0][1]} lines)")
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--root", default=None,
                        help="res:// root (default: <repo>/darkest)")
    parser.add_argument("--warn", type=int, default=DEFAULT_WARN,
                        help=f"soft target in lines (default {DEFAULT_WARN})")
    parser.add_argument("--hard", type=int, default=DEFAULT_HARD,
                        help=f"hard red line in lines (default {DEFAULT_HARD})")
    parser.add_argument("--top", type=int, default=0,
                        help="only print the biggest N offenders (0 = all)")
    parser.add_argument("--selfcheck", action="store_true",
                        help="run the built-in negative self-test instead of the scan")
    args = parser.parse_args(argv)

    if args.selfcheck:
        return run_selfcheck()

    root = pathlib.Path(args.root).resolve() if args.root else default_root()
    if not (root / "project.godot").is_file():
        raise SystemExit(f"error: {root} does not look like a res:// root (no project.godot)")

    over_hard, over_warn = scan(root, args.warn, args.hard)

    if over_hard:
        print(f"[check_file_budget] VIOLATION: {len(over_hard)} file(s) over the "
              f"hard limit ({args.hard} lines) — must be split:")
        for rel, lines in over_hard[: args.top or None]:
            print(f"  {lines:6d}  {rel}")
    for rel, lines in over_warn[: args.top or None]:
        print(f"  warn  {lines:6d}  {rel}")
    if over_warn:
        print(f"[check_file_budget] {len(over_warn)} file(s) between "
              f"{args.warn + 1}..{args.hard} lines — split before they grow.")
    if not over_hard and not over_warn:
        print(f"[check_file_budget] OK: all files <= {args.warn} lines.")
        return 0

    return 1 if over_hard else 0


if __name__ == "__main__":
    sys.exit(main())
