# Upstream 3.2.0 integration

The next Linux preview, `0.2.4-dev`, pins PKForge v3.2.0 at
`c1d12b6d1b0239e57ead0c1e1add1432d24f7e3a`. Its PKHeX and Plugins
submodules are pinned by the upstream tag. Published 0.2.3 Preview remains
the v3.1.1 release; these changes do not replace its tag or assets.

| Upstream change | Linux route / implementation | Acceptance |
|---|---|---|
| Item Bank, conversion, trading, search and generation | Library actions → Item Bank; shared ItemBankPage and services | Host build and integration work; new physical acceptance pending |
| Pocket Pokéwalker, Radar, Dowsing, catches, EXP, HGSS synchronization | Park → Options → Pokéwalker; shared WalkerScreen and rules, Linux mutation service | Uses timed strolling without an Android step sensor; hardware acceptance pending |
| Painted menus and pickers | Original PadMenuPaint and PickerPaint with shared hit regions | Host input, search, touch confirmation and suspend tests; matched captures pending |
| Bank groups and box locks | Bank box management → Box overview; group navigation and durable metadata | New metadata reload, stale-index refusal and journal recovery checks; hardware pending |
| Saves, named snapshots, comparisons, journal, conversion, Hall of Fame museum | Library actions → Saves / snapshots / museum; shared library model and MuseumScreen | Save browser uses Linux menus; original Android page layout remains a parity gate |
| Raids, contest treats, Sinnoh Underground, Arceus research | Save data; capability-specific shared editor workflows | Upstream engine regressions plus host integration; complete format/action acceptance pending |
| Extended trainer statistics and card, Hall of Fame editing | Save data / World and game-specific tools | Capability-gated services through safe writes; Linux forms remain a layout adaptation awaiting review |
| Transfer portals and evolution scenes | Successful transfers and evolution; original painters over the primary storage display | Deterministic clock and bounded overlay lifecycle; timing/capture acceptance pending |
| Animation level, interface scale, Pokémon History, app music volume | Settings | Firmware/system volume is unchanged; history enabled only by user choice |
| Complete data archive | Settings → PKForge data archive | Safety archive before import; Bank changes use the Linux recovery journal; portable preferences validated before writes |
| New games, hacks, emulator formats and art | Shared detection/engine and bundled resources | Engine test evidence is distinct from physical game/save acceptance |
| Android launcher widgets | Unavailable as firmware launcher widgets | Explicit platform adaptation; in-app park and walker remain available |
| Optional two-way PKForge folder mirror | Domain/infrastructure imported; Linux settings route not enabled | Pending a Linux filesystem host and coordinated journal/recovery integration |

Linux remains ARM64, glibc 2.29+, with firmware SDL/graphics libraries and
optional audio. R2 + D-pad pan, printed KNULLI face-button hints, Menu + Start
exit, the reclaimed title strip, dual-display ownership and accepted park
animation corrections are retained. Earlier RGDS/KNULLI input reports are
historical, not acceptance of these new packages. NextUI remains experimental.

`PARITY_ACCEPTANCE.json` now inventories all 129 v3.2.0 reference units, with
every unit pending. The former 98-unit v3.1.1 ledger is retained in
`docs/parity/v3.1.1-baseline/`. Existing v3.1.1 captures keep their original
labels. Stable 0.3.0 remains gated on reference captures, action/data-safety
acceptance, exact-package hardware testing and the required soak.

Archive import merges portable data and does not modify game saves. It creates
`userdata/data-safety/before-import-*.zip` first. Linux paths, music files and
controller/device preferences do not travel to another device. On an interrupted
import, keep the safety archive and resolve any pending Bank recovery before
retrying; imports across all auxiliary stores are not one atomic transaction.
