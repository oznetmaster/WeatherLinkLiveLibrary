# WeatherLink Live Library

WeatherLink Live Library is a reusable API client for querying a local WeatherLink Live device.

## Highlights

- Dual-targeted for `net472` and `net10.0`
- Keeps legacy compatibility support isolated to the `net472` build
- Includes DocFX-based documentation support

## Build

```powershell
dotnet build .\WeatherLink Live Library.sln -c Release
```

## Release

Releases are prepared through GitHub Actions:

- `publish-docfx.yml` publishes the documentation site to GitHub Pages
- `release-nuget.yml` builds the release package and creates a GitHub release from tags

## Notes

- No personal, localized, or machine-specific values are published in this repository-facing documentation.