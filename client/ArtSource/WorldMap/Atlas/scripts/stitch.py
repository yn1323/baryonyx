"""Stitch tiles that overlap by half into one picture: rows first, then the rows, each joined along
the line where the two pictures differ least, after matching colours in the overlap."""
import sys

import numpy as np
from PIL import Image
from scipy import ndimage

DIR = sys.argv[1]
N = int(sys.argv[2])  # 横の区画の数
M = int(sys.argv[4]) if len(sys.argv) > 4 else N  # 縦の区画の数
TW, TH = 1536, 1024
SX, SY = TW // 2, TH // 2
FEATHER = 24


def load(j, i):
    return np.asarray(Image.open(f"{DIR}/tile_{j}{i}.png").convert("RGB")).astype(np.float32)


def match(src, ref):
    """Scales src's colours so that its mean and spread match ref (both the same overlap)."""
    out = []
    for c in range(3):
        s, r = src[..., c], ref[..., c]
        gain = np.clip(r.std() / max(s.std(), 1e-3), 0.85, 1.15)
        out.append((gain, r.mean() - s.mean() * gain))
    return out


def apply(img, params):
    img = img.copy()
    for c, (g, b) in enumerate(params):
        img[..., c] = img[..., c] * g + b
    return img


def seam_alpha(a, b):
    """Alpha (0 = a, 1 = b) over an overlap where a is on the left and b on the right."""
    h, w, _ = a.shape
    diff = ndimage.uniform_filter(np.abs(a - b).sum(2), 9)
    # 重なりの両端の近くは選ばない
    edge = np.zeros(w)
    edge[: w // 8] = edge[-w // 8:] = 1e6
    cost = diff + edge
    for y in range(1, h):
        prev = cost[y - 1]
        cost[y] += np.minimum(np.minimum(np.r_[np.inf, prev[:-1]], prev), np.r_[prev[1:], np.inf])
    seam = np.zeros(h, int)
    seam[-1] = int(np.argmin(cost[-1]))
    for y in range(h - 2, -1, -1):
        x = seam[y + 1]
        lo, hi = max(0, x - 1), min(w, x + 2)
        seam[y] = lo + int(np.argmin(cost[y, lo:hi]))
    xs = np.arange(w)[None, :]
    return np.clip((xs - seam[:, None]) / FEATHER + 0.5, 0, 1)[..., None]


def join_h(left, right, overlap):
    """Joins right onto left, which overlap by `overlap` columns."""
    a = left[:, -overlap:]
    right = apply(right, match(right[:, :overlap], a))
    b = right[:, :overlap]
    alpha = seam_alpha(a, b)
    return np.concatenate([left[:, :-overlap], a * (1 - alpha) + b * alpha, right[:, overlap:]], axis=1)


def join_v(top, bottom, overlap):
    t = np.transpose(top, (1, 0, 2))
    b = np.transpose(bottom, (1, 0, 2))
    return np.transpose(join_h(t, b, overlap), (1, 0, 2))


rows = []
for j in range(M):
    row = load(j, 0)
    for i in range(1, N):
        row = join_h(row, load(j, i), TW - SX)
    rows.append(row)
pic = rows[0]
for j in range(1, M):
    pic = join_v(pic, rows[j], TH - SY)
pic = np.clip(pic, 0, 255).astype(np.uint8)
print(pic.shape)
Image.fromarray(pic).save(sys.argv[3])
