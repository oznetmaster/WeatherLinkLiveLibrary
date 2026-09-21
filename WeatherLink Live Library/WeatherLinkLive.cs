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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using System.Text.Json;

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
		private static readonly Action<ILogger, IPAddress, Exception?> _logCreated = LoggerMessage.Define<IPAddress> (LogLevel.Debug, new EventId (1, "Created"), "WeatherLink Live client created for {Address}.");
		private static readonly Action<ILogger, Exception?> _logDisposed = LoggerMessage.Define (LogLevel.Debug, new EventId (2, "Disposed"), "WeatherLink Live client disposed.");
		private static readonly Action<ILogger, Exception?> _logRefreshing = LoggerMessage.Define (LogLevel.Debug, new EventId (3, "Refreshing"), "Refreshing WeatherLink Live readings.");
		private static readonly Action<ILogger, int, Exception?> _logReceived = LoggerMessage.Define<int> (LogLevel.Debug, new EventId (4, "Received"), "WeatherLink Live response received with {SensorCount} sensor records.");
		private static readonly Action<ILogger, Exception?> _logFailed = LoggerMessage.Define (LogLevel.Warning, new EventId (5, "RefreshFailed"), "Unable to refresh WeatherLink Live readings.");
		private readonly ILogger<WeatherLinkLive> _logger;
		private readonly TimeSpan _forceRefreshInterval;
		private readonly TimeSpan _refreshInterval;
		private readonly IPAddress _weatherLinkLiveIP;
		private readonly HttpClient _client;
		private readonly object _stateLock = new ();
		private readonly SemaphoreSlim _refreshGate = new (1, 1);
		private bool _disposed;
		private readonly Func<DateTime> _utcNow;
		private bool _initialized;
		private SensorConditions?[]? _currentConditions;
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

		/// <summary>Creates a local weather client with optional host-supplied logging.</summary>
		/// <param name="iPAddress">The device IPv4 address.</param>
		/// <param name="logger">Logger supplied by the host; null disables logging.</param>
		/// <param name="refreshIntervalSeconds">Minimum refresh interval, at least ten seconds.</param>
		/// <param name="forceRefreshIntervalSeconds">Maximum age of cached readings.</param>
		/// <param name="celciusTemperature">Return Celsius temperatures.</param>
		/// <param name="metricRain">Return metric rainfall.</param>
		/// <param name="metricWind">Return metric wind speed.</param>
		/// <param name="metricBarometer">Return metric pressure.</param>
		public WeatherLinkLive (string iPAddress, ILogger<WeatherLinkLive>? logger, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false)
			: this (IPAddress.Parse (iPAddress), new HttpClientHandler (), () => DateTime.UtcNow, refreshIntervalSeconds, forceRefreshIntervalSeconds, celciusTemperature, metricRain, metricWind, metricBarometer, logger)
			{
			}

		/// <summary>Creates a local weather client with optional host-supplied logging.</summary>
		/// <param name="weatherLinkLiveIP">The device IPv4 address.</param>
		/// <param name="logger">Logger supplied by the host; null disables logging.</param>
		/// <param name="refreshIntervalSeconds">Minimum refresh interval, at least ten seconds.</param>
		/// <param name="forceRefreshIntervalSeconds">Maximum age of cached readings.</param>
		/// <param name="celciusTemperature">Return Celsius temperatures.</param>
		/// <param name="metricRain">Return metric rainfall.</param>
		/// <param name="metricWind">Return metric wind speed.</param>
		/// <param name="metricBarometer">Return metric pressure.</param>
		public WeatherLinkLive (IPAddress weatherLinkLiveIP, ILogger<WeatherLinkLive>? logger, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false)
			: this (weatherLinkLiveIP, new HttpClientHandler (), () => DateTime.UtcNow, refreshIntervalSeconds, forceRefreshIntervalSeconds, celciusTemperature, metricRain, metricWind, metricBarometer, logger)
			{
			}

		internal WeatherLinkLive (IPAddress weatherLinkLiveIP, HttpMessageHandler handler, Func<DateTime> utcNow, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false, ILogger<WeatherLinkLive>? logger = null)
			{
#if NET10_0_OR_GREATER
			ArgumentNullException.ThrowIfNull (weatherLinkLiveIP);
#else
			if (weatherLinkLiveIP is null)
				{
				throw new ArgumentNullException (nameof (weatherLinkLiveIP));
				}
#endif

			_logger = logger ?? NullLogger<WeatherLinkLive>.Instance;
			_logCreated (_logger, weatherLinkLiveIP, null);

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
		public float BarometerAtSeaLevel => EnsureInitializedAndGet (() => GetPressure (GetConditions (3)?.BarometerAtSeaLevel));

		/// <summary>
		/// Gets the textual barometer trend.
		/// </summary>
		/// <value>
		/// A string such as "Rising", "Falling", or "Steady".
		/// </value>
		public string BarometerTrend => EnsureInitializedAndGet (() => GetBarometerTrend (GetConditions (3)?.BarometerTrend));

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
		public float DewPoint => EnsureInitializedAndGet (() => GetTemperature (GetConditions (1)?.DewPoint));

		/// <summary>
		/// Gets the heat index.
		/// </summary>
		/// <value>
		/// Heat index in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float HeatIndex => EnsureInitializedAndGet (() => GetTemperature (GetConditions (1)?.HeatIndex));

		/// <summary>
		/// Gets the current relative humidity.
		/// </summary>
		/// <value>
		/// Relative humidity as a percentage in the range0–100.
		/// </value>
		public float Humidity => EnsureInitializedAndGet (() => GetConditions (1)?.Humidity ?? 0);

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
		public float RainfallLast24Hours => EnsureInitializedAndGet (() => GetRainFall (GetConditions (1)?.RainfallLast24Hours));

		/// <summary>
		/// Gets the current rain rate.
		/// </summary>
		/// <value>
		/// Rain rate in device units; output is metric when <see cref="MetricRain"/> is true.
		/// </value>
		public float RainRate => EnsureInitializedAndGet (() => GetRainFall (GetConditions (1)?.RainRate));

		/// <summary>
		/// Gets the current temperature.
		/// </summary>
		/// <value>
		/// Temperature in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float Temperature => EnsureInitializedAndGet (() => GetTemperature (GetConditions (1)?.Temperature));

		/// <summary>
		/// Gets the THSW index (Temperature-Humidity-Sun-Wind).
		/// </summary>
		/// <value>
		/// THSW index in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float ThswIndex => EnsureInitializedAndGet (() => GetTemperature (GetConditions (1)?.ThswIndex));

		/// <summary>
		/// Gets the THW index (Temperature-Humidity-Wind).
		/// </summary>
		/// <value>
		/// THW index in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float ThwIndex => EnsureInitializedAndGet (() => GetTemperature (GetConditions (1)?.ThwIndex));

		/// <summary>
		/// Gets the wet bulb temperature.
		/// </summary>
		/// <value>
		/// Wet bulb temperature in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float WetBulb => EnsureInitializedAndGet (() => GetTemperature (GetConditions (1)?.WetBulb));

		/// <summary>
		/// Gets the wind chill.
		/// </summary>
		/// <value>
		/// Wind chill in Fahrenheit, or Celsius when <see cref="CelciusTemperature"/> is true.
		/// </value>
		public float WindChill => EnsureInitializedAndGet (() => GetTemperature (GetConditions (1)?.WindChill));

		/// <summary>
		/// Gets the1 minute scalar average wind direction.
		/// </summary>
		/// <value>
		/// Average wind direction in degrees (0–360).
		/// </value>
		public float WindDirectionAverage1Minute => EnsureInitializedAndGet (() => GetConditions (1)?.WindDirectionAverage1Minute ?? 0);

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
		public float WindDirectionLast => EnsureInitializedAndGet (() => GetConditions (1)?.WindDirectionLast ?? 0);

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
		public float WindSpeedHighLast10Minutes => EnsureInitializedAndGet (() => GetWindSpeed (GetConditions (1)?.WindSpeedHighLast10Minutes));

		/// <summary>
		/// Gets the most recent wind speed.
		/// </summary>
		/// <value>
		/// Wind speed in MPH, or KPH when <see cref="MetricWind"/> is true.
		/// </value>
		public float WindSpeedLast => EnsureInitializedAndGet (() => GetWindSpeed (GetConditions (1)?.WindSpeedLast));

		private SensorConditions?[]? CurrentConditions
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
				: _currentConditions;
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
				_currentConditions = null;
				}
			_client.Dispose ();
			_logDisposed (_logger, null);
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
		/// <exception cref="InvalidDataException">Thrown when the device response is malformed or reports an API error.</exception>
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
		/// <exception cref="InvalidDataException">Thrown when the device response is malformed or reports an API error.</exception>
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

				ConditionsResponse data = await RefreshDataAsync (cancellationToken).ConfigureAwait (false);
				if (data.Error.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null))
					{
					throw new InvalidDataException ("WeatherLink Live returned an API error.");
					}
				SensorConditions?[]? conditions = data.Data?.Conditions;
				if (conditions == null || conditions.Length == 0 || conditions.Any (condition => condition == null))
					{
					throw new InvalidDataException ("WeatherLink Live response must contain a nonempty data.conditions array of sensor records.");
					}
				int rainSize = FindConditions (conditions, 1)?.RainSize ?? 0;
				lock (_stateLock)
					{
					ThrowIfDisposed ();
					cancellationToken.ThrowIfCancellationRequested ();
					_currentConditions = conditions;
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

		private static SensorConditions? FindConditions (SensorConditions?[]? conditions, int type) =>
			 conditions?.FirstOrDefault (condition => condition?.DataStructureType == type);

		private SensorConditions? GetConditions (int type) => FindConditions (CurrentConditions, type);

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

		private async Task<ConditionsResponse> RefreshDataAsync (CancellationToken cancellationToken = default)
			{
			ConditionsResponse weatherLinkLiveData;

			_logRefreshing (_logger, null);

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
				try
					{
					weatherLinkLiveData = await JsonSerializer.DeserializeAsync<ConditionsResponse> (stream, ConditionsResponse.JsonOptions, cancellationToken).ConfigureAwait (false)
						?? throw new InvalidDataException ("WeatherLink Live returned a null response.");
					}
				catch (JsonException ex)
					{
					throw new InvalidDataException ("WeatherLink Live returned malformed sensor data.", ex);
					}
				_logReceived (_logger, weatherLinkLiveData.Data?.Conditions?.Length ?? 0, null);
				}
			catch (Exception ex)
				{
				_logFailed (_logger, ex);
				throw;
				}

			return weatherLinkLiveData;
			}
		}
	}