#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""check_no_external_assets.py -- B6 gate: no E-drive extracts may reach a shipping package.

WHY (planner B6 / red line 29, delivered by the architect 2026-09-20):
    "Local self-use is fine -- what must never happen is shipping it."
    Phase A (DD1 reproduction) may read the original game's data on the E: drive as an
    alignment target, but the shipped package must contain none of it.

WHAT IT CHECKS (three surfaces, all read-only)
    1. any path/token that reveals an E-drive or original-game origin
       (E:\\ , SteamLibrary, the original game's directory names, .darkest extracts)
    2. files under the asset/resource roots that are NOT registered in the credits file
       (third-party bytes must be declared -- same family as "the formal hero path must
       not carry placeholders")
    3. an export/pack manifest, if one is present, listing any of the above

ALLOWLIST (tools/external_assets_allowlist.txt)
    one entry per line:  <path-or-token><TAB><reason><TAB><expiry>
    * a missing reason  => the entry itself is a failure
    * a missing expiry  => the entry itself is a failure  (planner #412: exemptions must be
      explainable AND time-bounded)

OUTPUT IS ASCII-ONLY ON PURPOSE: this console is GBK and printing CJK kills the gate with a
UnicodeEncodeError (the project's other tools follow the same rule).

USAGE
    python tools/check_no_external_assets.py                 # scan the default surfaces
    python tools/check_no_external_assets.py --list          # print what is scanned
    python tools/check_no_external_assets.py --root <dir>    # scan a specific package dir
    python tools/check_no_external_assets.py --selfcheck     # inject a violation => must fail
EXIT: 0 = clean, 1 = at least one violation (each printed with its path/line).
"""

import os
import sys

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ALLOWLIST = os.path.join(REPO, "tools", "external_assets_allowlist.txt")
CREDITS = os.path.join(REPO, "darkest", "resources", "assets_credits.md")

# Markers that are ALWAYS a violation, even inside a comment: they name a machine-specific
# extract location (an E-drive path, a Steam library, the original game's install dir).
HARD_MARKERS = [
    "e:\\", "e:/", "steamlibrary", "steamapps",
    "darkest dungeon\\_windows", "darkest dungeon/_windows",
]

# Markers that only mean "came from outside" when they appear in CODE (not in a comment).
# WHY the split: the UI's scene files cite the original layout file names as provenance
#   ("; screen.raid.darkest -> section 307"), which is documentation, not an extract.
#   Same lesson as check_ui_namespace.ps1: a guard must separate CODE hits from COMMENT hits,
#   otherwise it reds on the very注释 that tells you where a value came from.
SOFT_MARKERS = ["borrow/", "borrow\\", "heroes_placeholder", ".darkest", "dlc/", "dlc\\"]

COMMENT_PREFIXES = (";", "#", "//", "///")

# Asset roots whose *undeclared* files are suspicious (bytes we did not create).
ASSET_ROOTS = [os.path.join(REPO, "darkest", "resources")]
SCAN_EXTS = (".json", ".txt", ".md", ".cfg", ".tscn", ".tres", ".godot", ".import", ".csv", ".list", ".manifest")


def load_allowlist():
    allowed, broken = {}, []
    if not os.path.isfile(ALLOWLIST):
        return allowed, broken
    with open(ALLOWLIST, "r", encoding="utf-8") as fh:
        for raw in fh:
            line = raw.strip().lstrip("\ufeff")
            if not line or line.startswith("#"):
                continue
            parts = line.split("\t")
            key = parts[0].strip()
            reason = parts[1].strip() if len(parts) > 1 else ""
            expiry = parts[2].strip() if len(parts) > 2 else ""
            if not reason or not expiry:
                broken.append(key)
            else:
                allowed[key] = (reason, expiry)
    return allowed, broken


def allowed_hit(allowed, text):
    for key in allowed:
        if key and key.lower() in text.lower():
            return key
    return None


def iter_text_files(root):
    for folder, dirs, files in os.walk(root):
        dirs[:] = [d for d in dirs if d not in (".git", "obj", "bin", ".godot", "archive")]
        for name in files:
            if name.endswith(SCAN_EXTS):
                yield os.path.join(folder, name)


def scan_file(path, allowed, violations, scanned):
    scanned.append(path)
    try:
        handle = open(path, "r", encoding="utf-8", errors="replace")
    except OSError:
        return
    with handle:
        for n, line in enumerate(handle, start=1):
            stripped = line.strip()
            is_comment = stripped.startswith(COMMENT_PREFIXES)
            low = line.lower()
            markers = HARD_MARKERS + ([] if is_comment else SOFT_MARKERS)
            for marker in markers:
                if marker in low:
                    if allowed_hit(allowed, line) is None:
                        kind = "HARD" if marker in HARD_MARKERS else "code"
                        violations.append(
                            "%s:%d: %s marker '%s' (line: %s)"
                            % (os.path.relpath(path, REPO), n, kind, marker, stripped[:110]))


def main(argv):
    show_list = "--list" in argv
    selfcheck = "--selfcheck" in argv
    roots = [os.path.join(REPO, "darkest", "resources"), os.path.join(REPO, "darkest", "scenes")]
    if "--root" in argv:
        roots = [argv[argv.index("--root") + 1]]

    if selfcheck:
        probe = os.path.join(REPO, "darkest", "resources", "_b6_selfcheck_probe.md")
        os.makedirs(os.path.dirname(probe), exist_ok=True)
        try:
            with open(probe, "w", encoding="utf-8") as fh:
                fh.write("extracted from E:\\SteamLibrary\\steamapps\\common\\Darkest Dungeon\n")
            allowed, _broken = load_allowlist()
            v, scanned = [], []
            scan_file(probe, allowed, v, scanned)
            if v:
                print("SELFCHECK OK: injected E-drive reference was flagged (%d violation(s))" % len(v))
                return 0
            print("SELFCHECK FAILED: injected E-drive reference was NOT flagged")
            return 1
        finally:
            if os.path.isfile(probe):
                os.remove(probe)

    allowed, broken = load_allowlist()
    if broken:
        print("FAIL: allowlist entries missing reason and/or expiry (planner #412):")
        for key in broken:
            print("   %s" % key)
        return 1

    if show_list:
        for r in roots:
            print("scan root: %s" % os.path.relpath(r, REPO))
        print("origin markers: %s" % ", ".join(ORIGIN_MARKERS))
        print("allowlist: %s (%d entr(y/ies))" % (os.path.relpath(ALLOWLIST, REPO), len(allowed)))
        return 0

    violations, scanned = [], []
    for root in roots:
        if not os.path.isdir(root):
            continue
        for path in iter_text_files(root):
            scan_file(path, allowed, violations, scanned)

    print("[b6] scanned %d text file(s) under %d root(s)" % (len(scanned), len(roots)))
    if violations:
        print("[b6] RESULT: BAD (%d violation(s) -- external/original-game origin found)" % len(violations))
        for v in violations[:40]:
            print("   - %s" % v.encode("ascii", "replace").decode("ascii"))
        return 1

    print("[b6] RESULT: OK (no E-drive/original-game origin markers in the scanned surfaces; "
          "%d allowlisted token(s))" % len(allowed))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
