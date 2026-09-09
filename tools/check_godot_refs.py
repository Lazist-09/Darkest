#!/usr/bin/env python3
"""check_godot_refs.py — `using Godot` whitelist static check (M0, T-M0-04).

Rule (blueprint §3 注 + §10; tasks/m0_bootstrap.md T-M0-04 要点1):
  `using Godot` is allowed ONLY under scripts/gameplay/scene/** and scripts/ui/**.
  Inside the kernel whitelist directories below it must be ZERO (exit 1, print
  file:line on any hit). tests/ links straight to the kernel, so it is forbidden
  too. The check keys on directory paths only — never on file-name guessing
  (T-M0-04 要点5).

Usage:
  python tools/check_godot_refs.py [--root DARKEST_DIR]      # baseline scan
  python tools/check_godot_refs.py [--root DARKEST_DIR] --selfcheck
Exit codes: 0 = clean; 1 = violation found / self-check failed.
Pure stdlib, cross-platform (runs on CI without Godot).
"""

from __future__ import annotations

import argparse
import os
import pathlib
import re
import sys

# `using Godot` exactly (word boundary keeps `using GodotSharp;` out).
_PATTERN = re.compile(r"\busing\s+Godot\b")

# Directories where `using Godot` is forbidden (relative to the res:// root).
FORBIDDEN_REL = ("scripts/core", "scripts/gameplay/sim", "scripts/data", "tests")

# Directories where `using Godot` is the only allowed home (documentation only;
# the check below enforces the forbidden half of the rule).
ALLOWED_REL = ("scripts/gameplay/scene", "scripts/ui")


def default_root() -> pathlib.Path:
    """tools/ lives at the repo root, darkest/ is its sibling."""
    return pathlib.Path(__file__).resolve().parent.parent / "darkest"


def scan(root: pathlib.Path) -> list[str]:
    """Return ['relative/path.cs:line: text'] for every forbidden hit."""
    hits: list[str] = []
    for rel in FORBIDDEN_REL:
        base = root / rel
        if not base.is_dir():
            continue
        for file in sorted(base.rglob("*.cs")):  # sorted → deterministic output
            try:
                lines = file.read_text(encoding="utf-8").splitlines()
            except (OSError, UnicodeDecodeError) as exc:
                hits.append(f"{file.relative_to(root)}: <unreadable: {exc}>")
                continue
            for lineno, text in enumerate(lines, start=1):
                if _PATTERN.search(text):
                    hits.append(f"{file.relative_to(root)}:{lineno}: {text.strip()}")
    return hits


def _resolve_root(value: str | None) -> pathlib.Path:
    root = pathlib.Path(value).resolve() if value else default_root()
    if not (root / "project.godot").is_file():
        raise SystemExit(f"error: {root} does not look like a res:// root (no project.godot)")
    return root


def run_selfcheck(root: pathlib.Path) -> int:
    """Negative self-test (T-M0-04 要点3): inject `using Godot;` under a forbidden
    dir, expect detection + non-zero exit, then clean up. Guards the checker
    against silently rotting."""
    target_dir = root / "scripts" / "core"
    if not target_dir.is_dir():
        raise SystemExit(f"selfcheck: {target_dir} not found — run against darkest/")
    probe = target_dir / f".godot_refs_selfcheck_{os.getpid()}.cs"
    try:
        probe.write_text("// negative self-test probe\nusing Godot;\n", encoding="utf-8")
        hits = scan(root)
        if not hits:
            print("[check_godot_refs] SELFTEST FAIL: injected `using Godot;` was NOT detected")
            return 1
        print(f"[check_godot_refs] SELFTEST PASS: negative probe caught -> {hits[0]}")
        return 0
    finally:
        probe.unlink(missing_ok=True)


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", default=None,
                        help="res:// root (default: <repo>/darkest)")
    parser.add_argument("--selfcheck", action="store_true",
                        help="run the built-in negative self-test instead of the baseline scan")
    args = parser.parse_args(argv)

    root = _resolve_root(args.root)

    if args.selfcheck:
        return run_selfcheck(root)

    hits = scan(root)
    if hits:
        print("[check_godot_refs] VIOLATION: `using Godot` found in kernel dirs "
              f"({', '.join(FORBIDDEN_REL)}):")
        for hit in hits:
            print("  " + hit)
        print(f"[check_godot_refs] {len(hits)} hit(s) — block build (blueprint §3/§10).")
        return 1

    print("[check_godot_refs] OK: 0 hits in " + ", ".join(FORBIDDEN_REL)
          + f" (allowed only in {', '.join(ALLOWED_REL)}).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
