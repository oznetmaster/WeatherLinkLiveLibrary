using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using log4net;
using System.Net;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

namespace WeatherLinkLive
	{
	public static class WeatherLinkLiveAPI
		{
		public static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WeatherLinkLiveAPI));

		const string WeatherLinkDataRequest = "http://{0}/v1/current_conditions";

		static readonly string[] WindDirections = new string[] { "N", "NNE", "NE", "ENE", "E", "ESE", "SE", "SSE", "S", "SSW", "SW", "WSW", "W", "WNW", "NW", "NNW", "N" };

		static WeatherLinkLiveAPI ()
			{
			}

		public class WeatherLinkLive
			{
			public readonly IPAddress weatherLinkLiveIP;
			public readonly TimeSpan refreshInterval;
			public readonly TimeSpan forceRefreshInterval;
			private DateTime lastRefresh = DateTime.MinValue;
			private JArray jCurrentConditions;
			private readonly HttpClient client;
			private readonly int rainSize;

			public float Temperature => GetTemperature ((float?)CurrentConditions?[0]["temp"]);
			public float Humidity => (float?)CurrentConditions?[0]["hum"] ?? 0;
			public float DewPoint => GetTemperature ((float?)CurrentConditions?[0]["dew_point"]);
			public float WetBulb => GetTemperature ((float?)CurrentConditions?[0]["wet_bulb"]);
			public float HeatIndex => GetTemperature ((float?)CurrentConditions?[0]["heat_index"]);
			public float WindChill => GetTemperature ((float?)CurrentConditions?[0]["wind_chill"]);
			public float ThwIndex => GetTemperature ((float?)CurrentConditions?[0]["thw_index"]);
			public float ThswIndex => GetTemperature ((float?)CurrentConditions?[0]["thsw_index"]);
			public float WindSpeedLast => GetWindSpeed ((float?)CurrentConditions?[0]["wind_speed_last"]);
			public float WindDirectionLast => (float?)CurrentConditions?[0]["wind_dir_last"] ?? 0;
			public float WindDirectionAverage1Minute => (float?)CurrentConditions?[0]["wind_dir_scalar_avg_last_1_min"] ?? 0;
			public float WindSpeedHighLast10Minutes => GetWindSpeed ((float?)CurrentConditions?[0]["wind_speed_hi_last_10_min"]);
			public float RainRate => GetRainFall ((int?)CurrentConditions?[0]["rain_rate_last"]);
			public float RainfallLast24Hours => GetRainFall ((int?)CurrentConditions?[0]["rainfall_last_24_hr"]);
			public float BarometerAtSeaLevel => GetPressure ((float?)CurrentConditions?[2]["bar_sea_level"]);
			public string BarometerTrend => GetBarometerTrend ((float?)CurrentConditions?[2]["bar_trend"]);
			public string WindDirectionLastCompass => WindDirections[(int)Math.Round (WindDirectionLast / 22.5)];
			public string WindDirectionAverage1MinuteCompass => WindDirections[(int)Math.Round (WindDirectionAverage1Minute / 22.5)];

			public WeatherLinkLive (string IPAddress, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false)
				: this (System.Net.IPAddress.Parse (IPAddress), refreshIntervalSeconds, forceRefreshIntervalSeconds, celciusTemperature, metricRain, metricWind, metricBarometer)
				{ }

			public WeatherLinkLive (IPAddress weatherLinkLiveIP, int refreshIntervalSeconds = 30, int forceRefreshIntervalSeconds = 60, bool celciusTemperature = false, bool metricRain = false, bool metricWind = false, bool metricBarometer = false)
				{
				_LOGGER.InfoFormat ("WeatherLink Live API Initialised : IPAddress {0}", weatherLinkLiveIP);

				if (refreshIntervalSeconds < 10)
					throw new ArgumentException ("refreshIntervalSeconds must be >= 10");

				CelciusTemperature = celciusTemperature;
				MetricRain = metricRain;
				MetricWind = metricWind;
				MetricBarometer = metricBarometer;

				this.weatherLinkLiveIP = weatherLinkLiveIP;
				refreshInterval = TimeSpan.FromSeconds (refreshIntervalSeconds);
				forceRefreshInterval = TimeSpan.FromSeconds (forceRefreshIntervalSeconds);
				client = new HttpClient ();
				client.DefaultRequestHeaders.Accept.Add (new MediaTypeWithQualityHeaderValue ("application/json"));
				client.Timeout = TimeSpan.FromMilliseconds (1000);

				jCurrentConditions = (JArray)RefreshData ().Result["data"]["conditions"];

				rainSize = (int)jCurrentConditions[0]["rain_size"];
				}

			private JArray CurrentConditions
				{
				get
					{
					var interval = DateTime.Now - lastRefresh;
					if (interval > forceRefreshInterval)
						jCurrentConditions = (JArray)RefreshData ().Result["data"]["conditions"];
					else if (interval > refreshInterval)
						RefreshData ().ContinueWith (j => jCurrentConditions = j.IsFaulted || j.IsCanceled ? jCurrentConditions : (JArray)j.Result["data"]["conditions"]);

					return jCurrentConditions;
					}
				}

			public bool CelciusTemperature { get; set; }
			public bool MetricRain { get; set; }
			public bool MetricWind { get; set; }
			public bool MetricBarometer { get; set; }

			private async Task<JObject> RefreshData ()
				{
				JObject weatherLinkLiveData;

				_LOGGER.Info ("Updating WeatherLinkLive data");

				lastRefresh = DateTime.Now;

				try
					{
					using (var stream = await client.GetStreamAsync (String.Format (WeatherLinkDataRequest, weatherLinkLiveIP)))
						weatherLinkLiveData = await JObject.LoadAsync (new JsonTextReader (new StreamReader (stream, Encoding.UTF8)));
					_LOGGER.DebugFormat ("WeatherLink Live data received {0} ", weatherLinkLiveData);
					}
				catch (Exception ex)
					{
					_LOGGER.Warn ("Connection error trying to update from WeatherLink Live:", ex);
					throw;
					}

				return weatherLinkLiveData;
				}

			float GetTemperature (float? tempFar)
				{
				if (!tempFar.HasValue)
					return 0;
				if (CelciusTemperature)
					return (tempFar.Value - 32) * 5 / 9;
				return tempFar.Value;
				}

			float GetWindSpeed (float? speedMPH)
				{
				if (!speedMPH.HasValue)
					return 0;
				if (MetricWind)
					return speedMPH.Value * 1.6f;
				return speedMPH.Value;
				}

			float GetRainFall (int? rainCount)
				{
				if (!rainCount.HasValue)
					return 0;
				if (MetricRain)
					switch (rainSize)
						{
						case 1:
							return rainCount.Value * 0.01f * 25.4f;
						case 2:
							return rainCount.Value * 0.2f;
						case 3:
							return rainCount.Value * 0.1f;
						case 4:
							return rainCount.Value * .001f * 25.4f;
						default:
							return 0;
						}
				else
					switch (rainSize)
						{
						case 1:
							return rainCount.Value * 0.01f;
						case 2:
							return rainCount.Value * 0.2f / 25.4f;
						case 3:
							return rainCount.Value * 0.1f / 25.4f;
						case 4:
							return rainCount.Value * .001f;
						default:
							return 0;
						}
				}

			float GetPressure (float? pressureInches)
				{
				if (!pressureInches.HasValue)
					return 0;
				if (MetricBarometer)
					return pressureInches.Value * 25.4f;
				return pressureInches.Value;
				}

			string GetBarometerTrend (float? trendInches)
				{
				if (!trendInches.HasValue)
					return "Unknown";
				var trend = trendInches.Value;
				if (trend > -0.003 && trend < 0.003)
					return "Steady";
				if (trend >= 0.003)
					{
					if (trend > 0.18)
						return "Rising Rapidly";
					if (trend < 0.04)
						return "Rising Slowly";
					return "Rising";
					}
				if (trend < -0.18)
					return "Falling Rapidly";
				if (trend > -0.04)
					return "Falling Slowly";
				return "Falling";
				}
			}
		}
	}

