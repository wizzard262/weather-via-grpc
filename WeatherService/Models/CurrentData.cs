using System.Text.Json.Serialization;

namespace WeatherService.Models;

public class CurrentData
{
    [JsonPropertyName("temperature_2m")]
    public double Temperature_2m { get; set; }

    [JsonPropertyName("weather_code")]
    public int Weather_Code { get; set; }

    [JsonPropertyName("weather_description")]
    public string Weather_Description { get; set; } = string.Empty;
}