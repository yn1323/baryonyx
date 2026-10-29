"""Writes the rune price of each square into the nested world map layouts
(doc/features/world-atlas.md#マスを開くルーン).

usage: price_squares.py            # every layout
       price_squares.py world marina

Run after place_events.py, since a square's event changes its price. The client shows these prices
and the server's catalog (server/scripts/world-atlas-catalog.mjs) copies them, so both use the same
numbers. Rules (仮, doc/plans/2026-10-27-world-map-rune-squares.md#値段仮):
- A map has a level (the recommended level of its towns) and a base price of 100 + level x 10.
- A square costs the base times its rate, rounded to tens. The start square of a map is free.
"""
import json
import sys
from pathlib import Path

ATLAS = Path("/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/Atlas/Layouts")

# 地図ごとの推奨Lv（仮）。地域の街の推奨Lv（doc/features/stage-progression.md の街とダンジョン）から取った。
LEVELS = {
    "world": 10,
    "verde": 10,
    "marina": 14,
    "gelo": 18,
    "nebbia": 22,
    "oro": 24,
    "bosco": 30,
    "fiamma": 35,
}

# マスの種類の倍率。出来事を置いたマスは出来事で、置いていないマスはマスの種類で決める。
EVENT_RATES = {
    "battle": 1.0,
    "pickup": 1.0,
    "choice": 1.25,
    "meet": 1.25,
    "bet": 1.25,
    "peddler": 1.5,
    "chest": 2.0,
}
KIND_RATES = {"empty": 1.0, "town": 2.0, "region": 2.0}


def start_of(key):
    return "world-start" if key == "world" else "gate-" + key


def price(layout):
    key = layout["key"]
    level = LEVELS[key]
    base = 100 + level * 10
    layout["level"] = level
    layout["base"] = base
    start = start_of(key)
    for node in layout["nodes"]:
        if node["id"] == start:
            node.pop("price", None)
            continue
        rate = EVENT_RATES[node["event"]] if "event" in node else KIND_RATES[node["kind"]]
        node["price"] = int(round(base * rate / 10.0)) * 10
    return base


def main():
    keys = sys.argv[1:] or sorted(LEVELS)
    for key in keys:
        path = ATLAS / (key + ".json")
        layout = json.load(open(path, encoding="utf-8"))
        # 一番上の項目の並びを key・name・level・base・width… にそろえる。
        base = price(layout)
        ordered = {k: layout[k] for k in ("key", "name", "level", "base")}
        ordered.update({k: v for k, v in layout.items() if k not in ordered})
        prices = [n["price"] for n in layout["nodes"] if "price" in n]
        print(key, "Lv", layout["level"], "base", base, "squares", len(prices), "total", sum(prices))
        with open(path, "w", encoding="utf-8") as f:
            json.dump(ordered, f, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
