"""Prepare an explicitly named, disposable smoke-test data directory on a handheld."""
import json
from pathlib import Path
import sys
import wave

root = Path(sys.argv[1]).resolve()
if "pkforge-port-test" not in root.parts:
    raise SystemExit("Use a disposable directory below pkforge-port-test.")
root.mkdir(parents=True, exist_ok=True)
track = root / "silent-test.wav"
with wave.open(str(track), "wb") as stream:
    stream.setnchannels(2)
    stream.setsampwidth(2)
    stream.setframerate(44100)
    stream.writeframes(b"\0" * 44100 * 4 * 5)
settings_path = root / "settings.json"
settings = json.loads(settings_path.read_text()) if settings_path.exists() else {}
settings["music_library"] = str(track) + "\x01Silent integration test"
settings["music_autostart"] = True
settings_path.write_text(json.dumps(settings))
(root / "extended-input.txt").write_text("""5 X
10 Start
11 Down
12 A
15 X
18 B
20 Start
21 R
22 R
23 R
24 A
30 B
35 B
37 Select
""")
