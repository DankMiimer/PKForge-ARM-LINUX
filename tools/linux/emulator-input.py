#!/usr/bin/env python3
"""Temporary keyboard for the isolated mGBA acceptance fixture; never targets normal gameplay."""
import argparse
import fcntl
import os
from pathlib import Path
import struct
import time

parser = argparse.ArgumentParser()
parser.add_argument("test_root", type=Path)
parser.add_argument("keys", nargs="+")
args = parser.parse_args()
root = args.test_root.resolve(strict=True)
if "pkforge-full-test" not in root.parts or root.name != "emulator":
    raise SystemExit("Use the isolated pkforge-full-test/emulator directory.")
expected = str(root / "PKForge-Test.gba").encode()
running = []
for entry in Path("/proc").iterdir():
    if not entry.name.isdecimal():
        continue
    try:
        command = (entry / "cmdline").read_bytes().split(b"\0")
    except (OSError, PermissionError):
        continue
    if command and Path(os.fsdecode(command[0])).name == "retroarch":
        running.append(expected in command)
if running != [True]:
    raise SystemExit("Exactly one RetroArch instance must be running the disposable fixture.")

keys = {"enter": 28, "x": 45, "z": 44, "up": 103, "down": 108, "left": 105, "right": 106}
codes = [("wait", min(30, max(0, float(key[5:])))) if key.startswith("wait:") else ("key", keys[key]) for key in args.keys]
with open("/dev/uinput", "wb", buffering=0) as device:
    fcntl.ioctl(device, 0x40045564, 0)  # EV_SYN
    fcntl.ioctl(device, 0x40045564, 1)  # EV_KEY
    for key in set(keys.values()) | {1, 30, 31, 57}:
        fcntl.ioctl(device, 0x40045565, key)
    device.write(struct.pack("<80sHHHHI", b"PKForge disposable emulator test keyboard", 3, 0x1209, 1, 1, 0) + bytes(1024))
    fcntl.ioctl(device, 0x5501)  # UI_DEV_CREATE
    try:
        time.sleep(2)
        for kind, code in codes:
            if kind == "wait":
                time.sleep(code)
                continue
            for value in (1, 0):
                device.write(struct.pack("@llHHi", 0, 0, 1, code, value))
                device.write(struct.pack("@llHHi", 0, 0, 0, 0, 0))
                time.sleep(0.12)
            time.sleep(1)
    finally:
        fcntl.ioctl(device, 0x5502)  # UI_DEV_DESTROY
print("Sent fixture-only emulator keys:", " ".join(args.keys))
