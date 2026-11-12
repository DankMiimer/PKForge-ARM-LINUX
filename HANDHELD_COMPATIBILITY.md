# ARM64 handheld compatibility

The standard Ports archive and the NextUI archives contain the same native
ARM64 application. A firmware name alone does not establish compatibility:
the userspace, SDL backend, graphics stack and frontend environment matter.

## Requirements and evidence

- AArch64 Linux userspace, glibc 2.29 or newer, an executable application
  filesystem, firmware SDL2, libgcc/libstdc++ and zlib.
- .NET and app-local ICU are bundled. SDL, libc and graphics drivers are not.
- SDL2_mixer is optional. A missing audio device does not prevent editing.
- GLES is preferred. Raster fallback works only when the firmware's SDL can
  create a raster window; KNULLI's Mali-only backend still requires firmware EGL.
- HTTPS features need a compatible system OpenSSL library. Offline save/Bank
  operations and bundled content do not require a network connection.

| Target | Evidence | Release classification |
| --- | --- | --- |
| RGDS / ROCKNIX 20261001 | Previous ARM64 trials; current device identity rechecked on 12 November 2026 | Final 0.3.0 package acceptance pending |
| RG34XXSP / KNULLI Scarab | Previous candidate ran on hardware | Previously tested; final 0.3.0 validation pending |
| NextUI `tg5040`, `tg5050`, `h700` | Package layout, ABI and simulated launcher checks | Experimental; hardware unverified |
| muOS, dArkOS, ArkOS, AmberELEC, SpruceOS | Requirements and simulated launcher environments only | Experimental; hardware unverified |
| Other ROCKNIX/KNULLI handhelds | Must meet the same requirements | Hardware unverified |
| 32-bit userspace, non-glibc Linux, glibc older than 2.29 | Incompatible with this runtime/package | Unsupported |

Shell tests are separate from installed, frontend-launched, rendered and physically
verified behavior. Package filenames do not claim every device using a firmware
has been tested.

## Firmware assessment — 12 November 2026

These are compatibility assessments of the named upstream sources, not additional
hardware passes. A newer development branch does not identify the firmware on an
installed handheld. Verify the actual AArch64 loader, `getconf GNU_LIBC_VERSION`,
SDL window creation, frontend mappings and writable data location before use.

| Firmware | Assessed combination and evidence | Exclusions / remaining validation |
| --- | --- | --- |
| ROCKNIX | Native AArch64 images. The installed RGDS reports glibc 2.41; the [inspected development recipe](https://github.com/ROCKNIX/distribution/blob/bb80e85f983296aab3d3b714c25487c860f60a68/packages/devel/glibc/package.mk) now specifies 2.44. Both exceed the package floor. | The installed RGDS still needs final-package acceptance. Source versions do not validate other devices or images. |
| KNULLI | The [H700 configuration](https://github.com/knulli-cfw/distribution/blob/ab3a6809286b3cff1648fb8c96a0200bc869beb6/configs/knulli-h700_defconfig) uses glibc; the previously tested RG34XXSP image met the ARM64 requirements. | ARM32 builds are excluded. Final 0.3.0 testing is pending; firmware EGL must remain available even when Skia uses raster rendering. |
| muOS / MustardOS | [Native ARM64 / ARM64_A53 targets](https://github.com/MustardOS/frontend/blob/9c54494454c8a2f1d815c22f33c461d2378d6b38/common.mk) include H700 and A133P. Use the selected image's PortMaster environment. | The source also supports ARM32, x86 and RISC-V targets, which this package cannot run on. The exact image's glibc and SDL backend remain unverified. |
| dArkOS | The [official image description](https://github.com/christianhaitian/dArkOS/wiki) identifies Debian Trixie and mixed 64/32-bit userspace; [Trixie arm64 libc](https://packages.debian.org/trixie/libc6) is 2.41. The native AArch64 path is a plausible ABI match. | Hardware unverified. Do not launch through its 32-bit library path. Retain the narrow app-local graphics-link exception described below. |
| ArkOS | The [archived official description](https://github.com/christianhaitian/arkos/wiki) identifies Ubuntu 19.10 and mixed 64/32-bit userspace. Assess the native AArch64 path on each installed image. | Legacy/community images require an actual libc probe; the historical OS name is insufficient evidence. ARM32-only combinations and libc below 2.29 are excluded. Hardware unverified. |
| AmberELEC | The [inspected glibc recipe](https://github.com/AmberELEC/AmberELEC/blob/108db43ddd6d4e3c6f9d68da8ed03b49ef151aa0/packages/devel/glibc/package.mk) specifies 2.38. Native AArch64 images are ABI candidates. | Check the installed image/profile, firmware SDL/EGL and frontend return. Hardware unverified; a 32-bit compatibility layer does not run this package. |
| SpruceOS | The [Flip profile](https://github.com/spruceUI/spruceOS/blob/dfc2148725348419b1595e518c3bfc02b445975e/spruce/scripts/platform/tina-stock/Flip.cfg) explicitly selects AArch64. Spruce runs over different stock/BaseOS userlands, so validate the selected base's libc and graphics libraries. | The [A30 profile](https://github.com/spruceUI/spruceOS/blob/dfc2148725348419b1595e518c3bfc02b445975e/spruce/scripts/platform/tina-stock/A30.cfg) selects armhf and is excluded. Do not generalize a Flip result to other Spruce devices. Hardware unverified. |
| NextUI | Separate packages match the upstream [tg5040](https://github.com/LoveRetro/NextUI/blob/b6ec1343c0a2636ddaf2502fde52cff237c67377/workspace/tg5040/platform/makefile.env), [tg5050](https://github.com/LoveRetro/NextUI/blob/b6ec1343c0a2636ddaf2502fde52cff237c67377/workspace/tg5050/platform/makefile.env) and [h700](https://github.com/LoveRetro/NextUI/blob/b6ec1343c0a2636ddaf2502fde52cff237c67377/workspace/h700/platform/makefile.env) SDL2/GLES platforms. H700 explicitly selects its own patched SDL instead of TrimUI SDL. | All three remain experimental. Toolchain flags do not prove the installed libc floor or graphics compatibility. Never substitute one platform's SDL/graphics libraries for another's. |

PortMaster's device catalogue can help discover profiles and paths. Its stored
libc versions are observations from particular images and may lag current
firmware; they are not a release-wide compatibility guarantee. No firmware name
overrides this release's ARM64, glibc 2.29+ and firmware-library requirements.

## Firmware integration

Keep the application folder beside both Ports launchers. A split installation can
set `PKFORGE_HOME` explicitly. Existing controller mapping overrides are retained
after PortMaster's generated mappings, including its mapping-file override.

When PortMaster is present, the launcher uses its control setup, CFW module,
platform helper and `pm_finish`. The current dArkOS module is the narrow exception:
it rewrites global `/usr/lib` graphics links. PKForge instead supplies missing
vendor SONAME links in its own userdata directory and leaves the firmware files
untouched. The inspected PortMaster-GUI source is commit
`1b4f1386236fd2319a5493632d5e836a55103e17`.

Standalone launches do not require PortMaster. NextUI uses its own `Tools/<platform>`
wrapper and firmware-provided shared userdata location; its two entry points share
one application and one data directory. They do not source PortMaster modules.

Automatic display selection uses SDL's available displays. A missing or refused
secondary window falls back to one screen. Explicit single-screen mode prevents
ROCKNIX panel activation; temporary panel changes are restored on exit. App-owned
child shutdown is bounded, and PortMaster retains ownership of its cleanup.

## Repeatable checks

Run `python3 tools/linux/test-launchers.py` on Linux, plus
`pwsh -File tools/linux/verify-package.ps1` on a clean release build. The shell
suite covers paths with spaces, split installations, module/helper/finish ordering,
controller overrides, both entry points, shared NextUI data, ABI/runtime refusal,
exit status and termination of an unresponsive test child.

On actual hardware, record the exact firmware, frontend, SDL mapping, installed
archive/hash and data paths. Use disposable fixtures, launch through the frontend,
exercise visible actions, exit and verify frontend restoration. Keep captures and
device identifiers out of public archives unless deliberately reviewed for sharing.
