#!/usr/bin/env python3
"""3Dの舞台のカメラから、世界の点が画面のどこに写るか、画面の点が地面のどこに当たるかを計算する。

hd2d-stage-set の「配置を計算する」で使う。撮影と作り直しの繰り返しで置き場所を探さず、
空いている画面の領域から地面の位置を求め、置いた物が画面に収まりキャラと重ならないかを先に確かめる。
カメラは左右に回さず（yaw 0）、見下ろし角だけを持つ透視のカメラとして扱う（3Dの舞台のカメラと同じ）。
カメラの値は舞台の組み立てのコード（StageSetAssets の TopView・HomeView・BattleView）と
doc/art/hd2d-stage.md の「画面ごとの構成」を見て渡す。

使い方（リポジトリ直下で）:
  python3 .agents/skills/hd2d-stage-set/scripts/stage_view.py --camera 0,4.4,-3.1 --pitch 18 --fov 32 point 0,0,6.16 -3.4,2,12.4
  python3 .agents/skills/hd2d-stage-set/scripts/stage_view.py --camera 0,4.4,-3.1 --pitch 18 --fov 32 ground 400,700 1500,700
  python3 .agents/skills/hd2d-stage-set/scripts/stage_view.py --camera 0,4.4,-3.1 --pitch 18 --fov 32 box -0.3,0,6.27:1.4,1.4,6.7

- point X,Y,Z ... : 世界の点の画面の位置（左上が原点のpx）と、カメラの向きに沿った奥行き（m）、その奥行きでの1mのpx
- ground PX,PY ... : 画面の点が地面（高さ --ground）に当たる位置 (x, z)
- box X0,Y0,Z0:X1,Y1,Z1 ... : 箱（キャラの板の bounds など）の8隅を写した画面の範囲
"""
import argparse
import math
import sys


def parse_vector(text):
    return tuple(float(v) for v in text.split(","))


class View:
    def __init__(self, camera, pitch, fov, width, height):
        self.camera = camera
        self.pitch = math.radians(pitch)
        self.tan = math.tan(math.radians(fov) / 2)
        self.width = width
        self.height = height
        self.aspect = width / height

    def to_camera(self, point):
        x, y, z = (p - c for p, c in zip(point, self.camera))
        forward = z * math.cos(self.pitch) - y * math.sin(self.pitch)
        up = z * math.sin(self.pitch) + y * math.cos(self.pitch)
        return x, up, forward

    def point(self, point):
        x, up, forward = self.to_camera(point)
        if forward <= 0:
            return None
        sx = 0.5 + x / (forward * self.tan * self.aspect) / 2
        sy = 0.5 + up / (forward * self.tan) / 2
        metre = self.height / (2 * forward * self.tan)
        return round(sx * self.width), round((1 - sy) * self.height), forward, metre

    def ground(self, px, py, height):
        yv = (1 - py / self.height - 0.5) * 2 * self.tan
        xv = (px / self.width - 0.5) * 2 * self.tan * self.aspect
        dy = yv * math.cos(self.pitch) - math.sin(self.pitch)
        dz = yv * math.sin(self.pitch) + math.cos(self.pitch)
        if dy >= 0:
            return None
        s = (height - self.camera[1]) / dy
        return self.camera[0] + xv * s, self.camera[2] + dz * s


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--camera", required=True, type=parse_vector, help="カメラの位置 x,y,z（m）")
    ap.add_argument("--pitch", required=True, type=float, help="見下ろし角（度。下向きが正）")
    ap.add_argument("--fov", required=True, type=float, help="縦の画角（度）")
    ap.add_argument("--screen", default="1920x1080", help="画面の大きさ（UIの設計座標）")
    ap.add_argument("--ground", type=float, default=0.0, help="ground で使う地面の高さ（m）")
    ap.add_argument("mode", choices=["point", "ground", "box"])
    # 負の座標（-3.4,0,12.4）がオプションと取り違えられないよう、mode より後ろは値として受け取る。
    argv = sys.argv[1:]
    split = next((i for i, a in enumerate(argv) if a in ("point", "ground", "box")), len(argv))
    args = ap.parse_args(argv[: split + 1])
    args.values = argv[split + 1 :]
    if not args.values:
        ap.error("値を1つ以上渡してください")
    width, height = (int(v) for v in args.screen.lower().split("x"))
    view = View(args.camera, args.pitch, args.fov, width, height)

    for value in args.values:
        if args.mode == "point":
            result = view.point(parse_vector(value))
            if result is None:
                print(f"{value}: カメラの後ろ")
            else:
                sx, sy, depth, metre = result
                print(f"{value}: 画面 ({sx}, {sy}) 奥行き {depth:.2f}m 1m={metre:.0f}px")
        elif args.mode == "ground":
            px, py = parse_vector(value)
            hit = view.ground(px, py, args.ground)
            print(f"{value}: " + ("地平より上" if hit is None else f"地面 ({hit[0]:.2f}, {hit[1]:.2f})"))
        else:
            low, high = (parse_vector(part) for part in value.split(":"))
            corners = [
                view.point((x, y, z))
                for x in (low[0], high[0])
                for y in (low[1], high[1])
                for z in (low[2], high[2])
            ]
            corners = [c for c in corners if c is not None]
            xs = [c[0] for c in corners]
            ys = [c[1] for c in corners]
            print(f"{value}: 画面 x {min(xs)}〜{max(xs)}, y {min(ys)}〜{max(ys)}")


if __name__ == "__main__":
    main()
