// Copyright (c) 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE in the repository root.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace WeatherLinkLive;

internal sealed class ConditionsResponse
	{
	public ConditionsResponse () { }
	internal static readonly JsonSerializerOptions JsonOptions = new () { NumberHandling = JsonNumberHandling.AllowReadingFromString };
	[JsonPropertyName ("data")]
	public ConditionsData? Data { get; set; }
	[JsonPropertyName ("error")]
	public JsonElement Error { get; set; }
	}

internal sealed class ConditionsData
	{
	public ConditionsData () { }
	[JsonPropertyName ("conditions")]
	public SensorConditions?[]? Conditions { get; set; }
	}

internal sealed class SensorConditions
	{
	public SensorConditions () { }
	[JsonPropertyName ("bar_sea_level")]
	public float? BarometerAtSeaLevel { get; set; }
	[JsonPropertyName ("bar_trend")]
	public float? BarometerTrend { get; set; }
	[JsonPropertyName ("dew_point")]
	public float? DewPoint { get; set; }
	[JsonPropertyName ("heat_index")]
	public float? HeatIndex { get; set; }
	[JsonPropertyName ("hum")]
	public float? Humidity { get; set; }
	[JsonPropertyName ("rainfall_last_24_hr")]
	public int? RainfallLast24Hours { get; set; }
	[JsonPropertyName ("rain_rate_last")]
	public int? RainRate { get; set; }
	[JsonPropertyName ("temp")]
	public float? Temperature { get; set; }
	[JsonPropertyName ("thsw_index")]
	public float? ThswIndex { get; set; }
	[JsonPropertyName ("thw_index")]
	public float? ThwIndex { get; set; }
	[JsonPropertyName ("wet_bulb")]
	public float? WetBulb { get; set; }
	[JsonPropertyName ("wind_chill")]
	public float? WindChill { get; set; }
	[JsonPropertyName ("wind_dir_scalar_avg_last_1_min")]
	public float? WindDirectionAverage1Minute { get; set; }
	[JsonPropertyName ("wind_dir_last")]
	public float? WindDirectionLast { get; set; }
	[JsonPropertyName ("wind_speed_hi_last_10_min")]
	public float? WindSpeedHighLast10Minutes { get; set; }
	[JsonPropertyName ("wind_speed_last")]
	public float? WindSpeedLast { get; set; }
	[JsonPropertyName ("rain_size")]
	public int? RainSize { get; set; }
	[JsonPropertyName ("data_structure_type")]
	public int? DataStructureType { get; set; }
	}