# WeatherLink Live Library v1.0.3

This patch corrects sensor selection, cached-data handling and metric wind conversion, and adds an automated NUnit suite for .NET Framework 4.7.2 and .NET 10.

## Fixes

- Select outdoor and barometer records by sensor type, independent of response order.
- Use the exact miles-to-kilometres conversion for wind speed.
- Respect the configured minimum refresh interval and serialize concurrent refreshes.
- Update cache age only after a successful response. Failed or malformed responses preserve the previous readings and their age.
- Refresh rain collector size with the readings, dispose HTTP and JSON resources, and reject use after disposal.
- Reject null device addresses and refresh calls made before initialization.

Public signatures and unit preferences remain unchanged. Refresh calls within the minimum interval now reuse cached data as documented. Invalid responses and use after disposal now fail clearly.

## Tests and documentation

- 127 offline tests passed on each target framework.
- Three opt-in, read-only live tests passed on each framework against a local WeatherLink Live device.
- Visual Studio Test Explorer support, example IP-only live settings, and offline CI/release test gates.
- Updated README, changelog and MIT license file.

Private live settings are excluded from source control and build/package output. Live tests are disabled in CI. The library package has no NUnit runtime dependency.

Install `WeatherLinkLiveLibrary` version `1.0.3` from NuGet. See [the changelog](https://github.com/oznetmaster/WeatherLinkLiveLibrary/blob/v1.0.3/CHANGELOG.md) for the full changes.
