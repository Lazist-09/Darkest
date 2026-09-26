import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import io
import json
import os
import re

ROOT = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
MON = os.path.join(ROOT, "Monsters")

files = sorted(f for f in os.listdir(MON) if not f.endswith(".meta"))
print(f"Monsters: {len(files)} 个文件")

# ============ 解析器 ============
def parse_file(path: str):
    """把 DD1 的 `.txt` 拆成 `{section: [行]}` —— **按段（section）分** ✓

    🔴 ⚠️ **我第一版把 `name:` / `type:` 也当"段内字段"去找 ⇒ 230/230 全空** ⚠️
       实测布局（`brigand_cutthroat_A.txt`）：
           L1 `name: brigand_cutthroat_A`   ← **文件级头两行，在任何 `xxx:` 段【之前】**
           L2 `type: brigand_cutthroat`
           L4 `art:`  段 … L10 `.end`
           L12 `info:` 段 … L26 `.end`
       ⇒ ✅ **两条字段是【段外】的** ⇒ 必须单独按"段名之前的前缀区"解析 ✓
       （纪律 BH：**换个口径，数就变了 ⇒ 我读窄了** —— 这是本任务里第三次同族错）
    """
    text = io.open(path, encoding="utf-8-sig", errors="replace").read()
    out = {"sections": {}, "lines": {}, "header": [], "header_lines": []}
    sec = None
    for i, raw in enumerate(text.splitlines(), 1):
        s = raw.strip()
        if not s:
            continue
        if s.endswith(":") and not s.startswith("."):
            sec = s[:-1]
            out["sections"].setdefault(sec, [])
            out["lines"].setdefault(sec, [])
            continue
        if s == ".end":
            sec = None
            continue
        if sec is None:
            out["header"].append(s)
            out["header_lines"].append(i)
            continue
        out["sections"][sec].append(s)
        out["lines"][sec].append(i)
    return out


def kv(line: str, key: str):
    m = re.search(r"\." + key + r"\s+(-?[\d.]+)(%?)", line)
    return (float(m.group(1)), m.group(2) == "%") if m else None


def parse_stats(line: str):
    d = {}
    for g in re.finditer(r"\.([a-z_]+)\s+(-?[\d.]+)(%?)", line):
        d[g.group(1)] = {"value": float(g.group(2)), "percent": g.group(3) == "%"}
    return d


def parse_skill(line: str):
    """skill 行：`.id "x" .type "melee" .atk 100% .dmg 1 2 .crit 0% .effect "A" "B" ..."""
    out = {}
    m = re.search(r'\.id\s+"([^"]+)"', line)
    out["id"] = m.group(1) if m else None
    m = re.search(r'\.type\s+"([^"]+)"', line)
    out["type"] = m.group(1) if m else None
    m = re.search(r"\.atk\s+(-?[\d.]+)%", line)
    out["atk_pct"] = float(m.group(1)) if m else None
    m = re.search(r"\.dmg\s+(-?\d+)\s+(-?\d+)", line)
    out["dmg_min"], out["dmg_max"] = (int(m.group(1)), int(m.group(2))) if m else (None, None)
    m = re.search(r"\.crit\s+(-?[\d.]+)%", line)
    out["crit_pct"] = float(m.group(1)) if m else None
    m = re.search(r"\.launch\s+(\S+)", line)
    out["launch"] = m.group(1) if m else None
    m = re.search(r"\.target\s+(\S+)", line)
    out["target"] = m.group(1) if m else None
    eff = re.search(r"\.effect\s+(.*?)(?:\s+\.\w|\s*$)", line)
    out["effects"] = re.findall(r'"([^"]*)"', eff.group(1)) if eff else []
    out["move"] = kv(line, "move")
    m = re.search(r"\.extra_targets_count\s+(\d+)", line)
    out["extra_targets_count"] = int(m.group(1)) if m else None
    m = re.search(r"\.extra_targets_chance\s+([\d.]+)", line)
    out["extra_targets_chance"] = float(m.group(1)) if m else None
    m = re.search(r"\.heal\s+(-?\d+)\s+(-?\d+)", line)
    out["heal"] = (int(m.group(1)), int(m.group(2))) if m else None
    for flag in ("is_crit_valid", "is_knowledgeable", "self_target_valid", "can_miss",
                 "is_user_selected_targets"):
        m = re.search(r"\." + flag + r"\s+(True|False)", line, re.I)
        out[flag] = (m.group(1).lower() == "true") if m else None
    return out


records = []
for name in files:
    p = os.path.join(MON, name)
    parsed = parse_file(p)
    info = parsed["sections"].get("info", [])
    info_lines = parsed["lines"].get("info", [])
    rec = {"file": name, "stem": name[:-4], "type": None, "type_line": None,
           "name": None, "name_line": None,
           "stats": None, "stats_line": None, "skills": [], "enemy_types": [],
           "monster_brain": None, "initiative": None, "loot": None,
           "display_size": None, "battle_modifier": None}

    # 🔴 **段外头两行**（`name:` / `type:`）—— 见 `parse_file` 的 WHY ✓
    for line, lineno in zip(parsed["header"], parsed["header_lines"]):
        if line.startswith("type:"):
            rec["type"] = line.split(":", 1)[1].strip()
            rec["type_line"] = lineno
        elif line.startswith("name:"):
            rec["name"] = line.split(":", 1)[1].strip()
            rec["name_line"] = lineno

    for line, lineno in zip(info, info_lines):
        if line.startswith("stats:"):
            rec["stats"] = parse_stats(line)
            rec["stats_line"] = lineno
        elif line.startswith("skill:"):
            sk = parse_skill(line)
            sk["line"] = lineno
            rec["skills"].append(sk)
        elif line.startswith("enemy_type:"):
            m = re.search(r'\.id\s+"?([^"\s]+)"?', line)
            if m:
                rec["enemy_types"].append(m.group(1))
        elif line.startswith("monster_brain:"):
            m = re.search(r"\.id\s+(\S+)", line)
            rec["monster_brain"] = m.group(1) if m else None
        elif line.startswith("initiative:"):
            m = re.search(r"\.number_of_turns_per_round\s+(\d+)", line)
            rec["initiative"] = int(m.group(1)) if m else None
        elif line.startswith("loot:"):
            m = re.search(r'\.code\s+"?([^"\s]+)"?', line)
            c = re.search(r"\.count\s+(\d+)", line)
            rec["loot"] = {"code": m.group(1) if m else None,
                           "count": int(c.group(1)) if c else None}
        elif line.startswith("display:"):
            m = re.search(r"\.size\s+(\d+)", line)
            rec["display_size"] = int(m.group(1)) if m else None
        elif line.startswith("battle_modifier:"):
            rec["battle_modifier"] = line.split(":", 1)[1].strip()
    records.append(rec)

print(f"解析出 {len(records)} 条记录")
print()
print("=== 完整性自检（不许有字段大面积为空）===")
for key in ("type", "name", "stats", "monster_brain", "initiative"):
    n = sum(1 for r in records if r[key] is not None)
    print(f"   {key:18s} 有值 {n}/{len(records)}")
    if n < len(records):
        miss = [r["stem"] for r in records if r[key] is None][:5]
        print(f"      缺失样例：{miss}")
n_sk = sum(len(r["skills"]) for r in records)
print(f"   skills 总行数 {n_sk}")

os.makedirs("reports/unity_ref", exist_ok=True)
with open("reports/unity_ref/monsters_from_ref.json", "w", encoding="utf-8", newline="") as fh:
    json.dump({
        "source": r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Monsters\*.txt",
        "note": "A4 步1 抽取：参考项目 230 个怪物文件（`.meta` 不入账）。"
                "每条带 file + line 出处 ⇒ 可复算 ✓ 本件【只抽不落库】✓",
        "count": len(records),
        "monsters": records,
    }, fh, ensure_ascii=False, indent=1)
    fh.write("\n")
print()
print("写出：reports/unity_ref/monsters_from_ref.json")
print("bytes:", os.path.getsize("reports/unity_ref/monsters_from_ref.json"))
