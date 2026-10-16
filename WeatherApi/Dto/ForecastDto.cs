using System.Text.Json.Serialization;

namespace WeatherApi.Dto;

// for speed test. Client copy of the WeatherService REST /forecast-echo JSON reply (same fields as ForecastResponse in weather.proto).
public class ForecastDto
{
    [JsonPropertyName("points")]
    public List<ForecastPointDto> Points { get; set; } = [];
}

public class ForecastPointDto
{
    [JsonPropertyName("unix_time")]
    public long Unix_Time { get; set; }

    [JsonPropertyName("temperature_2m")]
    public double Temperature_2m { get; set; }

    [JsonPropertyName("apparent_temperature")]
    public double Apparent_Temperature { get; set; }

    [JsonPropertyName("relative_humidity_2m")]
    public double Relative_Humidity_2m { get; set; }

    [JsonPropertyName("dew_point_2m")]
    public double Dew_Point_2m { get; set; }

    [JsonPropertyName("precipitation")]
    public double Precipitation { get; set; }

    [JsonPropertyName("pressure_msl")]
    public double Pressure_Msl { get; set; }

    [JsonPropertyName("cloud_cover")]
    public double Cloud_Cover { get; set; }

    [JsonPropertyName("wind_speed_10m")]
    public double Wind_Speed_10m { get; set; }

    [JsonPropertyName("wind_direction_10m")]
    public double Wind_Direction_10m { get; set; }

    [JsonPropertyName("wind_gusts_10m")]
    public double Wind_Gusts_10m { get; set; }

    [JsonPropertyName("weather_code")]
    public int Weather_Code { get; set; }
}
