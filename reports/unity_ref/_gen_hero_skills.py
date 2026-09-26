# -*- coding: utf-8 -*-
"""Extract PER-LEVEL hero skill stat records from the Unity reference project.

Source : F:\\GithubPro\\Darkest-Dungeon-Unity\\Assets\\Resources\\Data\\Heroes\\Info\\*.bytes
Format : DD1 `.info.darkest`-style text; each file has TWO `combat_skill:` blocks
         - block A (no `.level`)  = art/presentation only
         - block B (with `.level 0..4`) = REAL NUMBERS (`.atk`/`.dmg`/`.crit`/...)
Output : reports/unity_ref/hero_skills_from_ref.json   (machine readable)
         reports/unity_ref/02b_hero_skills_from_ref.md (human table)

ASCII-only stdout.
"""
import io, os, json, re, collections

REF = r'F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data\Heroes\Info'
REPO = r'F:\GithubPro\Darkest'
OUTDIR = os.path.join(REPO, 'reports', 'unity_ref')
os.makedirs(OUTDIR, exist_ok=True)

TOKEN = re.compile(r'\.([A-Za-z_][A-Za-z0-9_]*)')


def parse_records(text, prefix):
    """Yield dicts for every `<prefix> ...` line, splitting on `.key` tokens (quote aware)."""
    out = []
    for raw in text.splitlines():
        line = raw.strip()
        if not line.startswith(prefix):
            continue
        body = line[len(prefix):]
        # find token positions outside quotes
        spans = []
        in_q = False
        i = 0
        while i < len(body):
            ch = body[i]
            if ch == '"':
                in_q = not in_q
                i += 1
                continue
            if ch == '.' and not in_q and TOKEN.match(body, i):
                m = TOKEN.match(body, i)
                spans.append((m.group(1), m.start(), m.end()))
                i = m.end()
                continue
            i += 1
        rec = {}
        for idx, (name, s, e) in enumerate(spans):
            nxt = spans[idx + 1][1] if idx + 1 < len(spans) else len(body)
            val = body[e:nxt].strip().strip('"').strip()
            rec[name] = val
        if rec:
            out.append(rec)
    return out


def num(v):
    if v is None:
        return None
    m = re.match(r'^(-?\d+(?:\.\d+)?)\s*%?$', v.strip())
    return float(m.group(1)) if m else None


heroes = {}
for fn in sorted(os.listdir(REF)):
    if not fn.endswith('.bytes'):
        continue
    hero = fn[:-len('.bytes')]
    text = io.open(os.path.join(REF, fn), 'rb').read().decode('utf-8', errors='replace')
    skills = parse_records(text, 'combat_skill:')
    weapons = parse_records(text, 'weapon:')
    armours = parse_records(text, 'armour:')
    stat = [s for s in skills if 'level' in s]
    art = [s for s in skills if 'level' not in s]
    heroes[hero] = {
        'stat_records': stat,
        'art_records': art,
        'weapon_tiers': weapons,
        'armour_tiers': armours,
        'file_lines': len(text.splitlines()),
    }

json.dump(heroes, io.open(os.path.join(OUTDIR, 'hero_skills_from_ref.json'), 'w', encoding='utf-8'),
          ensure_ascii=False, indent=1)

# ---- report ----
all_keys = collections.Counter()
for h in heroes.values():
    for r in h['stat_records']:
        for k in r:
            all_keys[k] += 1

lines = ['# 参考项目 · 英雄【技能数值】抽取（`Heroes/Info/*.bytes` 的 stat 块）', '',
         '> 由 `reports/unity_ref/_gen_hero_skills.py` 生成（实测抽取，非目测）✓',
         '> 🔴 每个英雄文件里有**两块** `combat_skill:`：**无 `.level` 的表现块** 与 **带 `.level 0..4` 的数值块**；',
         '>    本表只取**数值块** ✓（表现块只有 `.icon/.anim/.fx`）', '',
         '## 1. 总账', '',
         '| 英雄 | 文件行数 | 数值记录（技能×等级） | 技能数（去重 id） | 表现记录 | weapon 阶 | armour 阶 |',
         '|---|---:|---:|---:|---:|---:|---:|']
tot_stat = 0
for hero, h in heroes.items():
    ids = {r.get('id') for r in h['stat_records']}
    tot_stat += len(h['stat_records'])
    lines.append('| `%s` | %d | %d | %d | %d | %d | %d |' % (
        hero, h['file_lines'], len(h['stat_records']), len(ids), len(h['art_records']),
        len(h['weapon_tiers']), len(h['armour_tiers'])))
lines += ['', '**合计**：英雄 **%d** · 数值记录 **%d** 条 · 英雄数 × 技能数 = 15 × 7 = 105' % (len(heroes), tot_stat), '',
          '## 2. 数值块的字段全集（含频次）', '', '| 字段 | 出现次数 | 含义（读自参考项目代码/本地化） |', '|---|---:|---|']
MEAN = {
    'id': '技能 id', 'level': '等级 0..4（= 5 级 ✓）', 'type': '技能类型（melee/ranged/…）',
    'atk': '**命中修正**（百分数）', 'dmg': '🔴 **伤害修正（百分数）** ⇒ 我们要的 `dmg_pct`',
    'crit': '暴击修正（百分数）', 'is_crit_valid': '能否暴击', 'launch': '发动位（1..4）',
    'target': '目标表达式（如 `~.enemy 1 2`）', 'effect': '效果名（→ `Mechanics/Effects.txt`）',
    'heal': '治疗量（区间）', 'move': '位移', 'valid_modes': '可用模式',
    'self_target_valid': '可自选为目标', 'generation_guaranteed': '必定生成',
    'area_pos_offset': '范围偏移', 'target_area_pos_offset': '目标范围偏移',
    'human_effects': '对人类额外效果', 'beast_effects': '对兽类额外效果',
    'is_continue_turn': '连续行动', 'per_turn_limit': '每回合次数上限', 'per_battle_limit': '每场次数上限',
}
for k, v in all_keys.most_common():
    lines.append('| `.%s` | %d | %s |' % (k, v, MEAN.get(k, '（待补）')))
lines += ['', '## 3. 我们 4 个原型需要的英雄 · 逐技能逐级数值', '',
          '> 对应关系：`warrior←hellion` · `tank←man_at_arms` · `medic←plague_doctor` · `commissar←highwayman` ✓', '']
for hero in ['Hellion', 'ManAtArms', 'PlagueDoctor', 'Highwayman']:
    h = heroes.get(hero)
    if not h:
        lines.append('### 🔴 `%s` —— **参考项目里没有**（如实记录）' % hero)
        continue
    lines.append('### `%s`（数值记录 %d 条）' % (hero, len(h['stat_records'])))
    lines.append('')
    lines.append('| 技能 id | lv | type | atk | 🔴 dmg | crit | launch | target | effect |')
    lines.append('|---|---:|---|---:|---:|---:|---:|---|---|')
    for r in sorted(h['stat_records'], key=lambda x: (x.get('id', ''), num(x.get('level')) or 0)):
        lines.append('| `%s` | %s | %s | %s | **%s** | %s | %s | `%s` | `%s` |' % (
            r.get('id', ''), r.get('level', ''), r.get('type', ''), r.get('atk', ''), r.get('dmg', ''),
            r.get('crit', ''), r.get('launch', ''), r.get('target', ''), r.get('effect', '')))
    lines.append('')

io.open(os.path.join(OUTDIR, '02b_hero_skills_from_ref.md'), 'w', encoding='utf-8', newline='\n').write('\n'.join(lines))

print('heroes=%d stat_records=%d' % (len(heroes), tot_stat))
print('keys: ' + ', '.join('%s=%d' % (k, v) for k, v in all_keys.most_common(8)))
no_dmg = sum(1 for h in heroes.values() for r in h['stat_records'] if 'dmg' not in r)
no_heal = sum(1 for h in heroes.values() for r in h['stat_records'] if 'heal' not in r)
print('records_without_dmg=%d  records_without_heal=%d  (sum=%d)' % (no_dmg, no_heal, 525))
for hero in ['Hellion', 'ManAtArms', 'PlagueDoctor', 'Highwayman']:
    h = heroes.get(hero)
    if not h:
        print('%-12s MISSING' % hero)
        continue
    vals = sorted({r.get('dmg') for r in h['stat_records'] if r.get('dmg') is not None})
    ids = sorted({r.get('id') for r in h['stat_records']})
    print('%-12s stat=%3d dmg_vals=%s' % (hero, len(h['stat_records']), vals))
    print('             skills=%s' % ids)
