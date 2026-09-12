// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE in the repository root.

using System.IO;

using NUnit.Framework.Internal;

namespace WeatherLinkLive.Tests;

[TestFixture]
public sealed class AdditionalTests
	{
	[TestCase ("")]
	[TestCase ("not json")]
	[TestCase ("[1,2]")]
	public void MalformedJson_FailsInitializationAndDisposesResponse (string json)
		{
		using var http = new DeviceHttp ();
		http.Reply (json);
		using Client client = http.Create ();
		Assert.That (async () => await client.InitializeAsync (), Throws.InstanceOf<Newtonsoft.Json.JsonException> ());
		Assert.That (http.Contents.Single ().Disposed, Is.True);
		}

	[Test]
	public async Task DisposeDuringRequest_DoesNotResurrectClient ()
		{
		using var http = new DeviceHttp ();
		http.Hold ();
		Client client = http.Create ();
		Task pending = client.InitializeAsync ();
		client.Dispose ();
		try
			{
			await pending;
			}
		catch (OperationCanceledException)
			{
			// Disposing the client cancels its outstanding HTTP request.
			}
		Assert.Throws<ObjectDisposedException> (() => _ = client.Temperature);
		}

	[Test]
	public async Task WaitingRefresh_CanBeCancelledWithoutCancellingActiveRequest ()
		{
		using var http = new DeviceHttp ();
		TaskCompletionSource<HttpResponseMessage> response = http.Hold ();
		using Client client = http.Create ();
		Task first = client.InitializeAsync ();
		using var cancellation = new CancellationTokenSource ();
		Task waiting = client.InitializeAsync (cancellation.Token);
		cancellation.Cancel ();
		Assert.That (async () => await waiting, Throws.InstanceOf<OperationCanceledException> ());
		response.SetResult (new HttpResponseMessage (HttpStatusCode.OK) { Content = new StringContent (DeviceHttp.Payload ().ToString ()) });
		await first;
		Assert.That (client.Temperature, Is.EqualTo (68));
		Assert.That (http.Requests, Has.Count.EqualTo (1));
		}

	[Test]
	public async Task ConcurrentRefreshes_UseOneNewSnapshot ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		TaskCompletionSource<HttpResponseMessage> response = http.Hold ();
		using Client client = http.Create ();
		await client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (10);
		Task first = client.RefreshAsync ();
		Task second = client.RefreshAsync ();
		response.SetResult (new HttpResponseMessage (HttpStatusCode.OK) { Content = new StringContent (DeviceHttp.Payload ().ToString ()) });
		await Task.WhenAll (first, second);
		Assert.That (http.Requests, Has.Count.EqualTo (2));
		}

	[Test]
	public void Constructor_AppliesAllUnitPreferences ()
		{
		using var client = new Client ("192.0.2.10", celciusTemperature: true, metricRain: true, metricWind: true, metricBarometer: true);
		Assert.That (client.CelciusTemperature && client.MetricRain && client.MetricWind && client.MetricBarometer, Is.True);
		}
	}

[TestFixture]
public sealed class LiveSettingsTests
	{
	private string _folder = null!;
	private string SettingsPath => Path.Combine (_folder, "LiveTestSettings.json");

	[SetUp]
	public void CreateFolder ()
		{
		_folder = Path.Combine (Path.GetTempPath (), "WeatherLink-settings-" + Guid.NewGuid ().ToString ("N"));
		Directory.CreateDirectory (_folder);
		}

	[TearDown]
	public void DeleteFolder () => Directory.Delete (_folder, true);

	[Test]
	public void MissingSettings_AreDisabledByDefault ()
		{
		using var isolated = new TestExecutionContext.IsolatedContext ();
		Assert.Throws<IgnoreException> (() => LiveTestSupport.LoadForRun (SettingsPath, ""));
		}

	[Test]
	public void EnabledMissingSettings_FailClearly () => Assert.Throws<InvalidDataException> (() => LiveTestSupport.LoadForRun (SettingsPath, "true"));

	[TestCase ("invalid")]
	[TestCase ("1")]
	public void InvalidOverride_IsRejected (string value) => Assert.Throws<InvalidDataException> (() => LiveTestSupport.LoadForRun (SettingsPath, value));

	[TestCase ("{}")]
	[TestCase ("null")]
	[TestCase ("not json")]
	[TestCase ("{\"ipAddress\":\"invalid\"}")]
	[TestCase ("{\"ipAddress\":\"::1\"}")]
	public void EnabledInvalidSettings_AreRejected (string json)
		{
		File.WriteAllText (SettingsPath, json);
		Assert.Throws<InvalidDataException> (() => LiveTestSupport.LoadForRun (SettingsPath, "true"));
		}

	[TestCase (true, "")]
	[TestCase (false, "true")]
	public void SettingsOrOverride_EnableReadOnlyTests (bool enabled, string enableOverride)
		{
		File.WriteAllText (SettingsPath, new JObject { ["enabled"] = enabled, ["ipAddress"] = "192.0.2.10" }.ToString ());
		Assert.That (LiveTestSupport.LoadForRun (SettingsPath, enableOverride).IpAddress, Is.EqualTo ("192.0.2.10"));
		}

	[Test]
	public void FalseOverride_DisablesEvenMalformedSettings ()
		{
		File.WriteAllText (SettingsPath, "not json");
		using var isolated = new TestExecutionContext.IsolatedContext ();
		Assert.Throws<IgnoreException> (() => LiveTestSupport.LoadForRun (SettingsPath, "false"));
		}
	}