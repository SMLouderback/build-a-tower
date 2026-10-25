"""Rebuild micro_office face plates from the v2 contact sheet.

Source problems:
  - _ext walls are painted hot magenta (keyed → hollow + leftover pink)
  - _R / _int keep a left window even when that side should be interior
  - _int uses tan plaster instead of cool-grey office walls

Fix: rebuild from the _L band (good cool-grey walls), seal interior sides,
and only key true glass / plate magenta.
"""
from __future__ import annotations

import uuid
from pathlib import Path

from PIL import Image

import crop_face_contact_sheet as crop

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "Resources" / "Art" / "Dollhouse"
ASSETS = Path(
    r"C:\Users\smlou\.cursor\projects\c-OldPC-Importaint-Docs-Work-Steve-Escape\assets"
)
SHEET = ASSETS / "office_micro_3x1_v2_face_contact.jpg"
STEM = "micro_office_3x1"
COOL_GREY = (126, 139, 155, 255)
FRAME = (42, 40, 38, 255)


def is_hot_plate_magenta(r: int, g: int, b: int) -> bool:
    """True #FF00FF / generator plate — not mauve furniture."""
    if g > 60:
        return False
    if r >= 200 and b >= 160 and r >= g + 120 and b >= g + 100:
        return True
    if r >= 200 and g <= 40 and b >= 90 and r >= g + 140:
        return True
    return False


def sample_cool_grey(band: Image.Image) -> tuple[int, int, int, int]:
    rgba = band.convert("RGBA")
    w, h = rgba.size
    samples: list[tuple[int, int, int]] = []
    for y in range(int(h * 0.25), int(h * 0.65)):
        for x in range(int(w * 0.35), int(w * 0.65)):
            r, g, b, a = rgba.getpixel((x, y))
            if a < 200:
                continue
            if is_hot_plate_magenta(r, g, b):
                continue
            mx, mn = max(r, g, b), min(r, g, b)
            if mx - mn > 40:
                continue
            if 90 <= mx <= 180:
                samples.append((r, g, b))
    if not samples:
        return COOL_GREY
    n = len(samples)
    return (
        sum(s[0] for s in samples) // n,
        sum(s[1] for s in samples) // n,
        sum(s[2] for s in samples) // n,
        255,
    )


def is_magenta_wall_paint(r: int, g: int, b: int) -> bool:
    """Hot plate magenta OR JPG-crushed magenta wall fields (not wood/plant)."""
    if is_hot_plate_magenta(r, g, b):
        return True
    if crop.is_magenta_key(r, g, b):
        return True
    # Saturated pink/magenta plaster used as false walls on _ext/_L.
    if g > 70:
        return False
    if r >= 160 and b >= 80 and r >= g + 80 and r >= b + 20:
        return True
    return False


def fill_magenta_walls(band: Image.Image, wall: tuple[int, int, int, int]) -> Image.Image:
    """Replace magenta wall fields with cool grey; leave window-pane magenta."""
    rgba = band.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    # Window pane sits on the left wall — keep magenta only inside that rect.
    wx0, wx1 = int(w * 0.18), int(w * 0.42)
    wy0, wy1 = int(h * 0.18), int(h * 0.72)
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 8:
                continue
            if not is_magenta_wall_paint(r, g, b):
                continue
            in_window = wx0 <= x <= wx1 and wy0 <= y <= wy1
            if in_window:
                continue
            px[x, y] = wall
    return rgba


def recolor_tan_walls(band: Image.Image, wall: tuple[int, int, int, int]) -> Image.Image:
    """Shift Micro-tan plaster toward cool-grey office walls."""
    rgba = band.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    wr, wg, wb, _ = wall
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 200:
                continue
            # Warm tan plaster: R dominant, mid brightness, moderate chroma.
            if r < 140 or r < g + 15 or r < b + 25:
                continue
            if g < 90 or b < 70:
                continue
            if r > 230 and g > 200:
                continue  # lights
            # Keep wood furniture (higher red lift + lower blue).
            if r > g + 40 and b < g - 10:
                continue
            # Blend toward cool grey while keeping panel shading.
            shade = (r + g + b) / (3 * 255.0)
            target = (
                int(wr * 0.55 + r * 0.45 * shade / max(shade, 0.35)),
                int(wg * 0.55 + g * 0.45 * shade / max(shade, 0.35)),
                int(wb * 0.55 + b * 0.45 * shade / max(shade, 0.35)),
                255,
            )
            # Stronger replace: mostly wall sample with luminance from source.
            lum = int(0.30 * r + 0.59 * g + 0.11 * b)
            base_lum = int(0.30 * wr + 0.59 * wg + 0.11 * wb)
            delta = lum - base_lum
            px[x, y] = (
                max(0, min(255, wr + delta)),
                max(0, min(255, wg + delta)),
                max(0, min(255, wb + delta)),
                255,
            )
    return rgba


def seal_side_glass(
    band: Image.Image,
    wall: tuple[int, int, int, int],
    left: bool,
    frac: float = 0.30,
) -> Image.Image:
    rgba = band.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    x0 = 0 if left else int(w * (1.0 - frac))
    x1 = int(w * frac) if left else w
    y0, y1 = int(h * 0.12), int(h * 0.82)
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            # Keep furniture / plants / wood.
            if a > 20 and g > r + 20 and g > b + 20 and g > 60:
                continue
            if a > 20 and r > g + 25 and r > b + 25 and r > 70 and g > 30:
                continue
            if a < 20 or is_hot_plate_magenta(r, g, b):
                px[x, y] = wall
                continue
            # Sky / glass blues already keyed to opaque blue fills.
            if b > 140 and b > r + 15 and b > g + 5 and a > 20:
                px[x, y] = wall
                continue
            # Dark mirror panes.
            mx, mn = max(r, g, b), min(r, g, b)
            if a > 20 and mx <= 90 and (mx - mn) <= 30:
                px[x, y] = wall
    return rgba


def process_band(band: Image.Image, kind: str, wall: tuple[int, int, int, int]) -> Image.Image:
    strip = crop.crop_opaque_bounds(band)
    if kind == "ext":
        # Rebuild from L-like content: fill non-window magenta walls first.
        strip = fill_magenta_walls(strip, wall)
    elif kind == "int":
        strip = fill_magenta_walls(strip, wall)
        strip = recolor_tan_walls(strip, wall)
        strip = seal_side_glass(strip, wall, left=True)
        strip = seal_side_glass(strip, wall, left=False)
    elif kind == "L":
        # Right side is interior in source (magenta) — leave clear after key for party wall,
        # but convert non-window magenta walls if any.
        strip = fill_magenta_walls(strip, wall)
        # Re-clear the right wall band so party-wall underlay shows when right is interior.
        strip = clear_side_for_party(strip, left=False, frac=0.12)
    elif kind == "R":
        strip = fill_magenta_walls(strip, wall)
        strip = seal_side_glass(strip, wall, left=True)
        strip = clear_side_for_party(strip, left=True, frac=0.08)
        # Right exterior keeps any right glass; if none, leave solid wall.

    strip = crop.key_magenta_to_alpha(strip)
    # Second pass: key remaining hot magenta (window + plate).
    px = strip.load()
    w, h = strip.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a > 8 and is_hot_plate_magenta(r, g, b):
                px[x, y] = (r, g, b, 0)

    strip = crop.strip_outer_shell_frame(strip)
    strip = crop.key_magenta_to_alpha(strip)
    strip = crop.paint_window_frames(strip, thickness=2)
    strip = crop.fit_to_cell_canvas(strip, 3, 1)
    strip = crop.paint_window_frames(strip, thickness=2)
    strip = crop.key_magenta_to_alpha(strip)
    strip = crop.scrub_plate_edge_fringe(strip, depth=3)
    strip = crop.clear_outer_frame_paint(strip, depth=2)
    return strip


def clear_side_for_party(band: Image.Image, left: bool, frac: float) -> Image.Image:
    """Clear a thin side strip so cement party-wall underlay reads through."""
    rgba = band.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    x0 = 0 if left else int(w * (1.0 - frac))
    x1 = int(w * frac) if left else w
    for y in range(h):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            if a < 8 or is_hot_plate_magenta(r, g, b):
                px[x, y] = (0, 0, 0, 0)
            elif max(r, g, b) <= 90 and (max(r, g, b) - min(r, g, b)) <= 28:
                px[x, y] = (0, 0, 0, 0)
    return rgba


def rewrite_guid_meta(template: Path, dest: Path) -> None:
    lines = []
    for line in template.read_text(encoding="utf-8").splitlines():
        if line.startswith("guid:"):
            lines.append(f"guid: {uuid.uuid4().hex}")
        else:
            lines.append(line)
    dest.write_text("\n".join(lines) + "\n", encoding="utf-8")


def write_leaf(strip: Image.Image, leaf: str) -> None:
    png = OUT / f"{leaf}.png"
    byt = OUT / f"{leaf}.bytes"
    strip.save(png, format="PNG")
    data = png.read_bytes()
    byt.write_bytes(data)
    png_meta_t = OUT / f"{STEM}.png.meta"
    bytes_meta_t = OUT / f"{STEM}.bytes.meta"
    rewrite_guid_meta(png_meta_t, OUT / f"{leaf}.png.meta")
    rewrite_guid_meta(bytes_meta_t, OUT / f"{leaf}.bytes.meta")
    clear = sum(1 for p in strip.getdata() if p[3] < 20)
    print(f"wrote {leaf} {strip.size} clear%={100 * clear / (strip.size[0] * strip.size[1]):.1f}")


def main() -> None:
    im = Image.open(SHEET).convert("RGBA")
    n = 4
    bh = im.height // n
    bands = {
        "ext": im.crop((0, 0, im.width, bh)),
        "L": im.crop((0, bh, im.width, 2 * bh)),
        "R": im.crop((0, 2 * bh, im.width, 3 * bh)),
        "int": im.crop((0, 3 * bh, im.width, im.height)),
    }
    # Use L band for wall sample + as the structural base for ext.
    wall = sample_cool_grey(bands["L"])
    print("wall sample", wall)

    # Rebuild ext from L (walls intact) rather than magenta-walled ext band.
    base_l = bands["L"].copy()
    bands["ext"] = base_l

    for kind in ("ext", "L", "R", "int"):
        strip = process_band(bands[kind], kind, wall)
        write_leaf(strip, f"{STEM}_{kind}")

    # Legacy stem mirrors ext.
    ext_png = OUT / f"{STEM}_ext.png"
    for name in (STEM,):
        (OUT / f"{name}.png").write_bytes(ext_png.read_bytes())
        (OUT / f"{name}.bytes").write_bytes(ext_png.read_bytes())
        print(f"mirrored legacy {name}")


if __name__ == "__main__":
    main()
