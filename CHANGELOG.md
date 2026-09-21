# Changelog

This changelog records shipped features, fixes, compatibility and runtime dependency changes. See [development and validation history](DEVELOPMENT-HISTORY.md) for tests, CI, build tooling and work not yet released.

## [2.0.0] - 2026-09-21

### Changed

- Replace Newtonsoft.Json parsing with typed, attribute-mapped System.Text.Json sensor responses. Preserve nullable readings, sensor selection, unit conversions, cache timing and cancellation.
- Report malformed device responses as `InvalidDataException` without requiring callers to depend on a JSON serializer's exception type.
- Replace log4net with optional, host-supplied `ILogger` logging. Existing constructors remain available and default to no logging; new overloads accept the host's logger.
- Move routine refresh diagnostics to Debug and omit raw sensor payloads. Update the console example to use the standard console logging provider.

See the [2.0 migration guide](docs/migration-v2.md) for dependency, exception and logging changes.

## [1.0.3] - 2026-09-12

### Added

- Add a root MIT license file.

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
[2.0.0]: https://github.com/oznetmaster/WeatherLinkLiveLibrary/compare/v1.0.3...v2.0.0
