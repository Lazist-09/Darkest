# land_attachment_verbatim.py -- land a file byte-exactly under a header, then prove it.
#
# Why: a third-party report must be cited from inside the repo, and "verbatim" must
# be a *measured* claim, not a promise (discipline AW/BC).  This tool:
#   1) writes HEADER bytes + SOURCE bytes to DEST (no newline translation at all)
#   2) re-reads DEST, strips the header, and compares bytes + sha256 with SOURCE
#   3) prints ASCII only (GBK console safe)
#
# Usage:
#   python tools/dsh/land_attachment_verbatim.py <header-file> <source-file> <dest-file>

import hashlib
import sys


def main(argv):
    if len(argv) != 3:
        print("usage: land_attachment_verbatim.py <header> <source> <dest>")
        return 2
    header_path, source_path, dest_path = argv
    with open(header_path, "rb") as fh:
        header = fh.read()
    with open(source_path, "rb") as fh:
        source = fh.read()
    with open(dest_path, "wb") as fh:
        fh.write(header)
        fh.write(source)
    with open(dest_path, "rb") as fh:
        landed = fh.read()
    body = landed[len(header):]
    ok = body == source
    print("header bytes : %d" % len(header))
    print("source bytes : %d  sha256=%s" % (len(source), hashlib.sha256(source).hexdigest()))
    print("landed bytes : %d" % len(landed))
    print("body bytes   : %d  sha256=%s" % (len(body), hashlib.sha256(body).hexdigest()))
    print("VERBATIM     : %s" % ok)
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
