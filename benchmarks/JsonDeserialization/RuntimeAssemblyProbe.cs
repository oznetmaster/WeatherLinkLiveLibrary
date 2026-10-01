#nullable disable
// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
using System;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework;
namespace ProcessorRuntimeProbe
{
    [TestFixture]
    public sealed class AssemblyAvailability
    {
        [Test]
        public void ReportSharedJsonAndLoggingAssemblies()
        {
            foreach (string name in new[] { "System.Text.Json", "System.Text.Encodings.Web", "Microsoft.Extensions.Logging.Abstractions", "System.Diagnostics.DiagnosticSource", "Newtonsoft.Json" })
            {
                try
                {
                    Assembly assembly = Assembly.Load(new AssemblyName(name));
                    TestContext.Out.WriteLine("AVAILABLE " + name + " => " + assembly.FullName + " | " + assembly.Location);
                }
                catch (Exception error)
                {
                    TestContext.Out.WriteLine("UNAVAILABLE " + name + " => " + error.GetType().FullName);
                }
            }
            const string json = "{\"data\":{\"conditions\":[{\"data_structure_type\":1,\"temp\":68.5,\"hum\":78,\"dew_point\":61.5,\"heat_index\":69,\"wind_chill\":67,\"thw_index\":68,\"thsw_index\":69,\"wet_bulb\":62,\"wind_speed_last\":2.5,\"wind_speed_hi_last_10_min\":5.5,\"wind_dir_last\":270,\"wind_dir_scalar_avg_last_1_min\":265,\"rain_size\":2,\"rain_rate_last\":0,\"rainfall_last_24_hr\":8},{\"data_structure_type\":3,\"bar_sea_level\":29.92,\"bar_trend\":0}]},\"error\":null}";
            var options = new JsonSerializerOptions { NumberHandling = JsonNumberHandling.AllowReadingFromString };
            Func<Response> systemText = () => JsonSerializer.Deserialize<Response>(json, options);
            Func<Response> newtonsoft = () => Newtonsoft.Json.JsonConvert.DeserializeObject<Response>(json);
            foreach (Func<Response> parse in new[] { systemText, newtonsoft })
            {
                Response value = parse();
                Assert.That(value.Data.Conditions.Length, Is.EqualTo(2));
                Assert.That(value.Data.Conditions[0].Temperature, Is.EqualTo(68.5f));
                Assert.That(value.Data.Conditions[1].Pressure, Is.EqualTo(29.92f));
                for (int i = 0; i < 200; i++) parse();
            }
            MethodInfo counter = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            Func<long> allocated = counter == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), counter);
            const int iterations = 3000;
            for (int round = 0; round < 4; round++)
            {
                if (round % 2 == 0) { Measure("System.Text.Json", systemText, iterations, round, allocated); Measure("Newtonsoft.Json", newtonsoft, iterations, round, allocated); }
                else { Measure("Newtonsoft.Json", newtonsoft, iterations, round, allocated); Measure("System.Text.Json", systemText, iterations, round, allocated); }
            }
            Assert.Pass("Read-only assembly and warmed deserialization comparison completed; see output.");
        }
        private static void Measure(string name, Func<Response> parse, int iterations, int round, Func<long> allocated)
        {
            GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
            var timer = new Stopwatch();
            long before = allocated == null ? 0 : allocated();
            timer.Start();
            double total = 0;
            for (int i = 0; i < iterations; i++) total += parse().Data.Conditions[0].Temperature.Value;
            timer.Stop();
            long bytes = allocated == null ? -1 : allocated() - before;
            TestContext.Out.WriteLine("BENCH " + name + " round=" + round + " count=" + iterations + " ms=" + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " allocatedBytes=" + bytes + " checksum=" + total);
        }
        public sealed class Response
        {
            public Response() { }
            [JsonPropertyName("data"), Newtonsoft.Json.JsonProperty("data")] public Data Data { get; set; }
        }
        public sealed class Data
        {
            public Data() { }
            [JsonPropertyName("conditions"), Newtonsoft.Json.JsonProperty("conditions")] public Sensor[] Conditions { get; set; }
        }
        public sealed class Sensor
        {
            public Sensor() { }
            [JsonPropertyName("data_structure_type"), Newtonsoft.Json.JsonProperty("data_structure_type")] public int? Type { get; set; }
            [JsonPropertyName("temp"), Newtonsoft.Json.JsonProperty("temp")] public float? Temperature { get; set; }
            [JsonPropertyName("hum"), Newtonsoft.Json.JsonProperty("hum")] public float? Humidity { get; set; }
            [JsonPropertyName("dew_point"), Newtonsoft.Json.JsonProperty("dew_point")] public float? DewPoint { get; set; }
            [JsonPropertyName("heat_index"), Newtonsoft.Json.JsonProperty("heat_index")] public float? HeatIndex { get; set; }
            [JsonPropertyName("wind_chill"), Newtonsoft.Json.JsonProperty("wind_chill")] public float? WindChill { get; set; }
            [JsonPropertyName("thw_index"), Newtonsoft.Json.JsonProperty("thw_index")] public float? Thw { get; set; }
            [JsonPropertyName("thsw_index"), Newtonsoft.Json.JsonProperty("thsw_index")] public float? Thsw { get; set; }
            [JsonPropertyName("wet_bulb"), Newtonsoft.Json.JsonProperty("wet_bulb")] public float? WetBulb { get; set; }
            [JsonPropertyName("wind_speed_last"), Newtonsoft.Json.JsonProperty("wind_speed_last")] public float? WindSpeed { get; set; }
            [JsonPropertyName("wind_speed_hi_last_10_min"), Newtonsoft.Json.JsonProperty("wind_speed_hi_last_10_min")] public float? WindHigh { get; set; }
            [JsonPropertyName("wind_dir_last"), Newtonsoft.Json.JsonProperty("wind_dir_last")] public float? WindDirection { get; set; }
            [JsonPropertyName("wind_dir_scalar_avg_last_1_min"), Newtonsoft.Json.JsonProperty("wind_dir_scalar_avg_last_1_min")] public float? WindAverage { get; set; }
            [JsonPropertyName("rain_size"), Newtonsoft.Json.JsonProperty("rain_size")] public int? RainSize { get; set; }
            [JsonPropertyName("rain_rate_last"), Newtonsoft.Json.JsonProperty("rain_rate_last")] public int? RainRate { get; set; }
            [JsonPropertyName("rainfall_last_24_hr"), Newtonsoft.Json.JsonProperty("rainfall_last_24_hr")] public int? Rain24 { get; set; }
            [JsonPropertyName("bar_sea_level"), Newtonsoft.Json.JsonProperty("bar_sea_level")] public float? Pressure { get; set; }
            [JsonPropertyName("bar_trend"), Newtonsoft.Json.JsonProperty("bar_trend")] public float? PressureTrend { get; set; }
        }
    }
}