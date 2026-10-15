#!/usr/bin/env python3
"""Create an offline sprite-animation QA review from source frames or a grid atlas.

Dependencies: Pillow (PIL). No Unity runtime, model API, or network access.
"""
import argparse
import json
import statistics
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def parse_pair(raw):
    try:
        a, b = raw.split(",", 1)
        return int(a), int(b)
    except (AttributeError, ValueError) as exc:
        raise argparse.ArgumentTypeError("Expected X,Y integers") from exc


def open_frames(args):
    if args.sheet:
        im = Image.open(args.sheet).convert("RGBA")
        if not args.cols or not args.rows or args.cols < 1 or args.rows < 1:
            raise ValueError("--sheet requires positive --cols and --rows")
        if im.width % args.cols or im.height % args.rows:
            raise ValueError("Atlas must divide exactly into cols x rows; inspect margins manually")
        w, h = im.width // args.cols, im.height // args.rows
        if args.indices:
            indices = [int(x.strip()) for x in args.indices.split(",")]
        else:
            indices = list(range(args.cols * args.rows))
        if not indices or any(i < 0 or i >= args.cols * args.rows for i in indices):
            raise ValueError("Frame indices out of range or empty")
        return [im.crop(((i % args.cols) * w, (i // args.cols) * h,
                         (i % args.cols + 1) * w, (i // args.cols + 1) * h)) for i in indices], [f"cell {i}" for i in indices]
    paths = []
    for entry in args.frames:
        p = Path(entry)
        if p.is_dir():
            paths += sorted(q for q in p.iterdir() if q.suffix.lower() in (".png", ".webp"))
        elif p.is_file():
            paths.append(p)
        else:
            raise FileNotFoundError(str(p))
    if not paths:
        raise ValueError("No input frames")
    return [Image.open(p).convert("RGBA") for p in paths], [p.name for p in paths]


def checker(size, tile=8):
    out = Image.new("RGB", size, (232, 232, 232))
    d = ImageDraw.Draw(out)
    for y in range(0, size[1], tile):
        for x in range(0, size[0], tile):
            if (x // tile + y // tile) % 2:
                d.rectangle((x, y, min(x + tile - 1, size[0] - 1), min(y + tile - 1, size[1] - 1)), fill=(197, 197, 197))
    return out


def displayed(im, scale, anchor=None):
    bg = checker(im.size)
    bg.paste(im, mask=im.getchannel("A"))
    if anchor is not None:
        d = ImageDraw.Draw(bg)
        x, y = anchor
        d.line([(x - 2, y), (x + 2, y)], fill=(245, 30, 130), width=1)
        d.line([(x, y - 2), (x, y + 2)], fill=(245, 30, 130), width=1)
    return bg.resize((im.width * scale, im.height * scale), Image.Resampling.NEAREST)


def opaque_statistics(im):
    a = im.getchannel("A")
    bbox = a.getbbox()
    return {"opaque_bbox": list(bbox) if bbox else None,
            "opaque_bottom_y": bbox[3] - 1 if bbox else None,
            "opaque_box_width": bbox[2] - bbox[0] if bbox else 0,
            "opaque_box_height": bbox[3] - bbox[1] if bbox else 0}


def output(args):
    if args.fps <= 0 or args.scale < 1 or args.scale > 16:
        raise ValueError("--fps must be positive and --scale must be 1..16")
    frames, labels = open_frames(args)
    w, h = frames[0].size
    if w * h > 4_000_000 or len(frames) > 200:
        raise ValueError("Limit report to <=200 selected frames, <=4M pixels each")
    sizes = [list(im.size) for im in frames]
    warnings = []
    if any(im.size != (w, h) for im in frames):
        raise ValueError("All selected frame canvases must have same size; fix source contract before generating the report")
    if args.contact_index is not None and not 0 <= args.contact_index < len(frames):
        raise ValueError("--contact-index must be a valid index within selected frames")
    if args.anchor and not (0 <= args.anchor[0] < w and 0 <= args.anchor[1] < h):
        raise ValueError("--anchor lies outside image canvas")
    measures = [opaque_statistics(im) for im in frames]
    bottoms = [m["opaque_bottom_y"] for m in measures if m["opaque_bottom_y"] is not None]
    bottom_median = statistics.median(bottoms) if bottoms else None
    if not bottoms:
        warnings.append("No visible pixels in any frame")
    for i, m in enumerate(measures):
        if m["opaque_bbox"] is None:
            warnings.append(f"Frame {i} is blank")
        if bottom_median is not None and m["opaque_bottom_y"] is not None:
            if abs(m["opaque_bottom_y"] - bottom_median) > max(2, h * 0.08):
                warnings.append(f"Frame {i}: opaque content bottom deviates from median by >8% of canvas (not necessarily foot drift)")
        if i:
            prev = measures[i - 1]
            if m["opaque_bbox"] and prev["opaque_bbox"]:
                a, b = m["opaque_bbox"], prev["opaque_bbox"]
                dx = abs((a[0]+a[2])/2 - (b[0]+b[2])/2)
                dy = abs((a[1]+a[3])/2 - (b[1]+b[3])/2)
                if dx > max(3, w * 0.16) or dy > max(3, h * 0.16):
                    warnings.append(f"Frames {i-1}->{i}: large opaque bounding-box center jump; inspect weapons/effects vs root")
            if frames[i - 1].tobytes() == frames[i].tobytes():
                warnings.append(f"Frames {i-1}->{i} are byte-equivalent pixels (may be intentional hold)")

    out = Path(args.out)
    out.mkdir(parents=True, exist_ok=True)
    shown = [displayed(im, args.scale, args.anchor) for im in frames]
    fw, fh = shown[0].size
    label_h = 25
    columns = min(6, len(frames))
    rows = (len(frames) + columns - 1) // columns
    strip = Image.new("RGB", (columns * fw, rows * (fh + label_h)), (33, 33, 33))
    draw = ImageDraw.Draw(strip)
    for i, frame in enumerate(shown):
        x, y = (i % columns) * fw, (i // columns) * (fh + label_h)
        strip.paste(frame, (x, y + label_h))
        tag = f"{i}: {labels[i]}"
        if args.contact_index == i:
            tag = "CONTACT " + tag
        draw.text((x + 3, y + 4), tag[:max(5, fw // 6)], fill=(255, 255, 255), font=ImageFont.load_default())
    strip.save(out / "filmstrip.png")

    # Composite full frames; never auto-recenter source frames as that can hide drift.
    gif_frames = [displayed(im, max(1, min(args.scale, 5)), None) for im in frames]
    gif_frames[0].save(out / "preview.gif", save_all=True, append_images=gif_frames[1:],
                       duration=max(20, int(round(1000 / args.fps))), loop=0,
                       optimize=False, disposal=2)

    pairs = len(frames) - 1
    if pairs:
        ow = shown[0].width
        oh = shown[0].height
        onion = Image.new("RGB", (min(5, pairs) * ow, ((pairs + 4) // 5) * (oh + label_h)), (33, 33, 33))
        d = ImageDraw.Draw(onion)
        for i in range(1, len(frames)):
            previous, current = shown[i - 1], shown[i]
            blended = Image.blend(previous, current, 0.5)
            x, y = ((i - 1) % 5) * ow, ((i - 1) // 5) * (oh + label_h)
            onion.paste(blended, (x, y + label_h))
            d.text((x + 3, y + 4), f"{i-1}->{i}", fill=(255, 255, 255))
        onion.save(out / "onion_skin.png")
    else:
        shown[0].save(out / "onion_skin.png")  # one frame: no transition to compare

    info = {"source": "sheet" if args.sheet else "separate_frames", "frame_count": len(frames),
            "frame_size": [w, h], "fps": args.fps, "scale": args.scale,
            "anchor": list(args.anchor) if args.anchor else None,
            "contact_index": args.contact_index,
            "frames": [{"index": i, "label": labels[i], "canvas": sizes[i], **measures[i]} for i in range(len(frames))],
            "warnings": warnings,
            "interpretation": "Heuristic warnings only. No anatomy, natural motion, runtime or user acceptance inference."}
    (out / "metrics.json").write_text(json.dumps(info, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    lines = ["# Offline sprite animation QA", "", f"- Frames: {len(frames)}; canvas: {w}x{h}; fps: {args.fps}",
             "- Viewing: `filmstrip.png`, `onion_skin.png`, `preview.gif`", "", "## Candidate warnings (not pass/fail)", ""]
    lines += [f"- {w}" for w in warnings] if warnings else ["- No numerical warnings; visual review is still necessary."]
    lines += ["", "## Manual visual sequence review", "", "- [ ] Root/foot anchor and size stay stable across frames", "- [ ] Legs move plausibly, not whole-body bobbing/sliding", "- [ ] Head, face, silhouette, palette and equipment identity persist", "- [ ] Hands, wrists, shoulder-elbow chain, weapon grip, arrow/strings are correct", "- [ ] Direction, attack-release contact frame, projectile/VFX/Hitbox agree with runtime", "- [ ] Start/end of loop and walk→attack→walk transitions are natural", "- [ ] Rejected/restored/accepted asset ledger reconciled", "- [ ] Preview inspected by a visual reviewer; Unity native state reported separately", "", "**Status: UNREVIEWED until an actual visual inspection.**", ""]
    (out / "report.md").write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {out} ({len(frames)} frames, {len(warnings)} candidate warnings)")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    group = parser.add_mutually_exclusive_group(required=True)
    group.add_argument("--frames", nargs="+", metavar="PNG", help="Ordered PNG paths or one directory (alphabetically sorted)")
    group.add_argument("--sheet", help="Uniform sprite-sheet path")
    parser.add_argument("--cols", type=int)
    parser.add_argument("--rows", type=int)
    parser.add_argument("--indices", help="Comma-separated 0-based atlas indices, in playback order")
    parser.add_argument("--fps", type=float, default=8)
    parser.add_argument("--scale", type=int, default=4)
    parser.add_argument("--anchor", type=parse_pair, default=None, help="X,Y visual-only root marker")
    parser.add_argument("--contact-index", type=int, default=None)
    parser.add_argument("--out", required=True)
    args = parser.parse_args()
    try:
        output(args)
    except (ValueError, FileNotFoundError, OSError) as exc:
        parser.error(str(exc))


if __name__ == "__main__":
    main()
