#nullable disable
// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
using System;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Linq;
using NUnit.Framework;
#if SYSTEM_TEXT
using System.Text.Json;
using System.Text.Json.Serialization;
#endif
namespace ProcessorResidentComparison
{
    [TestFixture]
    public sealed class ResidentComparison
    {
        [Test]
        public void VerifyIdentityAndMeasureDeserialization()
        {
            TestContext.Out.WriteLine("PROCESS pid=" + Process.GetCurrentProcess().Id + " runtime=" + Environment.Version + " os=" + Environment.OSVersion);
            TestContext.Out.WriteLine("BASELINE managedBytes=" + GC.GetTotalMemory(true));
            RunComparison();
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void RunComparison()
        {
#if SYSTEM_TEXT
            var assembly = typeof(JsonSerializer).Assembly;
            Assert.That(assembly, Is.EqualTo(typeof(ResidentComparison).Assembly), "Expected bundled serializer");
            const string label = "Bundled.System.Text.Json.10.0.12";
#else
            var assembly = typeof(Newtonsoft.Json.JsonConvert).Assembly;
            Assert.That(assembly.Location, Is.EqualTo("/simpl/app00/Newtonsoft.Json.dll"), "Must use resident serializer, not a bundled copy");
            const string label = "Resident.Newtonsoft.Json";
#endif
            TestContext.Out.WriteLine("IDENTITY " + label + " => " + assembly.FullName + " | " + assembly.Location);
            var file = FileVersionInfo.GetVersionInfo(assembly.Location);
            TestContext.Out.WriteLine("VERSION file=" + file.FileVersion + " product=" + file.ProductVersion);
            const string json = "{\"data\":{\"conditions\":[{\"data_structure_type\":1,\"temp\":68.5,\"hum\":78,\"dew_point\":61.5,\"heat_index\":69,\"wind_chill\":67,\"thw_index\":68,\"thsw_index\":69,\"wet_bulb\":62,\"wind_speed_last\":2.5,\"wind_speed_hi_last_10_min\":5.5,\"wind_dir_last\":270,\"wind_dir_scalar_avg_last_1_min\":265,\"rain_size\":2,\"rain_rate_last\":0,\"rainfall_last_24_hr\":8},{\"data_structure_type\":3,\"bar_sea_level\":29.92,\"bar_trend\":0}]},\"error\":null}";
#if SYSTEM_TEXT
            var options = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString };
            Func<Response> parse = () => JsonSerializer.Deserialize<Response>(json, options);
#else
            Func<Response> parse = () => Newtonsoft.Json.JsonConvert.DeserializeObject<Response>(json);
#endif
            Response value = parse();
            Assert.That(value.Data.Conditions.Length, Is.EqualTo(2));
            Assert.That(value.Data.Conditions[0].Temperature, Is.EqualTo(68.5f));
            Assert.That(value.Data.Conditions[1].Pressure, Is.EqualTo(29.92f));
            for (int i=0; i<200; i++) parse();
            MethodInfo counter = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            Func<long> allocated = counter == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), counter);
            for (int round=0; round<4; round++)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var timer = new Stopwatch();
                long before = allocated == null ? 0 : allocated();
                double sum=0;
                timer.Start();
                for (int i=0; i<3000; i++) sum += parse().Data.Conditions[0].Temperature.Value;
                timer.Stop();
                long bytes = allocated == null ? -1 : allocated()-before;
                TestContext.Out.WriteLine("BENCH " + label + " round=" + round + " count=3000 ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " allocatedBytes=" + bytes + " checksum=" + sum);
                ReportMemory(round);
            }
            // Check Compact only after measurement so this lookup cannot influence timing or memory samples.
            try { var compact = Assembly.Load(new AssemblyName("Newtonsoft.Json.Compact")); TestContext.Out.WriteLine("COMPACT " + compact.FullName + " | " + compact.Location); }
            catch(Exception error) { TestContext.Out.WriteLine("COMPACT_LOOKUP " + error.GetType().FullName); }
        }
        private static void ReportMemory(int round)
        {
            TestContext.Out.WriteLine("MEMORY round=" + round + " managedBytesAfterGc=" + GC.GetTotalMemory(true));
            try
            {
                string path=File.Exists("/proc/self/smaps_rollup") ? "/proc/self/smaps_rollup" : "/proc/self/smaps";
                var totals=new System.Collections.Generic.Dictionary<string,long>();
                foreach(var line in File.ReadLines(path))
                {
                    var parts=line.Split(new[]{' ', '\t', ':'},StringSplitOptions.RemoveEmptyEntries);
                    long amount;
                    if(parts.Length>=2 && new[]{"Rss","Pss","Private_Clean","Private_Dirty","Shared_Clean","Shared_Dirty"}.Contains(parts[0]) && long.TryParse(parts[1],out amount))
                        totals[parts[0]]=(totals.ContainsKey(parts[0])?totals[parts[0]]:0)+amount;
                }
                foreach(var item in totals) TestContext.Out.WriteLine("PROCESS_MEMORY_KIB round="+round+" "+item.Key+"="+item.Value);
            }
            catch(Exception error) { TestContext.Out.WriteLine("PROCESS_MEMORY_UNAVAILABLE " + error.GetType().FullName); }
        }
        public sealed class Response
        {
            public Response() { }
            #if SYSTEM_TEXT
            [JsonPropertyName("data")]
#else
            [Newtonsoft.Json.JsonProperty("data")]
#endif
 public Data Data { get; set; }
        }
        public sealed class Data
        {
            public Data() { }
            #if SYSTEM_TEXT
            [JsonPropertyName("conditions")]
#else
            [Newtonsoft.Json.JsonProperty("conditions")]
#endif
 public Sensor[] Conditions { get; set; }
        }
        public sealed class Sensor
        {
            public Sensor() { }
            #if SYSTEM_TEXT
            [JsonPropertyName("data_structure_type")]
#else
            [Newtonsoft.Json.JsonProperty("data_structure_type")]
#endif
 public int? Type { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("temp")]
#else
            [Newtonsoft.Json.JsonProperty("temp")]
#endif
 public float? Temperature { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("hum")]
#else
            [Newtonsoft.Json.JsonProperty("hum")]
#endif
 public float? Humidity { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("dew_point")]
#else
            [Newtonsoft.Json.JsonProperty("dew_point")]
#endif
 public float? DewPoint { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("heat_index")]
#else
            [Newtonsoft.Json.JsonProperty("heat_index")]
#endif
 public float? HeatIndex { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("wind_chill")]
#else
            [Newtonsoft.Json.JsonProperty("wind_chill")]
#endif
 public float? WindChill { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("thw_index")]
#else
            [Newtonsoft.Json.JsonProperty("thw_index")]
#endif
 public float? Thw { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("thsw_index")]
#else
            [Newtonsoft.Json.JsonProperty("thsw_index")]
#endif
 public float? Thsw { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("wet_bulb")]
#else
            [Newtonsoft.Json.JsonProperty("wet_bulb")]
#endif
 public float? WetBulb { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("wind_speed_last")]
#else
            [Newtonsoft.Json.JsonProperty("wind_speed_last")]
#endif
 public float? WindSpeed { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("wind_speed_hi_last_10_min")]
#else
            [Newtonsoft.Json.JsonProperty("wind_speed_hi_last_10_min")]
#endif
 public float? WindHigh { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("wind_dir_last")]
#else
            [Newtonsoft.Json.JsonProperty("wind_dir_last")]
#endif
 public float? WindDirection { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("wind_dir_scalar_avg_last_1_min")]
#else
            [Newtonsoft.Json.JsonProperty("wind_dir_scalar_avg_last_1_min")]
#endif
 public float? WindAverage { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("rain_size")]
#else
            [Newtonsoft.Json.JsonProperty("rain_size")]
#endif
 public int? RainSize { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("rain_rate_last")]
#else
            [Newtonsoft.Json.JsonProperty("rain_rate_last")]
#endif
 public int? RainRate { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("rainfall_last_24_hr")]
#else
            [Newtonsoft.Json.JsonProperty("rainfall_last_24_hr")]
#endif
 public int? Rain24 { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("bar_sea_level")]
#else
            [Newtonsoft.Json.JsonProperty("bar_sea_level")]
#endif
 public float? Pressure { get; set; }
            #if SYSTEM_TEXT
            [JsonPropertyName("bar_trend")]
#else
            [Newtonsoft.Json.JsonProperty("bar_trend")]
#endif
 public float? PressureTrend { get; set; }
        }
    }
}