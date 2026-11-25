"""
make_textures.py - membuat tekstur procedural (aspal, rumput, curb, finish line).
Jalankan:  python make_textures.py            (butuh: pip install pillow numpy)
Hasil ke:  ../Assets/Textures
"""
import os
import numpy as np
from PIL import Image

OUT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Textures"))
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(42)
SIZE = 512


def smooth_noise(size, scale):
    """Noise halus tileable sederhana (interpolasi bilinear dari grid acak)."""
    g = rng.random((scale, scale))
    xs = np.linspace(0, scale, size, endpoint=False)
    x0 = np.floor(xs).astype(int) % scale
    x1 = (x0 + 1) % scale
    t = xs - np.floor(xs)
    t = t * t * (3 - 2 * t)
    rows = g[x0][:, :] * (1 - t)[:, None] + g[x1][:, :] * t[:, None]
    cols = rows[:, x0] * (1 - t)[None, :] + rows[:, x1] * t[None, :]
    return cols


def save(arr, name):
    img = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGB")
    img.save(os.path.join(OUT, name))
    print("[OK]", name)


def asphalt():
    n = sum(smooth_noise(SIZE, s) * w for s, w in [(8, .5), (32, .3), (128, .2)])
    grain = rng.random((SIZE, SIZE))
    v = 52 + n * 22 + grain * 18
    img = np.stack([v, v, v + 3], axis=-1)
    # retakan / kerikil terang
    speck = rng.random((SIZE, SIZE)) > 0.992
    img[speck] += 50
    save(img, "Asphalt.png")


def grass():
    n = sum(smooth_noise(SIZE, s) * w for s, w in [(6, .4), (24, .35), (96, .25)])
    g = rng.random((SIZE, SIZE))
    base = np.stack([40 + n * 25 + g * 14, 105 + n * 45 + g * 25, 38 + n * 18 + g * 10], axis=-1)
    save(base, "Grass.png")


def curb():
    """Strip merah-putih (tiling di sumbu V)."""
    img = np.zeros((SIZE, SIZE, 3))
    stripe = SIZE // 4
    for i in range(4):
        col = (215, 25, 25) if i % 2 == 0 else (240, 240, 240)
        img[i * stripe:(i + 1) * stripe, :] = col
    img += (rng.random((SIZE, SIZE, 1)) - 0.5) * 14
    save(img, "Curb.png")


def finish_line():
    img = np.zeros((SIZE, SIZE, 3))
    cells = 8
    s = SIZE // cells
    for r in range(cells):
        for c in range(cells):
            img[r * s:(r + 1) * s, c * s:(c + 1) * s] = 245 if (r + c) % 2 == 0 else 15
    save(img, "FinishLine.png")


def sand():
    n = smooth_noise(SIZE, 16)
    g = rng.random((SIZE, SIZE))
    v = 190 + n * 25 + g * 15
    save(np.stack([v, v * 0.88, v * 0.62], axis=-1), "Sand.png")


if __name__ == "__main__":
    asphalt()
    grass()
    curb()
    finish_line()
    sand()
