"""Generate placeholder seasonal tree plates from the summer near_trees.png.

Outputs (under Assets/Resources/Art/Parallax/):
  near_trees_winter / near_trees_spring / near_trees_fall  (.png + .bytes)

Placeholders only: replace with authored art later (plan Task 7). Background plate
stays white so ParallaxBackdrop.KeyTreeBackground keeps working.
Requires Pillow only (no numpy).
"""
import colorsys
import os
import random
import shutil

from PIL import Image

ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..")
DIR = os.path.join(ROOT, "Assets", "Resources", "Art", "Parallax")
SRC = os.path.join(DIR, "near_trees.png")


def is_plate(r, g, b):
    mx, mn = max(r, g, b), min(r, g, b)
    lum = (0.3 * r + 0.59 * g + 0.11 * b) / 255.0
    return (mx - mn) / 255.0 <= 0.14 and lum > 0.80


def is_foliage(r, g, b):
    # Yellow-green..green hues; trunks are red/brown (hue < ~0.12).
    hh, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    return 0.15 <= hh <= 0.55 and s > 0.15


def block_noise(w, h, cell, seed):
    rnd = random.Random(seed)
    cw, ch = w // cell + 2, h // cell + 2
    grid = [[rnd.random() for _ in range(cw)] for _ in range(ch)]
    return lambda x, y: grid[y // cell][x // cell]


def smooth_noise_x(w, cell, seed):
    rnd = random.Random(seed)
    pts = [rnd.random() for _ in range(w // cell + 3)]

    def f(x):
        i, t = divmod(x, cell)
        t = t / cell
        t = t * t * (3 - 2 * t)
        return pts[i] * (1 - t) + pts[i + 1] * t

    return f


def fall(px, w, h):
    # Per-tree bands along x: mix red / orange / yellow / brown (spec §5.3).
    noise = smooth_noise_x(w, 90, 5)
    jitter = random.Random(7)
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 8 or is_plate(r, g, b) or not is_foliage(r, g, b):
                continue
            _, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            n = (noise(x) * 0.75 + jitter.random() * 0.25)
            # Four buckets: red, orange, yellow, brown
            if n < 0.22:
                hue, s_boost, v_boost = 0.00, 1.25, 1.05  # red
            elif n < 0.48:
                hue, s_boost, v_boost = 0.06, 1.20, 1.10  # orange
            elif n < 0.72:
                hue, s_boost, v_boost = 0.12, 1.15, 1.15  # yellow
            else:
                hue, s_boost, v_boost = 0.07, 0.75, 0.72  # brown
            s = min(1.0, s * s_boost + 0.12)
            v = min(1.0, v * v_boost)
            nr, ng, nb = colorsys.hsv_to_rgb(hue, s, v)
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)


def winter(px, w, h):
    rnd = random.Random(13)
    trunk_pts = []
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 8 or is_plate(r, g, b):
                continue
            if is_foliage(r, g, b):
                px[x, y] = (255, 255, 255, 255)  # bare: foliage becomes plate
            else:
                # Trunks / branches: desaturate and cool slightly.
                lum = (0.3 * r + 0.59 * g + 0.11 * b)
                px[x, y] = (int(lum * 0.95), int(lum * 0.88), int(lum * 0.82), a)
                trunk_pts.append((x, y))

    def solid(x, y):
        r, g, b, a = px[x, y]
        return a >= 8 and not is_plate(r, g, b)

    # Drop stray fringe pixels left over from foliage edges; keep only dense trunk mass.
    dense = []
    for (x, y) in trunk_pts:
        if not (2 <= x < w - 2 and 2 <= y < h - 2):
            continue
        n = sum(solid(x + i, y + j) for i in range(-2, 3) for j in range(-2, 3))
        if n >= 12:
            dense.append((x, y))
        elif n <= 6:
            px[x, y] = (255, 255, 255, 255)
    trunk_pts = dense

    # Fine twigs: short random walks climbing from upper trunk/branch pixels.
    top = sorted(trunk_pts, key=lambda p: p[1])[: max(1, len(trunk_pts) // 3)]
    for _ in range(260):
        x, y = rnd.choice(top)
        dx = rnd.choice((-1, 1))
        for _step in range(rnd.randint(14, 42)):
            y -= 1 if rnd.random() < 0.75 else 0
            x += dx if rnd.random() < 0.6 else 0
            if rnd.random() < 0.08:
                dx = -dx
            if not (0 <= x < w and 0 <= y < h):
                break
            t = 0.35 + 0.15 * rnd.random()
            px[x, y] = (int(255 * t * 0.95), int(255 * t * 0.85), int(255 * t * 0.78), 255)


def spring(px, w, h):
    rnd = random.Random(21)
    blossom = [(244, 168, 190), (250, 196, 210), (232, 140, 170), (255, 224, 232)]
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 8 or is_plate(r, g, b) or not is_foliage(r, g, b):
                continue
            hh, s, v = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
            # Lighter, fresher yellow-green.
            hh = hh - 0.02
            s = s * 0.85
            v = min(1.0, v * 1.15 + 0.06)
            nr, ng, nb = colorsys.hsv_to_rgb(hh, s, v)
            px[x, y] = (int(nr * 255), int(ng * 255), int(nb * 255), a)
    # Pink blossom dots on foliage (2x2 / 3x3 clusters).
    for _ in range(w * h // 90):
        x, y = rnd.randrange(2, w - 3), rnd.randrange(2, h - 3)
        r, g, b, a = px[x, y]
        if a < 8 or is_plate(r, g, b) or not is_foliage(r, g, b):
            continue
        col = rnd.choice(blossom)
        size = rnd.choice((2, 3, 3, 4))
        for dy in range(size):
            for dx in range(size):
                pr, pg, pb, pa = px[x + dx, y + dy]
                if pa >= 8 and not is_plate(pr, pg, pb):
                    px[x + dx, y + dy] = (*col, pa)


def emit(name, fn):
    img = Image.open(SRC).convert("RGBA")
    w, h = img.size
    fn(img.load(), w, h)
    png = os.path.join(DIR, name + ".png")
    img.save(png, optimize=True)
    shutil.copyfile(png, os.path.join(DIR, name + ".bytes"))
    print("wrote", name, img.size)


if __name__ == "__main__":
    emit("near_trees_winter", winter)
    emit("near_trees_spring", spring)
    emit("near_trees_fall", fall)
