"""Moves tiles that do not match their guide aside and prints the keys left to make.
On the last round (third arg 'last'), keeps the last try of a tile that never matched."""
import glob, os, shutil, sys
import numpy as np
from PIL import Image
D, cols, rows = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
last = len(sys.argv) > 4 and sys.argv[4] == 'last'
os.makedirs(D + '/wrong', exist_ok=True)
s = lambda p: np.asarray(Image.open(p).convert('RGB').resize((96, 64), Image.BOX)).astype(float)
for p in glob.glob(D + '/tile_*-v*.png'):
    shutil.move(p, D + '/wrong/' + os.path.basename(p))
missing = []
for j in range(rows):
    for i in range(cols):
        k = f'{j}{i}'
        p = f'{D}/tile_{k}.png'
        if not os.path.exists(p):
            missing.append(k); continue
        d = np.abs(s(p) - s(f'{D}/guide_{k}.png')).mean()
        if d > 25:
            if last:
                print(f'kept {k} diff {d:.1f}', file=sys.stderr); continue
            shutil.move(p, f'{D}/wrong/tile_{k}-bad.png'); missing.append(k)
print(' '.join(missing))
