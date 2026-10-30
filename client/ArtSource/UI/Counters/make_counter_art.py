"""戦闘のエネルギーと山札の札（F4案：紺の横長の角丸の札に金の細い縁、左端に金の縁の丸のアイコン）の絵を描く。
4倍で描いて縮め、縁をなめらかにする。札（CounterPill.png）と、左端の丸のアイコン
（CounterEnergyIcon.png：金のひし形、CounterDeckIcon.png：白い線の2枚重ねのカード）を別の画像にし、Unityで重ねる。
色はターン終了の丸（../EndTurn/make_end_turn_art.py）とそろえる。

2026-10-30、Codexで作った5案のモックからユーザーが選んだF4を描き起こした。山札は2桁になるため、
札は2桁の数字が収まる幅にし、エネルギーも同じ幅にする。
使い方（リポジトリ直下で）：
  uv run --no-project --with pillow --with numpy python client/ArtSource/UI/Counters/make_counter_art.py client/Assets/Baryonyx/Features/Combat/UI/Art
"""

import sys
import numpy as np
from PIL import Image

S = 4
PILL = (176, 72)   # 札の大きさ（px）
ICON = 72          # 左端の丸の直径（札の高さと同じ）

GOLD_TOP = np.array([246, 204, 120])
GOLD_BOTTOM = np.array([164, 104, 38])
DARK = np.array([40, 26, 12])
NAVY_IN = np.array([16, 25, 70])
NAVY_OUT = np.array([7, 11, 42])


def grid(w, h):
    yy, xx = np.mgrid[0:h * S, 0:w * S]
    return (xx + 0.5) / S, (yy + 0.5) / S


def rounded_rect_sd(x, y, w, h, inset):
    """角丸の長方形（半径は高さの半分）までの符号付き距離。内側が負。"""
    r = h / 2 - inset
    cx = np.clip(x, h / 2, w - h / 2)
    return np.hypot(x - cx, y - h / 2) - r


def coverage(sd):
    return np.clip(0.5 - sd * S, 0, 1)


def gold_at(y, h):
    t = np.clip(y / h, 0, 1)[..., None]
    return (1 - t) * GOLD_TOP + t * GOLD_BOTTOM


def compose(layers, shape):
    rgb = np.zeros(shape + (3,))
    alpha = np.zeros(shape)
    for mask, color in layers:
        m = mask[..., None]
        rgb = rgb * (1 - m) + color * m
        alpha = np.maximum(alpha, mask)
    return rgb, alpha


def to_image(rgb, alpha, w, h):
    img = np.dstack([rgb, alpha * 255]).astype(np.uint8)
    return Image.fromarray(img, "RGBA").resize((w, h), Image.LANCZOS)


def pill():
    w, h = PILL
    x, y = grid(w, h)
    t = np.clip(np.abs(y - h / 2) / (h / 2), 0, 1)[..., None]
    navy = (1 - t) * NAVY_IN + t * NAVY_OUT
    rgb, alpha = compose(
        [
            (coverage(rounded_rect_sd(x, y, w, h, 0)), DARK + 0 * navy),
            (coverage(rounded_rect_sd(x, y, w, h, 1.2)), gold_at(y, h)),
            (coverage(rounded_rect_sd(x, y, w, h, 3.6)), DARK * 0.8 + 0 * navy),
            (coverage(rounded_rect_sd(x, y, w, h, 4.4)), navy),
        ],
        x.shape,
    )
    return to_image(rgb, alpha, w, h)


def badge(symbol):
    """金の縁の紺の丸に記号を載せる。symbol(x, y) は記号の (mask, color) の並びを返す。"""
    n = ICON
    x, y = grid(n, n)
    r = np.hypot(x - n / 2, y - n / 2)
    t = np.clip(r / (n / 2), 0, 1)[..., None]
    navy = (1 - t) * NAVY_IN + t * NAVY_OUT
    layers = [
        (coverage(r - n / 2), DARK + 0 * navy),
        (coverage(r - (n / 2 - 1.2)), gold_at(y, n)),
        (coverage(r - (n / 2 - 5.0)), DARK * 0.8 + 0 * navy),
        (coverage(r - (n / 2 - 5.8)), navy),
    ]
    layers += symbol(x - n / 2, y - n / 2)
    rgb, alpha = compose(layers, x.shape)
    return to_image(rgb, alpha, n, n)


def diamond(x, y):
    """金のひし形。左上の面を明るく、右下の面を沈める。"""
    size = 17.0
    d = (np.abs(x) + np.abs(y)) - size
    body = coverage(d)
    lit = ((x < 0) & (y < 0)) | ((x < 0) ^ (y < 0)) & (y < 0)
    color = np.where(lit[..., None], GOLD_TOP, np.where((y >= 0)[..., None] & (x >= 0)[..., None], GOLD_BOTTOM, (GOLD_TOP + GOLD_BOTTOM) / 2))
    edge = coverage(d + 1.4) - body
    return [(np.clip(body + edge, 0, 1), DARK + 0 * color), (body, color)]


def card_outline(x, y, cx, cy, w, h, angle, line):
    """角の丸い縦長のカードの輪郭（白い線）と、その内側（紺で塗りつぶし、下のカードを隠す）。"""
    c, s = np.cos(angle), np.sin(angle)
    u = (x - cx) * c + (y - cy) * s
    v = -(x - cx) * s + (y - cy) * c
    r = 3.0
    q = np.stack([np.abs(u) - (w / 2 - r), np.abs(v) - (h / 2 - r)])
    sd = np.hypot(np.maximum(q[0], 0), np.maximum(q[1], 0)) + np.minimum(np.maximum(q[0], q[1]), 0) - r
    return coverage(sd), coverage(sd + line)


def cards(x, y):
    """白い線で描いた2枚重ねのカード。奥は左へ傾け、手前はまっすぐ。"""
    back_all, back_in = card_outline(x, y, -4.5, 1.0, 20, 28, -0.32, 2.6)
    front_all, front_in = card_outline(x, y, 3.5, 1.0, 20, 28, 0.08, 2.6)
    white = np.array([250, 250, 255])
    navy = NAVY_IN
    return [
        (back_all, white + 0 * x[..., None]),
        (back_in, navy + 0 * x[..., None]),
        (front_all, white + 0 * x[..., None]),
        (front_in, navy + 0 * x[..., None]),
    ]


if __name__ == "__main__":
    out = sys.argv[1]
    pill().save(f"{out}/CounterPill.png")
    badge(diamond).save(f"{out}/CounterEnergyIcon.png")
    badge(cards).save(f"{out}/CounterDeckIcon.png")
