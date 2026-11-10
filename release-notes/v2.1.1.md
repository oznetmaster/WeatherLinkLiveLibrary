# WeatherLinkLiveLibrary 2.1.1

This patch aligns build and documentation checks. It changes no station polling, recovery behavior or public API.

- Debug builds treat ordinary compiler and analyzer warnings as errors. Release builds retain warnings without promoting all of them to errors. Strict XML documentation validation remains independently enforced.

## Validation

- Offline workstation tests passed in Debug and Release on .NET 10 and .NET Framework 4.7.2: WeatherLink net10.0: 135; WeatherLink net472: 135. No failures or skips.
- 57 selected tests passed in the shared 374-case processor run on Mono 6.12.0.107 (32-bit), with no failures or skips. The merged host's resolved dependency graph was tested; these are not eight independent release-package tests.
- Fake transports and credentials exercised ordering, cancellation, shutdown and failure recovery. No new household-device or cloud control was performed. Pairing storage is unchanged.
- Strict XML, formatting and style checks passed. Package and companion-app preparation is verified separately by the shared release pipeline.

See [the changelog](https://github.com/oznetmaster/WeatherLinkLiveLibrary/blob/v2.1.1/CHANGELOG.md) and [validation provenance](https://github.com/oznetmaster/WeatherLinkLiveLibrary/blob/v2.1.1/release-validation/v2.1.1.json).
