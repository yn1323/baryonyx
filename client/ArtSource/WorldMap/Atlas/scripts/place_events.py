"""Places the square events (出来事) on a nested world map layout, as a trial
(doc/features/world-atlas.md#マスに置いた出来事).

usage: place_events.py <key>...                          # e.g. place_events.py marina
       place_events.py set <key> <square> <event|none> [form]   # one square, by hand
       place_events.py setting <key> <square> <field> [value]    # a square's setting; no value removes it
       place_events.py apply <file.json|->                       # the squares the map editor copied

Run after to_unity.py layouts, which writes the layouts without events. A map in HAND_PLACED
keeps the events written in its layout (the world map, 2026-11-01), and its squares are changed
one at a time with set, which keeps the spacing rule below. The other maps are placed by the
rules below, and the same key always gives the same placement. Rules (仮):
- Only empty squares get an event, and two events have at least two empty squares between
  them on the roads, so about a third of the squares hold one.
- The events nearest to the start are pickups, so the first one is a small, sure reward.
- Dead ends get a hidden chest (the reward for the detour). A map with no free dead end gets
  one on the square farthest from the start, so every kind shows on every map.
- One meeting per map, about halfway from the start.
- Peddlers sit far from towns, region squares and gates; about one for every 12 events.
- Bets: about one for every 15 events, and at least one for each way of playing (BET_FORMS),
  which they take in turn.
- The rest are battles, pickups and choices, and no two battles are 4 squares or nearer.
"""
import json
import os
import random
import subprocess
import sys
from collections import deque

ATLAS = "/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/Atlas/Layouts/"
# 出来事どうしの道のりの最小のマス数（間に空きのマスを GAP-1 個挟む）と、戦闘どうしの最小。
GAP = 3
BATTLE_GAP = 4
# 手で決めた地図。配置のJSONに書いた出来事が正本で、この決まりでは置き直さない。直すときは set を使う
# （地図の出来事のデバッグルームの付け替えも set を呼ぶ）。世界地図の割り当てのねらいは
# doc/features/world-atlas.md の「世界地図の出来事の割り当て」。
HAND_PLACED = {"world"}

# 賭けの遊び方（WorldAtlasLayout.FormOf の名前）。賭けは、この順に1つずつ遊び方を持つ。
BET_FORMS = ["dice", "chests"]


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
    if "chest" not in kinds.values():
        far_free = [i for i in by_far if i not in kinds]
        if far_free:
            kinds[far_free[-1]] = "chest"
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
    spread("bet", max(len(BET_FORMS), round(len(chosen) / 15)))

    bets = [i for i in by_far if kinds.get(i) == "bet"]
    forms = {i: BET_FORMS[n % len(BET_FORMS)] for n, i in enumerate(bets)}

    for i in by_far:
        if i in kinds:
            continue
        near = distances(links, i, BATTLE_GAP)
        battle_near = any(kinds.get(c) == "battle" for c in near)
        pool = ["pickup", "choice"] if battle_near else ["battle"] * 9 + ["pickup"] * 6 + ["choice"] * 5
        kinds[i] = rng.choice(pool)

    for n in layout["nodes"]:
        n.pop("form", None)
    for i, kind in kinds.items():
        nodes[i]["event"] = kind
        if i in forms:
            nodes[i]["form"] = forms[i]
    return kinds


EVENTS = ["meet", "choice", "chest", "pickup", "peddler", "bet", "battle"]

# 出来事ごとの設定（doc/features/world-atlas.md の「戦闘の設定」）。値の正しさは地図エディターが確かめる。
SETTINGS = {
    "battle": {"enemies": str, "enemyLevel": int, "strength": str},
}
STRENGTHS = ["elite", "boss"]


# 1つのマスの出来事を手で変える。何もないマスにだけ置き、始まりの隣には置かず、ほかの出来事から
# 道のりで GAP マス以上離す。決まりに合わないときは、理由を標準エラーに出して1で終わる。
def set_square(key, square, name, form=None):
    path = ATLAS + key + ".json"
    layout = json.load(open(path, encoding="utf-8"))
    nodes = {n["id"]: n for n in layout["nodes"]}
    links = {i: [] for i in nodes}
    for line in layout["lines"]:
        links[line["a"]].append(line["b"])
        links[line["b"]].append(line["a"])
    node = nodes.get(square)
    if node is None:
        fail(f"{square} は {key} にありません")
    if name == "none":
        node.pop("event", None)
        node.pop("form", None)
        for fields in SETTINGS.values():
            for field in fields:
                node.pop(field, None)
    else:
        if name not in EVENTS:
            fail(f"{name} という出来事はありません")
        if node["kind"] != "empty" or square == start_of(layout):
            fail("出来事は、何もないマスにだけ置けます")
        if start_of(layout) in links[square]:
            fail("始まりの隣には置けません")
        near = distances(links, square, GAP - 1)
        for other in near:
            if other != square and "event" in nodes[other]:
                fail("ほかの出来事と近すぎます（間に何もないマスを2つ以上空けます）")
        if node.get("event") != name:
            for fields in SETTINGS.values():
                for field in fields:
                    node.pop(field, None)
        node["event"] = name
        node.pop("form", None)
        if name == "bet":
            form = form or BET_FORMS[0]
            if form not in BET_FORMS:
                fail(f"{form} という賭けの遊び方はありません")
            node["form"] = form
    with open(path, "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False, indent=1)
    print(key, square, node.get("event", "none"), node.get("form", ""))


# マスの設定を1つ書く。出来事が持つ項目だけを書け、空の値は項目を消す。
def set_setting(key, square, field, value=""):
    path = ATLAS + key + ".json"
    layout = json.load(open(path, encoding="utf-8"))
    node = next((n for n in layout["nodes"] if n["id"] == square), None)
    if node is None:
        fail(f"{square} は {key} にありません")
    fields = SETTINGS.get(node.get("event"), {})
    if field not in fields:
        fail(f"この出来事には「{field}」の設定がありません")
    value = value.strip()
    if not value:
        node.pop(field, None)
    elif fields[field] is int:
        if not value.isdigit() or not 1 <= int(value) <= 100:
            fail("Lvは1〜100で書きます")
        node[field] = int(value)
    else:
        if field == "strength" and value not in STRENGTHS:
            fail(f"{value} という強さはありません")
        node[field] = value
    with open(path, "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False, indent=1)
    print(key, square, field, node.get(field, ""))


def fail(message):
    print(message, file=sys.stderr)
    sys.exit(1)


# 地図エディターの「コピー」が写したJSON（WorldEventLab.ChangesJson）を、配置へまとめて書く。
# 端末で決めたマスをAIに貼って反映するための入口（doc/features/screens/debug-rooms.md の地図エディター）。
# 決まりに合うよう、先に出来事を消すマスと替えるマスを空け、次に出来事を置き、最後に設定を書く。
# 書けなかったマスは理由を出して飛ばし、書けた地図の値段とサーバーのマスの表を作り直す。
def apply_changes(source):
    text = sys.stdin.read() if source == "-" else open(source, encoding="utf-8").read()
    data = json.loads(text)
    if data.get("format") != "baryonyx-world-event-lab":
        fail("地図エディターのコピーではありません（format が baryonyx-world-event-lab ではない）")
    changes = data.get("changes", [])
    failures = []
    maps = []

    def attempt(change, step, action):
        try:
            action()
            if change["map"] not in maps:
                maps.append(change["map"])
            return True
        except SystemExit:
            failures.append(f'{change["map"]} {change["square"]}（{change.get("label", "")}）の{step}')
            return False

    def current(change):
        path = ATLAS + change["map"] + ".json"
        layout = json.load(open(path, encoding="utf-8"))
        node = next((n for n in layout["nodes"] if n["id"] == change["square"]), {})
        return node.get("event"), node.get("form")

    placed = []
    for change in changes:
        if current(change) != (None if change["event"] == "none" else change["event"], change.get("form")):
            attempt(change, "出来事を消す", lambda c=change: set_square(c["map"], c["square"], "none"))
    for change in changes:
        if change["event"] == "none":
            continue
        if attempt(
            change,
            "出来事を置く",
            lambda c=change: set_square(c["map"], c["square"], c["event"], c.get("form")),
        ):
            placed.append(change)
    for change in placed:
        for field in SETTINGS.get(change["event"], {}):
            value = change.get(field)
            value = "" if value in (None, 0) else str(value)
            attempt(
                change,
                f"設定（{field}）",
                lambda c=change, f=field, v=value: set_setting(c["map"], c["square"], f, v),
            )

    if maps:
        here = os.path.dirname(os.path.abspath(__file__))
        root = os.path.abspath(os.path.join(here, "../../../../.."))
        subprocess.run([sys.executable, os.path.join(here, "price_squares.py"), *maps], check=True)
        subprocess.run(
            ["node", "scripts/world-atlas-catalog.mjs"], cwd=os.path.join(root, "server"), check=True
        )
    for failure in failures:
        print("書けませんでした：" + failure, file=sys.stderr)
    print(f"{len(changes)}マスのうち{len(changes) - len({f.split('（')[0] for f in failures})}マスを書きました")
    if failures:
        sys.exit(1)


def main():
    if sys.argv[1:2] == ["set"]:
        set_square(*sys.argv[2:6])
        return
    if sys.argv[1:2] == ["setting"]:
        set_setting(*sys.argv[2:6])
        return
    if sys.argv[1:2] == ["apply"]:
        apply_changes(sys.argv[2] if len(sys.argv) > 2 else "-")
        return
    for key in sys.argv[1:]:
        if key in HAND_PLACED:
            fail(f"{key} は手で決めた地図です。set で1マスずつ直します")
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
