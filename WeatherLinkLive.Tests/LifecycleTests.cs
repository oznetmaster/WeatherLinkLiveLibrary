// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE in the repository root.

using System.IO;
using System.Reflection;

namespace WeatherLinkLive.Tests;

[TestFixture]
public sealed class LifecycleTests
	{
	public static IEnumerable<string> Readings => typeof (Client).GetProperties ().Where (p => !p.CanWrite).Select (p => p.Name);

	[TestCaseSource (nameof (Readings))]
	public void Readings_RequireInitialization (string name)
		{
		using var http = new DeviceHttp ();
		using Client client = http.Create ();
		var exception = Assert.Throws<TargetInvocationException> (() => typeof (Client).GetProperty (name)!.GetValue (client));
		Assert.That (exception!.InnerException, Is.TypeOf<InvalidOperationException> ());
		Assert.That (http.Requests, Is.Empty);
		}

	[TestCase (-1)]
	[TestCase (0)]
	[TestCase (9)]
	public void Constructor_RejectsShortRefreshInterval (int interval)
		{
		using var http = new DeviceHttp ();
		Assert.That (() => http.Create (interval), Throws.ArgumentException);
		}

	[Test]
	public void Constructor_RejectsNullAddress () => Assert.Throws<ArgumentNullException> (() => new Client ((IPAddress)null!));

	[Test]
	public void Constructor_RejectsInvalidAddress () => Assert.Throws<FormatException> (() => new Client ("invalid"));

	[Test]
	public async Task Initialize_UsesLocalEndpointAndDisposesResponse ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		using Client client = http.Create ();
		Assert.That (http.Requests, Is.Empty);
		await client.InitializeAsync ();
		Assert.That (http.Requests.Single ().AbsoluteUri, Is.EqualTo ("http://192.0.2.10/v1/current_conditions"));
		Assert.That (http.Accept, Is.EqualTo ("application/json"));
		Assert.That (http.Contents.Single ().Disposed, Is.True);
		}

	[TestCase (400)]
	[TestCase (404)]
	[TestCase (500)]
	[TestCase (503)]
	public void Initialize_PropagatesHttpFailuresAndDisposesResponse (int status)
		{
		using var http = new DeviceHttp ();
		http.Reply (status: (HttpStatusCode)status);
		using Client client = http.Create ();
		Assert.ThrowsAsync<HttpRequestException> (() => client.InitializeAsync ());
		Assert.That (http.Contents.Single ().Disposed, Is.True);
		Assert.Throws<InvalidOperationException> (() => _ = client.Temperature);
		}

	[Test]
	public void Initialize_PropagatesTransportFailure ()
		{
		using var http = new DeviceHttp ();
		var failure = new HttpRequestException ("Synthetic connection failure");
		http.Fail (failure);
		using Client client = http.Create ();
		Assert.That (Assert.ThrowsAsync<HttpRequestException> (() => client.InitializeAsync ()), Is.SameAs (failure));
		}

	[TestCase ("{}")]
	[TestCase ("{\"data\":null}")]
	[TestCase ("{\"data\":{\"conditions\":null}}")]
	[TestCase ("{\"data\":{\"conditions\":{}}}")]
	[TestCase ("{\"data\":{\"conditions\":[]}}")]
	[TestCase ("{\"data\":{\"conditions\":[null]}}")]
	public void Initialize_RejectsInvalidResponseShape (string json)
		{
		using var http = new DeviceHttp ();
		http.Reply (json);
		using Client client = http.Create ();
		Assert.ThrowsAsync<InvalidDataException> (() => client.InitializeAsync ());
		Assert.Throws<InvalidOperationException> (() => _ = client.Temperature);
		}

	[Test]
	public void Initialize_RejectsApiErrorEvenWithConditions ()
		{
		JsonObject json = DeviceHttp.Payload ();
		json["error"] = "Device unavailable";
		using var http = new DeviceHttp ();
		http.Reply (json.ToString ());
		using Client client = http.Create ();
		Assert.ThrowsAsync<InvalidDataException> (() => client.InitializeAsync ());
		}

	[TestCase (0, 1)]
	[TestCase (9, 1)]
	[TestCase (10, 2)]
	[TestCase (60, 2)]
	public async Task Refresh_RespectsMinimumInterval (int seconds, int requests)
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		http.Reply ();
		using Client client = http.Create ();
		await client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (seconds);
		await client.RefreshAsync ();
		Assert.That (http.Requests, Has.Count.EqualTo (requests));
		}

	[Test]
	public async Task Initialize_CoalescesConcurrentCalls ()
		{
		using var http = new DeviceHttp ();
		TaskCompletionSource<HttpResponseMessage> pending = http.Hold ();
		using Client client = http.Create ();
		Task first = client.InitializeAsync ();
		Task second = client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (50);
		pending.SetResult (new HttpResponseMessage (HttpStatusCode.OK) { Content = new StringContent (DeviceHttp.Payload ().ToString ()) });
		await Task.WhenAll (first, second);
		http.Now = http.Now.AddSeconds (60);
		Assert.That (client.Temperature, Is.EqualTo (68), "Cache age starts when the response succeeds.");
		Assert.That (http.Requests, Has.Count.EqualTo (1));
		}

	[Test]
	public async Task FailedRefresh_DoesNotMakeStaleDataFreshAndCanRetry ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		http.Fail (new HttpRequestException ("Synthetic failure"));
		http.Reply ();
		using Client client = http.Create ();
		await client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (61);
		Assert.Throws<InvalidOperationException> (() => _ = client.Temperature);
		Assert.ThrowsAsync<HttpRequestException> (() => client.RefreshAsync ());
		Assert.Throws<InvalidOperationException> (() => _ = client.Temperature);
		await client.RefreshAsync ();
		Assert.That (client.Temperature, Is.EqualTo (68));
		}

	[TestCase ("not json")]
	[TestCase ("{\"data\":{\"conditions\":[]}}")]
	public async Task FailedRefresh_PreservesLastValidSnapshot (string json)
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		http.Reply (json);
		using Client client = http.Create ();
		await client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (10);
		Assert.That (async () => await client.RefreshAsync (), Throws.Exception);
		Assert.That (client.Temperature, Is.EqualTo (68));
		Assert.That (http.Contents.All (c => c.Disposed), Is.True);
		}

	[Test]
	public void Refresh_RequiresInitialization ()
		{
		using var http = new DeviceHttp ();
		using Client client = http.Create ();
		Assert.ThrowsAsync<InvalidOperationException> (() => client.RefreshAsync ());
		Assert.That (http.Requests, Is.Empty);
		}

	[Test]
	public async Task Cancellation_InterruptsRequestAndAllowsRetry ()
		{
		using var http = new DeviceHttp ();
		http.Hold ();
		http.Reply ();
		using Client client = http.Create ();
		using var cancellation = new CancellationTokenSource ();
		Task pending = client.InitializeAsync (cancellation.Token);
		cancellation.Cancel ();
		Assert.That (async () => await pending, Throws.InstanceOf<OperationCanceledException> ());
		await client.InitializeAsync ();
		Assert.That (client.Temperature, Is.EqualTo (68));
		}

	[Test]
	public async Task CancelledCachedRefresh_DoesNotIssueRequest ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		using Client client = http.Create ();
		await client.InitializeAsync ();
		Assert.That (async () => await client.RefreshAsync (new CancellationToken (true)), Throws.InstanceOf<OperationCanceledException> ());
		Assert.That (http.Requests, Has.Count.EqualTo (1));
		}

	[Test]
	public async Task Dispose_IsIdempotentAndRejectsFurtherUse ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		Client client = http.Create ();
		await client.InitializeAsync ();
		client.Dispose ();
		client.Dispose ();
		Assert.That (http.Disposed, Is.True);
		Assert.Throws<ObjectDisposedException> (() => _ = client.Temperature);
		Assert.ThrowsAsync<ObjectDisposedException> (() => client.InitializeAsync ());
		Assert.ThrowsAsync<ObjectDisposedException> (() => client.RefreshAsync ());
		}

	[Test]
	public async Task SensorRecords_AreSelectedByTypeRegardlessOfOrder ()
		{
		JsonObject json = DeviceHttp.Payload ();
		JsonArray conditions = (JsonArray)json["data"]!["conditions"]!;
		json["data"]!["conditions"] = new JsonArray (conditions.Reverse ().Select (node => node!.DeepClone ()).ToArray ());
		using var http = new DeviceHttp ();
		http.Reply (json.ToString ());
		using Client client = http.Create ();
		await client.InitializeAsync ();
		Assert.That (client.Temperature, Is.EqualTo (68));
		Assert.That (client.BarometerAtSeaLevel, Is.EqualTo (30));
		client.MetricRain = true;
		Assert.That (client.RainRate, Is.EqualTo (2));
		}

	[TestCase (1)]
	[TestCase (3)]
	public async Task MissingSensorType_UsesExistingUnavailableDefaults (int type)
		{
		JsonObject json = DeviceHttp.Payload ();
		json["data"]!["conditions"] = new JsonArray (((JsonArray)json["data"]!["conditions"]!).Where (c => (int)c!["data_structure_type"]! != type).Select (node => node!.DeepClone ()).ToArray ());
		using var http = new DeviceHttp ();
		http.Reply (json.ToString ());
		using Client client = http.Create ();
		await client.InitializeAsync ();
		Assert.That (type == 1 ? client.Temperature : client.BarometerAtSeaLevel, Is.Zero);
		}

	[Test]
	public async Task Refresh_UpdatesReadingsAndRainCollectorSizeTogether ()
		{
		JsonObject json = DeviceHttp.Payload ();
		json["data"]!["conditions"]![0]!["rain_size"] = 3;
		json["data"]!["conditions"]![0]!["temp"] = 80;
		using var http = new DeviceHttp ();
		http.Reply ();
		http.Reply (json.ToString ());
		using Client client = http.Create ();
		await client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (10);
		await client.RefreshAsync ();
		client.MetricRain = true;
		Assert.That (client.Temperature, Is.EqualTo (80));
		Assert.That (client.RainRate, Is.EqualTo (1));
		}
	}