# WeatherLinkLiveLibrary 2.1.0

This minor release adds automatic recovery after a previously working station stops returning valid readings. Existing public constructors, methods, properties, supported target frameworks and assembly version are retained.

- New Disconnected and Reconnected events fire once per transition. Initial connection does not produce Reconnected.
- Recovery delays are 10, 20, 30, 40, 50, 60 and 120 seconds, then 300 seconds repeatedly, measured after each failed attempt. Successful recovery resets the sequence.
- The failed refresh still throws to its caller and preserves the prior snapshot and its original age. Initial initialization failures still require caller retry. Caller cancellation does not announce an outage; disposal cancels recovery.
- Events run outside client locks. Handler failures are isolated; callers should marshal UI work to their own UI thread.

Automatic background retries are new behavior, even for existing callers that do not subscribe to events. Keep one client per station and dispose it when finished. No new runtime dependency is required. Supported frameworks remain .NET Framework 4.7.2 and .NET 10.

The maintained library tests use NUnit 5.0.0 and NUnit3TestAdapter 6.3.0; 135 offline tests passed on each framework. Test sources ship in the repository, not in the runtime NuGet package. Historical benchmark reproduction projects retain the versions used for those measurements.

See [usage and recovery](https://github.com/oznetmaster/WeatherLinkLiveLibrary/blob/master/README.md#connection-recovery-21) and the [changelog](https://github.com/oznetmaster/WeatherLinkLiveLibrary/blob/master/CHANGELOG.md).