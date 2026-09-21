# WeatherLinkLiveLibrary 2.0.0

Weather readings now come from typed System.Text.Json response models. Newtonsoft.Json and log4net are no longer library dependencies. Existing constructors, weather properties, unit conversion and asynchronous methods remain available on net472 and .NET 10.

This major release changes two integration contracts:

- Catch System.IO.InvalidDataException for malformed device responses instead of Newtonsoft.Json.JsonException. Failed refreshes preserve the previous valid snapshot and its age.
- Logging is disabled by default. Pass an ILogger to the new constructor overloads to use the host application's chosen provider. Routine refresh messages use Debug, failures use Warning, and raw sensor responses are omitted.

The console example uses Microsoft.Extensions.Logging.Console; the library requires only its logging abstractions. Rebuild and deploy the full updated dependency set, including System.Text.Json dependencies for .NET Framework applications.

See [migration instructions](https://github.com/oznetmaster/WeatherLinkLiveLibrary/blob/master/docs/migration-v2.md) and the [changelog](https://github.com/oznetmaster/WeatherLinkLiveLibrary/blob/master/CHANGELOG.md).