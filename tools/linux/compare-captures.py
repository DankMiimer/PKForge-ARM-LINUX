#!/usr/bin/env python3
"""Report matched-capture differences; a report alone does not grant UI acceptance.

Requires Pillow. Crops must be justified in the evidence record (for example an
approved platform backdrop); images are never resized or shifted automatically.
"""
import argparse
import hashlib
import json
from pathlib import Path

from PIL import Image, ImageChops, ImageStat


def compare(reference, candidate, crop, radius, color_tolerance):
    if reference.size != candidate.size:
        raise ValueError(f"Viewport mismatch: {reference.size} != {candidate.size}")
    bounds = crop or (0, 0, *reference.size)
    left, top, right, bottom = bounds
    if not (0 <= left < right <= reference.width and 0 <= top < bottom <= reference.height):
        raise ValueError("Crop is outside the matched viewport")
    ref = reference.crop(bounds).convert("RGB")
    current = candidate.crop(bounds).convert("RGB")
    difference = ImageChops.difference(ref, current)
    expected, actual = ref.load(), current.load()
    changed = near = 0
    largest = 0
    for y in range(ref.height):
        for x in range(ref.width):
            pixel = actual[x, y]
            delta = max(abs(a-b) for a, b in zip(pixel, expected[x, y]))
            largest = max(largest, delta)
            if delta == 0:
                continue
            changed += 1
            for yy in range(max(0, y-radius), min(ref.height, y+radius+1)):
                if any(max(abs(a-b) for a, b in zip(pixel, expected[xx, yy])) <= color_tolerance
                       for xx in range(max(0, x-radius), min(ref.width, x+radius+1))):
                    near += 1
                    break
    pixels = ref.width * ref.height
    return {
        "viewport": list(reference.size), "crop": list(bounds), "pixels": pixels,
        "exactlyChangedPixels": changed, "exactMatchFraction": (pixels-changed)/pixels,
        "meanChannelDifference": ImageStat.Stat(difference).mean,
        "largestChannelDifference": largest, "changedBoundsInCrop": difference.getbbox(),
        "neighborRadius": radius, "neighborColorTolerance": color_tolerance,
        "changedPixelsWithNearbyReferenceColor": near,
        "remainingChangedPixels": changed-near,
        "acceptance": "Review content, anchors, raster edges and state coverage separately.",
    }, difference


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("--crop", nargs=4, type=int)
    parser.add_argument("--neighbor-radius", type=int, choices=range(0, 4), default=1)
    parser.add_argument("--neighbor-color-tolerance", type=int, choices=range(0, 256), default=0)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    with Image.open(args.reference) as reference, Image.open(args.candidate) as candidate:
        report, difference = compare(reference, candidate, args.crop, args.neighbor_radius, args.neighbor_color_tolerance)
    report["inputs"] = [{"path": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                        for path in (args.reference, args.candidate)]
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    difference.save(args.output.with_suffix(".png"))
    print(f"{report['exactMatchFraction']:.4%} exact pixels; {report['remainingChangedPixels']} changed pixels without a nearby reference color. Report: {args.output}")
