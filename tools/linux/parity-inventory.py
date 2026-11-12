#!/usr/bin/env python3
"""Inventory the pinned Android UI; shared source alone never grants acceptance."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[2]
BASELINE = "799dfc310c94bb978962dcd31ddf7d39e6d8cba3"
LEDGER = ROOT / "PARITY_ACCEPTANCE.json"
STATES = ["normal", "focused", "empty", "populated", "loading", "disabled", "error", "confirmation", "long-text"]
ADAPTATIONS = {
    "title-strip": "Keep the reclaimed Linux title-strip space while retaining status/page headings.",
    "platform-input": "Use measured handheld controls, Linux R2+D-pad camera panning, frontend button names, and an app-owned Linux keyboard/file picker.",
    "display-routing": "Route the inspector to the secondary display or the corresponding single-screen surface.",
    "park-corrections": "Retain accepted steady Swellow flight and curled Donphan rolling; deliberate spins remain.",
    "android-services": "Android OS widgets, intents and OS-owned navigation are platform services, not Linux UI surfaces.",
}


def git(*args):
    return subprocess.check_output(["git", "-C", str(ROOT), *args])


def inventory():
    paths = git("ls-tree", "-r", "--name-only", BASELINE, "src/PKForge.App/Views").decode().splitlines()
    rows = []
    for path in paths:
        if not path.endswith(".cs"):
            continue
        content = git("show", f"{BASELINE}:{path}")
        text = content.decode("utf-8-sig")
        stem = Path(path).stem
        kind = "screen" if re.search(r"class \w+[^\n]*:\s*[^\n]*(?:ContentPage|IPadHandler|IPadPagingHandler)", text) else "component"
        effects = sorted(set(re.findall(r"\b(FadeToAsync|ScaleToAsync|TranslateToAsync|CreateTimer|StartTimer|Commit|TouchScroller|Spring|AnimateIn|AnimateOut)\b", text)))
        rows.append({"id": stem, "source": path, "sha256": hashlib.sha256(content).hexdigest(),
                     "kind": kind, "motionSources": effects})
    return rows


def update(rows):
    previous = json.loads(LEDGER.read_text(encoding="utf-8")) if LEDGER.exists() else {}
    old = {row["id"]: row for row in previous.get("surfaces", [])}
    surfaces = []
    for source in rows:
        row = dict(source)
        existing = old.get(row["id"], {})
        row.update({key: existing.get(key, default) for key, default in {
            "linuxRoute": "", "fixture": "", "states": STATES,
            "visualEvidence": [], "interactionEvidence": [], "motionEvidence": [],
            "status": "pending", "notes": "",
        }.items()})
        surfaces.append(row)
    result = {"baseline": {"version": "3.1.1", "commit": BASELINE},
              "viewports": [{"mode": "single", "width": 720, "height": 480},
                            {"mode": "dual", "width": 640, "height": 480}],
              "approvedAdaptations": ADAPTATIONS, "surfaces": surfaces}
    LEDGER.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def check(rows, complete):
    data = json.loads(LEDGER.read_text(encoding="utf-8"))
    if data["baseline"] != {"version": "3.1.1", "commit": BASELINE}:
        raise ValueError("Android reference baseline changed")
    recorded = data["surfaces"]
    keys = ("id", "source", "sha256", "kind", "motionSources")
    if [{key: r[key] for key in keys} for r in recorded] != rows:
        raise ValueError("UI source inventory is stale; update it and review the coverage")
    if data["approvedAdaptations"] != ADAPTATIONS:
        raise ValueError("Linux adaptations differ from the agreed release scope")
    pending = []
    for row in recorded:
        if row["status"] not in ("pending", "verified"):
            raise ValueError(f"Invalid acceptance status for {row['id']}")
        if row["status"] == "verified":
            required = ("linuxRoute", "fixture", "states", "visualEvidence", "interactionEvidence")
            if any(not row.get(key) for key in required) or row["motionSources"] and not row["motionEvidence"]:
                raise ValueError(f"Incomplete verification evidence for {row['id']}")
        else:
            pending.append(row["id"])
    print(f"Pinned Android inventory: {len(recorded)} reference units; {len(recorded)-len(pending)} verified; {len(pending)} pending.")
    if complete and pending:
        print("Stable publication blocked by pending parity acceptance: " + ", ".join(pending), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--update", action="store_true", help="Refresh source inventory while preserving reviewed acceptance records")
    parser.add_argument("--require-complete", action="store_true", help="Refuse stable publication with pending reference units")
    options = parser.parse_args()
    source = inventory()
    if options.update:
        update(source)
    try:
        sys.exit(check(source, options.require_complete))
    except (ValueError, KeyError) as error:
        sys.exit(str(error))
