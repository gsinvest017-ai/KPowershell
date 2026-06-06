"""
make-ico.py — SVG → multi-resolution ICO
Usage:
    python scripts/make-ico.py
    python scripts/make-ico.py --svg Resources/logo.svg --out Resources/icon.ico

Pipeline (同 autogo/scripts/make-ico.py):
    svglib → ReportLab drawing → PNG (256 px) → Pillow ICO (16/32/48/256)
"""

import argparse, sys, os
from pathlib import Path

def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--svg", default="Resources/logo.svg")
    ap.add_argument("--out", default="Resources/icon.ico")
    args = ap.parse_args()

    svg_path = Path(args.svg)
    out_path = Path(args.out)

    if not svg_path.exists():
        print(f"[ERR] SVG not found: {svg_path}", file=sys.stderr)
        sys.exit(1)

    out_path.parent.mkdir(parents=True, exist_ok=True)

    # ── Step 1: SVG → ReportLab drawing ──────────────────────────────────
    from svglib.svglib import svg2rlg
    drawing = svg2rlg(str(svg_path))
    if drawing is None:
        print("[ERR] svglib failed to parse SVG", file=sys.stderr)
        sys.exit(1)

    # ── Step 2: Render to PNG at 256×256 ────────────────────────────────
    import tempfile
    from reportlab.graphics import renderPM

    target = 256
    sx = target / drawing.width
    sy = target / drawing.height
    drawing.width  = target
    drawing.height = target
    drawing.transform = (sx, 0, 0, sy, 0, 0)

    tmp_png = Path(tempfile.mktemp(suffix=".png"))
    renderPM.drawToFile(drawing, str(tmp_png), fmt="PNG", dpi=72,
                        bg=0x141414)   # match SVG background

    # ── Step 3: PNG → ICO (multi-res) ────────────────────────────────────
    from PIL import Image
    img = Image.open(tmp_png).convert("RGBA")
    img.save(str(out_path), format="ICO",
             sizes=[(16,16),(32,32),(48,48),(256,256)])
    tmp_png.unlink(missing_ok=True)

    print(f"[OK] {out_path}  ({out_path.stat().st_size // 1024} KB)")

if __name__ == "__main__":
    main()
