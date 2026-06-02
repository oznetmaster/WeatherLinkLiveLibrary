# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0
- **Upgrade Goal**: Multi-target the library to `net472;net10.0`, keep C# 13 for the `net472` build, and omit net472 compatibility shims from the `net10.0` build.

## Source Control
- **Source Branch**: master
- **Working Branch**: dotnet-10-upgrade
- **Commit Strategy**: After Each Task

## Key Decisions Log
- Keep `WEATHER_LINK_DATA_REQUEST` as the URL source and avoid inlining the request text in `WeatherLinkLive.cs`.
- Use standard ASCII double quotes in the MIT license text.
- Do not publish personal or localized items such as IP strings, tokens, or machine-specific/local file paths.
