# Development and validation history

See the [product changelog](CHANGELOG.md) for shipped changes. This document preserves test, CI and build history. Dated development entries describe work at that time, not a published product version or completed acceptance. Version headings identify the release alongside which development work was recorded.

## Where changes belong

- Product changelog and product release notes: shipped behavior, API, compatibility, fixes and runtime dependencies. Mention validation briefly when it helps explain a fix.
- This history: test coverage, CI, build tooling, work on pending versions. Split mixed entries so the product effect remains easy to find.
- Testing and workflow guides: current setup and operating instructions.
- Test-only or documentation-only changes do not require a product release.

<!-- development-history -->

## 2.0.0 - 2026-09-21

- Migrate test fixtures to System.Text.Json, preserving the existing offline and opt-in live coverage.
- Add regression checks for optional and numeric-string readings, unknown fields, invalid-reading snapshot preservation and host-provided logging without raw response dumps.
- Build the console consumer alongside the library in CI. Private settings remain excluded from source, build output and packages.

## Offline release workflow option - 2026-09-15 (no package release)

- Allow an explicit manual release when local hardware or the self-hosted runner is unavailable, with the reason and exact source recorded in the workflow summary.
- Keep hosted source validation mandatory and preserve all build, test and packaging steps. No runtime, API or package-version changes.

## CI validation - 2026-09-15 (no package release)

- Revalidate the current default-branch source after successful release workflows, including version commits created by GitHub Actions.
- Allow maintainers to configure exact-source, App-specific checks that must pass before publishing through `RELEASE_REQUIRED_CHECKS`; missing, failed or unconfirmed checks block the release.

## [1.0.3] - 2026-09-12

- NUnit test project in the existing Visual Studio solution, with 127 offline cases for .NET Framework 4.7.2 and .NET 10.

- Three opt-in, read-only WeatherLink Live device tests requiring only an IPv4 address, with private JSON settings and an example file.

- Offline CI and release test gates for both frameworks; live tests are explicitly disabled in CI.

- Testing and lifecycle documentation, a root MIT license file, and an option to skip DocFX generation during binary-only builds.

[1.0.3]: https://github.com/oznetmaster/WeatherLinkLiveLibrary/releases/tag/v1.0.3
