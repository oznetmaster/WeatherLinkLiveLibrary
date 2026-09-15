# WeatherLink Live™ Library

WeatherLink Live™ Library queries the current conditions from a local WeatherLink Live device. It supports .NET Framework 4.7.2 and .NET 10, with compatibility support isolated to the .NET Framework build.

- [Usage](#usage)
- [Automated tests](#automated-tests)
- [Live device tests](#live-device-tests)
- [Console test harness](#console-test-harness)
- [Build and release](#build-and-release)
- [Documentation and license](#documentation-and-license)

## Usage

Construct `WeatherLinkLive.WeatherLinkLiveAPI.WeatherLinkLive` with the device's IPv4 address, then await `InitializeAsync()` before reading any properties. No cloud account or API key is required. The client requests the local `/v1/current_conditions` endpoint.

Call `RefreshAsync()` periodically. The minimum refresh interval defaults to 30 seconds and must be at least 10 seconds. Calls within that interval reuse the last successful response. The maximum permitted cache age defaults to 60 seconds; reading older data throws `InvalidOperationException`. Choose a maximum age at least as long as the minimum refresh interval.

Concurrent refreshes are serialized. Only a successful response updates the cached readings and their age; failures propagate to the caller and leave the previous snapshot intact. Cancellation is supported. Dispose the client when finished; further reading, initialization and refresh operations throw `ObjectDisposedException`.

Readings cover temperature and calculated temperature indices, humidity, wind speed and direction, rainfall, and sea-level pressure and trend. The first ISS record (`data_structure_type: 1`) supplies outdoor readings, and the barometer record (`data_structure_type: 3`) supplies pressure, regardless of their positions in the response. The API does not currently select between multiple ISS transmitters.

Temperatures default to Fahrenheit; `CelciusTemperature` selects Celsius (the existing property spelling is retained). Wind defaults to mph, with `MetricWind` selecting km/h. Rain defaults to inches, with `MetricRain` selecting millimetres. `MetricBarometer` selects **millimetres of mercury**, not hPa. These preferences convert cached readings without requesting the device again.

The existing non-nullable numeric API returns zero for unavailable readings, so zero alone cannot distinguish missing sensor data from a measured zero. Missing pressure trend returns `Unknown`.

## Automated tests

Open `WeatherLink Live Library.sln` in Visual Studio and use Test Explorer. `WeatherLinkLive.Tests` uses NUnit and its Visual Studio adapter, targeting `net472` and `net10.0`. The test project is not a NuGet package.

The 127 offline cases cover every reading property, unit conversions, rain collector sizes, compass bearings, pressure trends, missing data, culture independence, HTTP and JSON failures, initialization, polling and stale-data timing, cancellation, concurrent requests, disposal, sensor ordering and live-setting validation. The HTTP handler and clock are controlled by the tests; offline cases never contact a weather station.

Run both frameworks on Windows:

```powershell
dotnet test .\WeatherLinkLive.Tests\WeatherLinkLive.Tests.csproj --settings .\WeatherLinkLive.Tests\offline.runsettings --filter "TestCategory!=Live"
```

Use `-f net472` or `-f net10.0` to run one target. CI runs the offline suite for both targets, and the release workflow requires it to pass before publishing.

## Live device tests

Three read-only tests check current readings, cached metric conversions and a fresh response after a ten-second polling interval. They do not modify the station or assume the weather will change. The test computer must be able to reach the device's local HTTP endpoint.

1. Copy `WeatherLinkLive.Tests/LiveTestSettings.example.json` to `WeatherLinkLive.Tests/LiveTestSettings.json`.
2. Replace the documentation-only example address with your device's IPv4 address. No credentials are needed.
3. Add `/WeatherLinkLive.Tests/LiveTestSettings.json` to your checkout's **`.git/info/exclude`** before saving personal settings. Keep that file local.

The private JSON contains `enabled` (default `false`) and `ipAddress`. Live tests are skipped unless enabled by that file or the NUnit `EnableLiveTests=true` parameter. `EnableLiveTests=false` always disables them. The fixtures are categorized `Live`; they do not use NUnit's `Explicit` attribute.

For Visual Studio, select `WeatherLinkLive.Tests/live.runsettings` through **Test > Configure Run Settings > Select Solution Wide runsettings File**, then select the `LiveDeviceTests` fixture in Test Explorer. Switch to `offline.runsettings` to force live tests off.

From a terminal, run one framework at a time:

```powershell
dotnet test .\WeatherLinkLive.Tests\WeatherLinkLive.Tests.csproj -f net472 --settings .\WeatherLinkLive.Tests\live.runsettings --filter "TestCategory=Live"
dotnet test .\WeatherLinkLive.Tests\WeatherLinkLive.Tests.csproj -f net10.0 --settings .\WeatherLinkLive.Tests\live.runsettings --filter "TestCategory=Live"
```

Settings are found by walking from the test output directory back towards the solution, which finds the private file beside the test project. A fallback is `%LOCALAPPDATA%/WeatherLinkLive/LiveTestSettings.json`. For an external runner, NUnit's `TestDataDirectory` parameter identifies the folder containing `LiveTestSettings.json`; when provided, that location is authoritative. An explicitly enabled run with missing or invalid settings fails clearly.

The private file is never copied to build or publish output and is never packed. Only the example is included in source control. The console's saved IP may be copied into this private JSON when setting up the tests; the fixtures do not depend on the console project.

## Console test harness

The existing `WeattherLinkLive Test` project is a console program for manual smoke testing. Its spelling is retained in the solution.

Provide the device IP using `WEATHERLINK_LIVE_IP`, or `.local/weatherlink-live-ip.txt` in the solution root. Exclude `.local/` through your checkout's `.git/info/exclude` file. These inputs are separate from the NUnit JSON settings.

## Build and release

```powershell
dotnet build ".\WeatherLink Live Library.sln" -c Release
```

Release builds can generate DocFX documentation. Pass `-p:GenerateApiDocumentation=false` when only building or testing binaries. The GitHub `release-nuget.yml` workflow builds and tests both frameworks, publishes the library package and creates a GitHub release from version tags. Live tests are disabled in CI.

See [CHANGELOG.md](CHANGELOG.md) for changes and [GitHub releases](https://github.com/oznetmaster/WeatherLinkLiveLibrary/releases) for released versions.

## Documentation and license

- [Published API documentation](https://oznetmaster.github.io/WeatherLinkLiveLibrary/)
- [Davis WeatherLink Live local API specification](https://github.com/weatherlink/weatherlink-live-local-api/blob/master/API.md)
- [MIT license](LICENSE)

The automated test project uses [NUnit](https://github.com/nunit/nunit), licensed under the MIT license. NUnit is a test dependency and is not required by users of the library.

This independent client library is not affiliated with or endorsed by Davis Instruments. WeatherLink Live is a trademark of Davis Instruments.

## Publishing when local hardware is unavailable

The publish/release workflows support an explicit manual override when the processor or local self-hosted GitHub Actions runner is unavailable. Select `skip_hardware_checks` and provide a single-line `hardware_skip_reason`. Use the workflow's normal source and version controls. The override applies only to that invocation and is recorded with the exact source revision in its warning and job summary; it does not create a passing hardware-test result.

GitHub-hosted validation remains mandatory for the checked-out source, and the normal build, tests and packaging steps still run. Wait for the configured hosted workflows to pass, or run them on the same source revision first. None of these hosted checks needs the local runner or processor. Automatic tag/release-triggered runs retain the normal hardware checks; use a manual invocation of the updated release workflow when an offline override is needed.
