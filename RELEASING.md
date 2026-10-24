# Linux release procedure

The `linux` branch is the maintained port; upstream `main` and `v*` tags retain their original meanings. Linux releases use `linux-v<revision>` tags. Upstream behavior remains pinned to v3.1.1 and the submodule commits in `tools/linux/source.ps1`.

1. Commit all intended changes. Use a clean checkout with initialized submodules and the SDK in `global.json`. Update `PortRevision`, the documentation and release notes for a new port revision.
2. Run `dotnet test PKForge.Linux.slnx -c Release`, check Android compilation, then run `pwsh -File tools/linux/build.ps1` and `pwsh -File tools/linux/verify-package.ps1`.
3. Build/test the extracted complete source archive. It must include both submodule source trees and build without a `.git` directory. `SOURCE-MANIFEST.json` detects changed or missing source inputs. NuGet restore still requires network access or a populated package cache.
4. Push the `linux` branch and wait for both Linux and Android compatibility CI jobs. The original Android signing/release workflow is restricted to the original repository; this fork needs no upstream signing secrets.
5. Tag the exact release commit, push only that Linux tag, then manually dispatch the Linux workflow at that tag with `create_draft=true`. Successful checks create a draft prerelease containing the CI-built binary, complete source, checksums and build information.
6. Download the draft assets, verify every checksum and smoke-test that exact ARM64 ZIP on both reference handhelds using isolated test userdata. Preserve firmware settings and personal data. Record any unavailable hardware as untested; do not claim it passed.
7. Confirm tag/`BUILDINFO.json` provenance and the public notes, then publish the draft as a prerelease. Verify anonymous release downloads and checksums. Never replace assets behind an already published tag; use a new revision for corrections.

The source exporter uses committed trees, excludes Git metadata and includes the exact pinned submodule sources. The binary has Unix executable bits, LF shell launchers, asset hashes, license/attribution files and no userdata. `tools/linux/verify-package.ps1` verifies the release set. Generated build outputs and private device evidence remain ignored by Git.
