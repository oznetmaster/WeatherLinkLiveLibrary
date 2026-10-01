// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
namespace WeatherLinkLive.Tests;

[TestFixture]
public sealed class RecoveryTests
	{
	private sealed class Delays
		{
		private readonly SemaphoreSlim _arrived = new (0);
		private TaskCompletionSource<bool>? _release;
		internal double Seconds;
		internal async Task Wait (TimeSpan delay, CancellationToken token)
			{
			Seconds = delay.TotalSeconds;
			_release = new (TaskCreationOptions.RunContinuationsAsynchronously);
			using var registration = token.Register (() => _release.TrySetCanceled ());
			_arrived.Release ();
			await _release.Task;
			}
		internal async Task Next (double expected)
			{
			Assert.That (await _arrived.WaitAsync (5000), Is.True, "Recovery loop did not schedule a delay.");
			Assert.That (Seconds, Is.EqualTo (expected));
			}
		internal void Release (DeviceHttp http)
			{
			http.Now = http.Now.AddSeconds (Seconds);
			_release!.SetResult (true);
			}
		}

	[Test]
	public async Task Recovery_UsesExactSequence_RaisesTransitionsOnce_AndResetsAfterSuccess ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		using var client = http.Create ();
		var delays = new Delays ();
		client.RecoveryDelayAsync = delays.Wait;
		int disconnected = 0, reconnected = 0;
		client.Disconnected += (_, _) => disconnected++;
		client.Reconnected += (_, _) => { Assert.That (client.Temperature, Is.EqualTo (68)); reconnected++; };
		await client.InitializeAsync ();
		Assert.That (reconnected, Is.Zero);
		http.Now = http.Now.AddSeconds (10);
		http.Fail (new HttpRequestException ("offline"));
		await Assert.ThrowsAsync<HttpRequestException> (() => client.RefreshAsync ());
		int[] sequence = [10, 20, 30, 40, 50, 60, 120, 300, 300];
		for (int i = 0; i < sequence.Length; i++)
			{
			await delays.Next (sequence[i]);
			if (i == sequence.Length - 1) http.Reply ();
			else http.Fail (new HttpRequestException ("still offline"));
			delays.Release (http);
			}
		await client.RecoveryCompletion;
		Assert.That (disconnected, Is.EqualTo (1));
		Assert.That (reconnected, Is.EqualTo (1));
		http.Now = http.Now.AddSeconds (10);
		http.Fail (new HttpRequestException ("new outage"));
		await Assert.ThrowsAsync<HttpRequestException> (() => client.RefreshAsync ());
		await delays.Next (10);
		Assert.That (disconnected, Is.EqualTo (2));
		}

	[Test]
	public async Task ManualRecovery_CancelsOldDelay_AndNewOutageStartsAtTenSeconds ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		using var client = http.Create ();
		var delays = new Delays ();
		client.RecoveryDelayAsync = delays.Wait;
		await client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (10);
		http.Fail (new HttpRequestException ("offline"));
		await Assert.ThrowsAsync<HttpRequestException> (() => client.RefreshAsync ());
		await delays.Next (10);
		Task oldRecovery = client.RecoveryCompletion;
		http.Reply ();
		await client.RefreshAsync ();
		Assert.That (await Task.WhenAny (oldRecovery, Task.Delay (5000)), Is.SameAs (oldRecovery));
		await oldRecovery;
		http.Now = http.Now.AddSeconds (10);
		http.Fail (new HttpRequestException ("new outage"));
		await Assert.ThrowsAsync<HttpRequestException> (() => client.RefreshAsync ());
		await delays.Next (10);
		}

	[Test]
	public async Task Dispose_CancelsPendingRecoveryWithoutAnotherRequestOrEvent ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		using var client = http.Create ();
		var delays = new Delays ();
		client.RecoveryDelayAsync = delays.Wait;
		int reconnects = 0;
		client.Reconnected += (_, _) => reconnects++;
		await client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (10);
		http.Fail (new HttpRequestException ("offline"));
		await Assert.ThrowsAsync<HttpRequestException> (() => client.RefreshAsync ());
		await delays.Next (10);
		Task recovery = client.RecoveryCompletion;
		client.Dispose ();
		await recovery;
		Assert.That (http.Requests, Has.Count.EqualTo (2));
		Assert.That (reconnects, Is.Zero);
		}

	[Test]
	public async Task InitialFailure_AndCallerCancellation_DoNotAnnounceDisconnection ()
		{
		using var http = new DeviceHttp ();
		using var client = http.Create ();
		int disconnected = 0;
		client.Disconnected += (_, _) => disconnected++;
		client.RecoveryDelayAsync = (_, _) => throw new AssertionException ("Unexpected recovery");
		http.Fail (new HttpRequestException ("never connected"));
		await Assert.ThrowsAsync<HttpRequestException> (() => client.InitializeAsync ());
		http.Reply ();
		await client.InitializeAsync ();
		using var cancellation = new CancellationTokenSource ();
		cancellation.Cancel ();
		await Assert.CatchAsync<OperationCanceledException> (() => client.RefreshAsync (cancellation.Token));
		Assert.That (disconnected, Is.Zero);
		Assert.That (http.Requests, Has.Count.EqualTo (2));
		}

	[Test]
	public async Task MalformedRecovery_DoesNotReconnect_AndThrowingSubscriberDoesNotStopRecovery ()
		{
		using var http = new DeviceHttp ();
		http.Reply ();
		using var client = http.Create ();
		var delays = new Delays ();
		client.RecoveryDelayAsync = delays.Wait;
		int recovered = 0;
		client.Disconnected += (_, _) => throw new InvalidOperationException ("Subscriber bug");
		client.Reconnected += (_, _) => throw new InvalidOperationException ("Subscriber bug");
		client.Reconnected += (_, _) => recovered++;
		await client.InitializeAsync ();
		http.Now = http.Now.AddSeconds (10);
		http.Fail (new TaskCanceledException ("HTTP timeout, not caller cancellation"));
		await Assert.ThrowsAsync<TaskCanceledException> (() => client.RefreshAsync ());
		await delays.Next (10);
		http.Reply ("{}");
		delays.Release (http);
		await delays.Next (20);
		Assert.That (recovered, Is.Zero);
		http.Reply ();
		delays.Release (http);
		await client.RecoveryCompletion;
		Assert.That (recovered, Is.EqualTo (1));
		}
	}
