"""Quantitative screenshot checks for Build-A-Tower visual verify loop."""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


def is_hot_magenta(r: int, g: int, b: int, a: int) -> bool:
    if a < 40:
        return False
    if g > 70:
        return False
    if r >= 180 and b >= 80 and r >= g + 90 and r > b:
        return True
    if r >= 200 and b >= 160 and r >= g + 120:
        return True
    return False


def analyze(path: Path, max_magenta_pct: float, expect_notes: list[str]) -> dict:
    im = Image.open(path).convert("RGBA")
    w, h = im.size
    pixels = list(im.getdata())
    n = len(pixels)
    magenta = sum(1 for r, g, b, a in pixels if is_hot_magenta(r, g, b, a))
    # Edge band: outer 2% — high clear/sky blue often means plate gaps.
    ew = max(2, w // 50)
    eh = max(2, h // 50)
    edge = []
    for y in range(h):
        for x in range(w):
            if x < ew or x >= w - ew or y < eh or y >= h - eh:
                edge.append(pixels[y * w + x])
    skyish = sum(
        1
        for r, g, b, a in edge
        if a > 40 and b > 140 and b > r + 20 and b > g + 10
    )
    magenta_pct = 100.0 * magenta / max(1, n)
    sky_edge_pct = 100.0 * skyish / max(1, len(edge))

    checks = []
    checks.append(
        {
            "id": "magenta",
            "ok": magenta_pct <= max_magenta_pct,
            "value": round(magenta_pct, 3),
            "limit": max_magenta_pct,
            "detail": f"hot magenta pixels {magenta}/{n} ({magenta_pct:.3f}%)",
        }
    )
    checks.append(
        {
            "id": "edge_sky_bleed",
            "ok": sky_edge_pct <= 35.0,
            "value": round(sky_edge_pct, 2),
            "limit": 35.0,
            "detail": f"sky-like edge pixels {sky_edge_pct:.1f}% of rim (informational)",
        }
    )

    passed = all(c["ok"] for c in checks if c["id"] == "magenta")
    report = {
        "screenshot": str(path),
        "size": [w, h],
        "passed": passed,
        "checks": checks,
        "expectNotes": expect_notes,
        "agentHint": (
            "Open the PNG with the Read tool and visually confirm expectNotes. "
            "Quantitative magenta gate is necessary but not sufficient."
        ),
    }
    return report


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--image", required=True, type=Path)
    ap.add_argument("--scenario", type=Path, default=None)
    ap.add_argument("--max-magenta-pct", type=float, default=0.15)
    ap.add_argument("--out", type=Path, default=None)
    args = ap.parse_args()

    notes: list[str] = []
    max_m = args.max_magenta_pct
    if args.scenario and args.scenario.exists():
        data = json.loads(args.scenario.read_text(encoding="utf-8"))
        expect = data.get("expect") or {}
        max_m = float(expect.get("maxMagentaPct", max_m))
        notes = list(expect.get("notes") or [])

    report = analyze(args.image, max_m, notes)
    text = json.dumps(report, indent=2)
    out = args.out or args.image.with_suffix(".analysis.json")
    out.write_text(text + "\n", encoding="utf-8")
    print(text)
    raise SystemExit(0 if report["passed"] else 2)


if __name__ == "__main__":
    main()
