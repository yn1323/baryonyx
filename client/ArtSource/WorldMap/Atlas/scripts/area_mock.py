"""Mock screens of one area map with the plan A layout (gates where the world roads end)."""
import json
import os
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

Image.MAX_IMAGE_PIXELS = None
AREA = "/Users/natani/work/baryonyx/output/world-map-bg/area/"
ART = "/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/UI/Art/"
FONT = "/System/Library/Fonts/ヒラギノ角ゴシック W6.ttc"
OUT = os.path.dirname(os.path.abspath(__file__)) + "/roads/"
NAMES = {"verde": "ヴェルデ平原", "marina": "マリーナ海岸", "gelo": "ジェーロ連峰", "bosco": "ボスコ大森林",
         "fiamma": "フィアンマ火山帯", "nebbia": "ネッビア湿原", "oro": "オーロ砂漠"}

key = sys.argv[1]
lay = json.load(open(AREA + f"{key}_{os.environ.get('TAG', 'band')}_layout.json"))
bg = Image.open(AREA + f"{key}_z3_bg.png").convert("RGB")
nodes = {n["id"]: n for n in lay["nodes"]}


def sprite(name, size):
    im = Image.open(ART + name).convert("RGBA")
    r = size / max(im.size)
    return im.resize((max(1, round(im.size[0] * r)), max(1, round(im.size[1] * r))), Image.LANCZOS)


def draw(out, s, crop=None, node=56, town=92, gate=76, edge=10, fill=5, label=30):
    x0, y0, x1, y1 = crop or (0, 0, bg.size[0], bg.size[1])
    base = bg.crop((round(x0), round(y0), round(x1), round(y1))).resize((round((x1 - x0) * s), round((y1 - y0) * s)), Image.LANCZOS)
    d = ImageDraw.Draw(base, "RGBA")

    def P(n):
        return ((n["x"] - x0) * s, (n["y"] - y0) * s)

    for a, b in lay["lines"]:
        d.line([P(nodes[a]), P(nodes[b])], fill=(82, 51, 31), width=max(1, round(edge * s)))
    for a, b in lay["lines"]:
        d.line([P(nodes[a]), P(nodes[b])], fill=(255, 245, 219), width=max(1, round(fill * s)))
    ims = {"empty": sprite("WorldNodeEmpty.png", node * s), "town": sprite("WorldNodeTown.png", town * s),
           "gate": sprite("WorldNodeGate.png", gate * s)}
    font = ImageFont.truetype(FONT, max(12, round(label * s)))
    for n in nodes.values():
        im = ims[n["kind"]]
        px, py = P(n)
        base.paste(im, (round(px - im.size[0] / 2), round(py - im.size[1] / 2)), im)
        if n["kind"] == "gate":
            others = [k for k in n["id"].split("-")[1:] if k != key]
            text = NAMES[others[0]] + "へ" if others else "世界地図へ"
            h = font.size
            w = d.textlength(text, font=font)
            ty = py - im.size[1] / 2 - h * 1.1
            d.rounded_rectangle([px - w / 2 - h * 0.5, ty - h * 0.7, px + w / 2 + h * 0.5, ty + h * 0.7],
                                radius=h * 0.35, fill=(20, 28, 44, 215), outline=(140, 230, 210, 255), width=max(1, round(h / 12)))
            d.text((px, ty), text, font=font, fill=(255, 250, 235), anchor="mm")
    base.save(out, quality=88)


draw(OUT + f"area_{key}_{os.environ.get('TAG', 'band')}_overview.jpg", 2400 / bg.size[0], label=60)
for n in nodes.values():
    if n["kind"] == "gate" and len(sys.argv) > 2 and sys.argv[2] in n["id"]:
        s = 1.15
        w, h = 2000 / s, 900 / s
        draw(OUT + f"area_{key}_{os.environ.get('TAG', 'band')}_screen.jpg", s, crop=(n["x"] - w / 2, n["y"] - h * 0.6, n["x"] + w / 2, n["y"] + h * 0.4))
