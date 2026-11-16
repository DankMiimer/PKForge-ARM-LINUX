#!/usr/bin/env python3
"""Exercise the real shell launchers in temporary fake firmware installations."""
import json
import os
from pathlib import Path
import shutil
import signal
import subprocess
import tempfile
import time
import unittest

ROOT = Path(__file__).resolve().parents[2]


class Launchers(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="pkforge launcher ")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.ports = self.root / "card two" / "ports with spaces"
        self.app = self.ports / "pkforge" / "app"
        self.app.mkdir(parents=True)
        for name in ("PKForge.sh", "PKForge Pokepark.sh"):
            shutil.copyfile(ROOT / "packaging/linux" / name, self.ports / name)
            (self.ports / name).chmod(0o755)
        shutil.copyfile(ROOT / "packaging/linux/launch.sh", self.app / "launch.sh")
        self.capture = self.root / "capture.json"
        (self.app / "pkforge").write_text("""#!/usr/bin/env python3
import json, os, sys
if '--help' in sys.argv:
    sys.exit(int(os.environ.get('FAIL_PREFLIGHT', '0')))
if os.environ.get('STUB_HANG'):
    import signal, time
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
with open(os.environ['CAPTURE_FILE'], 'w') as f:
    panel = os.environ.get('PANEL_POWER')
    note = os.environ.get('PKFORGE_BANANA_PANEL_NOTE')
    json.dump({'args': sys.argv[1:], 'mapping': os.environ.get('SDL_GAMECONTROLLERCONFIG', ''), 'video': os.environ.get('SDL_VIDEODRIVER', ''), 'pid': os.getpid(),
               'panel': open(panel).read().strip() if panel else None, 'note': os.path.exists(note) if note else None,
               'malloc': [os.environ.get('MALLOC_ARENA_MAX'), os.environ.get('MALLOC_MMAP_THRESHOLD_')]}, f)
if os.environ.get('LIFECYCLE'):
    with open(os.environ['LIFECYCLE'], 'a') as f: f.write('app\\n')
if os.environ.get('STUB_HANG'):
    while True: time.sleep(1)
sys.exit(int(os.environ.get('APP_EXIT', '0')))
""")
        self.bin = self.root / "mock bin"
        self.bin.mkdir()
        self.mock("uname", "printf '%s\\n' aarch64")
        self.mock("getconf", "case $1 in LONG_BIT) echo 64;; *) echo 'glibc 2.33';; esac")
        self.env = {k: v for k, v in os.environ.items() if not k.startswith(("PKFORGE_", "SDL_", "WAYLAND", "DEVICE_", "SWAY", "CFW_", "MALLOC_"))}
        self.env.update(PATH=str(self.bin) + os.pathsep + os.environ["PATH"], HOME=str(self.root / "home"), CAPTURE_FILE=str(self.capture), XDG_RUNTIME_DIR=str(self.root), APP_EXIT="0")

    def mock(self, name, body):
        path = self.bin / name
        path.write_text("#!/bin/sh\n" + body + "\n")
        path.chmod(0o755)

    def run_launcher(self, name="PKForge.sh", args=(), expected=0):
        result = subprocess.run(["bash", str(self.ports / name), *args], env=self.env, cwd="/", capture_output=True, text=True, timeout=15)
        self.assertEqual(expected, result.returncode, result.stdout + result.stderr)
        return json.loads(self.capture.read_text()) if self.capture.exists() else None

    def test_sibling_install_spaces_and_no_portmaster(self):
        result = self.run_launcher(args=("--save", "save with spaces.sav"))
        self.assertEqual(["--data-dir", str(self.ports / "pkforge/userdata"), "--display-mode", "auto", "--save", "save with spaces.sav"], result["args"])

    def test_split_muos_paths_and_second_card_override(self):
        self.env["PKFORGE_HOME"] = str(self.ports / "pkforge")
        separated = self.root / "ROMS/Ports"
        separated.mkdir(parents=True)
        shutil.copyfile(self.ports / "PKForge.sh", separated / "PKForge.sh")
        self.ports = separated
        self.run_launcher()

    def test_launcher_leaves_a_log_beside_itself(self):
        self.env["APP_EXIT"] = "3"
        self.run_launcher(expected=3)
        log = (self.ports / "PKForge-launcher.log").read_text()
        self.assertIn("Application: " + str(self.ports / "pkforge"), log)
        self.assertIn("PortMaster: not found", log)
        self.assertIn("Ended with status 3", log)

    def test_missing_application_folder_is_logged_with_the_places_searched(self):
        shutil.move(self.ports / "pkforge", self.root / "elsewhere")
        self.run_launcher(expected=1)
        log = (self.ports / "PKForge-launcher.log").read_text()
        self.assertIn("The pkforge folder was not found. Looked in:", log)
        self.assertIn(str(self.ports / "pkforge"), log)
        self.assertIn("/mnt/mmc/ports/pkforge", log)

    def test_portmaster_card_directory_finds_a_split_install(self):
        # muOS: launchers in ROMS/Ports, the app in ports/ on the card PortMaster chose.
        card = self.root / "mnt" / "sdcard"
        (card / "ports").mkdir(parents=True)
        shutil.move(self.ports / "pkforge", card / "ports" / "pkforge")
        separated = self.root / "ROMS" / "Ports"
        separated.mkdir(parents=True)
        shutil.copyfile(self.ports / "PKForge.sh", separated / "PKForge.sh")
        self.ports = separated
        pm = self.root / "PortMaster"
        pm.mkdir()
        (pm / "control.txt").write_text(f"CFW_NAME=muOS\ndirectory='{str(card)[1:]}'\nget_controls() {{ sdl_controllerconfig=firmware; }}\n")
        self.env.update(controlfolder=str(pm))
        result = self.run_launcher()
        self.assertEqual(["--data-dir", str(card / "ports/pkforge/userdata"), "--display-mode", "auto"], result["args"])
        self.assertIn("firmware: muOS", (separated / "PKForge-launcher.log").read_text())

    def test_direct_park_entry_and_exit_status(self):
        self.env["APP_EXIT"] = "7"
        result = self.run_launcher("PKForge Pokepark.sh", expected=7)
        self.assertEqual(["--start-page", "park"], result["args"][-2:])

    def test_darkos_preserves_override_without_global_graphics_changes(self):
        pm = self.root / "PortMaster"
        pm.mkdir()
        (pm / "control.txt").write_text("CFW_NAME=dArkOS\nget_controls() { sdl_controllerconfig=firmware; }\n")
        marker = self.root / "system-mod-ran"
        (pm / "mod_dArkOS.txt").write_text(f"touch '{marker}'\n")
        self.env.update(controlfolder=str(pm), SDL_GAMECONTROLLERCONFIG="user override")
        result = self.run_launcher()
        self.assertEqual("firmware\nuser override", result["mapping"])
        self.assertFalse(marker.exists())

    def test_firmware_module_owns_helper_and_finish_with_exit_status_preserved(self):
        pm = self.root / "PortMaster"
        pm.mkdir()
        lifecycle = self.root / "lifecycle"
        (pm / "control.txt").write_text('get_controls() { printf "controls\\n" >> "$LIFECYCLE"; sdl_controllerconfig=firmware; }\n')
        for firmware in ("ROCKNIX", "KNULLI", "muOS", "ArkOS", "AmberELEC", "SpruceOS"):
            with self.subTest(firmware=firmware):
                lifecycle.unlink(missing_ok=True)
                (pm / f"mod_{firmware}.txt").write_text('''[ "$PORT_32BIT" = N ] || exit 99
printf 'module\\n' >> "$LIFECYCLE"
pm_platform_helper() { [ "$1" = "$PKFORGE_HOME/app/pkforge" ] || exit 98; printf 'helper\\n' >> "$LIFECYCLE"; }
pm_finish() { printf 'finish\\n' >> "$LIFECYCLE"; }
''')
                self.env.update(controlfolder=str(pm), CFW_NAME=firmware, LIFECYCLE=str(lifecycle), APP_EXIT="7")
                self.run_launcher(expected=7)
                self.assertEqual(["module", "controls", "helper", "app", "finish"], lifecycle.read_text().splitlines())

    def test_portmaster_finish_runs_after_runtime_failure(self):
        pm = self.root / "PortMaster"
        pm.mkdir()
        lifecycle = self.root / "lifecycle"
        (pm / "control.txt").write_text('get_controls() { :; }\npm_finish() { printf "finish\\n" >> "$LIFECYCLE"; }\n')
        self.env.update(controlfolder=str(pm), LIFECYCLE=str(lifecycle), FAIL_PREFLIGHT="8")
        self.run_launcher(expected=1)
        self.assertEqual(["finish"], lifecycle.read_text().splitlines())

    def test_term_of_shared_launcher_bounds_stalled_app_cleanup(self):
        self.env["STUB_HANG"] = "1"
        process = subprocess.Popen(["sh", str(self.app / "launch.sh")], env=self.env)
        child = None
        try:
            # The stub opens the file before it writes its JSON: wait for the whole of it, not
            # just the file (a slow runner once read it empty, in the 0.2.9 release build).
            deadline = time.monotonic() + 5
            while child is None and time.monotonic() < deadline:
                try: child = json.loads(self.capture.read_text())["pid"]
                except (FileNotFoundError, json.JSONDecodeError): time.sleep(.02)
            self.assertIsNotNone(child, "Application did not start")
            process.terminate()
            self.assertEqual(143, process.wait(timeout=6))
            with self.assertRaises(ProcessLookupError):
                os.kill(child, 0)
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=2)
            if child:
                try: os.kill(child, signal.SIGKILL)
                except ProcessLookupError: pass

    def test_nextui_paks_share_data_and_payload(self):
        tools = self.root / "Tools/h700"
        home = tools / "PKForge.pak"
        home.mkdir(parents=True)
        shutil.copytree(self.app.parent, home / "pkforge")
        shared = self.root / ".userdata/shared"
        self.env.update(SHARED_USERDATA_PATH=str(shared), SDCARD_PATH=str(self.root), SYSTEM_PATH=str(self.root / ".system/h700"), IS_NEXT="yes", PLATFORM="h700")
        for entry, name in (("home", "PKForge.pak"), ("park", "PKForge Pokepark.pak")):
            pak = tools / name
            pak.mkdir(exist_ok=True)
            launcher = pak / "launch.sh"
            launcher.write_text((ROOT / "packaging/linux/nextui-launch.sh").read_text().replace("@ENTRY@", entry))
            result = subprocess.run(["sh", str(launcher)], env=self.env, timeout=15)
            self.assertEqual(0, result.returncode)
            record = json.loads(self.capture.read_text())
            self.assertEqual(str(shared / "pkforge"), record["args"][1])
            self.assertEqual(["--start-page", entry], record["args"][-2:])
        self.assertTrue((shared / "pkforge/launcher.log").is_file())
        self.assertFalse((home / "pkforge/userdata").exists())

    def test_old_glibc_refused_with_readable_diagnostic(self):
        self.mock("getconf", "case $1 in LONG_BIT) echo 64;; *) echo 'glibc 2.28';; esac")
        self.run_launcher(expected=1)
        self.assertIn("glibc 2.29 or newer", (self.app.parent / "userdata/launcher.log").read_text())
        self.assertFalse(self.capture.exists())

    def test_32bit_userspace_refused(self):
        self.mock("getconf", "echo 32")
        self.run_launcher(expected=1)
        self.assertFalse(self.capture.exists())

    def test_runtime_failure_recorded_before_application_launch(self):
        self.env["FAIL_PREFLIGHT"] = "8"
        self.run_launcher(expected=1)
        self.assertIn("bundled runtime could not initialize", (self.app.parent / "userdata/launcher.log").read_text())
        self.assertFalse(self.capture.exists())

    def test_explicit_video_backend_preserved(self):
        self.env["SDL_VIDEODRIVER"] = "mali"
        self.assertEqual("mali", self.run_launcher()["video"])

    def test_allocator_settings_have_defaults_and_keep_the_users(self):
        # Two pools and a fixed 256 KiB large-block threshold keep freed native memory from staying resident.
        self.assertEqual(["2", "262144"], self.run_launcher()["malloc"])
        self.env.update(MALLOC_ARENA_MAX="4", MALLOC_MMAP_THRESHOLD_="1048576")
        self.assertEqual(["4", "1048576"], self.run_launcher()["malloc"])

    def banana_dark_panel(self, writable=True):
        """BananaOS before a port: one panel's backlight off (4) and a note naming it."""
        backlight = self.root / "sys class backlight" / "backlight0"
        note = self.root / "run banana" / "panel-off"
        note.parent.mkdir()
        if writable:
            backlight.mkdir(parents=True)
            (backlight / "bl_power").write_text("4\n")
        note.write_text(str(backlight) + "\n")
        self.env.update(PKFORGE_BANANA_PANEL_NOTE=str(note), PANEL_POWER=str(backlight / "bl_power") if writable else "")
        if not writable: del self.env["PANEL_POWER"]
        return backlight, note

    def test_bananaos_dark_panel_is_lit_for_the_run_and_put_back(self):
        backlight, note = self.banana_dark_panel()
        result = self.run_launcher(name="PKForge Pokepark.sh")
        self.assertEqual(["--data-dir", str(self.ports / "pkforge/userdata"), "--display-mode", "auto", "--start-page", "park"], result["args"])
        self.assertEqual("0", result["panel"])          # lit while PKForge ran
        self.assertFalse(result["note"])                # so the firmware does not darken it after a wake
        self.assertEqual("4", (backlight / "bl_power").read_text().strip())
        self.assertEqual(str(backlight), note.read_text().strip())
        self.assertIn("BananaOS: lit", (self.app.parent / "userdata/launcher.log").read_text())

    def test_bananaos_panel_that_cannot_be_lit_means_one_screen(self):
        _, note = self.banana_dark_panel(writable=False)
        result = self.run_launcher()
        self.assertEqual(["--display-mode", "single"], result["args"][-2:])
        self.assertTrue(note.exists())
        self.assertIn("using one screen", (self.app.parent / "userdata/launcher.log").read_text())

    def test_bananaos_explicit_single_screen_leaves_the_panel_dark(self):
        backlight, note = self.banana_dark_panel()
        result = self.run_launcher(args=("--display-mode", "single"))
        self.assertEqual("4", result["panel"])
        self.assertTrue(result["note"])
        self.assertEqual(["--data-dir", str(self.ports / "pkforge/userdata"), "--display-mode", "auto", "--display-mode", "single"], result["args"])

    def test_bananaos_panel_is_put_back_when_the_launcher_is_stopped(self):
        backlight, note = self.banana_dark_panel()
        self.env["STUB_HANG"] = "1"
        process = subprocess.Popen(["sh", str(self.app / "launch.sh")], env=self.env)
        child = None
        try:
            # The stub opens the file before it writes its JSON: wait for the whole of it, not
            # just the file (a slow runner once read it empty, in the 0.2.9 release build).
            deadline = time.monotonic() + 5
            while child is None and time.monotonic() < deadline:
                try: child = json.loads(self.capture.read_text())["pid"]
                except (FileNotFoundError, json.JSONDecodeError): time.sleep(.02)
            self.assertIsNotNone(child, "Application did not start")
            self.assertEqual("0", (backlight / "bl_power").read_text().strip())
            process.terminate()
            self.assertEqual(143, process.wait(timeout=6))
            self.assertEqual("4", (backlight / "bl_power").read_text().strip())
            self.assertTrue(note.exists())
        finally:
            if process.poll() is None:
                process.kill()
                process.wait(timeout=2)
            if child:
                try: os.kill(child, signal.SIGKILL)
                except ProcessLookupError: pass

    def test_card_without_permission_bits_needs_no_chmod(self):
        # exFAT: the mount already presents the file as executable and refuses mode changes.
        (self.app / "pkforge").chmod(0o755)
        self.mock("chmod", "exit 1")
        self.assertIsNotNone(self.run_launcher())


if __name__ == "__main__":
    unittest.main(verbosity=2)
