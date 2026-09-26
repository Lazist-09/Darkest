# -*- coding: utf-8 -*-
# Supplementary aggregates: durations, quirk diseases/incompatibilities, trinket price semantics,
# camping requirements, loot table structure.
import io, json, os, collections, re

REF = r"F:\GithubPro\Darkest-Dungeon-Unity\Assets\Resources\Data"
OUT = io.open(r"F:\GithubPro\Darkest\reports\unity_ref\_scan_out2.txt", "w", encoding="utf-8")
def w(*a): OUT.write(" ".join(str(x) for x in a) + "\n")
TRAILING = re.compile(r",(\s*[\]\}])")
def load(name):
    with io.open(os.path.join(REF, name), "r", encoding="utf-8-sig") as f: raw = f.read()
    try: return json.loads(raw)
    except Exception: return json.loads(TRAILING.sub(r"\1", raw))

b = load("JsonBuffs.json")["buffs"]
w("=== buffs WITH duration (n=%d) ===" % sum(1 for x in b if x.get("duration_type")))
w("duration_type freq:", collections.Counter(x.get("duration_type") for x in b if x.get("duration_type")).most_common())
w("duration value freq:", collections.Counter(x.get("duration") for x in b if x.get("duration_type")).most_common())
w("samples with duration:")
w(json.dumps([x for x in b if x.get("duration_type")][:4], ensure_ascii=False, indent=1)[:2500])
w("=== is_false_rule freq:", collections.Counter(x.get("is_false_rule") for x in b))
w("=== remove_if_not_active freq:", collections.Counter(x.get("remove_if_not_active") for x in b))
w("=== is_false_rule true samples (3) ===")
w(json.dumps([x for x in b if x.get("is_false_rule")][:3], ensure_ascii=False, indent=1)[:2200])
w("=== rule_data non-empty samples: float!=0 or string!='' ===")
nz = [x for x in b if x["rule_data"]["float"] != 0 or x["rule_data"]["string"] != ""]
w("count:", len(nz))
w(json.dumps(nz[:4], ensure_ascii=False, indent=1)[:2500])
w("=== rule_data.string distinct values (top 40) ===")
w(collections.Counter(x["rule_data"]["string"] for x in b if x["rule_data"]["string"]).most_common(40))
w("=== rule_data.float distinct (top 30) ===")
w(collections.Counter(x["rule_data"]["float"] for x in b if x["rule_data"]["float"] != 0).most_common(30))
w("=== amount sign per stat_type ===")
sg = collections.defaultdict(collections.Counter)
for x in b: sg[x["stat_type"]][("+" if x["amount"] >= 0 else "-")] += 1
for k in sorted(sg): w("   %-40s %s" % (k, dict(sg[k])))
w("=== rule_type x stat_type cross (top 60) ===")
cc = collections.Counter((x["rule_type"], x["stat_type"]) for x in b)
for k, v in cc.most_common(60): w("   %-30s %-40s %d" % (k[0], k[1], v))

q = load("JsonQuirks.json")["quirks"]
w("")
w("=== quirk ids by (is_disease, is_positive, classification) ===")
g = collections.defaultdict(list)
for e in q: g[(e["is_disease"], e["is_positive"], e["classification"])].append(e["id"])
for k in sorted(g, key=str): w("   %-40s n=%-3d %s" % (str(k), len(g[k]), ", ".join(g[k])))
w("=== quirk incompatible_quirks non-empty (n=%d) samples ===" % sum(1 for e in q if e["incompatible_quirks"]))
for e in q:
    if len(e["incompatible_quirks"]) >= 3:
        w("   %-28s -> %s" % (e["id"], e["incompatible_quirks"]))
w("=== quirk buff id freq (top 50) ===")
cb = collections.Counter()
for e in q: cb.update(e["buffs"])
w(cb.most_common(50))
w("=== quirks with curio_tag or keep_loot ===")
for e in q:
    if e["curio_tag"] or e["keep_loot"]: w("   %-28s tag=%-12s chance=%-5s keep_loot=%s" % (e["id"], e["curio_tag"], e["curio_tag_chance"], e["keep_loot"]))
w("=== quarantine/treatment related quirk fields: show_explicit_description freq ===", collections.Counter(e["show_explicit_description"] for e in q))

t = load("JsonTraits.json")["traits"]
w("")
w("=== traits: id / overstress_type / buff count / act_out ids ===")
for e in t:
    ids = [x["id"] for x in e["combat_start_turn_act_outs"]]
    w("   %-12s %-11s curio=%-10s buffs=%-3d actouts=%s" % (e["id"], e["overstress_type"], e["curio_tag"], len(e["buff_ids"]), ",".join(ids)))
w("=== all act_out ids seen ===")
cs = collections.Counter(); rs = collections.Counter()
for e in t:
    cs.update(x["id"] for x in e["combat_start_turn_act_outs"])
    rs.update(x["id"] for x in e["reaction_act_outs"])
w("combat_start_turn:", cs.most_common())
w("reaction:", rs.most_common())
w("=== virtue trait sample (full) ===")
for e in t:
    if e["overstress_type"] == "virtue":
        w(json.dumps(e, ensure_ascii=False, indent=1)[:3000]); break
w("=== traits buff_ids freq ===")
cb2 = collections.Counter()
for e in t: cb2.update(e["buff_ids"])
w(cb2.most_common(40))

k = load("JsonTrinkets.json")["trinkets"]
w("")
w("=== price x rarity ===")
pr = collections.defaultdict(collections.Counter)
for e in k: pr[e["rarity"]][e["price"]] += 1
for r in sorted(pr): w("   %-20s %s" % (r, dict(pr[r])))
w("=== limit==0 (unlimited) rarity dist ===", collections.Counter(e["rarity"] for e in k if e["limit"] == 0))
w("=== limit==3 ===", [e["id"] for e in k if e["limit"] == 3])
w("=== hero_class_requirements: distinct sets (n>0) ===")
hs = collections.Counter(tuple(sorted(e["hero_class_requirements"])) for e in k if e["hero_class_requirements"])
for s, v in hs.most_common(): w("   %-70s %d" % (",".join(s), v))
w("=== trinkets with most buffs (top 5) ===")
for e in sorted(k, key=lambda x: -len(x["buffs"]))[:5]:
    w("   %-42s n=%-2d %s" % (e["id"], len(e["buffs"]), e["buffs"]))
w("=== one vestal-only trinket full ===")
for e in k:
    if e["hero_class_requirements"] == ["vestal"]:
        w(json.dumps(e, ensure_ascii=False)); break
w("=== origin_dungeon non-empty list ===")
for e in [x for x in k if x["origin_dungeon"]][:14]: w("   %-40s %-10s %-12s price=%d" % (e["id"], e["origin_dungeon"], e["rarity"], e["price"]))

c = load("JsonCamping.json")["skills"]
w("")
w("=== camping: per-skill class counts / effect counts ===")
for e in c[:10]: w("   %-22s cost=%d limit=%d classes=%d effects=%d upg=%s" % (e["id"], e["cost"], e["use_limit"], len(e["hero_classes"]), len(e["effects"]), json.dumps(e["upgrade_requirements"], ensure_ascii=False)[:120]))
w("=== camping: distinct effect type aliases ===")
ets = collections.Counter(x["type"] for e in c for x in e["effects"])
w(ets.most_common())
w("=== camping: requirements values ===")
rq = collections.Counter()
for e in c:
    for x in e["effects"]: rq.update([tuple(x["requirements"])])
w(rq.most_common())
w("=== camping: buff sub_types ===")
sb = collections.Counter(x["sub_type"] for e in c for x in e["effects"] if x["type"] == "buff")
w(sb.most_common())
w("=== camping: upgrade_requirements currency types ===")
cu = collections.Counter(y["type"] for e in c for x in e["upgrade_requirements"] for y in x["currency_cost"])
w(cu.most_common())
w("=== camping: prereq non-empty ===")
for e in c:
    for x in e["upgrade_requirements"]:
        if x["prerequisite_requirements"]: w("   ", e["id"], json.dumps(x, ensure_ascii=False)[:250])

l = load("JsonLoot.json")
w("")
w("=== loot_tables: id/difficulty/dungeon map ===")
for e in l["loot_tables"]: w("   %-6s diff=%s dungeon=%-8s entries=%d" % (e["id"], e["difficulty"], e["dungeon"], len(e["entries"])))
w("=== loot entry types ===")
w(collections.Counter(x["type"] for e in l["loot_tables"] for x in e["entries"]).most_common())
w("=== loot entry data shapes by type ===")
ds = collections.Counter((x["type"], tuple(sorted(x["data"].keys()))) for e in l["loot_tables"] for x in e["entries"])
for kk, v in ds.most_common(): w("   %-60s %d" % (str(kk), v))
w("=== loot sample table full ===")
w(json.dumps([e for e in l["loot_tables"] if e["id"] == "A"][0], ensure_ascii=False, indent=1)[:1800])

n = load("Narration.json")["entries"]
w("")
w("=== narration entries: id/tone/chance/#events ===")
for e in n: w("   %-28s %-8s chance=%-5s events=%d" % (e["id"], e["tone"], e["chance"], len(e["audio_events"])))
w("=== narration filters/tags distinct (top 30) ===")
tg = collections.Counter()
for e in n:
    for a in e["audio_events"]: tg.update(a["tags"])
w(tg.most_common(30))
w("=== narration audio_event path prefixes ===")
w(collections.Counter(a["audio_event"].split("/")[1] if "/" in a["audio_event"] else "?" for e in n for a in e["audio_events"]).most_common(20))
w("=== narration chance values ===", collections.Counter(a["chance"] for e in n for a in e["audio_events"]).most_common())
w("=== narration one 'ancestor_talk' entry ===")
for e in n:
    if e["id"] == "ancestor_talk": w(json.dumps(e, ensure_ascii=False, indent=1)[:2500]); break

p = load("PartyNames.json")["party_names"]
w("")
w("=== party_names: distinct required_hero_class sets (top 20) ===")
w(collections.Counter(tuple(sorted(e["required_hero_class"])) for e in p).most_common(20))
w("=== party_names size dist ===", collections.Counter(len(e["required_hero_class"]) for e in p).most_common())
w("=== party_names ids ===", ", ".join(e["id"] for e in p))
OUT.close()
print("done")
