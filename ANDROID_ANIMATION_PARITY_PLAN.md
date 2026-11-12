# Android animation and interaction parity plan

Status: implementation in progress. Prepared 2026-10-25. See [implementation progress and evidence](ANDROID_PARITY_PROGRESS.md).

Companion: [UI parity plan, baseline and execution order](ANDROID_UI_PARITY_PLAN.md).

## Target

Reproduce every reachable animation in the pinned Android v3.1.1 application: its triggers, delays, curves, amplitude, speed, layering, interruption rules and completion behavior. Cover UI motion, scrolling, dialogue, loading, route visualizations, special content and Poképark. A matching final frame is insufficient.

Use the UI plan's version/fixture/viewport baseline. The inventory below is a source-backed starting point, not a claim that every effect has been captured or that every listed family needs rebuilding.

## Confirmed implementation gaps

- Linux [View](src/PKForge.Linux/Ui/Views.cs) implements `FadeToAsync` by immediately assigning the destination and returning a completed task; `CancelAnimations` is empty. Parent opacity also does not composite the entire subtree.
- The Linux build links several original park views, but does not link the general Android [Motion](src/PKForge.App/Services/Motion.cs), `CarryHand`, Bank overview, summary or general menu implementations. Many non-park screens are separate Linux presentations.
- Linux [Program](src/PKForge.Linux/Program.cs) caps the primary render loop at 30 FPS. The lower screen updates around 30 FPS when active/menu-driven, otherwise every 200 ms. Original code includes 16 ms timers and refresh-paced canvas animations; actual Android presentation cadence still needs measurement.
- [Linux input repeat](src/PKForge.Linux/Native/InputRouter.cs) uses a 400 ms delay and 115 ms repeat for navigation. [Android MainActivity](src/PKForge.App/Platforms/Android/MainActivity.cs) uses 320/80 ms for directions and 380/140 ms for shoulders. This affects perceived responsiveness even when drawing matches.
- Existing [park animation tests](tests/PKForge.Park.Tests/ParkAnimationTests.cs) and the [real-asset device check](tools/linux/DeviceTests/ParkAnimationCheck.cs) validate particular pose/fallback cases. They do not establish whole-app animation parity.

## M0 — Inventory, record and specify all effects

Audit Android views, services, platform navigation and Chrome/Domain animation consumers. Search beyond animation APIs: timers, stopwatches, sine/pulse functions, sprite frame selection, delayed callbacks, scrolling and audio-position-driven rendering can all create motion. Include effects reached through uncommon settings and supported save formats.

Give every effect an ID with: source method, route/trigger, fixture, start state, animated properties, timing/curve, loop/one-shot rule, render layers, cancel/reentry behavior, Android recording, Linux implementation and verification status. Map each to the UI screen/state ledger. Record static transitions as static; do not add decorative motion where the original has none.

| Effect family | Original source to preserve or extract | Planned treatment |
| --- | --- | --- |
| Tile focus and long-label marquee | `DsKit.cs` | Match focus scale, selection reversal, delay, linear glide, pauses and reset when focus leaves. |
| Floating windows, menus and dialogue choices | `Kit.AnimateIn`, `PadMenu`, `DialogueChoiceBox` | Match combined opacity/scale entrance and answer reveal. Determine actual close/navigation behavior from the reference. |
| Dialogue and notices | `DialogueBox`, `DialogueChoiceBox`, `PokeparkCardOverlay`, `PokeparkPage` | Preserve character reveal, instant completion on input, more-arrow blink, card rise and status fade. |
| Loading/progress | `Kit.WalkerStrip`, `LoadingOverlay` | Match strolling sprite, bob, wrapping, progress changes and cancellation. Run only during the visible operation. |
| Pokémon carrying | `CarryHand`, `BoxGridRenderer`, `BankPage`, `BoxBrowserPage` | Share pickup hop, spring tracking, landing shadow and settle; preserve cancel and cross-box/party behavior. |
| Party and box management | `PartyView`, `BoxBrowserPage` | Preserve held-mon breath, held-box/header pulse and selection-state timing. |
| Bank box overview | `BankBoxOverview` | Share card appearance/reorder, cursor glide, scroll, lift, bob/rotation and drop behavior. |
| Picker and content scrolling | `BankWallpaperPicker`, `ThemePicker`, `MonSummaryView`, `TouchScroller` | Share spring/tween constants, fling/clamp behavior, thumb hold/fade and rapid-input retargeting. |
| Autopilot routes | `LivingDexRouteMap`, `AutopilotArt` | Preserve launch spacing, arcs, sprite scaling, landing counts and route-change/reset behavior. |
| Park scene and residents | `PokeparkScene`, `ParkPoses`, `PokeparkSpriteService`, Domain park state | Retain shared sprite timing, direction, loop/hold rules, actor movement and camera behavior. Verify every reachable pose. |
| Park environment and events | `ParkSky`, `ParkWater`, `ParkProps`, `ParkEffects`, `ParkEmotes`, `ParkCaptionsPaint`, `ParkEventPaint`, park overlays | Verify weather/water/sky, shadows/glows, particles/emotes/dreams, captions, ball/ripples, social/care effects, highlights/evolution, portraits, dialogue and credits. |
| Special/media and platform transitions | `BadAppleEgg`, `BadApple`, Android navigation/controls | Audit reachability, playback synchronization and actual platform-provided effects; port app-owned behavior and record OS-specific adaptations. |

Deliverable: complete effect ledger plus Android recordings with input-event timestamps. Compare equivalent viewport and assets; record the Android display refresh rate and observed presentation times.

## M1 — Provide shared motion and correct Linux rendering

1. Extract the pure spring/tween/easing calculations into the shared rendering layer, preserving Android constants. Keep `FrameInvalidator`/dispatch platform-specific. Introduce an injectable monotonic clock and explicit elapsed time for deterministic evaluation.
2. Add a Linux animation scheduler advanced on the UI thread. Support property replacement/retargeting, cancellation, disposal/unload, async completion and the completion/cancellation contract required by the original callers. Awaiting a fade must correspond to a real animation.
3. Add group opacity, scale/translation pivots and clipped composition for shared views where needed. `SKCanvasView` children must respect animated opacity/transforms even when cached or direct-drawn. Inverse-transform input so moving/scaling controls remain correctly clickable.
4. Update an animation state once per logical frame, then render that same state on both displays. Avoid advancing shared springs twice because both screens painted. Invalidate cached surfaces only when their contents change; changing an outer transform still needs presentation.
5. Track animation activity per display. A lower-screen fade, dialogue, credits roll or route animation must temporarily lift the idle 200 ms update interval. Return to idle scheduling when the effect settles.
6. Measure Android cadence, then remove the blanket 30 FPS limitation for UI motion where the reference needs a higher presentation rate. Benchmark refresh-paced/60 FPS UI on both handhelds before choosing the device profile. Keep park simulation timing separate from presentation rate. If hardware cannot reproduce the reference cadence, document the remaining fidelity limit; do not call that exact parity.
7. Preserve lifecycle behavior: stop hidden work, cancel disposed overlays, reset frame deltas after suspend, prevent stale callbacks from reviving closed content and keep input responsive during animation. Retain firmware lid/power policy.
8. Match directional and shoulder repeat timing/context to Android. Stop repeat on release, focus loss, disconnect and suspension. Test press/hold sequences against animation retargeting.

Acceptance: deterministic intermediate frames exist for fade/scale/translation/springs, cancellation cannot change a closed view, transformed hit testing agrees with drawing, both displays render the same logical time, and active secondary motion is not throttled to 5 FPS.

## Source constants to preserve

These are implementation specifications from the inspected Android source. Timer intervals describe the source's intent, not measured hardware FPS. Recheck them if the baseline version changes.

| Effect | Source values |
| --- | --- |
| Feature-tile focus | Scale to `1.05` over `130 ms`, `SpringOut`; deselect to `1.0` over `110 ms`, `CubicOut`. |
| Selected-label marquee | Initial `700 ms` wait; duration `clamp(overflow × 45, 1800, 6500) ms`, `Linear`; `900 ms` end pause; reset then `700 ms` wait. |
| Floating-window entrance | Opacity `0 → 1` in `130 ms`, `CubicOut`; scale `0.92 → 1` in `160 ms`, `SpringOut`. |
| General dialogue | One character per `24 ms`; more-arrow opacity toggles `1.0/0.2` every `480 ms`; choices fade in over `100 ms`, `CubicOut`. |
| Summary pad scroll | `180 ms`, `CubicOut`; match viewport-relative target and clamp rules in `MonSummaryView.ScrollBy`. |
| Picker scroll springs | Stiffness/damping `300 / 34.6`. |
| Bank overview springs | Card position `340 / 36.9`, appearance `300 / 26`, cursor `900 / 60`, scroll `260 / 32.2`, lift `520 / 34`; preserve held-card bob/rotation formulas. |
| Carry springs | Position `700 / 52.9`; lift `520 / 30` while held and `520 / 45.6` on landing; preserve lift/shadow geometry. |
| Party/held-box cycles | Party phase period `1.4 s`; held-box header period `900 ms`; preserve source amplitude and selected/held conditions. |
| Touch scrolling | Friction `0.95` per `16 ms`; thumb visible for `900 ms`, fading over `300 ms`. Preserve equivalent behavior if evaluation uses variable dt. |
| Loading walker | Source timer `90 ms`, horizontal advance `2.2` source canvas units per tick, source bob and offscreen wrap. |
| Autopilot traveler | Flight duration `720 ms`; preserve source launch spacing, trajectory and sequence handling. |
| Park notices/cards | Status fade `600 ms` after its source hold; card rise `150 ms`; use the separate park dialogue reveal rules. |
| Pad repeat | Directions: delay `320 ms`, repeat `80 ms`; shoulders: delay `380 ms`, repeat `140 ms`, only in eligible contexts. |

Retain elapsed-time behavior across 30/60 FPS and dropped frames. Share the original spring substeps and stability clamp; changing frame rate must not speed up loops or make scrolling drift. Match intentionally discrete effects such as typewriter ticks and the walker rather than smoothing them into a different animation.

## M2 — Finish common UI motion

Implement and verify tile focus, marquee, overlay entrance, dialogue/choice reveal and blink, status notices, loading walker and progress surfaces alongside U2. Include all callers of shared helpers, not only Home.

For each effect capture start, an intermediate state, peak/overshoot if present, settle and interruption. Test rapid focus changes, repeatedly reopening a modal, confirm during typing, cancellation during load and a long label losing focus. Preserve the original's actual page/tab transition behavior.

Acceptance: common interactions match their reference timelines and remain responsive. No instant fade stubs remain on reachable paths.

## M3 — Finish storage, Bank, scrolling and route motion

- Extract and reuse `CarryHand` state/drawing, adapting only host inputs and render callbacks. Preserve source/landing slot identity so an async save refresh never duplicates, hides or moves the wrong Pokémon.
- Reuse Bank overview springs for cursor/card appearance, lift/reorder/drop, bob and scroll. Add picker scrolling and party/header pulses with the original trigger conditions.
- Match summary pad-scroll, touch drag/fling, selection auto-scroll, edge clamping, scrollbar interaction and thumb fade across all list/gallery consumers.
- Recreate Autopilot traveler launch, flight and landing using its original geometry and sequence semantics. Visual completion must follow workflow state without performing a second transfer.
- Match carry cancel, held-box cancel, mutation failure, adjacent-box changes, overlay interruption and returning from the party deck. Keep the data transaction and recovery rules independent of visual progress.

Acceptance: intermediate geometry and event ordering match Android for the same input script. Navigation during an unsettled spring follows the reference's retarget/cancel rule instead of queueing stale animations.

## M4 — Verify every Poképark animation

Keep the shared scene and effects. First isolate differences caused by timing, rendering cadence, clipping, assets or Linux wrappers before rewriting an existing park animation.

- Cover every `ParkPose` and its reachable gameplay triggers: idle/walk/sleep/wake/eat/sit/lie/nod/hop/pose/look-up/charge/attack/hurt/tumble/trip/deep-breath/float/dance/spin/cringe/sing/pain/pull/fly/roll.
- Check all eight directions, per-frame duration metadata, frame origins, shadows, loop versus hold-last-frame, form/shiny selection and fallback order. Use representative real sheets for different anatomy and animation availability, plus synthetic fixtures for exhaustive mapping.
- Verify travel, flight/rolling/swimming, care/social actions, games, emotes/dreams, environmental effects, event/evolution scenes, dialogue/cards, credits and camera gestures. Test day/weather combinations and relevant occlusion/depth layers.
- Preserve the already accepted Swellow directional flight and Donphan curled Rollout fixes and deliberate spin actions. Keep their existing regression/device cases.
- Repeat with warm/cold/partial/offline caches. A temporarily absent asset may use the documented fallback without changing the timeline or blocking input.
- Verify primary park motion and simultaneous lower-screen menus/journal/credits/status changes. Capture both screens and test physical pause/resume where the firmware supports it.

Acceptance: every reachable park effect has evidence, animation direction/timing/layers match, existing accepted fixes remain correct and no performance regression is hidden by a static screenshot.

## M5 — Timeline, performance and device acceptance

Build on the existing input script, headless capture, Chrome preview and device harnesses. Extend them only where they cannot expose the relevant motion.

| Check | Required evidence |
| --- | --- |
| Deterministic motion | Fixed-clock evaluations at start/mid/end, loop boundaries and interrupt points; compare positions, scale, opacity, sprite frame and callback/event order. |
| Visual motion | Time-aligned Android/Linux recordings, frame strips and difference overlays using identical fixture/seed/camera/weather. Freeze or inject wall-clock/random dependencies only in the diagnostic path. |
| Timing | Finite durations/trigger delays agree within one measured reference presentation interval; periodic effects show no accumulating drift. Deterministic evaluation uses the exact source times. Compare frame cadence separately so a low-rate render cannot pass as smooth parity. |
| Input feel | Timestamped press/release/hold/touch scripts, then physical gamepad and RGDS touch checks for response, repeat cadence, drag/slop, fling, long press, skip and cancel. |
| Interruption/lifecycle | Rapid retargeting, overlay close/reopen, failed operation, focus loss, controller disconnect, screen ownership changes and suspend/resume do not leave stale frames, timers or input ownership. |
| Performance | Report each display's presentation intervals and p95/p99 frame time, dropped frames, input-to-first-visible-response, allocations and memory over repeated journeys. Separate warm park, animated UI, transitions and cold asset loading. |

Use the recorded warm-park gate of at least `29.5 FPS` per measured park-only minute as a regression floor, not proof of matching Android smoothness. Set UI cadence/frame-time budgets from the measured reference and verify them on both devices. Compare idle activity before/after; settled non-looping animations should stop requesting frames even if the host still presents.

Run focused tests as the scheduler/components change, the complete Linux suite before acceptance, and Android regression checks for extracted shared code. Repeat the existing real-asset animation device check. Perform a mixed-workload soak, including the existing park workload and repeated UI/secondary-screen animation; inspect memory trend and retained timers rather than relying only on average FPS.

Done means every M0 effect is implemented/reused and verified, no unexplained animation or input-feel difference remains, and both reference handhelds meet the agreed presentation/performance gates. Any hardware-limited cadence or OS-specific behavior stays visible as an explicit exception. Passing the existing workflow harness alone cannot establish animation parity.
