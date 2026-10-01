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

The original processor package was built in **Debug** configuration, confirmed by its retained build identity and build/merge output. The September 22 follow-up also used Debug. This records the harness configuration; it does not mean the precompiled NuGet dependencies were rebuilt as Debug binaries. The two experiments still differ in host process, dependency deployment and execution order.

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

### Newtonsoft.Json versus Newtonsoft.Json.Compact

The measured dependency is the standard **`Newtonsoft.Json` NuGet package, version 13.0.5-beta1**. This benchmark does **not** measure **`Newtonsoft.Json.Compact`**, the separate assembly named in [Crestron's SIMPL# API documentation](https://help.crestron.com/SimplSharp/html/66438ce3-17d5-8de3-92a3-a20697ff0e76.htm). Sharing the `Newtonsoft.Json` namespace does not make those assemblies or their performance interchangeable.

The separate availability probe requested `Newtonsoft.Json` by assembly name and resolved `Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed` at `/simpl/app00/Newtonsoft.Json.dll`. It did not request `Newtonsoft.Json.Compact`, establish Compact's availability, or identify the resident assembly's exact NuGet patch version. The timing results therefore compare the bundled standard Newtonsoft package with bundled System.Text.Json; they do not compare either with Crestron's Compact assembly or the resident Newtonsoft copy.

The Newtonsoft prerelease is pinned because it was the previous dependency being evaluated, not as a recommendation to adopt a beta. On .NET 10 the project uses the runtime-provided System.Text.Json; use the .NET 10.0.12 runtime for comparison with the original desktop check. That desktop check also favored System.Text.Json, but its short timings varied substantially. The table above contains only the processor measurements.

### Crestron's documented resident assemblies

Crestron's [Best Practices: Crestron Home Driver Limitations](https://sdkcon78221.crestron.com/sdk/Crestron_Certified_Drivers_SDK/Content/Topics/Best-Practices/Best-Practices.htm) explicitly lists `Newtonsoft.Json` among the DLLs loaded by Home. The same section warns about DLL conflicts and instructs developers using the listed dependencies to merge their additional DLLs into the driver before packaging. Residency and recommended packaging are therefore separate questions. The list does not identify an exact Newtonsoft product version or guarantee compatibility with every API in a newer package.

The direct-use experiment below establishes what works on this processor and firmware. It is not a recommendation to remove merged dependencies from production drivers. System.Text.Json remains the choice for the libraries being evaluated.

## Follow-up: directly using resident Newtonsoft

On September 22 we ran [one shared fixture](ResidentComparison/ResidentComparison.cs) in two separate processor packages: [resident Newtonsoft](ResidentComparison/Resident/JsonBenchmark.Resident.csproj), with Newtonsoft excluded from the merge, and [bundled System.Text.Json](ResidentComparison/SystemText/JsonBenchmark.SystemText.csproj). The fixture asserts the serializer's actual assembly before timing direct, strongly typed deserialization. Reflection is not used for the measured parse calls. Both variants use the identical synthetic payload and DTO fields from the original comparison, with the corresponding serializer attributes.

The resident test **passed** using `/simpl/app00/Newtonsoft.Json.dll`: assembly version **13.0.0.0**, file version **13.0.2.27524**, product version **13.0.2+4fba53a324c445f06ee08e45a015c346000a7ef2**. Thus assembly version 13.0.0.0 alone was insufficient to identify its patch version. The build uses the same 13.0.5-beta1 compile-time reference as the original fixture, but the measured implementation here is the resident 13.0.2 binary.

A lookup performed **after** timing and memory collection also resolved the separate `Newtonsoft.Json.Compact` assembly, version **4.0.8.0**, token `1099c178b3b54c3b`, at `/simpl/app00/Newtonsoft.Json.Compact.dll`. Both assemblies are present on this firmware. No Compact parsing benchmark was performed. The original September 21 lookup had not requested Compact; this later observation does not change what that original probe established.

| Round (3,000 parses) | Bundled System.Text.Json 10.0.12 | Resident Newtonsoft 13.0.2 |
| --- | ---: | ---: |
| 0 | 324.699 ms | 431.950 ms |
| 1 | 388.026 ms | 430.408 ms |
| 2 | 326.794 ms | 430.696 ms |
| 3 | 326.157 ms | 431.120 ms |
| Mean per parse | 0.113806 ms | 0.143681 ms |
| Allocated bytes per parse, every round | 1,024 | 3,816 |

All four rounds are retained, including the slower System.Text.Json round. Both follow-up packages were built in Debug configuration, targeting `net472` with C# 13. These are sequential runs in different host processes, with no control of other Home activity. They corroborate the allocation difference and faster parsing for this workload, but are not a controlled comparison of resident versus merged assembly overhead. Do not combine these rounds with the September 21 table as one experiment.

Full [resident NUnit result](results/resident-newtonsoft-2026-09-22.xml), [System.Text.Json NUnit result](results/bundled-systemtext-2026-09-22.xml), and [follow-up provenance](results/resident-comparison-provenance.json) retain the output. The follow-up reports process memory too: the first RSS snapshots were 81,736 KiB (about 80 MiB) for resident Newtonsoft and 470,584 KiB (about 460 MiB) for System.Text.Json. The processes already had different managed heaps (about 20 MB and 67 MB, respectively, before the comparison method). RSS/PSS include host activity, mapped assemblies and runtime state. **Subtracting these process snapshots would not measure the serializer's memory cost.** This experiment does not establish the cause of the large gap. Allocation per parse, package bytes and total resident memory are distinct measures; lower parse allocations do not establish a smaller whole-driver working set.

To reproduce the two follow-up packages, use the same prerequisites and SDK checkout described below:

```powershell
dotnet build benchmarks/JsonDeserialization/ResidentComparison/Resident/JsonBenchmark.Resident.csproj -c Release -p:ProcessorTestSdkRoot=C:/Dev/CrestronHomeNUnit -p:DeployAfterBuild=false
dotnet build benchmarks/JsonDeserialization/ResidentComparison/SystemText/JsonBenchmark.SystemText.csproj -c Release -p:ProcessorTestSdkRoot=C:/Dev/CrestronHomeNUnit -p:DeployAfterBuild=false
```

Deploy and run one at a time, saving the output from each. The resident fixture deliberately fails if it resolves any other serializer location. The System.Text.Json fixture verifies that its serializer is merged into the benchmark assembly; its reported file/product version consequently belongs to the merged host, not the original System.Text.Json NuGet binary. The pinned package reference identifies the serializer version. The two follow-up runs passed, removed their temporary instances and uploaded packages, and released their processor reservations. Home may retain cached catalogue entries until its next planned reboot.

Use `-c Debug` instead to match the recorded follow-up's build configuration; the commands above build Release packages for an additional comparison. Always record the configuration alongside new results. The retained Debug packages were 410,124 bytes (resident Newtonsoft) and 779,466 bytes (bundled System.Text.Json). This compares an externally supplied dependency with a bundled dependency, not equal deployment strategies. These test-package sizes must not be substituted for the previously measured size difference between production driver packages.

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

The follow-up exposed missing host UI assets in the standalone packaging recipe. All three reproduction projects now include the public test host's UI and translation assets. Both new follow-up packages were installed and executed successfully on the processor after this correction. All three published reproduction projects also rebuilt in Release with zero warnings/errors, and their package contents were checked for the UI definition. Earlier failed installations produced no benchmark measurements and were cleaned up.

## License and acknowledgments

Fixture, synthetic payload and reproduction files: copyright (c) 2026 Neil Colvin, [MIT license](../../LICENSE). NUnit, Newtonsoft.Json and System.Text.Json are separately licensed dependencies; consult their NuGet package licenses. The processor host supplies its own dependency notices. Crestron SDK components retain Crestron's terms. This is an independent developer experiment, not a Crestron-endorsed benchmark.
