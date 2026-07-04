namespace WeatherService.Models;


// for speed test. Return Instance of CurrentData DTO
public static class CurrentDataInstance
{
    public static CurrentData CurrentData { get; } = new()
    {
        Temperature_2m = 0,
        Weather_Code = 1,
        Weather_Description = "OK"
    };
}