"""The world map of the nested map prototype: a square for each region, joined by roads that
follow the terrain, with squares along the way."""
import json
import math
import os
import sys

os.environ["ORGANIC_BG"] = "/Users/natani/work/baryonyx/output/world-map-bg/terrain_x5_calm_small.png"
sys.argv = [sys.argv[0]]
import organic as o  # noqa: E402

sys.path.insert(0, os.path.dirname(__file__))
from to_unity import REGION_AT, write  # noqa: E402

AREA = "/Users/natani/work/baryonyx/output/world-map-bg/area/"
SPACING = 45  # 元の背景の画素
MESH = 165
o.TOWNS = {}
gates = json.load(open(AREA + "gates.json"))
keys = list(REGION_AT)
# 網の目の地点（陸の上）。地域のマスもその1つ
regions = [REGION_AT[k] for k in keys]
mesh = o.land_points(MESH, 11, avoid=regions)
points = regions + mesh
tri = o.Delaunay(points)
rng = o.random.Random(11)
links = set()
for t in tri.simplices:
    for i in range(3):
        a, b = sorted((int(t[i]), int(t[(i + 1) % 3])))
        pa, pb = points[a], points[b]
        d = math.dist(pa, pb)
        if d > MESH * 1.9:
            continue
        blocked = any(max(math.dist(pa, q), math.dist(pb, q)) < d for q in points if q not in (pa, pb))
        if not blocked or rng.random() < 0.35:
            links.add((a, b))
# 寄り道：網の目の外の陸地に置き、いちばん近い地点だけにつなぐ行き止まり
detours = [p for p in o.land_points(MESH * 0.55, 23, avoid=points)
           if min(math.dist(p, q) for q in points) > MESH * 0.45][:40]
rng.shuffle(detours)
detours = detours[:18]
start = len(points)
points += detours
for i, p in enumerate(detours):
    j = min(range(start), key=lambda k: math.dist(p, points[k]))
    links.add((j, start + i))
links = sorted(links, key=lambda l: math.dist(points[l[0]], points[l[1]]))
cfg = {"seed": 7, "bridge": 400.0, "merge": 2, "sights": [], "terrain": 4.0, "noise": 1.0,
       "noise_scale": 6, "reuse": 0.85, "smooth": 3, "spacing": SPACING,
       "extra_points": points, "links": [(f"x{a}", f"x{b}") for a, b in links]}
net, named = o.build(cfg)
nodes, links = o.squares(net, named, SPACING)
region_of = {f"x{i}": k for i, k in enumerate(keys)}
names_of_detours = {f"x{start + i}" for i in range(len(detours))}
plain = sorted((k for k in nodes if nodes[k][2] not in region_of), key=lambda k: (round(nodes[k][1] / 20), nodes[k][0]))
ids = {k: f"world-{i + 1}" for i, k in enumerate(plain)}
for k, v in nodes.items():
    if v[2] in region_of:
        ids[k] = "region-" + region_of[v[2]]

layout = {"key": "world", "name": "世界地図", "width": 7680, "height": 5120,
          "nodes": [{"id": ids[k], "x": round(v[0] * 5, 1), "y": round(v[1] * 5, 1),
                     "kind": "region" if v[2] in region_of else "empty"} for k, v in nodes.items()],
          "lines": [[ids[a], ids[b]] for a, b in links]}
missing = [k for k in keys if "region-" + k not in ids.values()]
print("squares", len(plain), "lines", len(links), "missing", missing)
write("world", layout)
o.render(nodes, links, AREA + "world_layout_preview.png", 1, node=60 * 2.4, town=110 * 2.4, edge=16 * 2.4, fill=9 * 2.4)
