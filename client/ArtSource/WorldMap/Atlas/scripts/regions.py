"""Splits the continent into regions (nearest seed per land pixel) and draws them over the map."""
import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

SRC = "/Users/natani/work/baryonyx/output/world-map-bg/terrain_x5_small.png"
OUT = "/Users/natani/work/baryonyx/output/world-map-bg/area/regions.png"
F = ImageFont.truetype("/System/Library/Fonts/ヒラギノ角ゴシック W6.ttc", 22)
FB = ImageFont.truetype("/System/Library/Fonts/ヒラギノ角ゴシック W6.ttc", 30)

# 地域ごとの種（元の背景の座標、1536×1024）。地形の見た目に合わせて置く。
REGIONS = {
    "ポルタの草原": ((140, 200, 90), [(566, 488), (404, 376), (470, 470), (650, 420)]),
    "西の港": ((80, 170, 230), [(166, 350), (110, 320), (220, 330), (140, 420)]),
    "北の雪山": ((235, 240, 255), [(526, 236), (626, 116), (452, 62), (262, 112), (400, 180), (700, 200)]),
    "東の森と鐘の町": ((60, 140, 70), [(1100, 204), (900, 300), (1000, 420), (1150, 560), (920, 120)]),
    "火山": ((230, 90, 50), [(1322, 392), (1420, 120), (1300, 250), (1450, 420)]),
    "南西の沼": ((120, 160, 140), [(240, 708), (282, 556), (150, 800), (330, 680)]),
    "南の砂漠": ((240, 200, 110), [(700, 786), (1062, 812), (1186, 790), (880, 700), (600, 900), (1250, 900)]),
}
TOWNS = {"門前町ポルタ": (566, 488), "ブリーザ": (404, 376), "ポルトリア": (166, 350), "ピーノ": (526, 236),
         "フレッダ": (626, 116), "アルバ": (452, 62), "チーマ": (262, 112), "パルーデ": (240, 708),
         "サッビア": (700, 786), "ドゥーナ": (1062, 812), "カンパネラ": (1100, 204), "フォルジャ": (1322, 392)}

img = Image.open(SRC).convert("RGB")
a = np.asarray(img).astype(int)
water = (a[..., 0] < 110) & (a[..., 2] > a[..., 0] + 90)
land = ndimage.binary_opening(~water, iterations=2)
H, W = land.shape
seeds, labels = [], []
for k, (_, pts) in enumerate(REGIONS.values()):
    for p in pts:
        seeds.append(p)
        labels.append(k)
seeds = np.array(seeds, float)
yy, xx = np.mgrid[0:H, 0:W]
d = (xx[..., None] - seeds[:, 0]) ** 2 + (yy[..., None] - seeds[:, 1]) ** 2
region = np.array(labels)[np.argmin(d, axis=2)]
region = np.where(land, region, -1)

over = np.asarray(img).astype(float) * 0.55
colors = [c for c, _ in REGIONS.values()]
for k, c in enumerate(colors):
    m = region == k
    over[m] = over[m] * 0.6 + np.array(c) * 0.4
edge = np.zeros_like(land)
for k in range(len(colors)):
    m = region == k
    edge |= m & ~ndimage.binary_erosion(m, iterations=2)
over[edge & land] = (255, 255, 255)
out = Image.fromarray(over.clip(0, 255).astype(np.uint8))
dr = ImageDraw.Draw(out)
for k, (name, (c, pts)) in enumerate(REGIONS.items()):
    m = region == k
    ys, xs = np.nonzero(m)
    cx, cy = int(xs.mean()), int(ys.mean())
    w = dr.textlength(name, font=FB)
    dr.rectangle([cx - w / 2 - 8, cy - 22, cx + w / 2 + 8, cy + 20], fill=(20, 20, 24))
    dr.text((cx - w / 2, cy - 19), name, font=FB, fill=c)
for name, (x, y) in TOWNS.items():
    dr.ellipse([x - 7, y - 7, x + 7, y + 7], fill=(255, 220, 120), outline=(60, 35, 20), width=2)
    dr.text((x + 10, y - 12), name, font=F, fill=(255, 255, 255), stroke_width=2, stroke_fill=(0, 0, 0))
out.save(OUT)
np.save(OUT.replace(".png", ".npy"), region)
print({name: int((region == k).sum()) for k, name in enumerate(REGIONS)})
