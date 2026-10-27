"""World map with roads between regions only (plan A).

Each pair of neighbouring regions gets one road. It starts at an entrance a little inside region a,
crosses the border at the shared gate and ends at an entrance a little inside region b. Squares sit
only on these roads; the inside of every region is explored on its own area map. Writes the entrance
points (original background pixels) to entrances.json for the area maps, the world layout to
world_roads_layout.json, and mock screens."""
import json
import math
import os

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage
from skimage.graph import route_through_array

Image.MAX_IMAGE_PIXELS = None
BASE = "/Users/natani/work/baryonyx/output/world-map-bg/"
AREA = BASE + "area/"
OUT = os.path.dirname(os.path.abspath(__file__)) + "/roads/"
ART = "/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/UI/Art/"
FONT = "/System/Library/Fonts/ヒラギノ角ゴシック W6.ttc"
os.makedirs(OUT, exist_ok=True)

DEPTH = float(os.environ.get("DEPTH", "70"))  # 入口を境からどれだけ内側に置くか（元の背景の画素）
SPACING = 45
KEYS = ["verde", "marina", "gelo", "bosco", "fiamma", "nebbia", "oro"]
NAMES = {"verde": "ヴェルデ平原", "marina": "マリーナ海岸", "gelo": "ジェーロ連峰", "bosco": "ボスコ大森林",
         "fiamma": "フィアンマ火山帯", "nebbia": "ネッビア湿原", "oro": "オーロ砂漠"}

labels = np.load(AREA + "regions.npy").copy()
oro = Image.new("L", (1536, 1024), 0)
ImageDraw.Draw(oro).polygon([(440, 700), (560, 640), (720, 615), (860, 640), (1000, 640), (1180, 650), (1290, 690),
                             (1310, 800), (1300, 960), (1180, 1005), (800, 1010), (470, 1000), (420, 880)], fill=255)
oro = np.asarray(oro) > 0
labels[(labels == 6) & ~oro] = -1
labels[oro & (labels >= 0)] = 6

small = np.asarray(Image.open(BASE + "terrain_x5_calm2_small.png").convert("RGB")).astype(float)
water = (small[..., 0] < 110) & (small[..., 2] > small[..., 0] + 90)
lum = small.mean(axis=2) / 255
mu = ndimage.uniform_filter(lum, 9)
rough = np.sqrt(np.clip(ndimage.uniform_filter(lum * lum, 9) - mu * mu, 0, None))
rough = np.clip(rough / np.percentile(rough, 95), 0, 1)
cost = np.where(water, 3000.0, 1.0 + 10.0 * rough ** 2)

gates = json.load(open(AREA + "gates.json"))


def deep(key):
    """Land of a region at least DEPTH away from every other region."""
    i = KEYS.index(key)
    other = (labels >= 0) & (labels != i)
    far = ndimage.distance_transform_edt(~other) >= DEPTH
    return (labels == i) & far & ~ndimage.binary_dilation(water, iterations=4)


DEEP = {k: deep(k) for k in KEYS}
entrances = {}
for pair, g in gates.items():
    for key in pair.split("-"):
        ys, xs = np.nonzero(DEEP[key])
        i = np.argmin((xs - g[0]) ** 2 + (ys - g[1]) ** 2)
        entrances[f"{key}@{pair}"] = (int(xs[i]), int(ys[i]))
json.dump(entrances, open(OUT + "entrances.json", "w"), indent=1)


def smooth(pts, it=4):
    pts = [np.array(p, float) for p in pts]
    for _ in range(it):
        pts = [pts[0]] + [pts[i - 1] * 0.25 + pts[i] * 0.5 + pts[i + 1] * 0.25 for i in range(1, len(pts) - 1)] + [pts[-1]]
    return pts


def at(pts, t):
    for a, b in zip(pts, pts[1:]):
        d = np.linalg.norm(b - a)
        if t <= d:
            return a + (b - a) * (t / d if d else 0)
        t -= d
    return pts[-1]


nodes, lines, roads = {}, [], {}
for pair, g in gates.items():
    a, b = pair.split("-")
    ea, eb = entrances[f"{a}@{pair}"], entrances[f"{b}@{pair}"]
    path = []
    for p, q in ((ea, g), (g, eb)):
        cells, _ = route_through_array(cost, (p[1], p[0]), (q[1], q[0]), fully_connected=True, geometric=True)
        path += [(c[1], c[0]) for c in cells][(1 if path else 0):]
    pts = smooth(path)
    length = sum(np.linalg.norm(q - p) for p, q in zip(pts, pts[1:]))
    n = max(2, round(length / SPACING))
    ids = [f"enter-{a}-{pair}"] + [f"road-{pair}-{i}" for i in range(1, n)] + [f"enter-{b}-{pair}"]
    for i, nid in enumerate(ids):
        x, y = at(pts, length * i / n)
        nodes[nid] = (float(x), float(y), "region" if nid.startswith("enter") else "empty")
    lines += list(zip(ids, ids[1:]))
    roads[pair] = n - 1
    print(pair, f"length {length:.0f}", f"squares {n - 1}")

layout = {"key": "world", "name": "世界地図", "width": 7680, "height": 5120,
          "nodes": [{"id": k, "x": round(v[0] * 5, 1), "y": round(v[1] * 5, 1), "kind": v[2]} for k, v in nodes.items()],
          "lines": [[a, b] for a, b in lines]}
json.dump(layout, open(OUT + "world_roads_layout.json", "w"), ensure_ascii=False, indent=1)
print("squares", sum(roads.values()), "entrances", len(entrances))

# 地域の名前の札を置く場所：境から最も遠い陸地
centers = {}
for k in KEYS:
    m = (labels == KEYS.index(k)) & ~water
    d = ndimage.distance_transform_edt(m)
    y, x = np.unravel_index(np.argmax(d), d.shape)
    centers[k] = (int(x), int(y))


def sprite(name, size):
    im = Image.open(ART + name).convert("RGBA")
    r = size / max(im.size)
    return im.resize((max(1, round(im.size[0] * r)), max(1, round(im.size[1] * r))), Image.LANCZOS)


def draw(out, s, crop=None, node=50, gate=68, edge=12, fill=6, label=40, pawn_at=None):
    """s: screen pixels per original background pixel. Sizes are UI pixels at s=2.5 (world Scale 0.5)."""
    big = Image.open(BASE + "terrain_x5_calm2.png").convert("RGB")
    x0, y0, x1, y1 = crop or (0, 0, 1536, 1024)
    base = big.crop((x0 * 5, y0 * 5, x1 * 5, y1 * 5)).resize((round((x1 - x0) * s), round((y1 - y0) * s)), Image.LANCZOS)
    k = s / 2.5
    d = ImageDraw.Draw(base, "RGBA")

    def P(p):
        return ((p[0] - x0) * s, (p[1] - y0) * s)

    # 地域の境（陸の上だけ）を薄い点線で
    lab = np.where(water, -2, labels)
    edge_px = ((lab[1:, :] != lab[:-1, :])[:, :-1] | (lab[:, 1:] != lab[:, :-1])[:-1, :]) & (lab[:-1, :-1] >= 0)
    edge_px &= ~ndimage.binary_dilation(water, iterations=2)[:-1, :-1]
    ys, xs = np.nonzero(edge_px)
    dot = max(2.5, 3.2 * k)
    for x, y in zip(xs, ys):
        if (x + y) % 8 < 3 and x0 <= x < x1 and y0 <= y < y1:
            px, py = P((x, y))
            d.ellipse([px - dot, py - dot, px + dot, py + dot], fill=(255, 255, 255, 215))
    for a, b in lines:
        d.line([P(nodes[a]), P(nodes[b])], fill=(82, 51, 31), width=max(1, round(edge * k)))
    for a, b in lines:
        d.line([P(nodes[a]), P(nodes[b])], fill=(255, 245, 219), width=max(1, round(fill * k)))
    em, gm = sprite("WorldNodeEmpty.png", node * k), sprite("WorldNodeGate.png", gate * k)
    for nid, (x, y, kind) in nodes.items():
        im = gm if kind == "region" else em
        px, py = P((x, y))
        base.paste(im, (round(px - im.size[0] / 2), round(py - im.size[1] / 2)), im)
    font = ImageFont.truetype(FONT, max(10, round(label * k)))
    for key, c in centers.items():
        px, py = P(c)
        if not (0 <= px < base.size[0] and 0 <= py < base.size[1]):
            continue
        text = NAMES[key]
        w = d.textlength(text, font=font)
        h = font.size
        px = min(max(px, w / 2 + h), base.size[0] - w / 2 - h)
        d.rounded_rectangle([px - w / 2 - h * 0.6, py - h * 0.8, px + w / 2 + h * 0.6, py + h * 0.8],
                            radius=h * 0.4, fill=(20, 28, 44, 210), outline=(230, 220, 190, 255), width=max(1, round(h / 12)))
        d.text((px, py), text, font=font, fill=(255, 250, 235), anchor="mm")
    base.save(out, quality=88)


if __name__ == "__main__":
    draw(OUT + "world_overview.jpg", 1.6)
    # 実機の画面に近い大きさ（Scale 0.5 × 寄り 1.3）で、ジェーロ連峰とヴェルデ平原の境のあたり
    gx, gy = gates["gelo-verde"]
    s = 2.5 * 1.3
    w, h = 2000 / s, 900 / s
    draw(OUT + "world_screen_gelo.jpg", s, crop=(gx - w / 2, gy - h * 0.55, gx + w / 2, gy + h * 0.45))
