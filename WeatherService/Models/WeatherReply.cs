using System.Text.Json.Serialization;

namespace WeatherService.Models;

public class WeatherReply
{
    [JsonPropertyName("current")]
    public CurrentData Current { get; set; } = new();
}