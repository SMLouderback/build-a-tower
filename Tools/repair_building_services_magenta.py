"""Bake out magenta/crimson key fringe from building-services menu + dollhouse art.

Tan/cement walls must NOT match — require elevated blue vs green (or true crimson plate).
"""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
MENU = ROOT / "Assets" / "Resources" / "Art" / "Menu"
DOLL = ROOT / "Assets" / "Resources" / "Art" / "Dollhouse"


def is_magentaish(r: int, g: int, b: int, a: int = 255) -> bool:
    """True magenta / crimson key — rejects tan (178,147,118) and brown cardboard."""
    if a < 20:
        return True
    if r < g + 10:
        return False

    # Hot magenta / purple-pink: R and B both above G
    if g <= 160 and (r - g) >= 14 and (b - g) >= 8 and b >= 30:
        return True

    # Soft purple-grey silhouette fringe (menu AA against tan)
    if g <= 150 and (r - g) >= 12 and (b - g) >= 6 and b >= 40 and r <= 230:
        return True

    # Dusty rose AA (R high, G≈B) — still reads pink against tan; rejects brown (b << g)
    if (r - g) >= 22 and b >= g - 12 and b <= r and g < 175 and b >= 40:
        return True

    # Crimson AI key plate / edge: high R, near-zero G, B clearly above G
    # (e.g. 186,0,71 or 61,0,13). Rejects warm red bins (~200,40,40).
    if g <= 28 and r >= 50 and b >= 10 and (b - g) >= 10 and (r - g) >= 40 and r > b:
        return True

    # Dark wine / purple fringe along dollhouse frames
    if 20 <= r <= 160 and g <= 55 and 10 <= b <= 120 and (r - g) >= 12 and (b - g) >= 6:
        return True

    return False


def color_dist(a: tuple[int, int, int], b: tuple[int, int, int]) -> float:
    return abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2])


def median_color(pixels: list[tuple[int, int, int]]) -> tuple[int, int, int]:
    if not pixels:
        return (175, 147, 120)
    rs = sorted(p[0] for p in pixels)
    gs = sorted(p[1] for p in pixels)
    bs = sorted(p[2] for p in pixels)
    m = len(pixels) // 2
    return rs[m], gs[m], bs[m]


def write_png_and_bytes(img: Image.Image, png_path: Path) -> None:
    img.save(png_path)
    png_path.with_suffix(".bytes").write_bytes(png_path.read_bytes())


def repair_menu(path: Path, *, allow_floor: bool) -> None:
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    px = list(img.getdata())

    corner_samples: list[tuple[int, int, int]] = []
    for y in range(0, min(48, h), 2):
        for x in range(0, min(48, w), 2):
            r, g, b, a = px[y * w + x]
            if a >= 20 and not is_magentaish(r, g, b, a):
                corner_samples.append((r, g, b))
    wall = median_color(corner_samples)

    floor = None
    horizon = h  # unused unless floor
    if allow_floor:
        # Sample bottom corners only (warehouse floor), not under-subject shadows
        floor_samples: list[tuple[int, int, int]] = []
        for y in range(int(h * 0.78), h):
            for x in list(range(0, 40)) + list(range(w - 40, w)):
                r, g, b, a = px[y * w + x]
                if a >= 20 and r < 90 and g < 90 and b < 90 and color_dist((r, g, b), wall) > 90:
                    floor_samples.append((r, g, b))
        if len(floor_samples) > 40:
            floor = median_color(floor_samples)
            horizon = int(h * 0.62)

    def fill_for(y: int) -> tuple[int, int, int]:
        if floor is not None and y >= horizon:
            return floor
        return wall

    # Flood background from edges
    bg = [False] * (w * h)
    q: deque[int] = deque()

    def try_bg(x: int, y: int) -> None:
        if x < 0 or y < 0 or x >= w or y >= h:
            return
        i = y * w + x
        if bg[i]:
            return
        r, g, b, a = px[i]
        if a < 20 or is_magentaish(r, g, b, a):
            bg[i] = True
            q.append(i)
            return
        if color_dist((r, g, b), wall) <= 45:
            bg[i] = True
            q.append(i)
            return
        if floor is not None and color_dist((r, g, b), floor) <= 38:
            bg[i] = True
            q.append(i)

    for x in range(w):
        try_bg(x, 0)
        try_bg(x, h - 1)
    for y in range(h):
        try_bg(0, y)
        try_bg(w - 1, y)
    while q:
        i = q.popleft()
        x, y = i % w, i // w
        try_bg(x - 1, y)
        try_bg(x + 1, y)
        try_bg(x, y - 1)
        try_bg(x, y + 1)

    # Fringe: magentaish subject pixels within 2px of background
    fringe = [False] * (w * h)
    for y in range(h):
        for x in range(w):
            i = y * w + x
            if bg[i]:
                continue
            r, g, b, a = px[i]
            if not is_magentaish(r, g, b, a):
                continue
            for dx in range(-2, 3):
                for dy in range(-2, 3):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < w and 0 <= yy < h and bg[yy * w + xx]:
                        fringe[i] = True
                        break
                if fringe[i]:
                    break

    out: list[tuple[int, int, int, int]] = [(0, 0, 0, 255)] * (w * h)
    painted = 0
    for y in range(h):
        for x in range(w):
            i = y * w + x
            r, g, b, a = px[i]
            if bg[i] or fringe[i]:
                out[i] = (*fill_for(y), 255)
                painted += 1
            else:
                out[i] = (r, g, b, 255)

    # Passes: any remaining magentaish touching fill → fill (catches AA halo)
    for _ in range(3):
        data = out[:]
        changed = 0
        for y in range(h):
            for x in range(w):
                i = y * w + x
                r, g, b, a = data[i]
                if not is_magentaish(r, g, b, a):
                    continue
                touch = False
                for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1), (-2, 0), (2, 0), (0, -2), (0, 2)):
                    xx, yy = x + dx, y + dy
                    if not (0 <= xx < w and 0 <= yy < h):
                        touch = True
                        break
                    rr, gg, bb, _ = data[yy * w + xx]
                    if color_dist((rr, gg, bb), wall) <= 36 or (
                        floor is not None and color_dist((rr, gg, bb), floor) <= 32
                    ):
                        touch = True
                        break
                if touch:
                    out[i] = (*fill_for(y), 255)
                    painted += 1
                    changed += 1
        if changed == 0:
            break

    img.putdata(out)
    write_png_and_bytes(img, path)
    print(f"menu {path.name}: wall={wall} floor={floor} painted={painted}")


def repair_dollhouse(path: Path) -> None:
    img = Image.open(path).convert("RGBA")
    w, h = img.size
    px = list(img.getdata())

    visit = [False] * (w * h)
    q: deque[int] = deque()

    def try_enq(x: int, y: int) -> None:
        if x < 0 or y < 0 or x >= w or y >= h:
            return
        i = y * w + x
        if visit[i]:
            return
        r, g, b, a = px[i]
        if not is_magentaish(r, g, b, a):
            return
        visit[i] = True
        q.append(i)

    for x in range(w):
        try_enq(x, 0)
        try_enq(x, h - 1)
    for y in range(h):
        try_enq(0, y)
        try_enq(w - 1, y)
    while q:
        i = q.popleft()
        x, y = i % w, i // w
        try_enq(x - 1, y)
        try_enq(x + 1, y)
        try_enq(x, y - 1)
        try_enq(x, y + 1)

    out = [(r, g, b, a) for (r, g, b, a) in px]
    cleared = 0
    for i in range(w * h):
        if visit[i]:
            out[i] = (0, 0, 0, 0)
            cleared += 1

    def is_dark_frame(r: int, g: int, b: int, a: int) -> bool:
        return a >= 20 and r < 55 and g < 55 and b < 55 and not is_magentaish(r, g, b, a)

    for _ in range(4):
        cur = out[:]
        for y in range(h):
            for x in range(w):
                i = y * w + x
                r, g, b, a = cur[i]
                if a < 20 or not is_magentaish(r, g, b, a):
                    continue
                near_clear = False
                near_frame = False
                for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    xx, yy = x + dx, y + dy
                    if not (0 <= xx < w and 0 <= yy < h):
                        near_clear = True
                        continue
                    rr, gg, bb, aa = cur[yy * w + xx]
                    if aa < 20:
                        near_clear = True
                    elif is_dark_frame(rr, gg, bb, aa):
                        near_frame = True
                if near_clear or near_frame or x < 4 or y < 4 or x >= w - 4 or y >= h - 4:
                    out[i] = (28, 28, 30, 255) if (near_frame and not near_clear) else (0, 0, 0, 0)
                    cleared += 1

    # Drop bottom/top rows that are mostly crimson key junk (thin magenta line)
    def row_key_ratio(yy: int) -> float:
        key = 0
        solid = 0
        for x in range(w):
            r, g, b, a = out[yy * w + x]
            if a < 20:
                continue
            solid += 1
            if is_magentaish(r, g, b, a) or (g <= 8 and r >= 40 and b <= 40 and r > b):
                key += 1
        return (key / solid) if solid else 0.0

    min_y, max_y = 0, h - 1
    while min_y < max_y and row_key_ratio(min_y) > 0.05:
        for x in range(w):
            out[min_y * w + x] = (0, 0, 0, 0)
            cleared += 1
        min_y += 1
    while max_y > min_y and row_key_ratio(max_y) > 0.05:
        for x in range(w):
            out[max_y * w + x] = (0, 0, 0, 0)
            cleared += 1
        max_y -= 1

    min_x, max_x = 0, w - 1
    # Find opaque bounds after key clear
    min_x, min_y2, max_x, max_y2 = w, h, -1, -1
    for y in range(h):
        for x in range(w):
            if out[y * w + x][3] >= 20:
                min_x = min(min_x, x)
                min_y2 = min(min_y2, y)
                max_x = max(max_x, x)
                max_y2 = max(max_y2, y)
    if max_x < min_x:
        raise RuntimeError(f"empty after key: {path}")

    cw, ch = max_x - min_x + 1, max_y2 - min_y2 + 1
    crop_px = [out[y * w + x] for y in range(min_y2, max_y2 + 1) for x in range(min_x, max_x + 1)]

    # Final rim scrub on crop
    for y in range(ch):
        for x in range(cw):
            if not (x < 3 or y < 3 or x >= cw - 3 or y >= ch - 3):
                continue
            i = y * cw + x
            r, g, b, a = crop_px[i]
            if a >= 20 and is_magentaish(r, g, b, a):
                # Prefer dark frame if neighbors are dark
                dark_n = False
                for dx, dy in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                    xx, yy = x + dx, y + dy
                    if 0 <= xx < cw and 0 <= yy < ch:
                        rr, gg, bb, aa = crop_px[yy * cw + xx]
                        if aa >= 20 and rr < 55 and gg < 55 and bb < 55:
                            dark_n = True
                crop_px[i] = (28, 28, 30, 255) if dark_n else (0, 0, 0, 0)
                cleared += 1

    cropped = Image.new("RGBA", (cw, ch))
    cropped.putdata(crop_px)
    write_png_and_bytes(cropped, path)
    print(f"doll {path.name}: cleared~{cleared} crop={cw}x{ch}")


def main() -> None:
    repair_menu(MENU / "service_mail.png", allow_floor=False)
    repair_menu(MENU / "service_recycling.png", allow_floor=False)
    repair_menu(MENU / "service_loading_dock.png", allow_floor=True)
    for name in ("mail_4x1.png", "recycling_6x1.png", "loading_dock_8x1.png"):
        repair_dollhouse(DOLL / name)


if __name__ == "__main__":
    main()
