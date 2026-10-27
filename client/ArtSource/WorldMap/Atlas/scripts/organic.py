"""Organic road webs over the world map picture: least-cost roads that follow the terrain,
merge into roads already built, and carry squares spaced along them."""
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
from skimage.morphology import skeletonize, dilation as binary_dilation, disk

BG = os.environ.get("ORGANIC_BG", "/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/UI/Art/WorldMapBackground.png")
NODE = "/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/UI/Art/WorldNodeEmpty.png"
TOWN = "/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/UI/Art/WorldNodeTown.png"
R = 4  # 経路を探す方眼の大きさ（画素）

TOWNS = {
    "porta": (566, 488), "brezza": (404, 376), "portoria": (166, 350), "pino": (526, 236),
    "fredda": (626, 116), "alba": (452, 62), "cima": (262, 112), "palude": (240, 708),
    "sabbia": (700, 786), "duna": (1062, 812), "campanella": (1100, 204), "forgia": (1322, 392),
}
SIGHTS = {
    "lighthouse": (116, 318), "icecave": (642, 272), "snowcave": (708, 150), "volcano": (1420, 120),
    "pyramid": (1186, 790), "ruins": (282, 556), "islandruins": (1384, 640), "oasisrocks": (560, 900),
    "eastforest": (1000, 560), "riverfalls": (720, 540),
}

img = Image.open(BG).convert("RGB")
W, H = img.size
rgb = np.asarray(img).astype(np.float32)
water_px = (rgb[..., 0] < 110) & (rgb[..., 2] > rgb[..., 0] + 90)
lum = rgb.mean(axis=2) / 255.0
mean = ndimage.uniform_filter(lum, 13)
var = ndimage.uniform_filter(lum * lum, 13) - mean * mean
rough_px = np.sqrt(np.clip(var, 0, None))
rough_px = np.clip(rough_px / np.percentile(rough_px, 95), 0, 1)

gw, gh = W // R, H // R


def down(a):
    return a[: gh * R, : gw * R].reshape(gh, R, gw, R).mean(axis=(1, 3))


water = down(water_px.astype(np.float32)) > 0.5
rough = down(rough_px)


def to_cell(p):
    return (min(gh - 1, int(p[1] // R)), min(gw - 1, int(p[0] // R)))


def to_px(c):
    return (c[1] * R + R / 2, c[0] * R + R / 2)


def noise_field(scale, seed):
    rng = np.random.default_rng(seed)
    n = ndimage.gaussian_filter(rng.standard_normal((gh, gw)), scale)
    return (n - n.min()) / (n.max() - n.min())


def land_points(spacing, seed, rough_max=1.0, avoid=()):
    rng = random.Random(seed)
    pts = []
    for _ in range(6000):
        x, y = rng.uniform(40, W - 40), rng.uniform(40, H - 40)
        c = to_cell((x, y))
        if water[c] or rough[c] > rough_max:
            continue
        # 海から離れた所だけ
        y0, y1, x0, x1 = max(0, c[0] - 6), c[0] + 7, max(0, c[1] - 6), c[1] + 7
        if water[y0:y1, x0:x1].mean() > 0.2:
            continue
        if all(math.dist((x, y), q) >= spacing for q in list(pts) + list(avoid)):
            pts.append((x, y))
    return pts


def build(cfg):
    rng = random.Random(cfg["seed"])
    cost = 1.0 + cfg["terrain"] * rough + cfg["noise"] * noise_field(cfg["noise_scale"], cfg["seed"])
    cost = np.where(water, cfg.get("bridge", 80.0), cost)
    margin = 8
    cost[:margin, :] = cost[-margin:, :] = cost[:, :margin] = cost[:, -margin:] = 500.0
    points = dict(TOWNS)
    points.update({k: v for k, v in SIGHTS.items() if k in cfg["sights"]})
    for i, p in enumerate(cfg.get("extra_points", [])):
        points[f"x{i}"] = p
    road = np.zeros((gh, gw), bool)
    for a, b in cfg["links"]:
        pa, pb = points[a], points[b]
        path, _ = route_through_array(cost * np.where(road, cfg["reuse"], 1.0), to_cell(pa), to_cell(pb), fully_connected=True, geometric=True)
        for c in path:
            road[c] = True
    for name in points:
        road[to_cell(points[name])] = True
    # 隣り合った2本の道を1本にまとめ、中心の線にする。
    skel = skeletonize(binary_dilation(road, disk(cfg.get("merge", 2))))
    return graph_of(skel, points, cfg)


def graph_of(skel, points, cfg):
    g = nx.Graph()
    ys, xs = np.nonzero(skel)
    cells = set(zip(ys, xs))
    for (y, x) in cells:
        for dy in (-1, 0, 1):
            for dx in (-1, 0, 1):
                if not (dy or dx) or (y + dy, x + dx) not in cells:
                    continue
                # 斜めは、縦横の回り道がないときだけつなぐ（階段の角を交点にしない）。
                if dy and dx and ((y + dy, x) in cells or (y, x + dx) in cells):
                    continue
                if True:
                    g.add_edge((y, x), (y + dy, x + dx), w=math.hypot(dy, dx))
    # 名前のある地点を、いちばん近い線の上に置く。
    named = {}
    arr = np.array(sorted(cells))
    for name, p in points.items():
        c = np.array(to_cell(p))
        i = np.argmin(((arr - c) ** 2).sum(axis=1))
        named[tuple(arr[i])] = name
    # 斜めと縦横の両方でつながる三角を間引き、木の枝のような線にする。
    g = nx.minimum_spanning_tree(g, weight="w") if False else g
    keep = max(nx.connected_components(g), key=len)
    g = g.subgraph(keep).copy()
    # 交点（3本以上）と端と名前のある地点を節にして、その間を1本の線としてたどる。
    deg = dict(g.degree())
    special = {n for n in g if deg[n] != 2} | (set(named) & set(g))
    # 交点の塊は1つにまとめる。
    clusters = {}
    for comp in nx.connected_components(g.subgraph([n for n in special if deg[n] > 2])):
        comp = list(comp)
        cy = sum(c[0] for c in comp) / len(comp)
        cx = sum(c[1] for c in comp) / len(comp)
        names_in = [c for c in comp if c in named]
        rep = names_in[0] if names_in else min(comp, key=lambda c: (c[0] - cy) ** 2 + (c[1] - cx) ** 2)
        for c in comp:
            clusters[c] = rep
    def rep(n):
        return clusters.get(n, n)
    edges = []
    seen = set()
    for s in special:
        for nb in g[s]:
            if (s, nb) in seen:
                continue
            path = [s, nb]
            seen.add((s, nb)); seen.add((nb, s))
            prev, cur = s, nb
            while cur not in special:
                nxt = [n for n in g[cur] if n != prev]
                if not nxt:
                    break
                prev, cur = cur, nxt[0]
                seen.add((prev, cur)); seen.add((cur, prev))
                path.append(cur)
            a, b = rep(path[0]), rep(path[-1])
            if a == b and len(path) < 6:
                continue
            edges.append((a, b, path))
    net = nx.MultiGraph()
    for a, b, path in edges:
        pts = [to_px(c) for c in path]
        pts[0], pts[-1] = to_px(a), to_px(b)
        pts = smooth(pts, cfg.get("smooth", 3))
        if cfg.get("wiggle", 0) > 0:
            pts = wiggle(pts, cfg["wiggle"], cfg.get("wavelength", 100), hash((a, b)) % 1000)
        net.add_edge(a, b, pts=pts)
    # 名前のない短い行き止まり（線を細めたときのひげ）を消す。
    for _ in range(3):
        for a, b, k, d in list(net.edges(keys=True, data=True)):
            length = poly_len(d["pts"])
            for end in (a, b):
                if net.degree(end) == 1 and end not in named and length < cfg["spacing"] * 2.2:
                    if net.has_edge(a, b, k):
                        net.remove_edge(a, b, k)
        net.remove_nodes_from([n for n in list(net) if net.degree(n) == 0])
    return net, named


def smooth(pts, iters):
    pts = [np.array(p) for p in pts]
    for _ in range(iters):
        if len(pts) < 3:
            break
        new = [pts[0]]
        for i in range(1, len(pts) - 1):
            new.append(pts[i - 1] * 0.25 + pts[i] * 0.5 + pts[i + 1] * 0.25)
        new.append(pts[-1])
        pts = new
    return [tuple(p) for p in pts]


def wiggle(pts, amp, wavelength, seed):
    """Bends a road side to side like a road climbing a slope, keeping its ends and off water."""
    L = poly_len(pts)
    if L < wavelength * 0.6:
        return pts
    phase = (seed % 100) / 100 * 2 * math.pi
    out = []
    steps = max(2, int(L / 4))
    for i in range(steps + 1):
        t = L * i / steps
        x, y = at_length(pts, t)
        x2, y2 = at_length(pts, min(L, t + 4))
        x1, y1 = at_length(pts, max(0, t - 4))
        dx, dy = x2 - x1, y2 - y1
        n = math.hypot(dx, dy) or 1
        nx_, ny_ = -dy / n, dx / n
        taper = min(1.0, t / (wavelength * 0.5), (L - t) / (wavelength * 0.5))
        off = amp * taper * math.sin(2 * math.pi * t / wavelength + phase)
        px, py = x + nx_ * off, y + ny_ * off
        c = to_cell((min(W - 1, max(0, px)), min(H - 1, max(0, py))))
        if water[c]:
            px, py = x, y
        out.append((px, py))
    return out


def poly_len(pts):
    return sum(math.dist(a, b) for a, b in zip(pts, pts[1:]))


def at_length(pts, t):
    for a, b in zip(pts, pts[1:]):
        d = math.dist(a, b)
        if t <= d or b == pts[-1]:
            f = 0 if d == 0 else min(1, t / d)
            return (a[0] + (b[0] - a[0]) * f, a[1] + (b[1] - a[1]) * f)
        t -= d
    return pts[-1]


def squares(net, named, spacing):
    nodes = {}  # id -> (x, y, name)
    links = set()
    def node_id(cell):
        key = ("n", cell)
        if key not in nodes:
            nodes[key] = (*to_px(cell), named.get(cell))
        return key
    for a, b, d in net.edges(data=True):
        pts = d["pts"]
        if math.dist(pts[0], to_px(a)) > math.dist(pts[0], to_px(b)):
            pts = pts[::-1]
        L = poly_len(pts)
        n = max(1, round(L / spacing))
        prev = node_id(a)
        for i in range(1, n):
            key = ("e", a, b, id(d), i)
            x, y = at_length(pts, L * i / n)
            nodes[key] = (x, y, None)
            links.add((prev, key))
            prev = key
        links.add((prev, node_id(b)))
    # 近すぎるマスを1つにまとめる（交点のまわり）。残すマスは動かさず、連鎖させない。
    order = sorted(nodes, key=lambda k: (nodes[k][2] is None, k[0] != "n"))
    kept = []
    mapping = {}
    for k in order:
        x, y, _ = nodes[k]
        near = next((q for q in kept if math.dist((x, y), nodes[q][:2]) < spacing * 0.55), None)
        if near is None:
            kept.append(k)
            mapping[k] = k
        else:
            mapping[k] = near
    def find(k):
        return mapping[k]
    out_nodes = {k: nodes[k] for k in kept}
    out_links = {tuple(sorted((find(a), find(b)), key=str)) for a, b in links if find(a) != find(b)}
    return out_nodes, out_links


def stats(nodes, links):
    g = nx.Graph()
    g.add_nodes_from(nodes)
    g.add_edges_from(links)
    names = {nodes[k][2]: k for k in nodes if nodes[k][2]}
    porta = names["porta"]
    routes = {}
    for t in TOWNS:
        if t != "porta" and t in names:
            routes[t] = nx.edge_connectivity(g, porta, names[t])
    loops = g.number_of_edges() - g.number_of_nodes() + nx.number_connected_components(g)
    dead = sum(1 for n in g if g.degree(n) == 1 and not nodes[n][2])
    town_set = set(TOWNS)
    plain = sum(1 for k in nodes if nodes[k][2] not in town_set)
    return {"squares": plain, "towns": sum(1 for k in nodes if nodes[k][2] in town_set), "links": len(links), "loops": loops, "dead_ends": dead, "routes": routes, "components": nx.number_connected_components(g)}


def render(nodes, links, out, scale, crop=None, label=None, node=104, town=140, edge=32, fill=20):
    if crop:
        x0, y0, x1, y1 = crop
        base = img.crop(crop).resize((int((x1 - x0) * scale), int((y1 - y0) * scale)), Image.NEAREST)
    else:
        x0 = y0 = 0
        base = img.resize((W * scale, H * scale), Image.NEAREST)
    d = ImageDraw.Draw(base)
    def P(p):
        return ((p[0] - x0) * scale, (p[1] - y0) * scale)
    # 設計座標は背景1画素=6px。線の縁32・地20、マス104・街140を同じ比で描く。
    k = scale / 6
    for a, b in links:
        d.line([P(nodes[a]), P(nodes[b])], fill=(82, 51, 31), width=max(1, round(edge * k)))
    for a, b in links:
        d.line([P(nodes[a]), P(nodes[b])], fill=(255, 245, 219), width=max(1, round(fill * k)))
    node_im = Image.open(NODE).convert("RGBA")
    town_im = Image.open(TOWN).convert("RGBA")
    cache = {}
    for kkey, (x, y, name) in nodes.items():
        is_town = name in TOWNS
        size = round((town if is_town else node) * k)
        if (is_town, size) not in cache:
            src = town_im if is_town else node_im
            ratio = size / max(src.size)
            cache[(is_town, size)] = src.resize((max(1, round(src.size[0] * ratio)), max(1, round(src.size[1] * ratio))), Image.LANCZOS)
        im = cache[(is_town, size)]
        px, py = P((x, y))
        base.paste(im, (round(px - im.size[0] / 2), round(py - im.size[1] / 2)), im)
    if label:
        d.rectangle([0, 0, 18 * len(label) + 40, 64], fill=(0, 0, 0))
        d.text((20, 18), label, fill=(255, 255, 255))
    base.save(out)


if __name__ == "__main__":
    cfg = json.load(open(sys.argv[1]))
    net, named = build(cfg)
    nodes, links = squares(net, named, cfg["spacing"])
    st = stats(nodes, links)
    print(json.dumps(st, ensure_ascii=False))
    stem = sys.argv[2]
    look = {k: cfg[k] for k in ("node", "town", "edge", "fill") if k in cfg}
    render(nodes, links, stem + "_overview.png", 2, **look)
    porta = TOWNS["porta"]
    render(nodes, links, stem + "_screen.png", 6, crop=(porta[0] - 160, porta[1] - 90, porta[0] + 160, porta[1] + 90), **look)
    json.dump({"nodes": [[str(k), v[0], v[1], v[2]] for k, v in nodes.items()], "links": [[str(a), str(b)] for a, b in links], "stats": st}, open(stem + ".json", "w"), ensure_ascii=False)
