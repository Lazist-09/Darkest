# replace_text_in_files.py -- literal, byte-faithful find/replace across files.
#
# Why a tool: the shells in this project mangle CJK payloads (GBK console, .ps1 without
# BOM) and universal-newline reads silently rewrite CRLF.  So the payloads live in
# UTF-8 files and every read/write here uses newline="" (see discipline BX/BP/BZ).
#
# Usage:
#   python tools/dsh/replace_text_in_files.py <old-file> <new-file> <target> [<target> ...]
#
# Prints ASCII only: per target, how many literal occurrences were replaced.

import sys


def main(argv):
    if len(argv) < 3:
        print("usage: replace_text_in_files.py <old-file> <new-file> <target> [...]")
        return 2
    with open(argv[0], "r", encoding="utf-8", newline="") as fh:
        old = fh.read()
    with open(argv[1], "r", encoding="utf-8", newline="") as fh:
        new = fh.read()
    if old.endswith("\n") and not new.endswith("\n"):
        new += "\n"
    rc = 0
    for t in argv[2:]:
        with open(t, "r", encoding="utf-8", newline="") as fh:
            text = fh.read()
        n = text.count(old)
        print("%-46s occurrences=%d" % (t, n))
        if n == 0:
            rc = 1
            continue
        with open(t, "w", encoding="utf-8", newline="") as fh:
            fh.write(text.replace(old, new))
    return rc


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
