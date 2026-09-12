# Changelog

## [1.0.3] - 2026-09-12

### Added

- NUnit test project in the existing Visual Studio solution, with 127 offline cases for .NET Framework 4.7.2 and .NET 10.
- Three opt-in, read-only WeatherLink Live device tests requiring only an IPv4 address, with private JSON settings and an example file.
- Offline CI and release test gates for both frameworks; live tests are explicitly disabled in CI.
- Testing and lifecycle documentation, a root MIT license file, and an option to skip DocFX generation during binary-only builds.

### Fixed

- Sensor readings are selected by `data_structure_type` instead of fixed response-array positions.
- Metric wind conversion uses the exact 1.609344 km-per-mile factor.
- Refreshes respect the configured minimum interval and serialize concurrent requests.
- Cache age starts at successful response completion. Failed, malformed or API-error responses no longer replace or freshen cached readings.
- Rain collector size refreshes with the sensor data.
- HTTP responses and JSON readers are disposed, and the HTTP timeout covers response-body buffering.
- Disposal prevents further use and an in-flight response cannot restore disposed client state.
- Null device addresses are rejected at construction, and refresh before initialization fails clearly.

Existing public signatures, unit preference properties and unavailable-reading defaults are retained. Refresh timing and error behavior are corrected as described above.

[1.0.3]: https://github.com/oznetmaster/WeatherLinkLiveLibrary/releases/tag/v1.0.3
