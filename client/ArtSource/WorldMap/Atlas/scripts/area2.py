"""A sample area map: one region cut from the 5x background, shown 1:1, with a web of roads and
squares, the region's towns and landmarks as placeholders, and gates to the next regions."""
import math
import random
import sys

import networkx as nx
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from scipy.spatial import Delaunay
from skimage.graph import route_through_array
from skimage.morphology import dilation, disk, skeletonize

Image.MAX_IMAGE_PIXELS = None
BASE = "/Users/natani/work/baryonyx/output/world-map-bg/"
ART = "/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/UI/Art/"
OUT = BASE + "area/"
FONT = "/System/Library/Fonts/ヒラギノ角ゴシック W6.ttc"
X = 5  # 5倍の背景
REGION = 6  # regions.py の「南の砂漠」
R = 8  # 経路を探す方眼（5倍の画素）
MESH = int(sys.argv[1]) if len(sys.argv) > 1 else 560
SPACING = int(sys.argv[2]) if len(sys.argv) > 2 else 125
SEED = int(sys.argv[3]) if len(sys.argv) > 3 else 5
TAG = sys.argv[4] if len(sys.argv) > 4 else "desert"

# 元の背景（1536×1024）の座標
TOWNS = {"サッビア": (700, 786), "ドゥーナ": (1062, 812)}
SIGHTS = {"ピラミッドの王墓": (1186, 790), "オアシス": (790, 760), "岩の谷": (560, 880), "砂の海辺": (1250, 930)}
GATES = {"ヴェルデ平原へ": (720, 622), "ネッビア湿原へ": (448, 712), "ボスコ大森林へ": (1000, 648)}

x0, y0, x1, y1 = 420, 560, 1344, 1022
CW, CH = 6144, 3072
K = CW / (x1 - x0)
crop = Image.open(BASE + "terrain_x5.png").convert("RGB").crop((x0 * X, y0 * X, x1 * X, y1 * X)).resize((CW, CH), Image.LANCZOS)
# 砂漠の境（元の座標）。草原・東の森・沼・東の島々を外す
from PIL import ImageDraw as _D
_m = Image.new("L", (1536, 1024), 0)
_D.Draw(_m).polygon([(440, 700), (560, 640), (720, 615), (860, 640), (1000, 640), (1180, 650), (1290, 690), (1310, 800), (1300, 960), (1180, 1005), (800, 1010), (470, 1000), (420, 880)], fill=255)
mask_small = np.asarray(_m)[y0:y1, x0:x1] > 0


def P(p):  # 元の座標 → 切り出した絵の座標
    return ((p[0] - x0) * K, (p[1] - y0) * K)


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
inside = np.asarray(Image.fromarray(mask_small.astype(np.uint8) * 255).resize((gw, gh), Image.NEAREST)) > 0


def cell(p):
    return (min(gh - 1, max(0, int(p[1] // R))), min(gw - 1, max(0, int(p[0] // R))))


def px(c):
    return (c[1] * R + R / 2, c[0] * R + R / 2)


cost = 1.0 + 12.0 * rough ** 2
cost = np.where(water, 2000.0, cost)
cost = np.where(inside, cost, 400.0)

points = {}
for d in (TOWNS, SIGHTS, GATES):
    for k, v in d.items():
        points[k] = P(v)
rng = random.Random(SEED)
for _ in range(20000):
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
# 入口はいちばん近い1本だけでつなぐ
gate_idx = {names.index(g) for g in GATES}
keep, gate_done = [], set()
for a, b in links:
    g = gate_idx & {a, b}
    if g:
        if g <= gate_done:
            continue
        gate_done |= g
    keep.append((a, b))

road = np.zeros((gh, gw), bool)
for a, b in keep:
    path, _ = route_through_array(cost * np.where(road, 0.85, 1.0), cell(xy[a]), cell(xy[b]), fully_connected=True, geometric=True)
    for c in path:
        road[c] = True
for p in xy:
    road[cell(p)] = True
skel = skeletonize(dilation(road, disk(1)))

# 線を節と辺のグラフに直す
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
    if n.startswith("x"):
        continue
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
        key = ("e", len(nodes))
        nodes[key] = (*at(pts, L * i / n), None)
        lines.append((prev, key))
        prev = key
    end = ("n", b)
    nodes.setdefault(end, (*px(b), named.get(b)))
    lines.append((prev, end))

# 近すぎるマスをまとめる（名前のある所を残す）
order = sorted(nodes, key=lambda k: nodes[k][2] is None)
kept, mp = [], {}
for k in order:
    near = next((q for q in kept if math.dist(nodes[k][:2], nodes[q][:2]) < SPACING * 0.55), None)
    mp[k] = near or k
    if near is None:
        kept.append(k)
lines = {tuple(sorted((mp[a], mp[b]), key=str)) for a, b in lines if mp[a] != mp[b]}
nodes = {k: nodes[k] for k in kept}

gg = nx.Graph()
gg.add_edges_from(lines)
loops = gg.number_of_edges() - gg.number_of_nodes() + nx.number_connected_components(gg)
plain = sum(1 for v in nodes.values() if v[2] is None)
print(f"{TAG}: crop {CW}x{CH}, squares {plain}, links {len(lines)}, loops {loops}")

guide = crop.copy()
gd = ImageDraw.Draw(guide)
for a, b in lines:
    gd.line([nodes[a][:2], nodes[b][:2]], fill=(150, 112, 70), width=22)
for a, b in lines:
    gd.line([nodes[a][:2], nodes[b][:2]], fill=(214, 178, 122), width=14)
guide.save(OUT + f"{TAG}_guide.png")
import json
json.dump({"nodes": {str(k): v for k, v in nodes.items()}, "lines": [[str(a), str(b)] for a, b in lines]}, open(OUT + f"{TAG}_net.json", "w"), ensure_ascii=False)
# 描く（1:1）。台はB4と同じ比：マス68・街104を設計座標、背景1画素=設計1.0
base = crop.copy()
d = ImageDraw.Draw(base)
for a, b in lines:
    d.line([nodes[a][:2], nodes[b][:2]], fill=(82, 51, 31), width=18)
for a, b in lines:
    d.line([nodes[a][:2], nodes[b][:2]], fill=(255, 245, 219), width=11)
node_im = Image.open(ART + "WorldNodeEmpty.png").convert("RGBA")
town_im = Image.open(ART + "WorldNodeTown.png").convert("RGBA")
font = ImageFont.truetype(FONT, 34)


def stamp(im, size, x, y):
    r = size / max(im.size)
    s = im.resize((round(im.size[0] * r), round(im.size[1] * r)), Image.LANCZOS)
    base.paste(s, (round(x - s.size[0] / 2), round(y - s.size[1] / 2)), s)


def plaque(text, x, y, color):
    w = d.textlength(text, font=font)
    d.rounded_rectangle([x - w / 2 - 14, y - 26, x + w / 2 + 14, y + 26], 12, fill=(20, 20, 24), outline=color, width=3)
    d.text((x - w / 2, y - 21), text, font=font, fill=color)


for k, (x, y, name) in nodes.items():
    if name is None:
        stamp(node_im, 60, x, y)
for k, (x, y, name) in nodes.items():
    if name in TOWNS:
        # 町の絵を置く敷地（仮）
        d.ellipse([x - 120, y - 150, x + 120, y + 30], outline=(255, 220, 120), width=4)
        stamp(town_im, 96, x, y)
        plaque("町：" + name, x, y - 190, (255, 220, 120))
    elif name in SIGHTS:
        d.ellipse([x - 100, y - 130, x + 100, y + 20], outline=(160, 220, 255), width=4)
        stamp(node_im, 76, x, y)
        plaque("名所：" + name, x, y - 165, (160, 220, 255))
    elif name in GATES:
        d.regular_polygon((x, y, 46), 3, rotation=0, fill=(240, 120, 90), outline=(60, 20, 10))
        plaque(name, x, y - 80, (240, 140, 110))
base.save(OUT + f"{TAG}_full.png")
small = base.resize((CW // 3, CH // 3), Image.LANCZOS)
small.save(OUT + f"{TAG}_overview.png")
sx, sy = P(TOWNS["サッビア"])
base.crop((int(sx - 960), int(sy - 600), int(sx + 960), int(sy + 480))).save(OUT + f"{TAG}_screen.png")
