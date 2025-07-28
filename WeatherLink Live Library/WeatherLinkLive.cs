// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WeatherLinkLive
	{
	public static class WeatherLinkLiveAPI
		{
		private static readonly ILog _lOGGER = log4net.LogManager.GetLogger (typeof (WeatherLinkLiveAPI));

		const string _weatherLinkDataRequest = "http://{0}/v1/current_conditions";

		static readonly string[] _windDirections = ["N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N"];

		static WeatherLinkLiveAPI ()
			{
			}

		public class WeatherLinkLive : IDisposable
			{
			private readonly IPAddress _weatherLinkLiveIP;
			private readonly TimeSpan _refreshInterval;
			private readonly TimeSpan _forceRefreshInterval;
			private DateTime _lastRefresh = DateTime.MinValue;
			private JArray? _jCurrentConditions;
			private readonly HttpClient _client;
			private int _rainSize;
			private bool _initialized;

			public float Temperature => EnsureInitializedAndGet (() => GetTemperature ((float?)CurrentConditions?[0]["temp"]));
			public float Humidity => EnsureInitializedAndGet (() => (float?)CurrentConditions?[0]["hum"] ?? 0);
			public float DewPoint => EnsureInitializedAndGet (() => GetTemperature ((float?)CurrentConditions?[0]["dew_point"]));
			public float WetBulb => EnsureInitializedAndGet (() => GetTemperature ((float?)CurrentConditions?[0]["wet_bulb"]));
			public float HeatIndex => EnsureInitializedAndGet (() => GetTemperature ((float?)CurrentConditions?[0]["heat_index"]));
			public float WindChill => EnsureInitializedAndGet (() => GetTemperature ((float?)CurrentConditions?[0]["wind_chill"]));
			public float ThwIndex => EnsureInitializedAndGet (() => GetTemperature ((float?)CurrentConditions?[0]["thw_index"]));
			public float ThswIndex => EnsureInitializedAndGet (() => GetTemperature ((float?)CurrentConditions?[0]["thsw_index"]));
			public float WindSpeedLast => EnsureInitializedAndGet (() => GetWindSpeed ((float?)CurrentConditions?[0]["wind_speed_last"]));
			public float WindDirectionLast => EnsureInitializedAndGet (() => (float?)CurrentConditions?[0]["wind_dir_last"] ?? 0);
			public float WindDirectionAverage1Minute => EnsureInitializedAndGet (() => (float?)CurrentConditions?[0]["wind_dir_scalar_avg_last_1_min"] ?? 0);
			public float WindSpeedHighLast10Minutes => EnsureInitializedAndGet (() => GetWindSpeed ((float?)CurrentConditions?[0]["wind_speed_hi_last_10_min"]));
			public float RainRate => EnsureInitializedAndGet (() => GetRainFall ((int?)CurrentConditions?[0]["rain_rate_last"]));
			public float RainfallLast24Hours => EnsureInitializedAndGet (() => GetRainFall ((int?)CurrentConditions?[0]["rainfall_last_24_hr"]));
			public float BarometerAtSeaLevel => EnsureInitializedAndGet (() => GetPressure ((float?)CurrentConditions?[2]["bar_sea_level"]));
			public string BarometerTrend => EnsureInitializedAndGet (() => GetBarometerTrend ((float?)CurrentConditions?[2]["bar_trend"]));
			public string WindDirectionLastCompass => EnsureInitializedAndGet (() => _windDirections[(int)Math.Round (WindDirectionLast / 22.5)]);
			public string WindDirectionAverage1MinuteCompass => EnsureInitializedAndGet (() => _windDirections[(int)Math.Round (WindDirectionAverage1Minute / 22.5)]);

			public WeatherLinkLive (string IPAddress, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false)
				: this (System.Net.IPAddress.Parse (IPAddress), refreshIntervalSeconds, forceRefreshIntervalSeconds, celciusTemperature, metricRain, metricWind, metricBarometer)
				{
				}

			public WeatherLinkLive (IPAddress weatherLinkLiveIP, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false)
				{
				_lOGGER.InfoFormat ("WeatherLink Live API Initialised : IPAddress {0}", weatherLinkLiveIP);

				if (refreshIntervalSeconds < 10)
					throw new ArgumentException ("refreshIntervalSeconds must be >= 10");

				CelciusTemperature = celciusTemperature;
				MetricRain = metricRain;
				MetricWind = metricWind;
				MetricBarometer = metricBarometer;

				this._weatherLinkLiveIP = weatherLinkLiveIP;
				_refreshInterval = TimeSpan.FromSeconds (refreshIntervalSeconds);
				_forceRefreshInterval = TimeSpan.FromSeconds (forceRefreshIntervalSeconds);
				_client = new HttpClient ();
				_client.DefaultRequestHeaders.Accept.Add (new MediaTypeWithQualityHeaderValue ("application/json"));
				_client.Timeout = TimeSpan.FromMilliseconds (1000);
				// Do not call RefreshDataAsync().Result here! Use async initialization.
				}

			/// <summary>
			/// Call this method after constructing the object to initialize weather data.
			/// </summary>
			public async Task InitializeAsync (CancellationToken cancellationToken = default)
				{
				JObject data = await RefreshDataAsync (cancellationToken).ConfigureAwait (false);
				_jCurrentConditions = (JArray?)data?["data"]?["conditions"];
				_rainSize = (int?)_jCurrentConditions?[0]?["rain_size"] ?? 0;
				_initialized = true;
				}

			private JArray? CurrentConditions
				{
				get
					{
					if (!_initialized)
						throw new InvalidOperationException ("WeatherLinkLive must be initialized with InitializeAsync() before use.");
					TimeSpan interval = DateTime.Now - _lastRefresh;
					return interval > _forceRefreshInterval
						? throw new InvalidOperationException ("Weather data is stale. Please call RefreshAsync() to update.")
						: _jCurrentConditions;
					}
				}

			/// <summary>
			/// Call this method to refresh weather data asynchronously.
			/// </summary>
			public async Task RefreshAsync (CancellationToken cancellationToken = default)
				{
				JObject data = await RefreshDataAsync (cancellationToken).ConfigureAwait (false);
				_jCurrentConditions = (JArray?)data?["data"]?["conditions"];
				// rainSize is assumed not to change after initialization
				_lastRefresh = DateTime.Now;
				}

			private T EnsureInitializedAndGet<T> (Func<T> getter) =>
				!_initialized
					? throw new InvalidOperationException ("WeatherLinkLive must be initialized with InitializeAsync() before use.")
					: getter ();

			public bool CelciusTemperature
				{
				get; set;
				}
			public bool MetricRain
				{
				get; set;
				}
			public bool MetricWind
				{
				get; set;
				}
			public bool MetricBarometer
				{
				get; set;
				}

			private async Task<JObject> RefreshDataAsync (CancellationToken cancellationToken = default)
				{
				JObject weatherLinkLiveData;

				_lOGGER.Info ("Updating WeatherLinkLive data");

				_lastRefresh = DateTime.Now;

				try
					{
					using (Stream stream = await _client.GetStreamAsync (string.Format (System.Globalization.CultureInfo.InvariantCulture, _weatherLinkDataRequest, _weatherLinkLiveIP)).ConfigureAwait (false))
						{
						weatherLinkLiveData = await JObject.LoadAsync (new JsonTextReader (new StreamReader (stream, Encoding.UTF8)), cancellationToken).ConfigureAwait (false);
						_lOGGER.DebugFormat ("WeatherLink Live data received {0} ", weatherLinkLiveData);
						}
					}
				catch (Exception ex)
					{
					_lOGGER.Warn ("Connection error trying to update from WeatherLink Live:", ex);
					throw;
					}

				return weatherLinkLiveData;
				}

			float GetTemperature (float? tempFar) => !tempFar.HasValue ? 0 : CelciusTemperature ? (tempFar.Value - 32) * 5 / 9 : tempFar.Value;

			float GetWindSpeed (float? speedMPH) => !speedMPH.HasValue ? 0 : MetricWind ? speedMPH.Value * 1.6f : speedMPH.Value;

			float GetRainFall (int? rainCount) =>
				!rainCount.HasValue
					? 0
					: MetricRain
					? _rainSize switch
						{
							1 => rainCount.Value * 0.01f * 25.4f,
							2 => rainCount.Value * 0.2f,
							3 => rainCount.Value * 0.1f,
							4 => rainCount.Value * .001f * 25.4f,
							_ => 0,
							}
					: _rainSize switch
						{
							1 => rainCount.Value * 0.01f,
							2 => rainCount.Value * 0.2f / 25.4f,
							3 => rainCount.Value * 0.1f / 25.4f,
							4 => rainCount.Value * .001f,
							_ => 0,
							};

			float GetPressure (float? pressureInches) => !pressureInches.HasValue ? 0 : MetricBarometer ? pressureInches.Value * 25.4f : pressureInches.Value;

			static string GetBarometerTrend (float? trendInches)
				{
				if (!trendInches.HasValue)
					return "Unknown";
				var trend = trendInches.Value;

				return trend switch
					{
						> -0.003f and < 0.003f => "Steady",
						>= 0.003f => trend switch
							{
								> 0.18f => "Rising Rapidly",
								< 0.04f => "Rising Slowly",
								_ => "Rising",
								},
						_ => trend switch
							{
								< -0.18f => "Falling Rapidly",
								> -0.04f => "Falling Slowly",
								_ => "Falling",
								},
						};
				}

			public void Dispose () => GC.SuppressFinalize (this);
			}
		}
	}

