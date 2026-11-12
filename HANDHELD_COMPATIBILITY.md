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
