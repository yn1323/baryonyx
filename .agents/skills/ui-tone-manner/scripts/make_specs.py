#!/usr/bin/env python3
"""eval/inventory.cs が書いた work/inventory.txt から、撮る状態の一覧 work/specs.txt を作る。

案内人の画面（GuideMenuView）の状態の一覧には、中のパネルの状態が「パネル › 状態」として入っているため、
同じ状態をパネルの部品から重ねて撮らないよう外す。通知の帯はホームの分だけ残す。
1行は「シーン|部品の型|階層のパス|状態の番号|画像の名前<TAB>状態の名前」。
画像の名前は「シーン-部品の名前-状態の番号」（シーンそのものは「シーン-scene」）で、撮り直しても変わらない。

使い方（リポジトリ直下）: python3 .agents/skills/ui-tone-manner/scripts/make_specs.py
"""
from pathlib import Path

WORK = Path(__file__).resolve().parents[4] / "output/ui-audit/work"


def key(scene, path, index):
    """撮り直しても変わらない画像の名前。"""
    stem = Path(scene).stem
    if index == "-1":
        return f"{stem}-scene"
    return f"{stem}-{path.split('/')[-1]}-{int(index):02d}"


def main():
    rows = [line.rstrip("\n").split("|", 4) for line in (WORK / "inventory.txt").read_text(encoding="utf-8").splitlines() if line]
    parents = {}
    for scene, kind, _, _, name in rows:
        if kind.endswith("GuideMenuView"):
            parents.setdefault(scene, []).append(name)
    specs = []
    for scene, kind, path, index, name in rows:
        if kind.endswith("NoticeBand") and not scene.endswith("Home.unity"):
            continue
        nested = scene in parents and kind != "-" and not kind.endswith("GuideMenuView") and "/SafeArea/" in path
        # 案内人の画面の一覧に入っていない部品（付け替えの画面など）だけを残す。
        if nested and any(name in parent for parent in parents[scene]) and "GearPicker" not in path:
            continue
        specs.append((scene, kind, path, index, name))
    lines = [f"{s}|{k}|{p}|{i}|{key(s, p, i)}\t{name}" for s, k, p, i, name in specs]
    (WORK / "specs.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"{len(lines)} states -> {WORK / 'specs.txt'}")


if __name__ == "__main__":
    main()
