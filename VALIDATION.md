# ARM64 port validation

## Upstream 3.2.0 integration — 13 November 2026

New development pins v3.2.0. The feature/acceptance matrix is in
[UPSTREAM_3_2_0_SUPPORT.md](UPSTREAM_3_2_0_SUPPORT.md). Earlier test counts,
captures and handheld reports below refer to their recorded commits and
remain historical. None establishes acceptance of the 3.2.0 packages.
The 129-unit reference ledger starts with all parity gates pending.

On the merged source, the host suites pass **3,352 tests** (1,437 Domain,
1,741 Engine, 77 Park, 97 Linux UI) and the 12 launcher scenarios; the Android
project compiles (compile target only, no APK built). No 3.2.0 package has been
built, and nothing in this section was run on a handheld.

## 0.3.0 development work — 12 November 2026

The current Linux suites pass **1,989 tests** (861 Domain, 966 Engine,
71 Park, 91 Linux). The changed Android application compiles with zero warnings
or errors. Twelve real-shell launcher tests cover the PortMaster module/helper/
cleanup lifecycle, dArkOS exception, overrides and bounded child termination.
Sixty dialogue/menu/status/Bank/theme/loading/picker frames plus ten summary pages
exercise original shared views, timer/input cleanup and intermediate animation
states at two viewports. Linux-native raster previews provide the pixel evidence;
Windows font metrics differ and remain host diagnostics only.

Native labels now use NDS12's authored `hdmx` advances and the exact Roboto 3.011
fallback from the reference emulator, with the upstream OFL notice. Measured
15px "Lonely" advances total 52px; the 12.5px mixed pixel/Roboto detail is 17px
high with a 14px baseline. Linux-native component captures verify these metrics;
the complete matched-state and animation ledger remains pending.

Integer virtualized item extents and MAUI's intrinsic shape-stroke geometry now
match the measured row layout. Five settled 720px picker row-label anchors differ
by at most 0.8 physical pixels in position and 0.6 pixels in width. The body crop
is 91.40% exact pixels with 3,593 changed pixels lacking a nearby reference color
at radius 1 / tolerance 8. This remains diagnostic: initial opening centering,
remaining raster differences and the full state/timeline matrix are unresolved.

An exact clean CI archive for merge commit `20c65c255` (branch head `3e68051`)
was installed separately on RGDS / ROCKNIX 20261001. The user confirmed L/R
camera travel, D-pad zoom, L2+D-pad pan and normal behavior after release,
touch on both screens, and lid/resume. Both entry points launched through Ports
and recorded clean exits. All 1,015 existing userdata entries remained unchanged.
The six-minute park trial included interaction and lid testing; its minute
windows are not a controlled performance acceptance or the required soak.

The same archive launched Home on RG34XXSP / KNULLI 20260511, ARM64/glibc 2.40,
through firmware SDL 2.32.8 and GLES. Physical testing exposed swapped A/B and
X/Y in KNULLI's default Ports mapping. An isolated probe using that same SDL
library confirmed that a later AddMapping call returns success without replacing
the higher-priority frontend mapping. A pre-initialization hint applies it.
The app now merges explicit userdata overrides before SDL initialization and
recognizes KNULLI's reversed default relative to the matching frontend GUID/name.
The new app path still needs final-package physical acceptance.
The QA launcher prototype's pre-start mapping passed the user's A/B/X/Y check;
the RG34XXSP park camera, zoom, pan/release, options/back and lid close/reopen
checks also passed. The owner then requested **R2+D-pad** instead of L2+D-pad.
Linux source, hints and regression checks now use R2; the earlier physical
results apply to the old L2 chord. A subsequent exact CI archive, merge commit
`4acf5d2d7` / head `56abc5f`, passed automatic A/B/X/Y mapping without QA overrides
and R2+D-pad pan/release on RG34XXSP; R2 pan/release also passed on RGDS. All four
CI jobs, all binary packages and the full source archive passed verification.
KNULLI Menu+Start failed on that build: Menu was mapped as SDL Guide, which the
app ignored, and no external keyboard hotkey helper was running. The app now
handles the mapped Guide+Start chord and forwarded Alt+F4 on KNULLI, clearing
held state on focus/disconnect reset. The owner verified Menu+Start from both Home and Park in a clean committed local
`73a0405` preview (standard ZIP SHA-256
`129c970d76586f01cef0b3d222f9947767f8387c76a96f4b702e005e36766469`).
Both launches recorded clean exits; all 1,184 installed userdata entries matched
after removing the QA entries. The RGDS R2 trial ran for 810 seconds with all
twelve complete warm park minutes at 29.9-30 FPS, a clean return, and all 1,015
installed userdata entries unchanged. This is still not the 90-minute mixed soak.
One uncontrolled KNULLI camera trial measured 22.82 FPS in its complete warm
minute; a controlled scene/clock comparison remains necessary before making a
performance claim for that image.

Picker checks cover 1,200 entries with bounded retained rows, filtered IDs, empty
searches, drag cancellation, two-tap previews, suspend with an outstanding keyboard
request and nature previews using staged editor values. These checks establish
host behavior and read-only selection, not full matched Android state acceptance.
Information-rich picker tests also preserve game-specific held-item IDs, captured
move facts, legal ordering and filtering, Stellar type IDs and unavailable Hidden
Power spread labels. The shared overloads accept captured facts so a chooser
cannot outlive the temporary Bank session that supplied them.

Single-screen park overlays now present at UI cadence over a cached 30 FPS park
surface. Tests cover irregular frame spacing, cache/direct transitions and surface
recreation. An eight-second Linux SDL dummy/raster run opened and closed options
twice, composed 270 `Park UI` frames and 105 `Park` frames, and exited cleanly with
SDL2_mixer unavailable. Its aggregate 46.96 FPS mixes both cadences and is not a
park benchmark. Final RGDS GPU/input, motion timing and long-session checks remain
required.

All four development ZIPs pass the strengthened channel/provenance, exact checksum
set, embedded version/build information, asset/native ABI and source-layout checks.
They contain 6,901 assets and 19 ARM64 binaries each; complete source contains
15,753 entries. These are explicitly dirty candidate builds, not release assets.

Candidate ZIP SHA-256
`bf6df03c1afe2b6624be18bdcc360d07be92b5583000c16a0f12dddbf5aa8237`
was extracted into a separate RGDS installation and launched through the normal
EmulationStation Ports entry. Logs confirm both 640×480 Wayland/GLES windows,
controller discovery, independent touch-panel discovery and SDL2_mixer initialization.
The 35-second Home run measured 57.57 FPS overall with 8.75 ms mean drawing and
12.67 ms drawing p95, then exited cleanly and returned to the frontend. This is
a short UI measurement, not proof of physical controls. A separate 185-second
night/storm park run measured 29.62 FPS overall and 30 FPS in both measured warm
minutes. Both entry points returned through the frontend. All 1,015 installed
userdata entries matched their original hashes; the temporary Ports entries
were removed and normal volume was restored. These trials used an earlier
candidate, before the newest Bank/loading changes, and do not establish the
90-minute stability or final-byte gates. A first compositor capture
attempt selected the compositor's pre-start environment rather than the frontend's
complete environment and failed; that attempt does not establish screen capture.

The pinned original Android build runs in an isolated 720×480/160-dpi emulator.
A separate diagnostic APK adds a command entry hook and fixture driver; all 98
view-source files match the pinned baseline after LF normalization. It captures
common overlays at 720×480 single and two 640×480 logical displays, with measured
MAUI bounds. Capture metadata distinguishes that harness from the unmodified
reference APK and records the installed APK hash. Neither emulator captures nor
headless Linux previews establish physical handheld acceptance.
Its source-derived inventory has 98 reference units with acceptance still pending.
See `PARITY_ACCEPTANCE.json`, `ANDROID_PARITY_PROGRESS.md` and
`HANDHELD_COMPATIBILITY.md`. Final RGDS physical/parity/data safety acceptance and
the 90-minute mixed soak remain open. KNULLI is previously tested, awaiting the
final version; other firmware packages remain experimental/hardware unverified.

## Historical validation — 25 October 2026

This is an **experimental preview**, pinned to PKForge v3.1.1 at
`799dfc310c94bb978962dcd31ddf7d39e6d8cba3`. Both named handhelds have run the
actual self-contained ARM64 binary. This does not establish compatibility with
every ROCKNIX or KNULLI device, or complete every release acceptance condition.

## Automated checks

### Linux 0.2.3 UI and firmware compatibility

The normal Linux solution command passes **1,953 tests**, none failed or skipped:
861 Domain, 966 Engine, 71 park/application and 55 Linux UI/compatibility cases.
New coverage includes framebuffer sprite scaling, oversized sprite fitting,
L2 trigger hysteresis, either modifier press order, modal/disconnect reset,
display selection, saved opt-out, secondary-window failure and NextUI save-root
discovery. Event-gallery tests now serialize their process-wide PKHeX cache
mutations; a parallel run exposed their previous test isolation race. Android
compilation passes after the shared changes, with no warnings or errors.

All nine real-shell launcher scenarios pass under Linux: spaces, split muOS
paths, second-card override, both entry points/exit status, absent PortMaster,
preserved controller overrides, ignored global firmware mods, NextUI shared data,
old libc/32-bit refusal, runtime failure reporting and explicit SDL backend.
The standard Ports ZIP and three NextUI ZIPs pass asset/native hashes, executable
permissions, LF launchers, ICU configuration, ARM64/glibc ABI and userdata-exclusion
checks. Native requirements are glibc 2.29 and at most GLIBCXX 3.4.22; ICU 72.1.0.3
is bundled with its notices. Complete source includes both pinned submodules.

Home, storage and summary were captured at 640×480, 720×480, 720×720, 1024×768
and 1280×720. Render inspection confirms the removed title strip, retained status
row and pixel-aligned summary sprites. These captures use a disposable fixture.

The actual ARM64 candidate ran on RGDS / ROCKNIX 20261001 from a separate test
installation, using tmpfs logs and disposable userdata. Native Home/storage/summary
captures, SDL dummy/raster with unavailable audio, both graphical launchers,
automatic dual screens, persisted opt-out and swapped screens passed. A fresh
185-second, 24-resident night/storm GLES run measured **30.00 FPS in both complete
warm park-only minutes** (29.60 FPS overall). Warm draw averages were 12.13 and
11.64 ms. Publication pins the upstream build's timestamp-based revision metadata
to source commits so the packaged binaries can be matched to runtime evidence.
The trial's copied save remained byte-identical. The installed application binary
and every existing userdata file were verified unchanged before restoring the
original running application; the userdata recovery snapshot remains separate.

The same packaged binary ran on RG34XXSP / KNULLI Scarab (20260511, ARM64,
glibc 2.40) in a separate test installation. Both launchers, firmware Mali/GLES,
Home/storage/summary captures, raster rendering with unavailable audio, saved
opt-out and forced-dual/swap fallback to its single 720×480 display passed.
Framebuffer captures confirm the reclaimed Home space and crisp summary sprite.
Automatic SDL backend selection also chose Mali without an explicit override.
Deliberately unavailable EGL produced a readable failure and restored the frontend:
this firmware exposes only SDL's Mali backend, which requires EGL even for raster
windows. Working firmware EGL remains required on KNULLI; the RGDS dummy-driver
trial verifies automatic GLES-to-raster fallback where SDL supports raster windows.
The 185-second night/storm park trial measured **30.00 FPS in both complete warm
minutes**, 29.75 FPS overall, with warm draw averages of 21.83 and 21.89 ms.
A separate rollback archive preserves the prior application, launchers and data;
all 35,922 installed files and the disposable save verified byte-identical after
the trial. EmulationStation was restored and verified running.

Physical L2/D-pad and touch acceptance, hotplug/lid behavior and a new long soak
remain owner checks. The short run does not establish memory stability; the
existing long-session gate below remains open. muOS, dArkOS/R36S and NextUI
remain **experimental and untested on hardware**, with separate platform wrappers
and documented dependencies.

Development packages live under `dist/candidate`, explicitly record uncommitted
provenance and require `-AllowCandidate` for verification. They are not published
releases or claims that every named firmware/device has passed acceptance.

### Linux 0.2.2 travel animation fix

The owner reported Swellow and Donphan spinning during travel. The cached Swellow
FlapAround sheet turns through every direction within each animation row; it is
not a steady flight loop. Flight now selects Hover, Float or Idle. Donphan's
Rollout previously used Rotate, an upright turn; a separate rolling pose now
selects its curled Special0 animation, with Tumble/Walk fallbacks for other
rollers. Deliberate dance and spin actions retain their previous animations.
Existing pose enum values are preserved by appending the new rolling pose.

All **1,898 desktop tests pass** (861 Domain, 966 Engine, 71 park/application),
and Android compilation passes. The new cases exercise travelling Donphan,
direction/frame selection, steady flight, intentional spin, species-specific
rolling and missing-animation fallbacks. All **71/71 harness cases pass on both
ARM64 handhelds**, using the packaged Skia native library.

With copies of the actual cached Swellow and Donphan assets, both devices also
select Float and Special0 respectively for all eight directions at 100 timestamps
per direction. The harness exports frame strips for visual inspection under
`pkforge-full-test/movement-v022`; live park preferences and sprite caches are
not modified by these checks. This does not replace a fresh performance soak.

The owner subsequently confirmed that Swellow's flight and Donphan's rolling
animation both look correct in the installed 0.2.2 preview. This closes the
reported visual travel-animation regression.

### Linux 0.2.1 development checkpoint

The desktop suite passes **1,892 tests**: 860 Domain, 966 Engine and 66
park/application cases. Ten new cases cover the shared read-only Gen 3/6 audit
and persistent library reopening, cancellation, identity changes, stale-entry
removal and corrupt/unknown caches. A cancelled scan retains unvisited roots and
new discoveries across a process restart. Cached metadata never bypasses live
save reads or the source-fingerprint write guard.

Android now delegates audit fingerprints and the original report formatter to
the same `AuditWorkflow` used by Linux. Native audits have cancellation, the
complete text report, structured findings and a local text export. The catalogue
uses a new versioned file in `userdata/cache`; existing preferences and save data
are unchanged. All **66/66 cases also pass on both ARM64 handhelds**. A separate
Linux filesystem check passes on both: folder and file symlink aliases collapse
to one canonical save, a directory loop terminates, save states stay excluded,
and cached identity/source bytes survive reopening.

Cold native Home startup restored a scanned fixture catalogue on both handhelds;
rendered screenshots show the cached save and the current name/edition override.
On RGDS the actual native controller router also scanned, opened the copied save,
ran the audit and displayed its full text report through SDL's dummy/raster
driver; source bytes stayed unchanged. KNULLI's SDL has no dummy driver, so the
offscreen menu exercise is RGDS-only. These checks did not replace the installed
preview or claim the physical displays.

### Full-port development checkpoint

The original 1,833-test baseline remains passing. The expanded workflow suite
currently has 56 cases (860 Domain + 966 Engine + 56 park/application cases =
1,882). All 56 workflow cases passed on both ARM64 devices, including the
last-usable-party guards, the staged replacement/index-failure regression,
QR format and typed-import cases. Source presence alone does not count as hardware verification.

The expanded suite covers destination-first recovery before and after each
publication, external edits, exact Bank formats/IDs, box metadata, stale Bank
reads, UI synchronization, game text encoders, party compaction, read-only raw
writes, preview path aliases, isolated staging and failed Bank index writes.
The collection test retains Bank IDs and exact save/party/box locations. Legacy
PKF1 QR tests preserve PK6/PK7/PK8/PB8/PK9/PA9 bytes and recorded formats; equal-size
siblings now require a source-format choice, without changing the v3.1.1 envelope.
Unavailable/full-storage/failed-publication cases are injected IO failures;
the handhelds' storage was not deliberately filled.

Actual SIGKILL testing also passed **16/16 cases on each handheld**. Each fresh
fixture stages two valid saves and a Bank entry/index, kills its process before
or after every one of four publications, then restarts to complete or restore.
Completion verifies exact transferred bytes and stable Bank ID; restoration
compares every original participant byte-for-byte. These cases use
`tools/linux/verify-sigkill.sh` and the separate device harness, never live saves.

The native controller script edited a disposable Gen 3 save's nickname `MON → A`
on both devices, verified every unrelated slot and both backups, and restored
the entire file byte-for-byte. These are native UI router tests, separate from
physical controller/touch acceptance of the expanded UI.

Two fresh 1,865-second night/storm runs with 24 test residents completed park,
summary/editor, Home and Bank transitions and clean exits:

| Device | Overall FPS, including transitions | Warm park-only minute windows | Final RSS |
|---|---|---|---|
| RGDS / ROCKNIX | 28.98 | 16; 29.98–30.00 FPS | 358.4 MiB |
| RG34XXSP / KNULLI | 29.25 | 16; each 30.00 FPS | 283.3 MiB |

The warm park-only windows meet the 29.5 FPS acceptance gate. The overall rates
include menu work and do not meet that threshold. These runs include the latest
library/inspector caching and artwork, with binary module ID
`b3e5a158-2386-4b1f-859b-a395b307f295`. Subsequent summary/collection UI changes and
memory diagnostics are not covered by this soak. Both disposable source copies
remained byte-identical. Full results are in `artifacts/soak-v2-*-performance.json`.

The memory-stability gate remains open: RGDS RSS rose from 339.4 MiB at minute 12
to 359.2 MiB at minute 30; KNULLI rose from 304.0 to 312.0 MiB before exit cleanup.
There was no crash, but these samples do not prove a stable memory plateau. The
next run also records managed heap size, cumulative allocations and GC counts to
separate retained managed objects from native allocations and caches. Older runs
remain in `artifacts/soak-*-performance.json` as historical evidence.

Android compilation passed after the shared mutation and library-workflow
extractions. Binary and UTF-8 accented/Japanese QR round trips passed on the
desktop; the native form harness checks controller entry and named array pickers.
The native form and binary/UTF-8 QR harnesses subsequently passed on both ARM64
devices as well. Physical hand input remains a separate acceptance check.

The offline-data harness on desktop and both ARM64 handhelds placed all nine bundled living dex generations,
extracted all six event archives and listed compatible gifts (Gen 4–9 counts:
581, 709, 834, 891, 533, 209). Gen 3 and Gen 9 Pikachu generation returned legal
results using only bundled data. This harness performs no network requests.

Headless Home startup against isolated copies of the actual preview data retained
all 31 existing RGDS files/9 preference keys and all 638 KNULLI files/11 keys.
Additional Linux catalogue settings/files are permitted; existing Bank, photographs,
cached assets and preferences were compared without printing their contents.

The checkpoint was subsequently installed on both reference devices. Both Home
and direct park launchers entered the intended screen and exited cleanly. The
installer verified existing userdata byte-for-byte before launching; the two
launcher runs retained all resident identities and KNULLI's seven existing
Bank/backup/photo files. RGDS had no existing files in those three categories.
The original application, launchers and compressed userdata remain in a dated
`pkforge/rollback-*` directory on each device. The installed archive SHA-256 is
`fb23f3b5b775f9ca924de7881bc377e5e9c034702d9f1188ec377e5cee03a865`;
binary module ID is `429dd136-eefc-479c-9d05-c19cf7e794c1`.

The owner then confirmed physical Home → Browse Bank → Start/tools navigation
on both devices and lower-screen menu touch on RGDS. This closes that specific
expanded-UI check; it does not certify every editor, keyboard or touch gesture.

mGBA loaded Continue into actual gameplay from the edited disposable Emerald copy
on both devices, with SRAM redirected into the test directory and `load-nosave`
enabled. Screenshots show the same saved player location. Original source hashes
remain unchanged. Party-screen inspection remains separate. The local KNULLI candidates
were empty or unrecognized, so its core test used the edited RGDS test copy.

### Original park preview evidence (historical)

`dotnet test PKForge.Linux.slnx -c Release`: **1,833 passed**, none failed/skipped:
860 Domain, 966 Engine and 7 portable park integration tests.

The seven integration tests also passed on **each physical handheld**, using the
self-contained `tools/linux/DeviceTests` harness. Tests create synthetic Gen 3
saves and isolated settings/Bank directories. They verify scoped release,
preservation of an unrelated slot, backup bytes, re-opening written saves,
out-of-scope refusal with rollback, stale-source refusal, no-op behavior,
Hardcore guards, actual daycare growth through the shared writer and settings
round trips. No personal save was edited during these checks.

The upstream test suite needed two platform portability corrections: portable
temporary paths in HaXAbilityTests and newline normalization in Showdown tests.
No engine behavior was changed to make those tests pass.

## Physical hardware

| Device | Firmware | Display path | Result |
| --- | --- | --- | --- |
| Anbernic RGDS | ROCKNIX 20261001, kernel 7.0.2 | SDL Wayland, Skia GLES, dual 640×480 | Runs; owner confirmed corrected physical layout and touch routing |
| Anbernic RG34XXSP | KNULLI Scarab / Batocera 42, kernel 4.9.170 | SDL mali, Skia GLES or raster, 720×480 | Runs; built-in controller recognized after importing ES mapping |

RGDS physical top is **DSI-2**, bottom is **DSI-1** on this firmware. The park
uses the ordinary window; `[w2] PKForge Controls` places its journal and controls
below. Independent evdev touch streams avoid Sway's duplicate Goodix identifiers.
`--swap-screens` is available for a different connector assignment.

Both devices load the bundled CLR, Skia and native SDL/mixer libraries. Native
dependency inspection found no unresolved dependencies for Skia/System.Native.
Headless rendering and timed GUI exits worked on both devices. RGDS physical
buttons were observed in the SDL log during owner interaction. The owner also
confirmed physical D-pad and A/B menu navigation on the RG34XXSP.

## Measured performance

These are wall-clock window presentation rates, including startup/loading and
menu work, with 24 test residents plus incidental visitors. The renderer is
capped at 30 FPS. Values are measurements, not guarantees.

| Device / run | Duration | Average FPS | Draw mean / p95 | Final RSS |
| --- | --- | --- | --- | --- |
| RGDS GLES, dual, sunny, owner interaction | 180 s | 29.54 | 13.39 / 22.98 ms | 374.7 MiB |
| RGDS GLES, dual, storm, scripted menus | 35 s | 27.30 | 21.53 / 28.92 ms | 262.3 MiB |
| RG34XXSP GLES, storm, scripted menus | 35 s | 28.50 | 27.06 / 33.24 ms | 324.5 MiB |
| RG34XXSP raster, storm | 35 s | 29.19 | 29.25 / 34.33 ms | 271.8 MiB |
| RG34XXSP GLES, night/storm, owner interaction | 150 s | 29.63 | 22.43 / 28.39 ms | 274.5 MiB |

The historical measurements below predate the full-port soak above. The latest
warm park-only windows meet the 29.5 FPS gate; cold startup and UI transitions
remain slower. No overlapping-process benchmark is included above.
`performance.json` records the full result after each clean exit.

## Exercised features and remaining release checks

The original map, animated and shiny residents, weather, camera regions/zoom,
card/options/spectate menus, photo creation and simultaneous RGDS journal updates
ran through the native host. Installed-launcher testing also confirmed photo
export and software pause/resume on RGDS, plus photo export on KNULLI.
SDL2_mixer successfully played a silent WAV on each device. Driver enumeration
selects PulseAudio when provided by SDL (RGDS), and leaves the working ALSA
default on KNULLI, whose SDL has no PulseAudio backend.
Timed controller scripts use the same menu router;
they do not prove physical button mappings or touch gestures.

The owner subsequently confirmed Home → Bank → tools navigation on both installed
handhelds, lower-screen menu touch on RGDS, and correct RGDS lid resume. The
RG34XXSP is configured in KNULLI to shut down when its lid closes, so its
suspend/resume check was not exercised under that configuration. This is not a
confirmed application resume failure. OS lid/power settings remain untouched;
the port observes lid events without taking over the firmware's power policy.

Daycare and Bank/round-trip rules have automated coverage. Full hands-on egg,
release and restore workflows, in-game party inspection after emulator loading,
remaining touch gestures, KNULLI suspend/resume where applicable, low-storage fault
injection, download interruption/offline combinations, broader upgrade preservation,
long-session memory behavior and deterministic screenshot comparison against
Android remain release checks. A software suspend hook alone is not evidence of
a completed physical suspend/resume test.

The portable library intentionally links upstream park handlers and scenes to
reduce behavior drift. This preview is manually installable; it is not a
PortMaster catalogue submission.
