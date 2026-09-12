// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using log4net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WeatherLinkLive;

/// <summary>
/// Entry point and container for the WeatherLink Live API.
/// Contains the <see cref="WeatherLinkLive"/> client used to query a local WeatherLink Live device.
/// </summary>
public static class WeatherLinkLiveAPI
	{
	private const string WEATHER_LINK_DATA_REQUEST = "http://{0}/v1/current_conditions";
#if NET10_0_OR_GREATER
	private static readonly CompositeFormat _weatherLinkDataRequestCompositeFormat = CompositeFormat.Parse (WEATHER_LINK_DATA_REQUEST);
	private static string GetCurrentConditionsRequest (IPAddress weatherLinkLiveIP) => string.Format (CultureInfo.InvariantCulture, _weatherLinkDataRequestCompositeFormat, weatherLinkLiveIP);
#endif
	private static readonly ILog _logger = log4net.LogManager.GetLogger (typeof (WeatherLinkLiveAPI));
	private static readonly string[] _windDirections = [
	"N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S",
 "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N"
	];

	/// <summary>
	/// Client for a local WeatherLink Live device. Construct, call <see cref="InitializeAsync(CancellationToken)"/>,
	/// then use the exposed properties to read the latest values.
	/// </summary>
	public class WeatherLinkLive : IDisposable
		{
		private readonly TimeSpan _forceRefreshInterval;
		private readonly TimeSpan _refreshInterval;
		private readonly IPAddress _weatherLinkLiveIP;
		private readonly HttpClient _client;
		private readonly object _stateLock = new ();
		private readonly SemaphoreSlim _refreshGate = new (1, 1);
		private bool _disposed;
		private readonly Func<DateTime> _utcNow;
		private bool _initialized;
		private JArray? _jCurrentConditions;
		private DateTime _lastRefresh = DateTime.MinValue;
		private int _rainSize;

		/// <summary>
		/// Initializes a new instance of the <see cref="WeatherLinkLive"/> class.
		/// Initializes a new instance from an IP address string.
		/// </summary>
		/// <param name="iPAddress">The WeatherLink Live device IPv4 address.</param>
		/// <param name="refreshIntervalSeconds">Minimum seconds between refreshes. Must be greater than or equal to10.</param>
		/// <param name="forceRefreshIntervalSeconds">Maximum allowed age of cached data before access throws.</param>
		/// <param name="celciusTemperature">True to return temperatures in Celsius instead of Fahrenheit.</param>
		/// <param name="metricRain">True to return rainfall in metric units.</param>
		/// <param name="metricWind">True to return wind speed in metric units.</param>
		/// <param name="metricBarometer">True to return barometer in metric units.</param>
		/// <exception cref="ArgumentException">Thrown when <paramref name="refreshIntervalSeconds"/> is less than10.</exception>
		public WeatherLinkLive (string iPAddress, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false)
		: this (System.Net.IPAddress.Parse (iPAddress), refreshIntervalSeconds, forceRefreshIntervalSeconds, celciusTemperature, metricRain, metricWind, metricBarometer)
			{
			}

		/// <summary>
		/// Initializes a new instance of the <see cref="WeatherLinkLive"/> class.
		/// Initializes a new instance from an <see cref="System.Net.IPAddress"/>.
		/// </summary>
		/// <param name="weatherLinkLiveIP">The WeatherLink Live device IPv4 address.</param>
		/// <param name="refreshIntervalSeconds">Minimum seconds between refreshes. Must be greater than or equal to10.</param>
		/// <param name="forceRefreshIntervalSeconds">Maximum allowed age of cached data before access throws.</param>
		/// <param name="celciusTemperature">True to return temperatures in Celsius instead of Fahrenheit.</param>
		/// <param name="metricRain">True to return rainfall in metric units.</param>
		/// <param name="metricWind">True to return wind speed in metric units.</param>
		/// <param name="metricBarometer">True to return barometer in metric units.</param>
		/// <exception cref="ArgumentException">Thrown when <paramref name="refreshIntervalSeconds"/> is less than10.</exception>
		public WeatherLinkLive (IPAddress weatherLinkLiveIP, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false)
			: this (weatherLinkLiveIP, new HttpClientHandler (), () => DateTime.UtcNow, refreshIntervalSeconds, forceRefreshIntervalSeconds, celciusTemperature, metricRain, metricWind, metricBarometer)
			{
			}

		internal WeatherLinkLive (IPAddress weatherLinkLiveIP, HttpMessageHandler handler, Func<DateTime> utcNow, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false)
			{
#if NET10_0_OR_GREATER
			ArgumentNullException.ThrowIfNull (weatherLinkLiveIP);
#else
			if (weatherLinkLiveIP is null)
				{
				throw new ArgumentNullException (nameof (weatherLinkLiveIP));
				}
#endif

			_logger.InfoFormat ("WeatherLink Live API Initialised : IPAddress {0}", weatherLinkLiveIP);

			if (refreshIntervalSeconds < 10)
				{
				throw new ArgumentException ("refreshIntervalSeconds must be >=10");
				}

			CelciusTemperature = celciusTemperature;
			MetricRain = metricRain;
			MetricWind = metricWind;
			MetricBarometer = metricBarometer;

			_weatherLinkLiveIP = weatherLinkLiveIP;
			_refreshInterval = TimeSpan.FromSeconds (refreshIntervalSeconds);
			_forceRefreshInterval = TimeSpan.FromSeconds (forceRefreshIntervalSeconds);
			_utcNow = utcNow;
			_client = new HttpClient (handler);
			_client.DefaultRequestHeaders.Accept.Add (new MediaTypeWithQualityHeaderValue ("application/json"));
			_client.Timeout = TimeSpan.FromMilliseconds (1000);

			// Do not call RefreshDataAsync().Result here! Use async initialization.
			}

		/// <summary>
		/// Gets the sea-level corrected barometric pressure.
		/// </summary>
		/// <value>
		/// Pressure in inches of mercury, or millimeters when <see cref="MetricBarometer"/> is true.
		/// </value>
		public float BarometerAtSeaLevel => EnsureInitializedAndGet (() => GetPressure ((float?)GetConditions (3)?["bar_sea_level"]));

		/// <summary>
		/// Gets the textual barometer trend.
		/// </summary>
		/// <value>
		/// A string such as "Rising", "Falling", or "Steady".
		/// </value>
		public string BarometerTrend => EnsureInitializedAndGet (() => GetBarometerTrend ((float?)GetConditions (3)?["bar_trend"]));

		/// <summary>
		/// Gets or sets a value indicating whether temperatures are returned in Celsius (true) or Fahrenheit (false).
		/// </summary>
		/// <value>
		/// True for Celsius; otherwise false for Fahrenheit.
		/// </value>
		public bool CelciusTemperature
			{
			get; set;
			}

		/// <summary>
		/// Gets the dew point.
		/// </summary>
		/// <value>
		/// Dew point in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float DewPoint => EnsureInitializedAndGet (() => GetTemperature ((float?)GetConditions (1)?["dew_point"]));

		/// <summary>
		/// Gets the heat index.
		/// </summary>
		/// <value>
		/// Heat index in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float HeatIndex => EnsureInitializedAndGet (() => GetTemperature ((float?)GetConditions (1)?["heat_index"]));

		/// <summary>
		/// Gets the current relative humidity.
		/// </summary>
		/// <value>
		/// Relative humidity as a percentage in the range0–100.
		/// </value>
		public float Humidity => EnsureInitializedAndGet (() => (float?)GetConditions (1)?["hum"] ?? 0);

		/// <summary>
		/// Gets or sets a value indicating whether barometric pressure values are returned in metric units.
		/// </summary>
		/// <value>
		/// True for metric pressure; otherwise false.
		/// </value>
		public bool MetricBarometer
			{
			get; set;
			}

		/// <summary>
		/// Gets or sets a value indicating whether rainfall values are returned in metric units.
		/// </summary>
		/// <value>
		/// True for metric rainfall; otherwise false.
		/// </value>
		public bool MetricRain
			{
			get; set;
			}

		/// <summary>
		/// Gets or sets a value indicating whether wind speed values are returned in metric units.
		/// </summary>
		/// <value>
		/// True for metric wind speeds; otherwise false.
		/// </value>
		public bool MetricWind
			{
			get; set;
			}

		/// <summary>
		/// Gets the rainfall measured in the last24 hours.
		/// </summary>
		/// <value>
		/// Rainfall in device units; output is metric when <see cref="MetricRain"/> is true.
		/// </value>
		public float RainfallLast24Hours => EnsureInitializedAndGet (() => GetRainFall ((int?)GetConditions (1)?["rainfall_last_24_hr"]));

		/// <summary>
		/// Gets the current rain rate.
		/// </summary>
		/// <value>
		/// Rain rate in device units; output is metric when <see cref="MetricRain"/> is true.
		/// </value>
		public float RainRate => EnsureInitializedAndGet (() => GetRainFall ((int?)GetConditions (1)?["rain_rate_last"]));

		/// <summary>
		/// Gets the current temperature.
		/// </summary>
		/// <value>
		/// Temperature in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float Temperature => EnsureInitializedAndGet (() => GetTemperature ((float?)GetConditions (1)?["temp"]));

		/// <summary>
		/// Gets the THSW index (Temperature-Humidity-Sun-Wind).
		/// </summary>
		/// <value>
		/// THSW index in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float ThswIndex => EnsureInitializedAndGet (() => GetTemperature ((float?)GetConditions (1)?["thsw_index"]));

		/// <summary>
		/// Gets the THW index (Temperature-Humidity-Wind).
		/// </summary>
		/// <value>
		/// THW index in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float ThwIndex => EnsureInitializedAndGet (() => GetTemperature ((float?)GetConditions (1)?["thw_index"]));

		/// <summary>
		/// Gets the wet bulb temperature.
		/// </summary>
		/// <value>
		/// Wet bulb temperature in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float WetBulb => EnsureInitializedAndGet (() => GetTemperature ((float?)GetConditions (1)?["wet_bulb"]));

		/// <summary>
		/// Gets the wind chill.
		/// </summary>
		/// <value>
		/// Wind chill in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float WindChill => EnsureInitializedAndGet (() => GetTemperature ((float?)GetConditions (1)?["wind_chill"]));

		/// <summary>
		/// Gets the1 minute scalar average wind direction.
		/// </summary>
		/// <value>
		/// Average wind direction in degrees (0–360).
		/// </value>
		public float WindDirectionAverage1Minute => EnsureInitializedAndGet (() => (float?)GetConditions (1)?["wind_dir_scalar_avg_last_1_min"] ?? 0);

		/// <summary>
		/// Gets the compass direction string for the1 minute average wind direction.
		/// </summary>
		/// <value>
		/// One of N, NNE, NE, …, N.
		/// </value>
		public string WindDirectionAverage1MinuteCompass => EnsureInitializedAndGet (() => _windDirections[(int)Math.Round (WindDirectionAverage1Minute / 22.5)]);

		/// <summary>
		/// Gets the last wind direction.
		/// </summary>
		/// <value>
		/// Wind direction in degrees (0–360).
		/// </value>
		public float WindDirectionLast => EnsureInitializedAndGet (() => (float?)GetConditions (1)?["wind_dir_last"] ?? 0);

		/// <summary>
		/// Gets the compass direction string for the last wind direction.
		/// </summary>
		/// <value>
		/// One of N, NNE, NE, …, N.
		/// </value>
		public string WindDirectionLastCompass => EnsureInitializedAndGet (() => _windDirections[(int)Math.Round (WindDirectionLast / 22.5)]);

		/// <summary>
		/// Gets the highest wind speed over the last10 minutes.
		/// </summary>
		/// <value>
		/// Wind speed in MPH, or KPH when <see cref="MetricWind"/> is true.
		/// </value>
		public float WindSpeedHighLast10Minutes => EnsureInitializedAndGet (() => GetWindSpeed ((float?)GetConditions (1)?["wind_speed_hi_last_10_min"]));

		/// <summary>
		/// Gets the most recent wind speed.
		/// </summary>
		/// <value>
		/// Wind speed in MPH, or KPH when <see cref="MetricWind"/> is true.
		/// </value>
		public float WindSpeedLast => EnsureInitializedAndGet (() => GetWindSpeed ((float?)GetConditions (1)?["wind_speed_last"]));

		private JArray? CurrentConditions
			{
			get
				{
				if (!_initialized)
					{
					throw new InvalidOperationException ("WeatherLinkLive must be initialized with InitializeAsync() before use.");
					}

				TimeSpan interval = _utcNow () - _lastRefresh;
				return interval > _forceRefreshInterval
				? throw new InvalidOperationException ("Weather data is stale. Please call RefreshAsync() to update.")
				: _jCurrentConditions;
				}
			}

		/// <summary>
		/// Releases HTTP and managed resources used by this instance.
		/// </summary>
		public void Dispose ()
			{
			lock (_stateLock)
				{
				if (_disposed)
					{
					return;
					}
				_disposed = true;
				_initialized = false;
				_jCurrentConditions = null;
				}
			_client.Dispose ();
			_logger.Info ("WeatherLinkLive API disposed.");
			GC.SuppressFinalize (this);
			}

		/// <summary>
		/// Performs asynchronous initialization by fetching the initial dataset.
		/// Must be called before accessing any data properties.
		/// Repeated calls within the minimum refresh interval reuse the last successful snapshot.
		/// </summary>
		/// <param name="cancellationToken">Optional token to cancel the operation.</param>
		/// <returns>
		/// A task that represents the asynchronous initialization operation.
		/// </returns>
		/// <exception cref="HttpRequestException">Thrown on network errors while contacting the device.</exception>
		/// <exception cref="JsonException">Thrown when the received JSON cannot be parsed.</exception>
		public Task InitializeAsync (CancellationToken cancellationToken = default) => UpdateAsync (true, cancellationToken);

		/// <summary>
		/// Refreshes the cached data asynchronously after initialization.
		/// Calls within the configured minimum refresh interval reuse the last successful snapshot.
		/// Concurrent requests are serialized; a failed request preserves the previous snapshot and its age.
		/// </summary>
		/// <param name="cancellationToken">Optional token to cancel the operation.</param>
		/// <returns>
		/// A task that represents the asynchronous refresh operation.
		/// </returns>
		/// <exception cref="HttpRequestException">Thrown on network errors while contacting the device.</exception>
		/// <exception cref="JsonException">Thrown when the received JSON cannot be parsed.</exception>
		public Task RefreshAsync (CancellationToken cancellationToken = default) => UpdateAsync (false, cancellationToken);

		private async Task UpdateAsync (bool initialize, CancellationToken cancellationToken)
			{
			lock (_stateLock)
				{
				ThrowIfDisposed ();
				}
			await _refreshGate.WaitAsync (cancellationToken).ConfigureAwait (false);
			try
				{
				lock (_stateLock)
					{
					ThrowIfDisposed ();
					cancellationToken.ThrowIfCancellationRequested ();
					if (!initialize && !_initialized)
						{
						throw new InvalidOperationException ("Call InitializeAsync() before RefreshAsync().");
						}
					if (_initialized && _utcNow () - _lastRefresh < _refreshInterval)
						{
						return;
						}
					}

				JObject data = await RefreshDataAsync (cancellationToken).ConfigureAwait (false);
				if (data["error"] is JToken error && error.Type != JTokenType.Null)
					{
					throw new InvalidDataException ("WeatherLink Live returned an API error.");
					}
				if (data["data"] is not JObject body || body["conditions"] is not JArray conditions ||
					 conditions.Count == 0 || conditions.Any (condition => condition is not JObject))
					{
					throw new InvalidDataException ("WeatherLink Live response must contain a nonempty data.conditions array of sensor records.");
					}
				int rainSize = (int?)FindConditions (conditions, 1)?["rain_size"] ?? 0;
				lock (_stateLock)
					{
					ThrowIfDisposed ();
					cancellationToken.ThrowIfCancellationRequested ();
					_jCurrentConditions = conditions;
					_rainSize = rainSize;
					_lastRefresh = _utcNow ();
					_initialized = true;
					}
				}
			finally
				{
				// Keep the gate alive so disposal cannot break callers already waiting on it.
				_refreshGate.Release ();
				}
			}

		private static JObject? FindConditions (JArray? conditions, int type) =>
			 conditions?.OfType<JObject> ().FirstOrDefault (condition => (int?)condition["data_structure_type"] == type);

		private JObject? GetConditions (int type) => FindConditions (CurrentConditions, type);

		private void ThrowIfDisposed ()
			{
#if NET10_0_OR_GREATER
			ObjectDisposedException.ThrowIf (_disposed, this);
#else
			if (_disposed)
				{
				throw new ObjectDisposedException (nameof (WeatherLinkLive));
				}
#endif
			}

		private static string GetBarometerTrend (float? trendInches)
			{
			if (!trendInches.HasValue)
				{
				return "Unknown";
				}

			float trend = trendInches.Value;

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

		private T EnsureInitializedAndGet<T> (Func<T> getter)
			{
			lock (_stateLock)
				{
				ThrowIfDisposed ();
				if (!_initialized)
					{
					throw new InvalidOperationException ("WeatherLinkLive must be initialized with InitializeAsync() before use.");
					}
				return getter ();
				}
			}

		private float GetPressure (float? pressureInches) => !pressureInches.HasValue ? 0 : MetricBarometer ? pressureInches.Value * 25.4f : pressureInches.Value;

		private float GetRainFall (int? rainCount) =>
		!rainCount.HasValue
		? 0
		: MetricRain
		? _rainSize switch
			{
				1 => rainCount.Value * 0.01f * 25.4f,
				2 => rainCount.Value * 0.2f,
				3 => rainCount.Value * 0.1f,
				4 => rainCount.Value * 0.001f * 25.4f,
				_ => 0,
				}
		: _rainSize switch
			{
				1 => rainCount.Value * 0.01f,
				2 => rainCount.Value * 0.2f / 25.4f,
				3 => rainCount.Value * 0.1f / 25.4f,
				4 => rainCount.Value * 0.001f,
				_ => 0,
				};

		private float GetTemperature (float? tempFar) => !tempFar.HasValue ? 0 : CelciusTemperature ? (tempFar.Value - 32) * 5 / 9 : tempFar.Value;

		private float GetWindSpeed (float? speedMPH) => !speedMPH.HasValue ? 0 : MetricWind ? speedMPH.Value * 1.609344f : speedMPH.Value;

		private async Task<JObject> RefreshDataAsync (CancellationToken cancellationToken = default)
			{
			JObject weatherLinkLiveData;

			_logger.Info ("Updating WeatherLinkLive data");

			try
				{
#if NET10_0_OR_GREATER
				using HttpResponseMessage response = await _client.GetAsync (GetCurrentConditionsRequest (_weatherLinkLiveIP), HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait (false);
#else
				using HttpResponseMessage response = await _client.GetAsync (string.Format (CultureInfo.InvariantCulture, WEATHER_LINK_DATA_REQUEST, _weatherLinkLiveIP), HttpCompletionOption.ResponseContentRead, cancellationToken).ConfigureAwait (false);
#endif
				response.EnsureSuccessStatusCode ();
				using Stream stream = await response.Content.ReadAsStreamAsync (
#if NET10_0_OR_GREATER
					cancellationToken
#endif
					).ConfigureAwait (false);
				using var reader = new StreamReader (stream, Encoding.UTF8);
				using var jsonReader = new JsonTextReader (reader);
				weatherLinkLiveData = await JObject.LoadAsync (jsonReader, cancellationToken).ConfigureAwait (false);
				_logger.DebugFormat ("WeatherLink Live data received {0} ", weatherLinkLiveData);
				}
			catch (Exception ex)
				{
				_logger.Warn ("Connection error trying to update from WeatherLink Live:", ex);
				throw;
				}

			return weatherLinkLiveData;
			}
		}
	}