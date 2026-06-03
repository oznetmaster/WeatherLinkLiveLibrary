# Getting Started

The published API site is generated reference documentation for the `WeatherLinkLive` library. The most important entry points are:

- [WeatherLinkLiveAPI](../api/WeatherLinkLive.WeatherLinkLiveAPI.yml)
- [WeatherLinkLive](../api/WeatherLinkLive.WeatherLinkLiveAPI.WeatherLinkLive.yml)

## Typical Flow

1. Build the solution.
2. Run the release docs generation target.
3. Open the generated `docs/index.html` site.
4. Use the API reference and conceptual pages to browse the library.

## Example

```csharp
using WeatherLinkLive;

var client = new WeatherLinkLiveAPI.WeatherLinkLive("192.168.1.100");
await client.InitializeAsync();
```

## Next Pages

- [Overview](overview.md)
- [API Reference](../api/WeatherLinkLive.WeatherLinkLiveAPI.yml)
