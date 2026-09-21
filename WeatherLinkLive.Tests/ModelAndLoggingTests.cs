// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE in the repository root.

using System.IO;
using Microsoft.Extensions.Logging;

namespace WeatherLinkLive.Tests;

[TestFixture]
public sealed class ModelAndLoggingTests
	{
	[Test]
	public async Task TypedReadings_AcceptNumericStringsNullsAndFutureFields ()
		{
		using var http = new DeviceHttp ();
		http.Reply ("""
			{"data":{"conditions":[{"data_structure_type":"1","temp":"68.5","hum":null,"rain_size":"2","rain_rate_last":"10","future":{"arbitrary":[1,2]}}]},"error":null}
			""");
		using Client client = http.Create ();
		await client.InitializeAsync ();
		Assert.That (client.Temperature, Is.EqualTo (68.5));
		Assert.That (client.Humidity, Is.Zero);
		Assert.That (client.BarometerTrend, Is.EqualTo ("Unknown"));
		client.MetricRain = true;
		Assert.That (client.RainRate, Is.EqualTo (2));
		}

	[Test]
	public async Task InvalidReading_PreservesSnapshotAndExposesSerializerIndependentError ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		http.Reply ("""{"data":{"conditions":[{"data_structure_type":1,"temp":{}}]}}""");
		using Client client = http.Create ();
		await client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (10);
		Assert.ThrowsAsync<InvalidDataException> (() => client.RefreshAsync ());
		Assert.That (client.Temperature, Is.EqualTo (68));
		Assert.That (http.Contents.All (content => content.Disposed), Is.True);
		}

	[Test]
	public async Task HostLogger_ReceivesDiagnosticsWithoutRawDevicePayload ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		var failure = new HttpRequestException ("Synthetic failure");
		http.Fail (failure);
		var logger = new RecordingLogger ();
		using (var client = new Client (IPAddress.Parse ("192.0.2.10"), http, () => http.Now, logger: logger))
			{
			await client.InitializeAsync ();
			http.Now = http.Now.AddSeconds (31);
			Assert.That (Assert.ThrowsAsync<HttpRequestException> (() => client.RefreshAsync ()), Is.SameAs (failure));
			}
		Assert.That (logger.Events.Select (item => item.Id), Does.Contain (1).And.Contain (2).And.Contain (3).And.Contain (4).And.Contain (5));
		Assert.That (logger.Events.Single (item => item.Id == 5).Exception, Is.SameAs (failure));
		Assert.That (logger.Events.All (item => !item.Text.Contains ("SYNTHETIC")), Is.True, "Do not log raw sensor payloads.");
		}

	private sealed class RecordingLogger : ILogger<Client>
		{
		internal List<(int Id, string Text, Exception? Exception)> Events { get; } = [];
		public IDisposable? BeginScope<TState> (TState state) where TState : notnull => null;
		public bool IsEnabled (LogLevel logLevel) => true;
		public void Log<TState> (LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
			Events.Add ((eventId.Id, formatter (state, exception), exception));
		}
	}