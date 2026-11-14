"""Strip dark medal backdrop plates; keep cup+base with tight transparent crop."""
from __future__ import annotations

from collections import deque
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[1] / "Assets" / "Resources" / "Art" / "Trophies"


def lum(c: tuple[int, int, int, int]) -> float:
    r, g, b, _ = c
    return 0.2126 * r + 0.7152 * g + 0.0722 * b


def keep_subject(c: tuple[int, int, int, int]) -> bool:
    r, g, b, a = c
    if a < 220:
        return False
    L = lum(c)
    mx = max(r, g, b)
    if L >= 40:
        return True
    if mx >= 58:
        return True
    if a >= 252 and r >= g + 10 and r >= 30:
        return True
    if a >= 252 and mx >= 40 and (abs(r - g) < 22 and abs(g - b) < 22 or b >= r + 10):
        return True
    return False


def matte(im: Image.Image) -> Image.Image:
    im = im.convert("RGBA")
    w, h = im.size
    px = im.load()
    strict = [[keep_subject(px[x, y]) for x in range(w)] for y in range(h)]
    dil = [[False] * w for _ in range(h)]
    for y in range(h):
        for x in range(w):
            if not strict[y][x]:
                continue
            for dy in range(-3, 4):
                for dx in range(-1, 2):
                    nx, ny = x + dx, y + dy
                    if 0 <= nx < w and 0 <= ny < h:
                        dil[ny][nx] = True

    vis = [[False] * w for _ in range(h)]

    def flood(sx: int, sy: int) -> list[tuple[int, int]]:
        cells: list[tuple[int, int]] = []
        q = deque([(sx, sy)])
        vis[sy][sx] = True
        while q:
            x, y = q.popleft()
            cells.append((x, y))
            for nx, ny in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                if 0 <= nx < w and 0 <= ny < h and dil[ny][nx] and not vis[ny][nx]:
                    vis[ny][nx] = True
                    q.append((nx, ny))
        return cells

    comps: list[list[tuple[int, int]]] = []
    for y in range(h):
        for x in range(w):
            if dil[y][x] and not vis[y][x]:
                comps.append(flood(x, y))

    cx, cy = w // 2, h // 2
    main = max(comps, key=len) if comps else []
    for c in comps:
        if any(abs(x - cx) <= 3 and abs(y - cy) <= 3 for x, y in c):
            main = c
            break
    region = set(main)

    tmp = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    tp = tmp.load()
    for y in range(h):
        for x in range(w):
            if (x, y) not in region:
                continue
            if strict[y][x]:
                tp[x, y] = px[x, y]
                continue
            if px[x, y][3] < 160:
                continue
            if any(
                0 <= x + dx < w and 0 <= y + dy < h and strict[y + dy][x + dx]
                for dy in (-1, 0, 1)
                for dx in (-1, 0, 1)
            ):
                tp[x, y] = px[x, y]

    vis = [[False] * w for _ in range(h)]
    keep: set[tuple[int, int]] = set()
    for y in range(h):
        for x in range(w):
            if tp[x, y][3] == 0 or vis[y][x]:
                continue
            cells: list[tuple[int, int]] = []
            q = deque([(x, y)])
            vis[y][x] = True
            while q:
                x0, y0 = q.popleft()
                cells.append((x0, y0))
                for nx, ny in ((x0 + 1, y0), (x0 - 1, y0), (x0, y0 + 1), (x0, y0 - 1)):
                    if 0 <= nx < w and 0 <= ny < h and tp[nx, ny][3] > 0 and not vis[ny][nx]:
                        vis[ny][nx] = True
                        q.append((nx, ny))
            if len(cells) >= 25:
                keep |= set(cells)

    if not keep:
        return tmp

    xs = [x for x, _ in keep]
    ys = [y for _, y in keep]
    pad = 2
    minx = max(0, min(xs) - pad)
    miny = max(0, min(ys) - pad)
    maxx = min(w - 1, max(xs) + pad)
    maxy = min(h - 1, max(ys) + pad)
    cw, ch = maxx - minx + 1, maxy - miny + 1
    out = Image.new("RGBA", (cw, ch), (0, 0, 0, 0))
    op = out.load()
    for y in range(miny, maxy + 1):
        for x in range(minx, maxx + 1):
            if (x, y) in keep:
                op[x - minx, y - miny] = tp[x, y]
    return out


def main() -> None:
    pngs = sorted(ROOT.glob("trophy_*.png"))
    if not pngs:
        raise SystemExit(f"No trophy pngs in {ROOT}")
    for png in pngs:
        out = matte(Image.open(png))
        out.save(png, "PNG")
        data = png.read_bytes()
        bytes_path = png.with_suffix(".bytes")
        bytes_path.write_bytes(data)
        print(png.name, out.size, "->", bytes_path.name)


if __name__ == "__main__":
    main()
