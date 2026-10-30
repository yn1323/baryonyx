"""ターン終了ボタン（E2案：紺の丸に金の二重の縁、筆の払いの白い✓）の絵を描く。
4倍で描いて縮め、縁をなめらかにする。丸（EndTurnCircle.png）と✓（EndTurnCheck.png）を別の画像にし、
Unityで重ねる（押せないときは両方を灰色にする）。覚醒先の確定ボタンが同じ丸の後ろで息づかせる、
丸の外だけに広がる光（EndTurnHalo.png、白。色はUnityで付ける）も描く。

2026-10-30、Codexで作った5案のモックからユーザーが選んだE2を、ゲームで使う画像として描き起こした。
使い方（リポジトリ直下で）：
  uv run --no-project --with pillow --with numpy python client/ArtSource/UI/EndTurn/make_end_turn_art.py client/Assets/Baryonyx/Features/Combat/UI/Art
確かめ用の見本（preview-E2.png）も同じフォルダーに書き出すので、見たあとは消す。"""

import math
import sys
import numpy as np
from PIL import Image, ImageFilter

S = 4  # 描くときの倍率

R_OUT = 68.0      # 丸の外周（px）
R_GOLD_IN = 60.0  # 太い金の輪の内側
R_INNER = 55.0    # 細い金の輪の外側
R_FILL = 53.0     # 紺の地
FACE = 176        # 丸の画像の一辺（外のにじみの余白込み）


def radius_grid(size, scale):
    c = size * scale / 2
    yy, xx = np.mgrid[0:size * scale, 0:size * scale]
    return np.hypot(xx + 0.5 - c, yy + 0.5 - c) / scale, (yy + 0.5) / scale, (xx + 0.5) / scale


def band(r, a, b):
    """r が a 以上 b 未満の帯（縁を1/4pxなめらかにする）。"""
    return np.clip((r - a) * S, 0, 1) * np.clip((b - r) * S, 0, 1)


def face():
    r, y, x = radius_grid(FACE, S)
    n = FACE
    rgb = np.zeros(r.shape + (3,))
    alpha = np.zeros(r.shape)
    # 上が明るく下が沈む金
    t = np.clip((y - (n / 2 - R_OUT)) / (2 * R_OUT), 0, 1)[..., None]
    gold = (1 - t) * np.array([246, 204, 120]) + t * np.array([164, 104, 38])
    navy_t = np.clip(r / R_FILL, 0, 1)[..., None]
    navy = (1 - navy_t) * np.array([16, 25, 70]) + navy_t * np.array([7, 11, 42])

    def put(mask, color):
        nonlocal rgb, alpha
        m = mask[..., None]
        rgb = rgb * (1 - m) + color * m
        alpha = np.maximum(alpha, mask)

    put(band(r, 0, R_OUT), np.array([46, 28, 12]) + 0 * gold)           # 外の暗い縁取り
    put(band(r, 0, R_OUT - 1.4), gold)                                     # 太い金の輪
    put(band(r, 0, R_GOLD_IN), np.array([34, 22, 14]) + 0 * gold)          # 金の内の細い暗い線
    put(band(r, 0, R_GOLD_IN - 1.0), navy)                                 # 紺の隙間
    put(band(r, R_FILL, R_INNER), gold * 0.86)                             # 細い金の輪
    img = np.dstack([rgb, alpha * 255]).astype(np.uint8)
    body = Image.fromarray(img, "RGBA").resize((n, n), Image.LANCZOS)

    # 外のごく淡い金のにじみ（丸の外だけ）
    rr, _, _ = radius_grid(n, 1)
    glow_a = np.where(rr > R_OUT - 2, np.exp(-((rr - (R_OUT - 2)) / 7.0) ** 2), 0) * 0.55
    glow = np.dstack([np.full_like(rr, 255), np.full_like(rr, 176), np.full_like(rr, 70), glow_a * 255]).astype(np.uint8)
    out = Image.fromarray(glow, "RGBA")
    out.alpha_composite(body)
    return out


def check(w=88, h=72):
    """筆で一気に書いたような✓：左の短い払いは太く、右へ伸びる払いは先ほど細く鋭く抜ける。"""
    W, H = w * S, h * S
    yy, xx = np.mgrid[0:H, 0:W]
    m = np.zeros((H, W))
    cx, cy = w / 2, h / 2
    a = (cx - 27, cy + 0)    # 書き始め
    b = (cx - 8, cy + 19)    # 折れ目
    c = (cx + 33, cy - 27)   # 抜け
    def stroke(p, q, r0, r1, steps, ease):
        nonlocal m
        for i in range(steps + 1):
            t = i / steps
            px = (p[0] + (q[0] - p[0]) * t) * S
            py = (p[1] + (q[1] - p[1]) * t) * S
            rad = (r0 + (r1 - r0) * ease(t)) * S
            d = np.hypot(xx + 0.5 - px, yy + 0.5 - py)
            m = np.maximum(m, np.clip(rad - d + 0.5, 0, 1))
    stroke(a, b, 5.0, 8.0, 80, lambda t: math.sqrt(t))
    stroke(b, c, 8.0, 0.6, 200, lambda t: t ** 0.75)
    img = np.dstack([np.full((H, W), 252), np.full((H, W), 252), np.full((H, W), 255), m * 255]).astype(np.uint8)
    return Image.fromarray(img, "RGBA").resize((w, h), Image.LANCZOS)


HALO = 224  # 光の画像の一辺：丸（直径136）の外へ44pxずつ


def halo():
    """丸の縁のすぐ外が一番明るく、外へなめらかに薄れる白い光。画像の端では0になる。"""
    r, _, _ = radius_grid(HALO, 1)
    edge = R_OUT - 2
    a = np.where(r < edge, np.clip((r - (edge - 8)) / 8, 0, 1), np.exp(-((r - edge) / 17.0) ** 2))
    a *= np.clip((HALO / 2 - r) / 6, 0, 1)
    img = np.dstack([np.full_like(r, 255), np.full_like(r, 255), np.full_like(r, 255), a * 255]).astype(np.uint8)
    return Image.fromarray(img, "RGBA")


if __name__ == "__main__":
    out = sys.argv[1]
    face().save(f"{out}/EndTurnCircle.png")
    check().save(f"{out}/EndTurnCheck.png")
    halo().save(f"{out}/EndTurnHalo.png")
    # 確かめ用：背景の色の上に重ねた4倍の見本
    sheet = Image.new("RGBA", (FACE, FACE), (92, 40, 22, 255))
    sheet.alpha_composite(face())
    ck = check()
    sheet.alpha_composite(ck, ((FACE - ck.width) // 2 - 1, (FACE - ck.height) // 2))
    sheet.resize((FACE * 3, FACE * 3), Image.LANCZOS).save(f"{out}/preview-E2.png")
