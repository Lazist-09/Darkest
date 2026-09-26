import os, sys, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from ddulib import *

d = load(os.path.join(REF, "JsonQuests.json"))
rw = d["generation"]["rewards"]
out("### resolve_xp_table")
out(json.dumps(rw["resolve_xp_table"]))
out("")
out("### trinket_chance_table")
out(json.dumps(rw["trinket_chance_table"], indent=1))
out("")
out("### item_table (full)")
out(json.dumps(rw["item_table"]))
out("")
out("### comment")
out(json.dumps(rw["comment"]))
out("")
# counts
out("COUNTS:")
out("  goals                =", len(d["goals"]))
out("  town_progression_ids =", len(d["town_progression_goal_ids"]))
out("  types                =", len(d["types"]), [t["id"] for t in d["types"]])
out("  plot_quests          =", len(d["plot_quests"]))
out("  generation.type.available_quests_table =", len(d["generation"]["type"]["available_quests_table"]))
for ent in d["generation"]["type"]["available_quests_table"]:
    tb = ent["generated_quest_table"]
    out("    dungeon=%s mastery_levels=%d rows=%s" % (ent["dungeon"], len(tb), [len(r) for r in tb]))
# plot quests
out("")
out("PLOT QUESTS:")
for pq in d["plot_quests"]:
    q = pq["quest"]
    items = q.get("completion_reward", {}).get("items_definition", {}).get("items", {})
    desc = ";".join("%s:%s=%s" % (v.get("type"), v.get("id"), v.get("amount")) for v in items.values())
    out("  id=%-32s dlvl=%-2s diff=%s len=%s type=%-20s dungeon=%-8s xp=%s items=[%s]" % (
        pq["id"], pq.get("dungeon_level"), q["difficulty"], q["length"], q["type"], q["dungeon"],
        q.get("completion_reward", {}).get("resolve_xp"), desc))
out("")
out("PLOT QUEST KEYS:", list(d["plot_quests"][0].keys()))
out("PLOT QUEST.quest KEYS:", list(d["plot_quests"][0]["quest"].keys()))
out("GOAL KEYS:", list(d["goals"][0].keys()))
out("GOAL TYPES:", sorted(set(g["type"] for g in d["goals"])))
# diff/len distribution across plot quests
import collections
c = collections.Counter()
for pq in d["plot_quests"]:
    c[(pq["quest"]["difficulty"], pq["quest"]["length"])] += 1
out("PLOT (difficulty,length) counts:", sorted(c.items()))
out("STRESS_DAMAGE:", d["stress_damage"])
out("RESTRICTION:", json.dumps(d["restriction"]))
