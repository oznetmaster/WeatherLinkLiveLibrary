# JSON deserialization on Crestron Mono

A reproducible, focused comparison of **System.Text.Json 10.0.12** and **Newtonsoft.Json 13.0.5-beta1** on a Crestron MC4-R, targeting .NET Framework 4.7.2. It measures one synthetic WeatherLink-shaped response, not complete driver performance or general serializer superiority.

## Source and payload

- [Original NUnit fixture and attributed models](RuntimeAssemblyProbe.cs). This is the fixture used for the retained September 21 measurements; the JSON string is embedded directly in the method.
- [Synthetic JSON payload](payload.json), extracted without changing its contents. It contains no device identifiers, credentials or captured household data. The fixture uses its identical embedded string, so no file I/O is timed.
- [Desktop test project](JsonDeserialization.csproj), targeting `net472` and `net10.0` with C# 13.
- [Standalone processor project](Processor/JsonDeserialization.Processor.csproj), targeting `net472` with C# 13 and the public CrestronHomeNUnit processor host.
- [Original per-round Mono output](results/mono-2026-09-21.txt) and [measurement provenance](results/provenance.json).

The fixture's name, `ProcessorRuntimeProbe.AssemblyAvailability.ReportSharedJsonAndLoggingAssemblies`, reflects its original dual purpose: report shared-assembly availability, then perform the comparison. Only the `BENCH` lines are timed measurements.

## Recorded results

Measured on September 21, 2026, at 12:32 UTC. Each round contains 3,000 parses; the order alternates between serializers. Times below are for the whole round.

| Round (zero-based) | System.Text.Json | Newtonsoft.Json |
| --- | ---: | ---: |
| 0 | 324.553 ms | 440.976 ms |
| 1 | 324.821 ms | 442.914 ms |
| 2 | 323.404 ms | 440.126 ms |
| 3 | 324.255 ms | 441.608 ms |
| Mean per parse | 0.108086 ms | 0.147135 ms |
| Allocated bytes per parse, every round | 1,024 | 3,816 |

For this workload, System.Text.Json used approximately **26.54% less elapsed parsing time** and **73.17% fewer allocated bytes**. These are four rounds in one process, not four independent processor boots or a statistically representative population. At typical weather-polling intervals the absolute CPU savings are small.

Environment evidence:

| Component | Recorded value | Observation |
| --- | --- | --- |
| Processor | Crestron MC4-R, hardware revision 2 | Benchmark host; operator console output September 22 |
| Crestron Home program | `4.011.0322` | DevTools discovery, September 16 |
| Mono | `6.12.0.107`, tarball build September 17, 2025 | NUnit runtime diagnostic on the same processor, September 19 |
| CLR | `4.0.30319.42000` | Same September 19 diagnostic |
| OS/kernel | `Unix 4.19.35.5149` | Same September 19 diagnostic |
| Control-engine firmware | `2.8000.00057`, built September 17, 2025 | Operator-provided processor console output, September 22 |
| Target / compiler language | `net472` / C# 13 reproduction projects | Running on processor Mono, not desktop .NET Framework |
| Test framework | NUnit `4.6.1` | Package dependency |

The September 22 operator-provided console output also confirms Mono `6.12.0.107` and Crestron Home/PUF `4.011.0322`. Serial number and network hardware identifier are omitted. These firmware/runtime observations were not captured inside the September 21 timing fixture. Their different dates are retained rather than presenting them as contemporaneous measurements.

## Method and limits

Both serializers populate the same nullable DTO classes, carrying both libraries' property-name attributes. Each result is checked for the two condition records, temperature and pressure. Each serializer receives 200 warmup parses. Four alternating rounds then perform 3,000 synchronous deserializations each, after garbage collection. A checksum consumes the resulting temperature values.

`Stopwatch` measures elapsed time. `GC.GetAllocatedBytesForCurrentThread`, discovered through reflection, measures thread allocations when available; `allocatedBytes=-1` means the counter is unavailable, not zero allocation. The stopwatch and delegates are created outside their measured interval. Garbage collection before each round is outside that round's timing, but collections caused by the workload can occur during it.

System.Text.Json uses `AllowReadingFromString`; Newtonsoft uses its default settings. The payload itself contains numeric tokens. This does not compare every parser option or validate equivalence for all possible JSON inputs. It does not measure serialization, cold startup, network I/O, peak memory, whole-driver CPU use, application latency, or other payload sizes and shapes. No BenchmarkDotNet overhead corrections, confidence intervals or statistical significance claims are made.

The processor package merges private copies of **both** serializers. The availability probe also found a resident Newtonsoft assembly (`13.0.0.0` assembly version); that is a separate observation, not the copy used for these measured calls. System.Text.Json did not resolve through the same shared-assembly lookup. This does not establish a Crestron compatibility guarantee on other firmware.

The Newtonsoft prerelease is pinned because it was the previous dependency being evaluated, not as a recommendation to adopt a beta. On .NET 10 the project uses the runtime-provided System.Text.Json; use the .NET 10.0.12 runtime for comparison with the original desktop check. That desktop check also favored System.Text.Json, but its short timings varied substantially. The table above contains only the processor measurements.

## Run on Windows

Install the .NET 10 SDK; the `net472` run also requires the .NET Framework runtime on Windows. From the repository root:

```powershell
dotnet test benchmarks/JsonDeserialization/JsonDeserialization.csproj -c Release -f net10.0 --logger "trx;LogFileName=json-net10.trx" --results-directory artifacts/json-benchmark
dotnet test benchmarks/JsonDeserialization/JsonDeserialization.csproj -c Release -f net472 --logger "trx;LogFileName=json-net472.trx" --results-directory artifacts/json-benchmark
```

Run targets separately when comparing timings, on an otherwise idle machine. Read the `BENCH` lines in the test output/TRX. A passing test validates the fixture's assertions; it does not require either serializer to win. A Windows `net472` run is **not** the recorded Mono environment.

## Run on a Crestron Home processor

Use a Windows development computer with the public [CrestronHomeNUnit processor package prerequisites](https://github.com/oznetmaster/CrestronHomeNUnit). Follow that project's instructions for PowerShell, .NET Framework reference assemblies and ILRepack. This benchmark restores Crestron DevKit 27.0.24 through that SDK and downloads the complete ManifestUtil 29.0.10 NuGet package as a build tool; it does not rely on the incomplete ManifestUtil folder encountered in one desktop SDK installation. Crestron components remain subject to their own license. No Python, station, OpenWeather account or local weather credentials are needed by this fixture.

The standalone harness was prepared using the public CrestronHomeNUnit `v1.12.1` SDK. Check out that version outside this repository, then set its absolute path:

```powershell
git clone --branch v1.12.1 https://github.com/oznetmaster/CrestronHomeNUnit.git C:/Dev/CrestronHomeNUnit
dotnet build benchmarks/JsonDeserialization/Processor/JsonDeserialization.Processor.csproj -c Release -p:ProcessorTestSdkRoot=C:/Dev/CrestronHomeNUnit -p:DeployAfterBuild=false
```

For a different approved tool installation, use the SDK's `ManifestUtilExe`, `LocalCrestronSdkLibDir` and `IlRepackToolDirectory` overrides. Package output is `benchmarks/JsonDeserialization/Processor/bin/Release/net472/JsonDeserialization.Processor.pkg`. Building does not deploy it.

Install that package on your development processor using the public runner/DevTools setup instructions. It appears under **Utility** as **JSON Deserialization Benchmark**. Connect the Windows NUnit runner, select its one suite and run it once; save the processor `TestResult.xml`, including output. Record your processor model, Home version, firmware and Mono version alongside new results. Remove the temporary instance and uploaded package through the public cleanup tools when finished.

The September 21 measurements came from the identical fixture in an earlier temporary processor host. This newly published standalone harness makes reproduction independent of that private project; its package identity differs. Do not relabel a new run as the original measurement.

## Publication validation

On September 22, the published fixture passed on Windows for both desktop targets. The standalone processor harness compiled, merged and packaged successfully with zero warnings/errors, and its merged NUnit discovery found exactly one test. That new package was not deployed during publication; the retained Mono timings remain the original September 21 run.

## License and acknowledgments

Fixture, synthetic payload and reproduction files: copyright (c) 2026 Neil Colvin, [MIT license](../../LICENSE). NUnit, Newtonsoft.Json and System.Text.Json are separately licensed dependencies; consult their NuGet package licenses. The processor host supplies its own dependency notices. Crestron SDK components retain Crestron's terms. This is an independent developer experiment, not a Crestron-endorsed benchmark.
