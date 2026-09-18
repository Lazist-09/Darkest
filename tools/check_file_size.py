#!/usr/bin/env python3
# File-size gate -- user red line 2026-09-18: every PROGRAM file <= 600 lines.
# Applies to: darkest/scripts/**, darkest/tests/**, tools/**   (doc/** and skills/** are exempt)
# Usage:  python tools/check_file_size.py            # exit 1 on violation
#         python tools/check_file_size.py --list     # print every scanned file's line count
#
# Why it exists: without a gate, "<=600 lines" is a one-off action, not an engineering constraint --
# files grow back. Same reasoning as tools/dsh/check_ui_namespace.ps1 (regeneration guard).
#
# Anti-fake-split rules (a split that only satisfies the number is NOT a fix):
#   * do not collapse code to one line, do not delete comments, do not hide code behind #region
#   * a split commit may only MOVE code: build green + full test count UNCHANGED
#
# Allowlist: tools/file_size_allowlist.txt, one entry per line, format:  path<TAB>reason
#   * an entry WITHOUT a reason is itself a failure (exceptions must be explainable)
#
# Gate invariant: any change here must be verified in BOTH directions
#   (clean tree => pass ; a file over the limit => fail).  See README red line 20 item 6.

import fnmatch
import os
import sys

LIMIT = 600
ROOTS = ["darkest/scripts", "darkest/tests", "tools"]
EXTS = (".cs", ".py", ".ps1")
ALLOWLIST = os.path.join("tools", "file_size_allowlist.txt")
REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def load_allowlist():
    allowed, bad = {}, []
    path = os.path.join(REPO, ALLOWLIST)
    if not os.path.exists(path):
        return allowed, bad
    with open(path, "r", encoding="utf-8") as fh:
        for raw in fh:
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            parts = line.split("\t")
            # strip BOM defensively: PowerShell 5.1 'Set-Content -Encoding UTF8' writes a BOM on line 1,
            # which would otherwise make the FIRST allowlist key never match (found by self-check).
            key = parts[0].strip().lstrip("\ufeff").replace("\\", "/")
            reason = parts[1].strip() if len(parts) > 1 else ""
            if not reason:
                bad.append(key)
            else:
                allowed[key] = reason
    return allowed, bad


def count_lines(path):
    with open(path, "r", encoding="utf-8", errors="replace") as fh:
        return sum(1 for _ in fh)


def is_allowed(rel, allowed):
    """Exact path or glob match (globs survive file splits/moves -- see header note)."""
    for key in allowed:
        if rel == key or fnmatch.fnmatch(rel, key):
            return True
    return False


def main():
    show_all = "--list" in sys.argv
    allowed, unexplained = load_allowlist()

    scanned, over = 0, []
    for root in ROOTS:
        base = os.path.join(REPO, root)
        for dirpath, _dirs, files in os.walk(base):
            for name in files:
                if not name.endswith(EXTS):
                    continue
                full = os.path.join(dirpath, name)
                rel = os.path.relpath(full, REPO).replace("\\", "/")
                n = count_lines(full)
                scanned += 1
                if show_all:
                    print("%5d  %s" % (n, rel))
                if n > LIMIT and not is_allowed(rel, allowed):
                    over.append((n, rel))

    if unexplained:
        print("FAIL: allowlist entries without a reason (exceptions must be explainable):")
        for key in unexplained:
            print("   %s" % key)
        return 1

    if over:
        print("FAIL: program files over %d lines (user red line 2026-09-18):" % LIMIT)
        for n, rel in sorted(over, reverse=True):
            print("   %5d  %s" % (n, rel))
        print("   -> split by RESPONSIBILITY (partial class is the weakest form: declare what it depends on),")
        print("      or add an allowlist entry WITH a reason: %s" % ALLOWLIST)
        return 1

    print("OK: all %d scanned program files <= %d lines (%d allowlisted)"
          % (scanned, LIMIT, len(allowed)))
    return 0


if __name__ == "__main__":
    sys.exit(main())
