"""Split a magenta-keyed dollhouse contact sheet into face-variant strips."""
from __future__ import annotations

import argparse
import uuid
from pathlib import Path

from PIL import Image


def is_magenta_gutter(r: int, g: int, b: int) -> bool:
    """Loose detector for contact-sheet gutters / plate (JPG-safe)."""
    return r >= 180 and g <= 100 and b >= 95 and r >= g + 70


def is_magenta_key(r: int, g: int, b: int) -> bool:
    """
    Key hot pink / magenta glass + dark pink plate fringe.
    Must NOT match mauve hotel plaster (e.g. 76,44,62 / 144,59,85).
    """
    if g > 48:
        return False
    # Classic #FF00FF
    if r >= 200 and b >= 160 and r >= g + 120 and b >= g + 100:
        return True
    # Bright hot-pink panes / top fringe (221,44,111) / (203,54,111)
    if r >= 190 and g <= 48 and b >= 90 and r >= g + 130 and r >= b + 40:
        return True
    # Dark pink outer-shell fringe (113,10,58) / (71,16,46) — not mauve walls.
    # Require red clearly ahead of blue so purple plaster stays opaque.
    if (
        r >= 60
        and g <= 32
        and b >= 30
        and r >= b + 24
        and r >= g + 55
        and b >= g + 18
    ):
        return True
    return False


def is_magenta_rgb(r: int, g: int, b: int) -> bool:
    # Default used for gutter band finding + opaque-bound crop.
    return is_magenta_gutter(r, g, b)


def find_content_bands(im: Image.Image, mag_frac_thresh: float = 0.72) -> list[tuple[int, int]]:
    """Split on horizontal magenta gutters (high magenta fraction rows)."""
    rgba = im.convert("RGBA")
    w, h = rgba.size
    pixels = rgba.load()
    # Sample a center band — side chrome is often magenta even on content rows.
    x0, x1 = int(w * 0.2), int(w * 0.8)
    gutter: list[bool] = []
    for y in range(h):
        mag = 0
        n = 0
        for x in range(x0, x1):
            r, g, b, a = pixels[x, y]
            n += 1
            if a > 8 and is_magenta_rgb(r, g, b):
                mag += 1
        gutter.append((mag / max(1, n)) >= mag_frac_thresh)

    bands: list[tuple[int, int]] = []
    in_band = False
    start = 0
    for y, is_gutter in enumerate(gutter):
        content = not is_gutter
        if content and not in_band:
            in_band = True
            start = y
        elif not content and in_band:
            in_band = False
            bands.append((start, y))
    if in_band:
        bands.append((start, h))
    return [(a, b) for a, b in bands if (b - a) >= 24]


def is_shell_frame_rgb(r: int, g: int, b: int, a: int) -> bool:
    """
    Generator picture-frame + pink fringe (not mauve plaster / furniture).
    Clear counts as shell for column/row fraction tests.
    """
    if a < 8:
        return True
    if is_magenta_key(r, g, b):
        return True
    mx = max(r, g, b)
    mn = min(r, g, b)
    # Charcoal / graphite outer shell (~35-65).
    if mx <= 70 and (mx - mn) <= 22:
        return True
    # Purple-grey modular frame (65,55,63) / (82,51,69) — not bright mauve walls.
    if (
        mx <= 86
        and (mx - mn) <= 34
        and 45 <= g <= 64
        and r <= 88
        and abs(r - b) <= 22
    ):
        return True
    # Dark / mid pink plate fringe (not saturated micro walls ~248,56,128).
    if (
        mx <= 220
        and g <= 40
        and r >= 40
        and r >= b + 12
        and r >= g + 28
        and b <= 140
    ):
        return True
    return False


def strip_outer_shell_frame(
    strip: Image.Image, max_side_inset: int = 36, max_vert_inset: int = 18
) -> Image.Image:
    """
    Remove the generator's dark grey picture-frame so adjacent rooms seam
    without city showing through double borders / pink fringe gaps.

    Side insets can be deep (thick vertical pillars). Vertical insets are
    capped so dark floors/ceilings are not mistaken for the shell ring.
    """
    rgba = strip.convert("RGBA")
    w, h = rgba.size
    pixels = rgba.load()

    def col_shell_frac(x: int) -> float:
        n = hit = 0
        for y in range(h):
            r, g, b, a = pixels[x, y]
            n += 1
            if is_shell_frame_rgb(r, g, b, a):
                hit += 1
        return hit / max(1, n)

    def row_shell_frac(y: int) -> float:
        n = hit = 0
        for x in range(w):
            r, g, b, a = pixels[x, y]
            n += 1
            if is_shell_frame_rgb(r, g, b, a):
                hit += 1
        return hit / max(1, n)

    def advance(size: int, frac_at, positive: bool, max_inset: int) -> int:
        limit = min(max_inset, size // 3)
        inset = 0
        while inset < limit:
            idx = inset if positive else size - 1 - inset
            if frac_at(idx) < 0.72:
                break
            inset += 1
        return inset

    left = advance(w, col_shell_frac, True, max_side_inset)
    right = advance(w, col_shell_frac, False, max_side_inset)
    top = advance(h, row_shell_frac, True, max_vert_inset)
    bot = advance(h, row_shell_frac, False, max_vert_inset)

    if top + bot + left + right == 0:
        return rgba
    if left + right >= w - 8 or top + bot >= h - 8:
        return rgba
    return rgba.crop((left, top, w - right, h - bot))


def crop_opaque_bounds(strip: Image.Image) -> Image.Image:
    rgba = strip.convert("RGBA")
    w, h = rgba.size
    pixels = rgba.load()
    min_x, min_y = w, h
    max_x, max_y = -1, -1
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a <= 8 or is_magenta_rgb(r, g, b):
                continue
            if x < min_x:
                min_x = x
            if y < min_y:
                min_y = y
            if x > max_x:
                max_x = x
            if y > max_y:
                max_y = y
    if max_x < 0:
        return strip
    return strip.crop((min_x, min_y, max_x + 1, max_y + 1))


def key_magenta_to_alpha(strip: Image.Image) -> Image.Image:
    rgba = strip.convert("RGBA")
    pixels = rgba.load()
    w, h = rgba.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            # Strict key only — loose gutter pinks on mauve walls stay opaque.
            if is_magenta_key(r, g, b):
                pixels[x, y] = (r, g, b, 0)
    return rgba


def scrub_plate_edge_fringe(strip: Image.Image, depth: int = 3) -> Image.Image:
    """
    Final pass: clear leftover pink fringe on the outer ring only.
    Caps max channel so saturated micro pink walls (~248) on the edge stay.
    """
    rgba = strip.convert("RGBA")
    pixels = rgba.load()
    w, h = rgba.size
    depth = max(1, depth)
    for y in range(h):
        for x in range(w):
            if x >= depth and x < w - depth and y >= depth and y < h - depth:
                continue
            r, g, b, a = pixels[x, y]
            if a < 8:
                continue
            if is_magenta_key(r, g, b):
                pixels[x, y] = (r, g, b, 0)
                continue
            mx = max(r, g, b)
            if (
                mx <= 230
                and r >= 160
                and g <= 56
                and b >= 80
                and r >= b + 30
                and r >= g + 100
            ):
                pixels[x, y] = (r, g, b, 0)
    return rgba


def clear_outer_frame_paint(strip: Image.Image, depth: int = 2) -> Image.Image:
    """Clear charcoal FRAME paint on the outer ring so plates seam cleanly."""
    rgba = strip.convert("RGBA")
    pixels = rgba.load()
    w, h = rgba.size
    depth = max(1, depth)
    fr, fg, fb, _ = FRAME
    for y in range(h):
        for x in range(w):
            if x >= depth and x < w - depth and y >= depth and y < h - depth:
                continue
            r, g, b, a = pixels[x, y]
            if a < 8:
                continue
            if abs(r - fr) <= 6 and abs(g - fg) <= 6 and abs(b - fb) <= 6:
                pixels[x, y] = (r, g, b, 0)
                continue
            # Residual purple-grey shell on the border after resize.
            if is_shell_frame_rgb(r, g, b, a) and max(r, g, b) <= 86:
                pixels[x, y] = (r, g, b, 0)
    return rgba


def clear_sky_glass_panes(strip: Image.Image) -> Image.Image:
    """Clear generator-painted blue/cyan sky that should have been magenta key."""
    rgba = strip.convert("RGBA")
    pixels = rgba.load()
    w, h = rgba.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a < 8:
                continue
            if y < int(h * 0.05) or y > int(h * 0.78):
                continue
            # Sky blues / cyans (not warm plaster, not wood, not charcoal frames).
            if b < 140:
                continue
            if b <= r + 15 and b <= g + 10:
                continue
            if max(r, g, b) - min(r, g, b) < 25:
                continue
            # Avoid clearing blue-grey office walls: require fairly bright + blue-dominant.
            if b < 170 or b < g + 20:
                continue
            pixels[x, y] = (r, g, b, 0)
    return rgba


def clear_flat_glass_panes(strip: Image.Image) -> Image.Image:
    """
    Clear only bright near-neutral fills that sit *inside* already-transparent
    window holes' opaque neighbors — never mass-erase cool grey / blue walls.
    Heuristic: pixel must be very low-chroma AND surrounded mostly by clear or
    charcoal frame, in the upper window band.
    """
    rgba = strip.convert("RGBA")
    pixels = rgba.load()
    w, h = rgba.size
    # First pass: mark current clear.
    clear = [[pixels[x, y][3] < 20 for x in range(w)] for y in range(h)]
    for y in range(h):
        for x in range(w):
            r, g, b, a = pixels[x, y]
            if a < 8:
                continue
            # Window band only (not floor furniture).
            if y < int(h * 0.08) or y > int(h * 0.72):
                continue
            mx = max(r, g, b)
            mn = min(r, g, b)
            if mx < 160 or (mx - mn) > 18:
                continue
            # Must touch clear (pane) or near-black frame — skip solid wall fields.
            touch = False
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1), (2, 0), (-2, 0), (0, 2), (0, -2)):
                xx, yy = x + dx, y + dy
                if not (0 <= xx < w and 0 <= yy < h):
                    continue
                if clear[yy][xx]:
                    touch = True
                    break
                rr, gg, bb, aa = pixels[xx, yy]
                if aa > 8 and max(rr, gg, bb) < 55:
                    touch = True
                    break
            if not touch:
                continue
            pixels[x, y] = (r, g, b, 0)
    return rgba


FRAME = (42, 40, 38, 255)


def paint_window_frames(strip: Image.Image, thickness: int = 2) -> Image.Image:
    """
    Draw charcoal frames around interior transparent holes (window panes).
    Skips clear regions connected to the image border (outer plate).
    """
    rgba = strip.convert("RGBA")
    w, h = rgba.size
    px = rgba.load()
    clear = [[px[x, y][3] < 20 for x in range(w)] for y in range(h)]

    # Mark border-connected clear (outer chroma), BFS.
    from collections import deque

    border = [[False] * w for _ in range(h)]
    q: deque[tuple[int, int]] = deque()
    for x in range(w):
        for y in (0, h - 1):
            if clear[y][x]:
                border[y][x] = True
                q.append((x, y))
    for y in range(h):
        for x in (0, w - 1):
            if clear[y][x] and not border[y][x]:
                border[y][x] = True
                q.append((x, y))
    while q:
        x, y = q.popleft()
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            xx, yy = x + dx, y + dy
            if 0 <= xx < w and 0 <= yy < h and clear[yy][xx] and not border[yy][xx]:
                border[yy][xx] = True
                q.append((xx, yy))

    # Interior hole mask.
    hole = [[clear[y][x] and not border[y][x] for x in range(w)] for y in range(h)]

    # Paint frame on opaque neighbors of holes, and a 1px ring into the hole.
    out = rgba.copy()
    opx = out.load()
    for y in range(h):
        for x in range(w):
            if not hole[y][x]:
                continue
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                xx, yy = x + dx, y + dy
                if not (0 <= xx < w and 0 <= yy < h):
                    continue
                if hole[yy][xx]:
                    continue
                # Neighbor is opaque wall/frame — force charcoal.
                if not clear[yy][xx] or border[yy][xx]:
                    opx[xx, yy] = FRAME
            # Inset ring so thin frames survive rescale.
            for t in range(thickness):
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    xx, yy = x + dx * (t + 1), y + dy * (t + 1)
                    if 0 <= xx < w and 0 <= yy < h and hole[yy][xx]:
                        # Only paint hole edge cells (has opaque/border neighbor).
                        edge = False
                        for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                            nx, ny = xx + ax, yy + ay
                            if 0 <= nx < w and 0 <= ny < h and not hole[ny][nx]:
                                edge = True
                                break
                        if edge:
                            opx[xx, yy] = FRAME
    return out


def rewrite_guid_meta(template: Path, dest: Path) -> None:
    lines = []
    for line in template.read_text(encoding="utf-8").splitlines():
        if line.startswith("guid:"):
            lines.append(f"guid: {uuid.uuid4().hex}")
        else:
            lines.append(line)
    dest.write_text("\n".join(lines) + "\n", encoding="utf-8")


def parse_stem_cells(stem: str) -> tuple[int, int] | None:
    import re

    leaf = stem
    if leaf.endswith("_mid"):
        leaf = leaf[: -len("_mid")]
    m = re.search(r"(\d+)x(\d+)$", leaf)
    if not m:
        return None
    return int(m.group(1)), int(m.group(2))


def fit_to_cell_canvas(strip: Image.Image, cells_w: int, cells_h: int, ppu: int = 128) -> Image.Image:
    """Stretch strip to exact catalog canvas so floor/ceiling align across face variants."""
    tw, th = max(1, cells_w * ppu), max(1, cells_h * ppu)
    return strip.convert("RGBA").resize((tw, th), Image.Resampling.BILINEAR)


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--sheet", required=True, type=Path)
    ap.add_argument("--stem", required=True, help="e.g. micro_office_3x1")
    ap.add_argument(
        "--out-dir",
        type=Path,
        default=Path("Assets/Resources/Art/Dollhouse"),
    )
    ap.add_argument("--list-only", action="store_true")
    ap.add_argument(
        "--suffixes",
        default="ext,L,R,int",
        help="Comma list top-to-bottom strip suffixes",
    )
    ap.add_argument(
        "--equal-split",
        type=int,
        default=0,
        help="If >0, ignore magenta gutters and split height into N equal bands",
    )
    ap.add_argument(
        "--clear-flat-glass",
        action="store_true",
        help="Also clear opaque grey/white window fills to alpha",
    )
    ap.add_argument(
        "--clear-sky-glass",
        action="store_true",
        help="Clear painted blue/cyan sky fills inside window band to alpha",
    )
    ap.add_argument(
        "--no-fit-canvas",
        action="store_true",
        help="Skip stretch-to-cells×128 canvas (default fits)",
    )
    args = ap.parse_args()

    im = Image.open(args.sheet).convert("RGBA")
    suffixes = [s.strip() for s in args.suffixes.split(",") if s.strip()]
    if args.equal_split > 0:
        n = args.equal_split
        band_h = im.height // n
        bands = [(i * band_h, (i + 1) * band_h if i < n - 1 else im.height) for i in range(n)]
    else:
        bands = find_content_bands(im)
    print(f"sheet={args.sheet} size={im.size} bands={len(bands)}")
    for i, (y0, y1) in enumerate(bands):
        print(f"  [{i}] y={y0}:{y1} h={y1 - y0}")

    if args.list_only:
        return

    if len(bands) < len(suffixes):
        raise SystemExit(
            f"Need {len(suffixes)} bands, found {len(bands)}. "
            f"Try --equal-split {len(suffixes)}"
        )

    png_meta_t = args.out_dir / f"{args.stem}.png.meta"
    bytes_meta_t = args.out_dir / f"{args.stem}.bytes.meta"
    if not png_meta_t.exists() or not bytes_meta_t.exists():
        raise SystemExit(f"Missing meta templates for stem {args.stem}")

    args.out_dir.mkdir(parents=True, exist_ok=True)
    for suffix, (y0, y1) in zip(suffixes, bands[: len(suffixes)]):
        leaf = f"{args.stem}_{suffix}"
        strip = im.crop((0, y0, im.width, y1))
        strip = crop_opaque_bounds(strip)
        strip = key_magenta_to_alpha(strip)
        strip = strip_outer_shell_frame(strip)
        strip = key_magenta_to_alpha(strip)
        if args.clear_flat_glass:
            strip = clear_flat_glass_panes(strip)
        if args.clear_sky_glass:
            strip = clear_sky_glass_panes(strip)
        strip = paint_window_frames(strip, thickness=2)
        cells = parse_stem_cells(args.stem)
        if cells and not args.no_fit_canvas:
            strip = fit_to_cell_canvas(strip, cells[0], cells[1])
        # Re-apply frames after resize so they stay ~2px at final resolution.
        strip = paint_window_frames(strip, thickness=2)
        # Final edge pink scrub — JPG fringe often reappears after resize.
        strip = key_magenta_to_alpha(strip)
        strip = scrub_plate_edge_fringe(strip, depth=3)
        # Window-frame paint must not rebuild a modular picture-frame on the
        # plate border (reads as grey seams between stacked rooms).
        strip = clear_outer_frame_paint(strip, depth=2)
        png_path = args.out_dir / f"{leaf}.png"
        bytes_path = args.out_dir / f"{leaf}.bytes"
        strip.save(png_path, format="PNG")
        data = png_path.read_bytes()
        bytes_path.write_bytes(data)
        rewrite_guid_meta(png_meta_t, args.out_dir / f"{leaf}.png.meta")
        rewrite_guid_meta(bytes_meta_t, args.out_dir / f"{leaf}.bytes.meta")
        print(f"wrote {leaf} {strip.size} ({len(data)} bytes)")


if __name__ == "__main__":
    main()
