# Matched UI reference captures

The reference stays at Android v3.1.1,
`799dfc310c94bb978962dcd31ddf7d39e6d8cba3`. The acceptance ledger is
`PARITY_ACCEPTANCE.json`; captures and shared source do not automatically verify
a surface or close its state, interaction and motion gates.

## Linux components

`tools/UiPreview` renders the production Linux views with a deterministic motion
clock. Pass a synthetic Gen 3 save with two occupied slots to seed a portable
Bank, copied Home saves and all five summary pages. The input save is read-only.

```powershell
dotnet publish tools/UiPreview/UiPreview.csproj -c Release -r linux-x64 --self-contained true -o artifacts/ui-preview-linux
```

Run that executable on Linux (or WSL), passing an output directory and the
synthetic save path. It writes 58 component frames plus ten summary frames at
640×480 and 720×480. It waits for reference wallpaper decoding before selecting
the capture timestamp and checks timer/animation/input teardown. Loader layout
JSON accompanies the loader and picker images, including retained row bounds and
font metrics. Picker fixtures include nature and move previews, type/category/tag rows,
long text, muted entries and empty search results. Linux's Select search hint is
an explicit handheld-input adaptation.

Windows can run the same project for host diagnostics. Its Skia font backend has
different advances and rasterization; use the Linux-native output for Linux
pixel comparisons. Neither headless backend establishes SDL/graphics-driver,
physical input or handheld presentation timing acceptance.

For a short real-event-loop check, run the published Linux `pkforge` with
`SDL_VIDEODRIVER=dummy SDL_AUDIODRIVER=dummy`, `--software --windowed
--display-mode single --demo --start-page park --seconds 8`, a fresh disposable
`--data-dir` and this `--input-script`:

```text
0.5 Start
3 B
4 Start
6 B
```

The log should show two options-menu open/close pairs and a clean exit.
`performance.json` separates `Park UI` from `Park` in `viewFrames`; the aggregate
FPS mixes their different presentation cadences. This driver supplies no evidence
of a handheld's SDL backend, controls or sustained performance.

## Android baseline and component harness

Build and install the unmodified pinned app into a new, disposable emulator
first. Record the APK SHA-256, app version, viewport/density and fixtures. Keep
ordinary navigation captures: a fixture harness does not establish journey or
navigation behavior.

For short-lived states, use a separate diagnostic APK in an isolated checkout of
the same baseline. Copy `AndroidReferenceHarness.cs` to the reference app's
`Platforms/Android/` directory. Inside `MainActivity.OnNewIntent`, after assigning
`Intent`, add only this diagnostic hook:

```csharp
#if DIAGNOSTIC
if (intent?.GetStringExtra("pkforge.reference.command") is { } reference)
    ReferenceHarness.Handle(reference);
#endif
```

Build with `DiagnosticBuild=true`, `RuntimeIdentifier=android-x64`,
`RuntimeIdentifiers=android-x64`, `EmbedAssembliesIntoApk=true` and
`AndroidKeyStore=false`, plus the installed SDK/JDK paths. The baseline's
diagnostic build installs as `org.pkforge.app.debug`, beside the unmodified app.
The harness refuses non-emulator hardware. Never install it on a personal device
or reuse an emulator containing personal saves or accounts.

Use only copied synthetic Bank data. Verify all 98 `Views/` source files against
the baseline (normalize LF/CRLF before comparing content); record both the
unmodified and diagnostic APK hashes, the hook and harness source hashes, and
the installed APK hash reported by `sha256sum` on the path from `pm path`.

Configure a 720×480, 160-dpi main display for single-screen references. For the
dual logical reference, set the main logical viewport to 640×480 and create a
640×480, 160-dpi emulator presentation display. Capture both using the display IDs
reported by `dumpsys SurfaceFlinger --display-id`. Do not stretch an image.

Start the diagnostic app normally, resolve its activity through
`cmd package resolve-activity --brief org.pkforge.app.debug`, then deliver
`--es pkforge.reference.command ROUTE` to that activity with
`am start --activity-single-top`. Routes are `theme`, `overview`, `wallpaper`,
`picker`, `dialogue`, `choice`, `loading` and `about`. The driver waits for launch
insets to settle and writes app-private `reference-layout-ROUTE.json` after the
normal opening/typewriter interval. Text dumps include native line height, baseline,
font padding, glyph advances and first-line metrics to distinguish text measurement from outer
layout differences. Read the dump after it has completed.
Linux layout dumps also report advances with linear/subpixel metrics, native/auto
hinting and embedded-bitmap settings. These are calibration measurements; review
the actual production capture when assessing a text change.

Record emulator key events as app input only. They are not physical handheld
acceptance. Preserve the baseline APK and distinguish harness images in every
manifest; the production Linux packages never contain the harness.

## Comparison reports

With Pillow available, run:

```text
python tools/linux/compare-captures.py REFERENCE.png LINUX.png --output REPORT.json
```

The report records input hashes, exact changed pixels, channel deltas, change
bounds and optional neighboring-color diagnostics. It also writes a difference
image. It never resizes or aligns inputs, and never grants acceptance. A crop
must be justified in the evidence record: comparing an overlay's own region
does not establish that its underlying screen matches. Review matched content,
anchors, loading/error/disabled/long-text states and timestamped motion separately.
