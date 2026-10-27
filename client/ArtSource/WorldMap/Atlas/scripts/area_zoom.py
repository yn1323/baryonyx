"""Builds one region's area map: the box cut from the 5x background, a web of roads and squares
inside the region, its towns and the gates to the next regions, the guide with the roads painted
in for the tile redraw, and the layout (squares and lines) for Unity.

usage: area_all.py <region key>"""
import json
import math
import os
import random
import sys

import networkx as nx
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage
from scipy.spatial import Delaunay
from skimage.graph import route_through_array
from skimage.morphology import dilation, disk, skeletonize

Image.MAX_IMAGE_PIXELS = None
BASE = "/Users/natani/work/baryonyx/output/world-map-bg/"
OUT = BASE + "area/"
K = 6144 / 924 * float(os.environ.get("AREA_ZOOM", "1"))  # エリアマップの1画素あたりの元の背景の画素（オーロ砂漠と同じ）
TW, TH = 1536, 1024
R = 8
MESH = int(os.environ.get("AREA_MESH", "560"))
SPACING = int(os.environ.get("AREA_SPACING", "125"))
TAG = os.environ.get("AREA_TAG", "")
# 区画をずらす割合（0.5で半分ずつ重ねる、0.75で4分の1ずつ重ねる）
STEP = float(os.environ.get("AREA_STEP", "0.5"))

REGIONS = {
    # key: (名前, regions.npy の番号, 町 {id: 元の座標})
    "verde": ("ヴェルデ平原", 0, {"porta": (566, 488), "brezza": (404, 376)}),
    "marina": ("マリーナ海岸", 1, {"portoria": (166, 350)}),
    "gelo": ("ジェーロ連峰", 2, {"pino": (526, 236), "fredda": (626, 116), "alba": (452, 62), "cima": (262, 112)}),
    "bosco": ("ボスコ大森林", 3, {"campanella": (1100, 204)}),
    "fiamma": ("フィアンマ火山帯", 4, {"forgia": (1322, 392)}),
    "nebbia": ("ネッビア湿原", 5, {"palude": (240, 708)}),
    "oro": ("オーロ砂漠", 6, {"sabbia": (700, 786), "duna": (1062, 812)}),
}
# 地域どうしの入口（元の座標）。両側のエリアマップの同じ場所に置く
GATES = {
    ("oro", "verde"): (720, 622),
    ("nebbia", "oro"): (448, 712),
    ("bosco", "oro"): (1000, 648),
}
LINKS = [("marina", "verde"), ("gelo", "verde"), ("bosco", "verde"), ("nebbia", "verde"),
         ("bosco", "gelo"), ("bosco", "fiamma"), ("marina", "nebbia")]

regions = np.load(OUT + "regions.npy")
small = Image.open(BASE + "terrain_x5_small.png").convert("RGB")
sa = np.asarray(small).astype(int)
small_water = (sa[..., 0] < 110) & (sa[..., 2] > sa[..., 0] + 90)


def gate_point(a, b):
    """A land pixel on the border of a and b, near the middle of their towns."""
    ia, ib = REGIONS[a][1], REGIONS[b][1]
    ma, mb = regions == ia, regions == ib
    border = (ma & ndimage.binary_dilation(mb, iterations=3)) | (mb & ndimage.binary_dilation(ma, iterations=3))
    border &= ~ndimage.binary_dilation(small_water, iterations=6)
    ta = np.mean(list(REGIONS[a][2].values()), axis=0)
    tb = np.mean(list(REGIONS[b][2].values()), axis=0)
    mid = (ta + tb) / 2
    ys, xs = np.nonzero(border)
    i = np.argmin((xs - mid[0]) ** 2 + (ys - mid[1]) ** 2)
    return (int(xs[i]), int(ys[i]))


for a, b in LINKS:
    GATES[tuple(sorted((a, b)))] = gate_point(a, b)


def gate_id(pair):
    return "gate-" + "-".join(sorted(pair))


def box_of(key):
    mask = regions == REGIONS[key][1]
    if key == "oro":
        mask = np.zeros_like(mask)
        mask[615:1010, 420:1310] = True
    ys, xs = np.nonzero(mask & ~small_water)
    x0, x1, y0, y1 = xs.min() - 12, xs.max() + 12, ys.min() - 12, ys.max() + 12
    w = (x1 - x0) * K
    h = (y1 - y0) * K
    sx, sy = round(TW * STEP), round(TH * STEP)
    cols = max(2, math.ceil((w - TW) / sx) + 1)
    rows = max(2, math.ceil((h - TH) / sy) + 1)
    W, H = TW + (cols - 1) * sx, TH + (rows - 1) * sy
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    bw, bh = W / K, H / K
    bx0 = min(max(0, cx - bw / 2), 1536 - bw)
    by0 = min(max(0, cy - bh / 2), 1024 - bh)
    return (bx0, by0, bx0 + bw, by0 + bh), W, H, cols, rows


def build(key):
    name, index, towns = REGIONS[key]
    (bx0, by0, bx1, by1), CW, CH, cols, rows = box_of(key)
    crop = Image.open(BASE + os.environ.get("AREA_BASE", "terrain_x5.png")).convert("RGB").crop(
        (round(bx0 * 5), round(by0 * 5), round(bx1 * 5), round(by1 * 5))).resize((CW, CH), Image.LANCZOS)

    def P(p):
        return ((p[0] - bx0) * K, (p[1] - by0) * K)

    rgb = np.asarray(crop).astype(np.float32)
    gw, gh = CW // R, CH // R

    def down(a):
        return a[: gh * R, : gw * R].reshape(gh, R, gw, R).mean(axis=(1, 3))

    water = down(((rgb[..., 0] < 110) & (rgb[..., 2] > rgb[..., 0] + 90)).astype(np.float32)) > 0.5
    lum = rgb.mean(axis=2) / 255.0
    mu = ndimage.uniform_filter(lum, 25)
    var = ndimage.uniform_filter(lum * lum, 25) - mu * mu
    rough = down(np.sqrt(np.clip(var, 0, None)))
    rough = np.clip(rough / np.percentile(rough, 95), 0, 1)
    mask = regions == index
    if key == "oro":
        # オーロ砂漠の境は手で引いた（草原・東の森・沼・東の島々を外す）
        from PIL import ImageDraw as _D
        _m = Image.new("L", (1536, 1024), 0)
        _D.Draw(_m).polygon([(440, 700), (560, 640), (720, 615), (860, 640), (1000, 640), (1180, 650), (1290, 690),
                             (1310, 800), (1300, 960), (1180, 1005), (800, 1010), (470, 1000), (420, 880)], fill=255)
        mask = np.asarray(_m) > 0
    m = Image.fromarray(mask.astype(np.uint8) * 255).crop((round(bx0), round(by0), round(bx1), round(by1)))
    inside = np.asarray(m.resize((gw, gh), Image.NEAREST)) > 0
    inside = ndimage.binary_opening(inside, iterations=2)

    def cell(p):
        return (min(gh - 1, max(0, int(p[1] // R))), min(gw - 1, max(0, int(p[0] // R))))

    def px(c):
        return (c[1] * R + R / 2, c[0] * R + R / 2)

    cost = 1.0 + 12.0 * rough ** 2
    cost = np.where(water, 2000.0, cost)
    cost = np.where(ndimage.binary_dilation(inside, iterations=4), cost, 400.0)

    points, kinds = {}, {}
    for tid, p in towns.items():
        points[tid] = P(p)
        kinds[tid] = "town"
    for pair, p in GATES.items():
        if key in pair:
            gid = gate_id(pair)
            points[gid] = P(p)
            kinds[gid] = "gate"
    rng = random.Random(5)
    for _ in range(30000):
        x, y = rng.uniform(60, CW - 60), rng.uniform(60, CH - 60)
        c = cell((x, y))
        if not inside[c] or water[c] or rough[c] > 0.75:
            continue
        if water[max(0, c[0] - 4):c[0] + 5, max(0, c[1] - 4):c[1] + 5].mean() > 0.1:
            continue
        if all(math.dist((x, y), q) >= MESH for q in points.values()):
            points[f"x{len(points)}"] = (x, y)

    names = list(points)
    xy = [points[n] for n in names]
    links = []
    for s in Delaunay(xy).simplices:
        for i in range(3):
            a, b = sorted((s[i], s[(i + 1) % 3]))
            if (a, b) not in links and math.dist(xy[a], xy[b]) < MESH * 1.9:
                links.append((a, b))
    links.sort(key=lambda l: math.dist(xy[l[0]], xy[l[1]]))
    gates = {i for i, n in enumerate(names) if kinds.get(n) == "gate"}
    keep, done = [], set()
    for a, b in links:
        g = gates & {a, b}
        if g:
            if g <= done or len(g) == 2:
                continue
            done |= g
        keep.append((a, b))

    road = np.zeros((gh, gw), bool)
    for a, b in keep:
        path, _ = route_through_array(cost * np.where(road, 0.85, 1.0), cell(xy[a]), cell(xy[b]),
                                      fully_connected=True, geometric=True)
        for c in path:
            road[c] = True
    for p in xy:
        road[cell(p)] = True
    skel = skeletonize(dilation(road, disk(1)))

    cells = set(zip(*np.nonzero(skel)))
    g = nx.Graph()
    for (y, x) in cells:
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if not (dy or dx) or (y + dy, x + dx) not in cells:
                    continue
                if dy and dx and ((y + dy, x) in cells or (y, x + dx) in cells):
                    continue
                g.add_edge((y, x), (y + dy, x + dx))
    g = g.subgraph(max(nx.connected_components(g), key=len)).copy()
    arr = np.array(sorted(g.nodes))
    named = {}
    for n, p in points.items():
        if n in kinds:
            c = np.array(cell(p))
            named[tuple(arr[np.argmin(((arr - c) ** 2).sum(axis=1))])] = n
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

    def smooth(pts, it=3):
        pts = [np.array(p) for p in pts]
        for _ in range(it):
            if len(pts) < 3:
                break
            pts = [pts[0]] + [pts[i - 1] * 0.25 + pts[i] * 0.5 + pts[i + 1] * 0.25 for i in range(1, len(pts) - 1)] + [pts[-1]]
        return [tuple(p) for p in pts]

    def plen(pts):
        return sum(math.dist(a, b) for a, b in zip(pts, pts[1:]))

    def at(pts, t):
        for a, b in zip(pts, pts[1:]):
            d = math.dist(a, b)
            if t <= d:
                f = 0 if d == 0 else t / d
                return (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f)
            t -= d
        return pts[-1]

    nodes, lines = {}, []
    for path in edges:
        a, b = path[0], path[-1]
        pts = smooth([px(c) for c in path])
        L = plen(pts)
        if L < SPACING * 0.6 and not (a in named or b in named):
            continue
        n = max(1, round(L / SPACING))
        prev = ("n", a)
        nodes.setdefault(prev, (*px(a), named.get(a)))
        for i in range(1, n):
            k = ("e", len(nodes))
            nodes[k] = (*at(pts, L * i / n), None)
            lines.append((prev, k))
            prev = k
        end = ("n", b)
        nodes.setdefault(end, (*px(b), named.get(b)))
        lines.append((prev, end))
    order = sorted(nodes, key=lambda k: nodes[k][2] is None)
    kept, mp = [], {}
    for k in order:
        near = next((q for q in kept if math.dist(nodes[k][:2], nodes[q][:2]) < SPACING * 0.55), None)
        mp[k] = near or k
        if near is None:
            kept.append(k)
    lines = {tuple(sorted((mp[a], mp[b]), key=str)) for a, b in lines if mp[a] != mp[b]}
    nodes = {k: nodes[k] for k in kept}
    # いちばん大きいつながりだけを残す（細い道で切れた所を外す）
    cg = nx.Graph()
    cg.add_nodes_from(nodes)
    cg.add_edges_from(lines)
    main = max(nx.connected_components(cg), key=len)
    nodes = {k: v for k, v in nodes.items() if k in main}
    lines = {l for l in lines if l[0] in main}

    # 名前のないマスは、上から下・左から右の順に番号を振る
    plain = sorted((k for k in nodes if nodes[k][2] is None), key=lambda k: (round(nodes[k][1] / 40), nodes[k][0]))
    ids = {k: f"{key}-{i + 1}" for i, k in enumerate(plain)}
    for k in nodes:
        if nodes[k][2] is not None:
            ids[k] = nodes[k][2]
    missing = [n for n in kinds if n not in ids.values()]
    layout = {
        "key": key, "name": name, "width": CW, "height": CH, "cols": cols, "rows": rows, "step": STEP,
        "box": [bx0, by0, bx1, by1],
        "nodes": [{"id": ids[k], "x": round(v[0], 1), "y": round(v[1], 1), "kind": kinds.get(v[2], "empty")}
                  for k, v in nodes.items()],
        "lines": [[ids[a], ids[b]] for a, b in lines],
    }
    gg = nx.Graph()
    gg.add_edges_from(layout["lines"])
    print(f"{key}: {CW}x{CH} tiles {cols}x{rows}, squares {len(plain)}, lines {len(lines)}, "
          f"loops {gg.number_of_edges() - gg.number_of_nodes() + 1}, missing {missing}")
    json.dump(layout, open(OUT + f"{key}{TAG}_layout.json", "w"), ensure_ascii=False)

    # 背景には道を描かない（ルートを後から足せるように）。下絵は地形だけにする
    crop.save(OUT + f"{key}{TAG}_guide.png")
    prev = crop.copy()
    pd = ImageDraw.Draw(prev)
    for a, b in lines:
        pd.line([nodes[a][:2], nodes[b][:2]], fill=(255, 245, 219), width=10)
    for k, v in nodes.items():
        r = 34 if v[2] else 22
        color = {"town": (255, 200, 80), "gate": (240, 110, 80)}.get(kinds.get(v[2]), (250, 240, 220))
        pd.ellipse([v[0] - r, v[1] - r, v[0] + r, v[1] + r], fill=color, outline=(60, 35, 20), width=4)
    prev.resize((CW // 3, CH // 3), Image.LANCZOS).save(OUT + f"{key}{TAG}_preview.png")


if __name__ == "__main__":
    for key in sys.argv[1:]:
        build(key)
    if not TAG:
        json.dump({"-".join(k): v for k, v in GATES.items()}, open(OUT + "gates.json", "w"))
