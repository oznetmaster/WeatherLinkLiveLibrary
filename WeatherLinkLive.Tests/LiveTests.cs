// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE in the repository root.

using System.IO;

using Newtonsoft.Json;

namespace WeatherLinkLive.Tests;

internal sealed class LiveTestSettings
	{
	public bool Enabled
		{
		get; set;
		}
	public string? IpAddress
		{
		get; set;
		}
	}

internal static class LiveTestSupport
	{
	internal static LiveTestSettings LoadForRun () => LoadForRun (FindSettingsPath (), TestContext.Parameters.Get ("EnableLiveTests", ""));

	internal static string FindSettingsPath ()
		{
		string directory = TestContext.Parameters.Get ("TestDataDirectory", "");
		if (!string.IsNullOrWhiteSpace (directory))
			{
			return Path.Combine (directory, "LiveTestSettings.json");
			}
		for (DirectoryInfo? parent = new (TestContext.CurrentContext.TestDirectory); parent != null; parent = parent.Parent)
			{
			string path = Path.Combine (parent.FullName, "LiveTestSettings.json");
			if (File.Exists (path))
				{
				return path;
				}
			if (File.Exists (Path.Combine (parent.FullName, "WeatherLink Live Library.sln")))
				{
				break;
				}
			}
		return Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "WeatherLinkLive", "LiveTestSettings.json");
		}

	internal static LiveTestSettings LoadForRun (string path, string enableOverride)
		{
		bool? enabled = null;
		if (!string.IsNullOrWhiteSpace (enableOverride))
			{
			if (!bool.TryParse (enableOverride, out bool value))
				{
				throw new InvalidDataException ("EnableLiveTests must be true or false.");
				}
			enabled = value;
			}
		if (enabled == false)
			{
			Assert.Ignore ("Live WeatherLink tests are disabled by EnableLiveTests=false.");
			}
		LiveTestSettings? settings = null;
		if (File.Exists (path))
			{
			try
				{
				settings = JsonConvert.DeserializeObject<LiveTestSettings> (File.ReadAllText (path));
				}
			catch (JsonException)
				{
				throw new InvalidDataException ("LiveTestSettings.json is not valid settings JSON.");
				}
			}
		if (enabled != true && settings?.Enabled != true)
			{
			Assert.Ignore ("Live WeatherLink tests are disabled. Use live.runsettings or enable private LiveTestSettings.json.");
			}
		if (settings == null)
			{
			throw new InvalidDataException ("Enabled live tests require LiveTestSettings.json beside the test project or in TestDataDirectory.");
			}
		if (!IPAddress.TryParse (settings.IpAddress, out IPAddress? address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
			{
			throw new InvalidDataException ("Enabled live tests require a valid IPv4 ipAddress.");
			}
		return settings;
		}
	}

[TestFixture]
[Category ("Live")]
[NonParallelizable]
public sealed class LiveDeviceTests
	{
	private Client? _client;

	[OneTimeSetUp]
	public async Task Connect ()
		{
		LiveTestSettings settings = LiveTestSupport.LoadForRun ();
		_client = new Client (settings.IpAddress!, refreshIntervalSeconds: 10, forceRefreshIntervalSeconds: 300);
		await _client.InitializeAsync ();
		}

	[OneTimeTearDown]
	public void Disconnect () => _client?.Dispose ();

	[Test]
	public void Readings_AreFiniteAndDirectionsAreValid ()
		{
		Client client = _client!;
		foreach (System.Reflection.PropertyInfo property in typeof (Client).GetProperties ().Where (p => p.PropertyType == typeof (float)))
			{
			float value = (float)property.GetValue (client)!;
			Assert.That (float.IsNaN (value) || float.IsInfinity (value), Is.False, property.Name);
			}
		Assert.That (client.Humidity, Is.InRange (0, 100));
		Assert.That (client.WindDirectionLast, Is.InRange (0, 360));
		Assert.That (client.WindDirectionAverage1Minute, Is.InRange (0, 360));
		Assert.That (client.WindDirectionLastCompass, Is.Not.Empty);
		Assert.That (client.BarometerTrend, Is.Not.Empty);
		}

	[Test]
	public void UnitPreferences_ConvertCachedReadings ()
		{
		Client client = _client!;
		float wind = client.WindSpeedLast;
		float rain = client.RainfallLast24Hours;
		float pressure = client.BarometerAtSeaLevel;
		try
			{
			client.MetricWind = client.MetricRain = client.MetricBarometer = true;
			Assert.That (client.WindSpeedLast, Is.EqualTo (wind * 1.609344f).Within (0.0001));
			Assert.That (client.RainfallLast24Hours, Is.EqualTo (rain * 25.4f).Within (0.001));
			Assert.That (client.BarometerAtSeaLevel, Is.EqualTo (pressure * 25.4f).Within (0.001));
			}
		finally
			{
			client.MetricWind = client.MetricRain = client.MetricBarometer = false;
			}
		}

	[Test]
	public async Task Refresh_AfterPollingInterval_ReturnsReadableSnapshot ()
		{
		await Task.Delay (TimeSpan.FromSeconds (10));
		await _client!.RefreshAsync ();
		Readings_AreFiniteAndDirectionsAreValid ();
		}
	}