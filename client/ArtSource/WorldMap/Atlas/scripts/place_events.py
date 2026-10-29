"""Places the square events (出来事) on a nested world map layout, as a trial
(doc/features/world-atlas.md#マスに置いた出来事).

usage: place_events.py <key>...    # e.g. place_events.py world marina

Run after to_unity.py layouts, which writes the layouts without events. The same key always
gives the same placement. Rules (仮):
- Only empty squares get an event, and two events have at least two empty squares between
  them on the roads, so about a third of the squares hold one.
- The events nearest to the start are pickups, so the first one is a small, sure reward.
- Dead ends get a hidden chest (the reward for the detour).
- One meeting per map, about halfway from the start.
- Peddlers sit far from towns, region squares and gates; about one for every 12 events.
- Bets: about one for every 15 events.
- The rest are battles, pickups and choices, and no two battles are 4 squares or nearer.
"""
import json
import random
import sys
from collections import deque

ATLAS = "/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/Atlas/Layouts/"
# 出来事どうしの道のりの最小のマス数（間に空きのマスを GAP-1 個挟む）と、戦闘どうしの最小。
GAP = 3
BATTLE_GAP = 4


# 地図ごとの始まりのマス。世界地図は冒険の始まり、エリアマップは入口。
def start_of(layout):
    if layout["key"] == "world":
        return "world-start"
    return "gate-" + layout["key"]


def distances(links, start, limit=None):
    seen = {start: 0}
    queue = deque([start])
    while queue:
        at = queue.popleft()
        if limit is not None and seen[at] >= limit:
            continue
        for nxt in links[at]:
            if nxt not in seen:
                seen[nxt] = seen[at] + 1
                queue.append(nxt)
    return seen


def place(layout):
    rng = random.Random(layout["key"])
    nodes = {n["id"]: n for n in layout["nodes"]}
    links = {i: [] for i in nodes}
    for line in layout["lines"]:
        links[line["a"]].append(line["b"])
        links[line["b"]].append(line["a"])
    for n in layout["nodes"]:
        n.pop("event", None)

    far = distances(links, start_of(layout))
    empty = [i for i, n in nodes.items() if n["kind"] == "empty" and far.get(i, 0) >= 2]
    rng.shuffle(empty)
    # 行き止まりを先に選び、残りは始まりに近い順に、ほかの出来事から GAP マス以上離れたものを選ぶ。
    dead = [i for i in empty if len(links[i]) == 1]
    empty.sort(key=lambda i: (len(links[i]) != 1, far[i]))
    chosen = []
    for i in empty:
        near = distances(links, i, GAP - 1)
        if not any(c in near for c in chosen):
            chosen.append(i)

    kinds = {}
    by_far = sorted(chosen, key=lambda i: far[i])
    for i in chosen:
        if far[i] == far[by_far[0]]:
            kinds[i] = "pickup"
    for i in chosen:
        if i in dead and i not in kinds:
            kinds[i] = "chest"
    middle = [i for i in by_far if i not in kinds]
    if middle:
        kinds[middle[len(middle) // 2]] = "meet"

    landmarks = [i for i, n in nodes.items() if n["kind"] != "empty"]
    from_landmarks = {}
    for mark in landmarks:
        for i, d in distances(links, mark).items():
            from_landmarks[i] = min(d, from_landmarks.get(i, d))

    def spread(kind, count):
        for _ in range(count):
            free = [i for i in chosen if i not in kinds]
            if not free:
                return
            same = [i for i, k in kinds.items() if k == kind]
            best = max(
                free,
                key=lambda i: (
                    min((distances(links, i).get(s, 99) for s in same), default=99)
                    + (from_landmarks.get(i, 0) if kind == "peddler" else 0)
                ),
            )
            kinds[best] = kind

    spread("peddler", max(1, round(len(chosen) / 12)))
    spread("bet", max(1, round(len(chosen) / 15)))

    for i in by_far:
        if i in kinds:
            continue
        near = distances(links, i, BATTLE_GAP)
        battle_near = any(kinds.get(c) == "battle" for c in near)
        pool = ["pickup", "choice"] if battle_near else ["battle"] * 9 + ["pickup"] * 6 + ["choice"] * 5
        kinds[i] = rng.choice(pool)

    for i, kind in kinds.items():
        nodes[i]["event"] = kind
    return kinds


def main():
    for key in sys.argv[1:]:
        path = ATLAS + key + ".json"
        layout = json.load(open(path, encoding="utf-8"))
        kinds = place(layout)
        empty = sum(1 for n in layout["nodes"] if n["kind"] == "empty")
        counts = {}
        for kind in kinds.values():
            counts[kind] = counts.get(kind, 0) + 1
        print(key, f"{len(kinds)}/{empty}", dict(sorted(counts.items())))
        with open(path, "w", encoding="utf-8") as f:
            json.dump(layout, f, ensure_ascii=False, indent=1)


if __name__ == "__main__":
    main()
