// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE in the repository root.

global using System;
global using System.Collections.Generic;
global using System.Linq;
global using System.Net;
global using System.Net.Http;
global using System.Threading;
global using System.Threading.Tasks;

global using Newtonsoft.Json.Linq;

global using NUnit.Framework;

global using Client = WeatherLinkLive.WeatherLinkLiveAPI.WeatherLinkLive;

namespace WeatherLinkLive.Tests;

internal sealed class DeviceHttp : HttpMessageHandler
	{
	private readonly Queue<Func<CancellationToken, Task<HttpResponseMessage>>> _responses = new ();
	internal List<Uri> Requests { get; } = [];
	internal List<ObservedContent> Contents { get; } = [];
	internal string? Accept
		{
		get; private set;
		}
	internal bool Disposed
		{
		get; private set;
		}
	internal DateTime Now { get; set; } = new (2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
	internal Client Create (int refresh = 10, int stale = 60) => new (IPAddress.Parse ("192.0.2.10"), this, () => Now, refresh, stale);
	internal void Reply (string? json = null, HttpStatusCode status = HttpStatusCode.OK)
		{
		var content = new ObservedContent (json ?? Payload ().ToString ());
		Contents.Add (content);
		_responses.Enqueue (_ => Task.FromResult (new HttpResponseMessage (status) { Content = content }));
		}
	internal TaskCompletionSource<HttpResponseMessage> Hold ()
		{
		var pending = new TaskCompletionSource<HttpResponseMessage> (TaskCreationOptions.RunContinuationsAsynchronously);
		_responses.Enqueue (async token =>
			{
				using CancellationTokenRegistration registration = token.Register (() => pending.TrySetCanceled ());
				return await pending.Task;
			});
		return pending;
		}
	internal void Fail (Exception exception) => _responses.Enqueue (_ => Task.FromException<HttpResponseMessage> (exception));
	protected override Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, CancellationToken cancellationToken)
		{
		Requests.Add (request.RequestUri!);
		Accept = request.Headers.Accept.ToString ();
		if (_responses.Count == 0)
			{
			throw new InvalidOperationException ("Unexpected request; offline tests never contact a device.");
			}
		return _responses.Dequeue () (cancellationToken);
		}
	protected override void Dispose (bool disposing)
		{
		Disposed = true;
		base.Dispose (disposing);
		}
	internal static JObject Payload () => JObject.Parse ("""
		{"data":{"did":"SYNTHETIC","ts":1767225600,"conditions":[
		{"data_structure_type":1,"txid":1,"temp":68,"hum":55,"dew_point":50,"wet_bulb":59,"heat_index":77,"wind_chill":41,"thw_index":86,"thsw_index":95,"wind_speed_last":10,"wind_speed_hi_last_10_min":20,"wind_dir_last":90,"wind_dir_scalar_avg_last_1_min":180,"rain_size":2,"rain_rate_last":10,"rainfall_last_24_hr":50},
		{"data_structure_type":4,"temp_in":72,"hum_in":45},
		{"data_structure_type":3,"bar_sea_level":30,"bar_trend":0.01}
		]},"error":null}
		""");
	}

internal sealed class ObservedContent (string body) : StringContent (body)
	{
	internal bool Disposed
		{
		get; private set;
		}
	protected override void Dispose (bool disposing)
		{
		Disposed = true;
		base.Dispose (disposing);
		}
	}