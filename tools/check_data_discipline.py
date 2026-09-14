#!/usr/bin/env python3
"""数据纪律审计（用户指令 2026-09-14：数字外置 / 不许死代码·死函数·死数据）。

三扫 + 负向自检（照 `tools/check_godot_refs.py` 的模式）：

  ① --numbers   内核（core / data / gameplay/sim）里的**数字字面量** ⇒ 可调数字应住 `darkest/data/*.json`
                 只放行【结构性常量】：0 / 1 / 2 / -1 / 100 与 0.0 / 1.0 / 2.0 / 100.0 / 0.01，
                 以及 `const` 声明行上的字面量 ✓（其余一律报可疑 —— 供人判读，不做自动改写）
  ② --deadfuncs `public` 方法在【生产代码】里没有任何调用点（**只被测试调用也算**）⇒ 必须接线或删
  ③ --deadkeys  `darkest/data/*.json` 里**没有任何代码读取**的键 ⇒ 死数据（必须接线或显式登记"未消费"）

用法：
  python tools/check_data_discipline.py --numbers            # 扫数字（有可疑 ⇒ 退出码 1）
  python tools/check_data_discipline.py --deadfuncs
  python tools/check_data_discipline.py --deadkeys
  python tools/check_data_discipline.py --all --report       # 只报告、不因可疑而失败（基线用）
  python tools/check_data_discipline.py --selfcheck          # 注入探针 ⇒ 三扫都必须抓到（否则退出码 1）
"""

from __future__ import annotations

import argparse
import json
import re
import sys
import tempfile
from pathlib import Path

# ⚠️ Windows 控制台默认 GBK ⇒ 输出里的非 GBK 字符（如 emoji）会直接抛 UnicodeEncodeError
#    （我第一版就被它打断：`print("🔴 …")` ⇒ 工具崩在打印上）⇒ 统一把 stdout 设成 UTF-8 + 容错 ✓
try:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")  # type: ignore[attr-defined]
except Exception:  # pragma: no cover - 老解释器/非常规 stdout
    pass

REPO = Path(__file__).resolve().parent.parent


def rel(path: Path) -> str:
    """相对仓库显示（探针在临时目录时会失败 ⇒ 退回绝对路径，不抛）✓"""
    try:
        return str(path.relative_to(REPO))
    except ValueError:
        return str(path)
SCRIPTS = REPO / "darkest" / "scripts"
DATA = REPO / "darkest" / "data"
KERNEL_DIRS = [SCRIPTS / "core", SCRIPTS / "data", SCRIPTS / "gameplay" / "sim"]

# 结构性常量白名单（**按数值**的兜底下限）——
# ⚠️ 优先用 `tools/number_allowlist.txt`（**按路径+片段+理由**放行，可审）；
#    这里的按数值白名单只保留"到处都算结构性"的极小集合，**绝不**为了变绿而加平衡数字（那是本纪律禁止的）
ALLOWED_NUMBERS = {
    "0", "1", "2", "-1", "100",                       # 下标 / 哨兵 / 百分数基数
    "0.0", "1.0", "2.0", "100.0", "0.01", "1e-9",     # 归一 / 容差 / 单位换算
}

ALLOWLIST_FILE = REPO / "tools" / "number_allowlist.txt"


def load_allowlist() -> list[tuple[str, str, str]]:
    """读路径级白名单：`路径 | 片段 | 理由` ⇒ [(path_pattern, needle, reason)] ✓"""
    rules: list[tuple[str, str, str]] = []
    if not ALLOWLIST_FILE.exists():
        return rules
    for raw in ALLOWLIST_FILE.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        parts = [p.strip() for p in line.split("|")]
        if len(parts) >= 2:
            rules.append((parts[0], parts[1], parts[2] if len(parts) > 2 else ""))
    return rules


def allowlisted(rel_path: str, line: str, rules: list[tuple[str, str, str]]) -> bool:
    norm = rel_path.replace("\\", "/")
    for path_pat, needle, _reason in rules:
        if needle and needle in line and (path_pat in norm or norm.endswith(path_pat.lstrip("*"))):
            return True
    return False


NUM_RE = re.compile(r"(?<![\w.])(-?\d+(?:\.\d+)?(?:e-?\d+)?)(?![\w.])")
LINE_COMMENT_RE = re.compile(r"//.*?$", re.MULTILINE)
BLOCK_COMMENT_RE = re.compile(r"/\*.*?\*/", re.DOTALL)
STRING_RE = re.compile(r'"(?:\\.|[^"\\])*"')


def cs_files(dirs: list[Path]) -> list[Path]:
    out: list[Path] = []
    for d in dirs:
        if d.exists():
            out.extend(sorted(d.rglob("*.cs")))
    return out


# 打印条数上限（`--limit` 写入；0 = 全部）—— `--limit 0` 用于生成 triage 全清单 ✓
PRINT_LIMIT = 25


def show(items: list[str], label: str) -> None:
    if PRINT_LIMIT == 0 or len(items) <= PRINT_LIMIT:
        for line in items:
            print(f"  [!] {line}")
    else:
        for line in items[:PRINT_LIMIT]:
            print(f"  [!] {line}")
        print(f"  …（其余 {len(items) - PRINT_LIMIT} 处略；用 --limit 0 看全部）")
    _ = label


def strip_code(text: str) -> str:
    """去注释与字符串字面量（避免把注释里的数字/说明当成代码数字）✓"""
    text = BLOCK_COMMENT_RE.sub(" ", text)
    text = LINE_COMMENT_RE.sub(" ", text)
    return STRING_RE.sub('""', text)


def scan_numbers(files: list[Path], verbose: bool) -> tuple[int, list[str]]:
    suspicious: list[str] = []
    total_literals = 0
    rules = load_allowlist()
    for f in files:
        raw = f.read_text(encoding="utf-8", errors="replace")
        code = strip_code(raw)
        for lineno, line in enumerate(code.splitlines(), start=1):
            if re.search(r"\bconst\b", line):  # const 声明行：命名常量 ⇒ 放行（但仍计入统计）
                continue
            if allowlisted(rel(f), line, rules):  # 路径级白名单（带理由）⇒ 放行 ✓
                continue
            for m in NUM_RE.finditer(line):
                lit = m.group(1)
                total_literals += 1
                if lit not in ALLOWED_NUMBERS:
                    suspicious.append(f"{rel(f)}:{lineno}: 数字 {lit} ⇒ {line.strip()[:88]}")
    if verbose:
        print(f"[numbers] 内核扫描：{len(files)} 文件 ／ 字面量 {total_literals} 处 ／ 可疑 {len(suspicious)} 处"
              f"（路径级白名单 {len(rules)} 条）")
        for line in suspicious[:25]:
            print("  [!] " + line)
        if len(suspicious) > 25:
            print(f"  …（其余 {len(suspicious) - 25} 处略）")
    return len(suspicious), suspicious


PUBLIC_METHOD_RE = re.compile(r"\bpublic\s+(?:static\s+|virtual\s+|override\s+|sealed\s+|async\s+)*"
                              r"(?:[\w<>,\[\]\.\?]+\s+)?(\w+)\s*\(")
GODOT_LIFECYCLE = {"_Ready", "_Process", "_PhysicsProcess", "_Input", "_UnhandledInput", "_Draw",
                   "_GuiInput", "_Notification", "_EnterTree", "_ExitTree", "Dispose"}
# 编译器/记录生成的成员（不是"我们写的函数"，别算死函数）✓
COMPILER_GENERATED = {"GetHashCode", "Equals", "ToString", "Deconstruct", "PrintMembers", "Clone"}


def scan_deadfuncs(verbose: bool) -> tuple[int, list[str]]:
    kernel = cs_files(KERNEL_DIRS)
    everything = cs_files([SCRIPTS]) + cs_files([REPO / "darkest" / "tests"])
    corpus = {f: strip_code(f.read_text(encoding="utf-8", errors="replace")) for f in everything}
    raw = {f: f.read_text(encoding="utf-8", errors="replace") for f in everything}

    dead: list[str] = []
    for f in kernel:
        text = corpus.get(f, "")
        # 🔴 该文件里声明的类型名（record/class）⇒ 与类型同名的"方法"其实是**构造函数**，不是死函数 ✓
        type_names = set(re.findall(r"\b(?:record|class)\s+(\w+)", raw.get(f, "")))
        for m in PUBLIC_METHOD_RE.finditer(text):
            name = m.group(1)
            if name in GODOT_LIFECYCLE or name in COMPILER_GENERATED or name in type_names:
                continue
            if name.startswith("get_") or name.startswith("set_") or name.startswith("op_"):
                continue
            # 生产调用点 = 除本文件与 tests 之外的任何地方出现该方法名
            callers = 0
            for g, gtext in corpus.items():
                if g == f or "tests" in g.parts:
                    continue
                if re.search(rf"\b{re.escape(name)}\s*\(", gtext):
                    callers += 1
                    break
            if callers == 0:
                dead.append(f"{rel(f)}: public {name}(…) 无生产调用点（只被测试调用也算死函数）")
    if verbose:
        print(f"[deadfuncs] 内核 public 方法扫描完成 ⇒ 疑似死函数 {len(dead)} 个")
        for line in dead[:25]:
            print("  [!] " + line)
        if len(dead) > 25:
            print(f"  …（其余 {len(dead) - 25} 个略）")
    return len(dead), dead


def leaf_keys(obj, prefix: str = "") -> list[str]:
    out: list[str] = []
    if isinstance(obj, dict):
        for k, v in obj.items():
            out.append(k)
            out.extend(leaf_keys(v, prefix + k + "."))
    elif isinstance(obj, list):
        for v in obj:
            out.extend(leaf_keys(v, prefix))
    return out


DOC_ONLY_KEYS = {"_note", "note", "source", "config", "version"}

# 🔴 **文档键约定**（P29 允许的"显式登记未消费"形态之一）：以这些后缀结尾的键 = **给人读的设计说明**
#    （`*_note` 写"为什么这么定/决策号"，`*_rule` 写规则语义）⇒ 它们**不进**死数据报告 ✓
#    ⚠️ 约定必须**可审**：判据是"该键是否被任何 .cs 解析" —— 已解析却无人读的键**不许**走这条豁免 ⚠️
DOC_KEY_SUFFIXES = ("_note", "_rule")

# 其它**显式登记为未消费**的数据键（非 `_note` 命名，但同样是"给人看的语义注解"）—— 见 `tools/deadkey_allowlist.txt` ✓
DEADKEY_ALLOWLIST_FILE = REPO / "tools" / "deadkey_allowlist.txt"


def load_deadkey_allowlist() -> list[tuple[str, str, str]]:
    """读 `json文件 | 键 | 理由` ⇒ [(json, key, reason)] ✓"""
    rules: list[tuple[str, str, str]] = []
    if not DEADKEY_ALLOWLIST_FILE.exists():
        return rules
    for raw in DEADKEY_ALLOWLIST_FILE.read_text(encoding="utf-8", errors="replace").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        parts = [p.strip() for p in line.split("|")]
        if len(parts) >= 2:
            rules.append((parts[0], parts[1], parts[2] if len(parts) > 2 else ""))
    return rules


def scan_deadkeys(verbose: bool) -> tuple[int, list[str]]:
    # 🔴 **必须搜【原始文本】**：JSON 键在 C# 里就住在 `[JsonPropertyName("buffs")]` 这类**字符串字面量**中
    #    ⇒ 我第一版先 `strip_code()`（把字面量清空）再搜 ⇒ **395 个假死数据**（"buffs"/"duration" 全中招）⚠️
    code = "\n".join(f.read_text(encoding="utf-8", errors="replace") for f in cs_files([SCRIPTS]))
    allow = load_deadkey_allowlist()
    exempted = 0
    dead: list[str] = []
    for jf in sorted(DATA.glob("*.json")):
        try:
            obj = json.loads(jf.read_text(encoding="utf-8"))
        except json.JSONDecodeError as ex:
            dead.append(f"{jf.name}: JSON 解析失败（{ex}）")
            continue
        for key in sorted(set(leaf_keys(obj))):
            # 🔴 放行两类（都属 P29 允许的"显式登记未消费"）：
            #    ① 工具内置的文档键（`_note`/`source`/…）② **文档键约定**（`*_note` / `*_rule`）
            #    ③ 显式豁免清单（`tools/deadkey_allowlist.txt`，逐条带理由）✓
            if key in DOC_ONLY_KEYS or key.endswith(DOC_KEY_SUFFIXES):
                exempted += 1
                continue
            if any(j == jf.name and k == key for j, k, _r in allow):
                exempted += 1
                continue
            if not re.search(rf'"{re.escape(key)}"', code):
                dead.append(f"{jf.name}: 键 \"{key}\" 在任何 .cs 里都不出现 ⇒ 疑似死数据")
    if verbose:
        print(f"[deadkeys] 数据键扫描完成 ⇒ 疑似死数据 {len(dead)} 个"
              f"（按文档键约定/显式豁免放行 {exempted} 个）")
        show(dead, "deadkeys")
    return len(dead), dead


def selfcheck() -> int:
    """注入探针 ⇒ 三扫都必须抓到（否则退出码 1）。探针用完即删 ✓"""
    with tempfile.TemporaryDirectory() as td:
        tdp = Path(td)
        (tdp / "Probe.cs").write_text(
            "namespace Probe;\npublic sealed class P {\n"
            "    public int Dmg() => 37;                    // 探针①：可调数字\n"
            "    public int NeverCalled() => 1;             // 探针②：死函数\n"
            "    public int Reads() => 1;                   // （这个也不被调用，但重复报同一类即可）\n"
            "}\n", encoding="utf-8")
        (tdp / "probe.json").write_text('{ "probe_key_xyz": 1 }', encoding="utf-8")

        global SCRIPTS, DATA, KERNEL_DIRS
        old_scripts, old_data, old_kernel = SCRIPTS, DATA, KERNEL_DIRS
        try:
            SCRIPTS, DATA = tdp, tdp
            KERNEL_DIRS = [tdp]
            n1, _ = scan_numbers([tdp / "Probe.cs"], verbose=False)
            n2, _ = scan_deadfuncs(verbose=False)
            n3, _ = scan_deadkeys(verbose=False)
        finally:
            SCRIPTS, DATA, KERNEL_DIRS = old_scripts, old_data, old_kernel

        ok = n1 >= 1 and n2 >= 1 and n3 >= 1
        print(f"[selfcheck] 探针①数字={n1}（须≥1） ②死函数={n2}（须≥1） ③死数据={n3}（须≥1） "
              f"=> {'✅ PASS' if ok else '🔴 FAIL'}")
        return 0 if ok else 1


def main() -> int:
    ap = argparse.ArgumentParser(description="数据纪律审计（数字外置 / 死函数 / 死数据）")
    ap.add_argument("--numbers", action="store_true")
    ap.add_argument("--deadfuncs", action="store_true")
    ap.add_argument("--deadkeys", action="store_true")
    ap.add_argument("--all", action="store_true")
    ap.add_argument("--report", action="store_true", help="只报告，不因可疑而失败（基线用）")
    # ⚠️ 我一度加了 `--limit` 但没把它接进打印 ⇒ 那是**死参数**（违反"不许死代码"）⇒ 已删除：
    #    三扫固定打印前 25 条 + "其余 N 处略"（要全清单就改这里的常量，别留一个不起作用的开关）✓
    ap.add_argument("--selfcheck", action="store_true")
    a = ap.parse_args()

    if a.selfcheck:
        return selfcheck()

    if not any([a.numbers, a.deadfuncs, a.deadkeys, a.all]):
        a.all = True

    total = 0
    if a.numbers or a.all:
        n, _ = scan_numbers(cs_files(KERNEL_DIRS), verbose=True)
        total += n
    if a.deadfuncs or a.all:
        n, _ = scan_deadfuncs(verbose=True)
        total += n
    if a.deadkeys or a.all:
        n, _ = scan_deadkeys(verbose=True)
        total += n

    print(f"[summary] 三扫合计可疑 {total} 处（⚠️ 这是【供人判读】的清单，不是自动判罪；"
          f"结构性常量可加入白名单，平衡数字必须搬去 data/*.json）")
    return 0 if (a.report or total == 0) else 1


if __name__ == "__main__":
    sys.exit(main())
