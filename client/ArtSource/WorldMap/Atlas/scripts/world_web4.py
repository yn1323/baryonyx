"""World map, plan A with one entrance per region.

Roads run along the borders between regions and form one web; each region has a single entrance a
little inside its border, near its main town. Squares sit only on the web. The area maps keep no
squares in the band along the borders (where the web runs), so no land is walked twice."""
import json
import math
import os

import networkx as nx
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from skimage.graph import route_through_array
from skimage.morphology import dilation, disk, skeletonize

import roads_only as ro  # labels, water, drawing helpers

OUT = ro.OUT
DEPTH = ro.DEPTH
SPACING = 45
KEYS = ro.KEYS
MAIN = {"verde": (566, 488), "marina": (166, 350), "gelo": (526, 236), "bosco": (1100, 204),
        "fiamma": (1322, 392), "nebbia": (240, 708), "oro": (700, 786)}

# 地域に入っていない陸地は、いちばん近い地域に入れる（オーロ砂漠を手で引いた所のすき間）
labels = ro.labels.copy()
land = ~ro.water
idx = ndimage.distance_transform_edt(labels < 0, return_distances=False, return_indices=True)
filled = labels[idx[0], idx[1]]
labels = np.where(land & (labels < 0), filled, labels)
labels = np.where(land, labels, -2)

# 地域の境（陸の上）
diff = np.zeros_like(land)
for dy, dx in ((0, 1), (1, 0)):
    a, b = labels, np.roll(np.roll(labels, -dy, 0), -dx, 1)
    diff |= (a != b) & (a >= 0) & (b >= 0)
border = ndimage.binary_dilation(diff, iterations=1)

# 入口：境からDEPTH内側の線の上で、地域の中心の町にいちばん近い所
entrances = {}
for k in KEYS:
    i = KEYS.index(k)
    other = (labels >= 0) & (labels != i)
    dist = ndimage.distance_transform_edt(~other)
    ring = (labels == i) & (dist >= DEPTH) & (dist < DEPTH + 2) & ~ndimage.binary_dilation(ro.water, iterations=4)
    ys, xs = np.nonzero(ring)
    m = MAIN[k]
    j = np.argmin((xs - m[0]) ** 2 + (ys - m[1]) ** 2)
    entrances[k] = (int(xs[j]), int(ys[j]))
def ring_near(k, target):
    i = KEYS.index(k)
    other = (labels >= 0) & (labels != i)
    dist = ndimage.distance_transform_edt(~other)
    ring = (labels == i) & (dist >= DEPTH) & (dist < DEPTH + 2) & ~ndimage.binary_dilation(ro.water, iterations=4)
    ys, xs = np.nonzero(ring)
    j = np.argmin((xs - target[0]) ** 2 + (ys - target[1]) ** 2)
    return (int(xs[j]), int(ys[j]))


json.dump(entrances, open(OUT + "entrances_one4.json", "w"), indent=1)

# 境の上は通りやすく、ほかの陸は通りにくく、水はほぼ通れない
cost = np.where(border, 1.0, 25.0) * (1 + 3 * ro.rough ** 2)
cost = np.where(ro.water, 3000.0, cost)
road = np.zeros_like(land)
pairs = [p.split("-") for p in ro.gates]
pairs.sort(key=lambda p: math.dist(entrances[p[0]], entrances[p[1]]))
# 入口は袋小路にする：街道は境の上の「入口の分かれ目」どうしを結び、入口へは分かれ目から1本だけ枝を出す
bys, bxs = np.nonzero(diff & land)
junction = {}
for k, e in entrances.items():
    j = np.argmin((bxs - e[0]) ** 2 + (bys - e[1]) ** 2)
    junction[k] = (int(bxs[j]), int(bys[j]))
for a, b in pairs:
    ea, eb = junction[a], junction[b]
    cells, _ = route_through_array(cost * np.where(road, 0.3, 1.0), (ea[1], ea[0]), (eb[1], eb[0]),
                                   fully_connected=True, geometric=True)
    for y, x in cells:
        road[y, x] = True
# 回り道：ボスコ大森林へ北西（ジェーロ連峰との境）から、オーロ砂漠へ西（ネッビア湿原との境）から入る道
# 今ある道に重ならないよう、今ある道を通りにくくして引く
for a, b in (("nebbia", "oro"),):
    p, q = junction[a], junction[b]
    cells, _ = route_through_array(cost * np.where(ndimage.binary_dilation(road, iterations=3), 4.0, 1.0),
                                   (p[1], p[0]), (q[1], q[0]), fully_connected=True, geometric=True)
    for y, x in cells:
        road[y, x] = True
# 北回り：ジェーロ連峰とボスコ大森林の境を北へ上り、ボスコ大森林の北の海沿いを東へ進んで入口の分かれ目に着く
sea = ndimage.binary_opening(ro.water, iterations=3)
coast = land & (ndimage.distance_transform_edt(~sea) < 22) & (labels == KEYS.index("bosco"))
coast_cost = np.where(coast, 1.0, 30.0) * (1 + 3 * ro.rough ** 2)
coast_cost = np.where(ro.water, 3000.0, coast_cost)
north = ro.gates["bosco-gelo"]
ys_, xs_ = np.nonzero(coast)
k_ = np.argmin((xs_ - north[0]) ** 2 + (ys_ - north[1]) ** 2)
cape = (int(xs_[k_]), int(ys_[k_]))
busy = np.where(ndimage.binary_dilation(road, iterations=3), 4.0, 1.0)
for p, q, c in ((junction["gelo"], cape, cost * busy), (cape, junction["bosco"], coast_cost)):
    cells, _ = route_through_array(c, (p[1], p[0]), (q[1], q[0]), fully_connected=True, geometric=True)
    for y, x in cells:
        road[y, x] = True
spur_cost = np.where(ro.water, 3000.0, 1.0 + 3 * ro.rough ** 2)
for k, e in entrances.items():
    j = junction[k]
    cells, _ = route_through_array(spur_cost, (e[1], e[0]), (j[1], j[0]), fully_connected=True, geometric=True)
    for y, x in cells:
        road[y, x] = True
skel = skeletonize(dilation(road, disk(2)))

# 骨格をグラフにして、分かれ目と入口のあいだの道にマスを並べる
cells = set(zip(*np.nonzero(skel)))
g = nx.Graph()
for (y, x) in cells:
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            if (dy or dx) and (y + dy, x + dx) in cells:
                if dy and dx and ((y + dy, x) in cells or (y, x + dx) in cells):
                    continue
                g.add_edge((y, x), (y + dy, x + dx))
g = g.subgraph(max(nx.connected_components(g), key=len)).copy()
# 短い枝（骨格のひげ）を落とす
for _ in range(6):
    tips = [n for n in g if g.degree(n) == 1]
    named_cells = {min(g.nodes, key=lambda c: (c[0] - e[1]) ** 2 + (c[1] - e[0]) ** 2): k for k, e in entrances.items()}
    drop = []
    for t in tips:
        if t in named_cells:
            continue
        path, cur, prev = [t], t, None
        while g.degree(cur) <= 2 and len(path) < 25:
            nxt = [n for n in g[cur] if n != prev]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
            path.append(cur)
        if len(path) < 25:
            drop += path[:-1]
    if not drop:
        break
    g.remove_nodes_from(drop)
named = {min(g.nodes, key=lambda c: (c[0] - e[1]) ** 2 + (c[1] - e[0]) ** 2): k for k, e in entrances.items()}

deg = dict(g.degree())
special = {n for n in g if deg[n] != 2} | set(named)
edges, seen = [], set()
for s in special:
    for nb in g[s]:
        if (s, nb) in seen:
            continue
        path = [s, nb]
        seen |= {(s, nb), (nb, s)}
        prev, cur = s, nb
        while cur not in special:
            nxt = [n for n in g[cur] if n != prev]
            if not nxt:
                break
            prev, cur = cur, nxt[0]
            seen |= {(prev, cur), (cur, prev)}
            path.append(cur)
        edges.append(path)

nodes, lines = {}, []
for path in edges:
    a, b = path[0], path[-1]
    pts = ro.smooth([(c[1], c[0]) for c in path], 3) if len(path) > 2 else [np.array((c[1], c[0]), float) for c in path]
    length = sum(np.linalg.norm(q - p) for p, q in zip(pts, pts[1:]))
    n = max(1, round(length / SPACING))
    prev = ("n", a)
    nodes.setdefault(prev, (float(a[1]), float(a[0]), named.get(a)))
    for i in range(1, n):
        k = ("e", len(nodes))
        x, y = ro.at(pts, length * i / n)
        nodes[k] = (float(x), float(y), None)
        lines.append((prev, k))
        prev = k
    end = ("n", b)
    nodes.setdefault(end, (float(b[1]), float(b[0]), named.get(b)))
    lines.append((prev, end))
# 近すぎるマスをまとめる（入口を残す）
order = sorted(nodes, key=lambda k: nodes[k][2] is None)
kept, mp = [], {}
for k in order:
    near = next((q for q in kept if math.dist(nodes[k][:2], nodes[q][:2]) < SPACING * 0.55), None)
    mp[k] = near or k
    if near is None:
        kept.append(k)
lines = sorted({tuple(sorted((mp[a], mp[b]), key=str)) for a, b in lines if mp[a] != mp[b]}, key=str)
nodes = {k: nodes[k] for k in kept}
ids = {}
plain = sorted((k for k in nodes if nodes[k][2] is None), key=lambda k: (round(nodes[k][1] / 20), nodes[k][0]))
for i, k in enumerate(plain):
    ids[k] = f"world-{i + 1}"
for k in nodes:
    if nodes[k][2]:
        ids[k] = "region-" + nodes[k][2]
layout = {"key": "world", "name": "世界地図", "width": 7680, "height": 5120,
          "nodes": [{"id": ids[k], "x": round(v[0] * 5, 1), "y": round(v[1] * 5, 1),
                     "kind": "region" if v[2] else "empty"} for k, v in nodes.items()],
          "lines": [[ids[a], ids[b]] for a, b in lines]}
json.dump(layout, open(OUT + "world_web4_layout.json", "w"), ensure_ascii=False, indent=1)
gg = nx.Graph()
gg.add_edges_from(layout["lines"])
deg_of = dict(gg.degree())
print("entrance degrees", {k: deg_of.get("region-" + k) for k in KEYS})
print("squares", len(plain), "entrances", sum(1 for v in nodes.values() if v[2]), "lines", len(lines),
      "connected", nx.is_connected(gg), "loops", gg.number_of_edges() - gg.number_of_nodes() + 1,
      "missing", [k for k in KEYS if "region-" + k not in ids.values()])

# 描画は roads_only の draw を、この配置で使う
ro.nodes = {ids[k]: (v[0], v[1], "region" if v[2] else "empty") for k, v in nodes.items()}
ro.lines = [(ids[a], ids[b]) for a, b in lines]
ro.labels = np.where(labels == -2, -1, labels)
ro.draw(OUT + "web4_overview.jpg", 1.6)
gx, gy = entrances["bosco"]
s = 2.5 * 1.3
w, h = 2000 / s, 900 / s
ro.draw(OUT + "web4_screen_bosco.jpg", s, crop=(gx - w / 2, gy - h * 0.45, gx + w / 2, gy + h * 0.55))
