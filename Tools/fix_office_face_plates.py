"""Quality-safe rebuild of office dollhouse face plates from contact sheets.

Spec: docs/superpowers/specs/2026-10-18-dollhouse-face-quality-design.md §5–§7

Rules:
  - Prefer authored contact bands (_ext/_L/_R/_int) — do not invent walls by
    grey-filling exterior glass then punching holes.
  - Interior sides keep finished plaster + decor from the band.
  - Key hot magenta / clear sky-blue pane fills to alpha (real glass).
  - Ceiling-band pink leftovers → sampled plaster (not mid-band glass).
  - Fit to cells×128 once; strip modular outer shell; light fringe scrub.
  - Snapshot prior *_ext.png to Temp/plate-baseline before overwrite.
"""
from __future__ import annotations

import shutil
import uuid
from pathlib import Path

from PIL import Image

import crop_face_contact_sheet as crop

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "Resources" / "Art" / "Dollhouse"
BASELINE = ROOT / "Temp" / "plate-baseline"
ASSETS = Path(
    r"C:\Users\smlou\.cursor\projects\c-OldPC-Importaint-Docs-Work-Steve-Escape\assets"
)

COOL_GREY = (126, 139, 155, 255)

OFFICE_SHEETS: list[tuple[str, str]] = [
    ("micro_office_3x1", "office_micro_3x1_v2_face_contact.jpg"),
    ("studio_office_4x1", "office_studio_4x1_v2_face_contact.jpg"),
    ("small_office_6x1", "office_base_6x1_v2_face_contact.jpg"),
    ("mid_office_8x1", "office_mid_8x1_v2_face_contact.jpg"),
    ("professional_suite_9x1", "office_clinic_9x1_v2_face_contact.jpg"),
    ("team_bay_10x1", "office_team_10x1_v2_face_contact.jpg"),
    ("upper_office_10x1", "office_upper_10x1_v2_face_contact.jpg"),
    ("corner_suite_12x1", "office_corner_12x1_v2_face_contact.jpg"),
    ("corporate_14x1", "office_corporate_14x1_v2_face_contact.jpg"),
]


def is_hot_plate_magenta(r: int, g: int, b: int) -> bool:
    if g > 60:
        return False
    if r >= 200 and b >= 160 and r >= g + 120 and b >= g + 100:
        return True
    if r >= 200 and g <= 40 and b >= 90 and r >= g + 140:
        return True
    return False


def is_ceiling_pink(r: int, g: int, b: int) -> bool:
    """Pink leftover safe for ceiling band only — not mid-room mauve furniture."""
    if is_hot_plate_magenta(r, g, b):
        return True
    if crop.is_magenta_key(r, g, b):
        return True
    if g <= 110 and r >= 100 and b >= 70 and r >= g + 25 and r >= b + 8 and b >= g - 8:
        return True
    if g <= 45 and r >= 45 and b >= 28 and r >= g + 18 and b >= g + 8:
        return True
    return False


def sample_plaster(band: Image.Image) -> tuple[int, int, int, int]:
    rgba = band.convert("RGBA")
    w, h = rgba.size
    samples: list[tuple[int, int, int]] = []
    for y in range(int(h * 0.25), int(h * 0.65)):
        for x in range(int(w * 0.35), int(w * 0.65)):
            r, g, b, a = rgba.getpixel((x, y))
            if a < 200:
                continue
            if is_hot_plate_magenta(r, g, b) or crop.is_magenta_key(r, g, b):
                continue
            mx, mn = max(r, g, b), min(r, g, b)
            if mx - mn > 36:
                continue
            if r > b + 10:
                continue
            if 85 <= mx <= 175:
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


def fill_ceiling_pink(band: Image.Image, wall: tuple[int, int, int, int]) -> Image.Image:
    rgba = band.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    y1 = max(2, int(h * 0.20))
    for y in range(y1):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 8:
                continue
            if is_ceiling_pink(r, g, b):
                px[x, y] = wall
    return rgba


def magenta_wall_fraction(band: Image.Image) -> float:
    """Fraction of center pixels that are hot plate magenta (false walls)."""
    rgba = band.convert("RGBA")
    w, h = rgba.size
    mag = tot = 0
    for y in range(int(h * 0.2), int(h * 0.7)):
        for x in range(int(w * 0.2), int(w * 0.8)):
            r, g, b, a = rgba.getpixel((x, y))
            tot += 1
            if a > 8 and (
                is_hot_plate_magenta(r, g, b) or crop.is_magenta_key(r, g, b)
            ):
                mag += 1
    return mag / max(1, tot)


def restore_false_magenta_walls(
    band: Image.Image, wall: tuple[int, int, int, int], kind: str
) -> Image.Image:
    """
    Some generator _ext bands paint entire walls hot magenta (keyed → hollow).
    Restore those to sampled plaster, but KEEP magenta in exterior window columns
    so they still key to sky alpha. Interior-side kinds seal side columns to plaster.
    """
    rgba = band.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    y0, y1 = int(h * 0.12), int(h * 0.82)
    left_win = (0, int(w * 0.38))
    right_win = (int(w * 0.62), w)
    # Which columns are true exterior glass for this face?
    keep_left_glass = kind in ("ext", "L")
    keep_right_glass = kind in ("ext", "R")

    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a < 8:
                continue
            if not (is_hot_plate_magenta(r, g, b) or crop.is_magenta_key(r, g, b)):
                continue
            if y < int(h * 0.18):
                px[x, y] = wall  # ceiling key → plaster
                continue
            if y >= int(h * 0.80):
                px[x, y] = wall
                continue
            in_left = left_win[0] <= x <= left_win[1] and y0 <= y <= y1
            in_right = right_win[0] <= x <= right_win[1] and y0 <= y <= y1
            if in_left and keep_left_glass:
                continue  # leave for alpha key
            if in_right and keep_right_glass:
                continue
            # Bright plate magenta in the window band = back glass → keep for key.
            if (
                y0 <= y <= y1
                and is_hot_plate_magenta(r, g, b)
                and r >= 220
                and b >= 180
            ):
                continue
            # Center / interior sides: false magenta wall → finished plaster
            px[x, y] = wall
    return rgba


def clear_sky_and_flat_glass(band: Image.Image) -> Image.Image:
    """Clear painted sky + flat grey panes that already touch clear (real glass)."""
    strip = crop.clear_sky_glass_panes(band)
    strip = crop.clear_flat_glass_panes(strip)
    return strip


def seal_interior_side_to_plaster(
    band: Image.Image, wall: tuple[int, int, int, int], left: bool, frac: float = 0.28
) -> Image.Image:
    """Finished plaster on an interior side — keep furniture/plants/wood/art."""
    rgba = band.convert("RGBA")
    px = rgba.load()
    w, h = rgba.size
    x0 = 0 if left else int(w * (1.0 - frac))
    x1 = int(w * frac) if left else w
    y0, y1 = int(h * 0.10), int(h * 0.85)
    for y in range(y0, y1):
        for x in range(x0, x1):
            r, g, b, a = px[x, y]
            # Keep plants / wood / warm art frames.
            if a > 20 and g > r + 18 and g > b + 12 and g > 55:
                continue
            if a > 20 and r > g + 28 and r > b + 18 and r > 70 and g > 35:
                continue
            if a < 20 or is_hot_plate_magenta(r, g, b) or crop.is_magenta_key(r, g, b):
                px[x, y] = wall
                continue
            if b > 150 and b > r + 12 and b >= g + 10:
                px[x, y] = wall
                continue
            mx, mn = max(r, g, b), min(r, g, b)
            if a > 20 and mx - mn <= 22 and 100 <= mx <= 210:
                px[x, y] = wall
            if a > 20 and mx <= 85 and (mx - mn) <= 30:
                px[x, y] = wall
    return rgba


def process_band(
    band: Image.Image, wall: tuple[int, int, int, int], cells_w: int, kind: str
) -> Image.Image:
    """Surgical pipeline — preserve authored walls/decor; key glass only."""
    strip = crop.crop_opaque_bounds(band)
    strip = fill_ceiling_pink(strip, wall)
    if magenta_wall_fraction(strip) > 0.30:
        strip = restore_false_magenta_walls(strip, wall, kind)
    # Authored interior sides: solid finished plaster (contact sheets often leave glass).
    if kind == "int":
        strip = seal_interior_side_to_plaster(strip, wall, left=True)
        strip = seal_interior_side_to_plaster(strip, wall, left=False)
    elif kind == "L":
        strip = seal_interior_side_to_plaster(strip, wall, left=False)
    elif kind == "R":
        strip = seal_interior_side_to_plaster(strip, wall, left=True)
    strip = crop.key_magenta_to_alpha(strip)
    # Second pass: remaining hot plate magenta → alpha.
    px = strip.load()
    w, h = strip.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            if a > 8 and is_hot_plate_magenta(r, g, b):
                px[x, y] = (r, g, b, 0)

    strip = clear_sky_and_flat_glass(strip)
    strip = crop.strip_outer_shell_frame(strip)
    strip = crop.key_magenta_to_alpha(strip)
    strip = clear_sky_and_flat_glass(strip)
    strip = crop.paint_window_frames(strip, thickness=2)
    strip = crop.fit_to_cell_canvas(strip, cells_w, 1)
    strip = crop.paint_window_frames(strip, thickness=2)
    strip = crop.key_magenta_to_alpha(strip)
    strip = fill_ceiling_pink(strip, wall)
    strip = crop.scrub_plate_edge_fringe(strip, depth=3)
    strip = crop.clear_outer_frame_paint(strip, depth=2)
    return strip


def rewrite_guid_meta(template: Path, dest: Path) -> None:
    lines = []
    for line in template.read_text(encoding="utf-8").splitlines():
        if line.startswith("guid:"):
            lines.append(f"guid: {uuid.uuid4().hex}")
        else:
            lines.append(line)
    dest.write_text("\n".join(lines) + "\n", encoding="utf-8")


def snapshot_baseline(stem: str) -> None:
    BASELINE.mkdir(parents=True, exist_ok=True)
    src = OUT / f"{stem}_ext.png"
    if src.exists():
        shutil.copy2(src, BASELINE / f"{stem}_ext.png")


def write_leaf(strip: Image.Image, leaf: str, stem: str) -> None:
    png = OUT / f"{leaf}.png"
    byt = OUT / f"{leaf}.bytes"
    strip.save(png, format="PNG")
    data = png.read_bytes()
    byt.write_bytes(data)
    png_meta_t = OUT / f"{stem}.png.meta"
    bytes_meta_t = OUT / f"{stem}.bytes.meta"
    if png_meta_t.exists():
        rewrite_guid_meta(png_meta_t, OUT / f"{leaf}.png.meta")
    if bytes_meta_t.exists():
        rewrite_guid_meta(bytes_meta_t, OUT / f"{leaf}.bytes.meta")
    clear = sum(1 for p in strip.getdata() if p[3] < 20)
    print(
        f"  wrote {leaf} {strip.size} clear%={100 * clear / (strip.size[0] * strip.size[1]):.1f}"
    )


def parse_cells(stem: str) -> int:
    import re

    m = re.search(r"(\d+)x(\d+)$", stem)
    return int(m.group(1)) if m else 3


def rebuild_stem(stem: str, sheet_name: str) -> None:
    sheet = ASSETS / sheet_name
    if not sheet.exists():
        print(f"SKIP {stem}: missing sheet {sheet_name}")
        return
    print(f"=== {stem} <- {sheet_name}")
    snapshot_baseline(stem)

    im = Image.open(sheet).convert("RGBA")
    n = 4
    bh = im.height // n
    bands = {
        "ext": im.crop((0, 0, im.width, bh)),
        "L": im.crop((0, bh, im.width, 2 * bh)),
        "R": im.crop((0, 2 * bh, im.width, 3 * bh)),
        "int": im.crop((0, 3 * bh, im.width, im.height)),
    }
    # Plaster sample from L (usually best solid walls); fall back to COOL_GREY.
    wall = sample_plaster(bands["L"])
    if wall == COOL_GREY:
        wall = sample_plaster(bands["ext"])
    print(f"  plaster sample {wall}")

    cells_w = parse_cells(stem)
    for kind in ("ext", "L", "R", "int"):
        strip = process_band(bands[kind], wall, cells_w, kind)
        write_leaf(strip, f"{stem}_{kind}", stem)

    ext_png = OUT / f"{stem}_ext.png"
    if ext_png.exists():
        (OUT / f"{stem}.png").write_bytes(ext_png.read_bytes())
        (OUT / f"{stem}.bytes").write_bytes(ext_png.read_bytes())
        print(f"  mirrored legacy {stem}")


def main() -> None:
    for stem, sheet in OFFICE_SHEETS:
        rebuild_stem(stem, sheet)


if __name__ == "__main__":
    main()
