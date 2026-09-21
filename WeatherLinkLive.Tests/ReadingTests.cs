// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE in the repository root.

namespace WeatherLinkLive.Tests;

[TestFixture]
public sealed class ReadingTests
	{
	[TestCase ("Temperature", 68, 20)]
	[TestCase ("DewPoint", 50, 10)]
	[TestCase ("WetBulb", 59, 15)]
	[TestCase ("HeatIndex", 77, 25)]
	[TestCase ("WindChill", 41, 5)]
	[TestCase ("ThwIndex", 86, 30)]
	[TestCase ("ThswIndex", 95, 35)]
	public async Task Temperatures_ConvertAndCanChangeUnitsWithoutRequests (string property, float fahrenheit, float celsius)
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		using Client client = http.Create ();
		await client.InitializeAsync ();
		Assert.That (Read (client, property), Is.EqualTo (fahrenheit));
		client.CelciusTemperature = true;
		Assert.That (Read (client, property), Is.EqualTo (celsius).Within (0.0001));
		Assert.That (http.Requests, Has.Count.EqualTo (1));
		}

	[TestCase (1, false, 0.1)]
	[TestCase (1, true, 2.54)]
	[TestCase (2, false, 0.07874016)]
	[TestCase (2, true, 2)]
	[TestCase (3, false, 0.03937008)]
	[TestCase (3, true, 1)]
	[TestCase (4, false, 0.01)]
	[TestCase (4, true, 0.254)]
	[TestCase (0, false, 0)]
	[TestCase (0, true, 0)]
	public async Task RainCounts_RespectCollectorSizeAndUnits (int size, bool metric, double expected)
		{
		JsonObject json = DeviceHttp.Payload ();
		json["data"]!["conditions"]![0]!["rain_size"] = size;
		using var http = new DeviceHttp ();
		http.Reply (json.ToString ());
		using Client client = http.Create ();
		await client.InitializeAsync ();
		client.MetricRain = metric;
		Assert.That (client.RainRate, Is.EqualTo (expected).Within (0.00001));
		Assert.That (client.RainfallLast24Hours, Is.EqualTo (expected * 5).Within (0.00001));
		}

	[TestCase (false, 10, 20)]
	[TestCase (true, 16.09344, 32.18688)]
	public async Task WindSpeed_ConvertsMilesToKilometres (bool metric, double last, double high)
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		using Client client = http.Create ();
		await client.InitializeAsync ();
		client.MetricWind = metric;
		Assert.That (client.WindSpeedLast, Is.EqualTo (last).Within (0.00001));
		Assert.That (client.WindSpeedHighLast10Minutes, Is.EqualTo (high).Within (0.00001));
		}

	[TestCase (false, 30)]
	[TestCase (true, 762)]
	public async Task Pressure_UsesDocumentedInchesOrMillimetres (bool metric, double expected)
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		using Client client = http.Create ();
		await client.InitializeAsync ();
		client.MetricBarometer = metric;
		Assert.That (client.BarometerAtSeaLevel, Is.EqualTo (expected).Within (0.0001));
		}

	[TestCase (-0.2, "Falling Rapidly")]
	[TestCase (-0.18, "Falling")]
	[TestCase (-0.04, "Falling")]
	[TestCase (-0.003, "Falling Slowly")]
	[TestCase (0, "Steady")]
	[TestCase (0.003, "Rising Slowly")]
	[TestCase (0.04, "Rising")]
	[TestCase (0.18, "Rising")]
	[TestCase (0.2, "Rising Rapidly")]
	[TestCase (null, "Unknown")]
	public async Task BarometerTrend_HandlesThresholdsAndMissingValues (double? trend, string expected)
		{
		JsonObject json = DeviceHttp.Payload ();
		json["data"]!["conditions"]![2]!["bar_trend"] = trend;
		using var http = new DeviceHttp ();
		http.Reply (json.ToString ());
		using Client client = http.Create ();
		await client.InitializeAsync ();
		Assert.That (client.BarometerTrend, Is.EqualTo (expected));
		}

	[TestCase (0, "N")]
	[TestCase (22.5, "NNE")]
	[TestCase (45, "NE")]
	[TestCase (90, "E")]
	[TestCase (180, "S")]
	[TestCase (270, "W")]
	[TestCase (337.5, "NNW")]
	[TestCase (360, "N")]
	public async Task BothWindDirections_UseCompassBearings (double degrees, string expected)
		{
		JsonObject json = DeviceHttp.Payload ();
		json["data"]!["conditions"]![0]!["wind_dir_last"] = degrees;
		json["data"]!["conditions"]![0]!["wind_dir_scalar_avg_last_1_min"] = degrees;
		using var http = new DeviceHttp ();
		http.Reply (json.ToString ());
		using Client client = http.Create ();
		await client.InitializeAsync ();
		Assert.That (client.WindDirectionLast, Is.EqualTo (degrees));
		Assert.That (client.WindDirectionAverage1Minute, Is.EqualTo (degrees));
		Assert.That (client.WindDirectionLastCompass, Is.EqualTo (expected));
		Assert.That (client.WindDirectionAverage1MinuteCompass, Is.EqualTo (expected));
		}

	[TestCase ("Temperature", "temp")]
	[TestCase ("DewPoint", "dew_point")]
	[TestCase ("WetBulb", "wet_bulb")]
	[TestCase ("HeatIndex", "heat_index")]
	[TestCase ("WindChill", "wind_chill")]
	[TestCase ("ThwIndex", "thw_index")]
	[TestCase ("ThswIndex", "thsw_index")]
	[TestCase ("Humidity", "hum")]
	[TestCase ("WindSpeedLast", "wind_speed_last")]
	[TestCase ("WindSpeedHighLast10Minutes", "wind_speed_hi_last_10_min")]
	[TestCase ("WindDirectionLast", "wind_dir_last")]
	[TestCase ("WindDirectionAverage1Minute", "wind_dir_scalar_avg_last_1_min")]
	[TestCase ("RainRate", "rain_rate_last")]
	[TestCase ("RainfallLast24Hours", "rainfall_last_24_hr")]
	public async Task NullReadings_PreserveExistingZeroDefault (string property, string field)
		{
		JsonObject json = DeviceHttp.Payload ();
		json["data"]!["conditions"]![0]![field] = null;
		using var http = new DeviceHttp ();
		http.Reply (json.ToString ());
		using Client client = http.Create ();
		await client.InitializeAsync ();
		client.CelciusTemperature = true;
		Assert.That (Read (client, property), Is.Zero);
		}

	[TestCase ("en-US")]
	[TestCase ("fr-FR")]
	[TestCase ("de-DE")]
	public async Task JsonAndConversions_AreCultureIndependent (string culture)
		{
		System.Globalization.CultureInfo original = System.Globalization.CultureInfo.CurrentCulture;
		try
			{
			System.Globalization.CultureInfo.CurrentCulture = new (culture);
			using var http = new DeviceHttp ();
			http.Reply ();
			using Client client = http.Create ();
			await client.InitializeAsync ();
			Assert.That (client.Temperature, Is.EqualTo (68));
			Assert.That (client.Humidity, Is.EqualTo (55));
			Assert.That (client.BarometerTrend, Is.EqualTo ("Rising Slowly"));
			}
		finally
			{
			System.Globalization.CultureInfo.CurrentCulture = original;
			}
		}

	private static float Read (Client client, string property) => (float)typeof (Client).GetProperty (property)!.GetValue (client)!;
	}