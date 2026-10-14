"""Split a magenta-keyed dollhouse contact sheet into face-variant strips."""
from __future__ import annotations

import argparse
import uuid
from pathlib import Path

from PIL import Image


def is_magenta_rgb(r: int, g: int, b: int) -> bool:
    # Hot #FF00FF and generator pinks like #F507B6 / #EE0791.
    return r >= 200 and g <= 90 and b >= 130 and r >= g + 80


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
            if is_magenta_rgb(r, g, b):
                pixels[x, y] = (r, g, b, 0)
    return rgba


def rewrite_guid_meta(template: Path, dest: Path) -> None:
    lines = []
    for line in template.read_text(encoding="utf-8").splitlines():
        if line.startswith("guid:"):
            lines.append(f"guid: {uuid.uuid4().hex}")
        else:
            lines.append(line)
    dest.write_text("\n".join(lines) + "\n", encoding="utf-8")


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
