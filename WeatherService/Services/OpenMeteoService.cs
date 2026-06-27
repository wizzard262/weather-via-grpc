using WeatherService.Models;

namespace WeatherService.Services;

public class OpenMeteoService(HttpClient http)
{
    private readonly HttpClient _http = http;

    public async Task<WeatherReply?> GetWeatherAsync(double latitude, double longitude)
    {
        var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}&current=temperature_2m,weather_code";
        var response = await _http.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var result = System.Text.Json.JsonSerializer.Deserialize<WeatherReply>(json);

        // Convert numeric weather code to get the enum name
        if (Enum.IsDefined(typeof(WeatherCode), result.Current.Weather_Code))
        {
            result.Current.Weather_Description = Enum.GetName(typeof(WeatherCode), result.Current.Weather_Code)!;
        }
        else
        {
            result.Current.Weather_Description = "Unknown";
        }

        // outputs data like: {"temperature_2m":27,"weather_code":1,"weather_description":"MainlyClear"}
        return result;
    }
}