# Migrating to WeatherLinkLiveLibrary 2.0

The package name, namespace, original constructors, asynchronous methods and weather-reading properties remain available. Update the package reference and rebuild consumers. .NET Framework 4.7.2 and .NET 10 remain supported.

This is a major release because the logging integration and malformed-response exception contract change. Applications that only read weather normally need no source changes.

## Typed responses

The library now uses attribute-mapped System.Text.Json response models internally. Sensor fields are optional and nullable; unknown fields are ignored. Existing unavailable-reading defaults and unit conversions are preserved. The library continues to select sensors by their type rather than their position in the response.

Malformed JSON or invalid sensor records now raise `System.IO.InvalidDataException`, also used for device API errors. Replace catches of `Newtonsoft.Json.JsonException` with `InvalidDataException`. HTTP failures and cancellation retain their existing exception categories. A rejected response does not replace or freshen the previous valid snapshot.

No Newtonsoft.Json types were exposed by the public weather API, so there are no JSON constructors or DOM objects to replace. Rebuild and redeploy the complete dependency set, especially for merged .NET Framework applications. Modern .NET uses its built-in System.Text.Json; net472 receives its package dependencies.

## Optional logging

The library no longer depends on log4net or configures a global logger. Existing constructors use a no-op logger. To receive diagnostics, pass a logger owned by the application:

```csharp
using Microsoft.Extensions.Logging;
using Client = WeatherLinkLive.WeatherLinkLiveAPI.WeatherLinkLive;

// The console provider is an application dependency, not a library requirement.
using ILoggerFactory logs = LoggerFactory.Create(builder =>
    builder.AddSimpleConsole().SetMinimumLevel(LogLevel.Debug));
using var weather = new Client(deviceAddress, logs.CreateLogger<Client>(),
    celciusTemperature: true);
await weather.InitializeAsync();
```

For that example, the application references `Microsoft.Extensions.Logging.Console`. The library references only `Microsoft.Extensions.Logging.Abstractions`; a host may use any compatible provider. Existing log4net settings no longer enable this library's messages. Routine lifecycle and polling messages are Debug, failures are Warning, and raw device responses are not emitted. The caller owns and disposes its logger factory.

The console project in this solution demonstrates this configuration. It still reads the device IP from the environment or a private local file; no credentials or personal addresses are included in the package.
