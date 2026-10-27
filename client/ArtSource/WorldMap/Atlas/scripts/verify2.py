"""Moves tiles that picked up another tile's picture (or are far off their guide) aside and prints
the keys left to make. Keys are <row 2 digits><column 2 digits>. With 'last', keeps far-off tiles."""
import glob, os, shutil, sys
import numpy as np
from PIL import Image

D, cols, rows = sys.argv[1], int(sys.argv[2]), int(sys.argv[3])
last = len(sys.argv) > 4 and sys.argv[4] == 'last'
os.makedirs(D + '/wrong', exist_ok=True)
s = lambda p: np.asarray(Image.open(p).convert('RGB').resize((48, 32), Image.BOX)).astype(float)
for p in glob.glob(D + '/tile_*-v*.png'):
    shutil.move(p, D + '/wrong/' + os.path.basename(p))
keys = [f'{j:02d}{i:02d}' for j in range(rows) for i in range(cols)]
guides = {k: s(f'{D}/guide_{k}.png') for k in keys}
missing = []
for k in keys:
    p = f'{D}/tile_{k}.png'
    if not os.path.exists(p):
        missing.append(k)
        continue
    t = s(p)
    own = np.abs(t - guides[k]).mean()
    best = min(np.abs(t - g).mean() for kk, g in guides.items() if kk != k)
    crossed = best < own - 3
    if crossed or (own > 55 and not last):
        shutil.move(p, f'{D}/wrong/tile_{k}-bad.png')
        missing.append(k)
print(' '.join(missing))
