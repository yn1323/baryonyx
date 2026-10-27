"""Writes the nested world map's layouts and background pieces into the Unity project.

usage: to_unity.py layouts            # every layout (world, regions, oro)
       to_unity.py art <key> <png>    # one map's background, cut into JPEG pieces"""
import json
import math
import os
import sys

from PIL import Image

Image.MAX_IMAGE_PIXELS = None
AREA = "/Users/natani/work/baryonyx/output/world-map-bg/area/"
ATLAS = "/Users/natani/work/baryonyx/client/Assets/Baryonyx/Features/WorldMap/Atlas/"
NAMES = {"verde": "ヴェルデ平原", "marina": "マリーナ海岸", "gelo": "ジェーロ連峰", "bosco": "ボスコ大森林",
         "fiamma": "フィアンマ火山帯", "nebbia": "ネッビア湿原", "oro": "オーロ砂漠"}
# 世界地図の地域のマス（元の背景の座標）。地域の中心の町の近く
REGION_AT = {"verde": (566, 488), "marina": (166, 350), "gelo": (526, 236), "bosco": (1100, 204),
             "fiamma": (1322, 392), "nebbia": (240, 708), "oro": (880, 800)}
MAX = 4096
MAIN = {"verde": "porta", "marina": "portoria", "gelo": "pino", "bosco": "campanella",
        "fiamma": "forgia", "nebbia": "palude", "oro": "sabbia"}


def write(key, layout):
    os.makedirs(ATLAS + "Layouts", exist_ok=True)
    layout["lines"] = [{"a": a, "b": b} for a, b in layout["lines"]]
    # 地域に入ったときに立つ町（地域の中心の町）を先頭に置く。WorldAtlasNavigator は最初の町に入る
    main = MAIN.get(key)
    layout["nodes"].sort(key=lambda n: n["id"] != main)
    keep = {k: layout[k] for k in ("key", "name", "width", "height", "nodes", "lines")}
    json.dump(keep, open(ATLAS + f"Layouts/{key}.json", "w"), ensure_ascii=False, indent=1)
    print(key, len(keep["nodes"]), "nodes", len(keep["lines"]), "lines")


def oro():
    net = json.load(open(AREA + "desert2_net.json"))
    rename = {"サッビア": ("sabbia", "town"), "ドゥーナ": ("duna", "town"),
              "ヴェルデ平原へ": ("gate-oro-verde", "gate"), "ネッビア湿原へ": ("gate-nebbia-oro", "gate"),
              "ボスコ大森林へ": ("gate-bosco-oro", "gate")}
    nodes = {k: v for k, v in net["nodes"].items()}
    plain = sorted((k for k, v in nodes.items() if v[2] not in rename),
                   key=lambda k: (round(nodes[k][1] / 40), nodes[k][0]))
    ids = {k: f"oro-{i + 1}" for i, k in enumerate(plain)}
    kinds = {}
    for k, v in nodes.items():
        if v[2] in rename:
            ids[k], kinds[k] = rename[v[2]]
    return {"key": "oro", "name": NAMES["oro"], "width": 6144, "height": 3072,
            "nodes": [{"id": ids[k], "x": round(v[0], 1), "y": round(v[1], 1), "kind": kinds.get(k, "empty")}
                      for k, v in nodes.items()],
            "lines": [[ids[a], ids[b]] for a, b in net["lines"]]}


def world():
    gates = json.load(open(AREA + "gates.json"))
    nodes = [{"id": "region-" + k, "x": x * 5, "y": y * 5, "kind": "region"} for k, (x, y) in REGION_AT.items()]
    lines = [["region-" + p.split("-")[0], "region-" + p.split("-")[1]] for p in gates]
    return {"key": "world", "name": "世界地図", "width": 7680, "height": 5120, "nodes": nodes, "lines": lines}


def art(key, png):
    os.makedirs(ATLAS + "Art", exist_ok=True)
    im = Image.open(png).convert("RGB")
    W, H = im.size
    cols, rows = math.ceil(W / MAX), math.ceil(H / MAX)
    total = 0
    for r in range(rows):
        for c in range(cols):
            box = (W * c // cols, H * r // rows, W * (c + 1) // cols, H * (r + 1) // rows)
            path = ATLAS + f"Art/{key}_{r}{c}.jpg"
            im.crop(box).save(path, quality=90, subsampling=0)
            total += os.path.getsize(path)
    print(key, f"{W}x{H} -> {cols}x{rows} pieces, {total / 1e6:.1f}MB")


if __name__ == "__main__":
    if sys.argv[1] == "layouts":
        write("world", world())
        write("oro", oro())
        for key in ("verde", "marina", "gelo", "bosco", "fiamma", "nebbia"):
            write(key, json.load(open(AREA + f"{key}_layout.json")))
    else:
        art(sys.argv[2], sys.argv[3])
