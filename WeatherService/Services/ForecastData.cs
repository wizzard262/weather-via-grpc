using System.Collections.Concurrent;
using Grpc;
using WeatherService.Models;

namespace WeatherService.Services;

// for speed test. Builds a made-up hourly forecast of `count` points, once per count, and keeps it in memory.
// The REST and gRPC endpoints both return this same data, so their time is only spent serializing and sending it.
public static class ForecastData
{
    public const int MaxCount = 50_000;

    private static readonly int[] WeatherCodes = [0, 1, 2, 3, 45, 51, 61, 63, 71, 80, 95];
    private static readonly ConcurrentDictionary<int, (ForecastResponse Grpc, ForecastDto Json)> Cache = new();

    public static ForecastResponse GetGrpc(int count) => Get(count).Grpc;

    public static ForecastDto GetRest(int count) => Get(count).Json;

    private static (ForecastResponse Grpc, ForecastDto Json) Get(int count)
    {
        count = Math.Clamp(count, 1, MaxCount);

        // stop the cache growing without limit if lots of different counts are asked for
        if (Cache.Count > 20 && !Cache.ContainsKey(count))
        {
            Cache.Clear();
        }

        return Cache.GetOrAdd(count, Build);
    }

    private static (ForecastResponse Grpc, ForecastDto Json) Build(int count)
    {
        // seeded, so the same count always gives the same data
        var random = new Random(count);
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

        // values rounded to 2 decimal places, like real sensor readings
        double Between(double min, double max) => Math.Round(min + random.NextDouble() * (max - min), 2);

        var grpc = new ForecastResponse();
        var rest = new ForecastDto { Points = new List<ForecastPointDto>(count) };

        for (int i = 0; i < count; i++)
        {
            var temperature = Between(-10, 30);
            var point = new ForecastPointDto
            {
                Unix_Time = start + i * 3600L,
                Temperature_2m = temperature,
                Apparent_Temperature = Math.Round(temperature - random.NextDouble() * 5, 2),
                Relative_Humidity_2m = Between(20, 100),
                Dew_Point_2m = Math.Round(temperature - random.NextDouble() * 10, 2),
                Precipitation = Between(0, 12),
                Pressure_Msl = Between(960, 1040),
                Cloud_Cover = Between(0, 100),
                Wind_Speed_10m = Between(0, 60),
                Wind_Direction_10m = Between(0, 360),
                Wind_Gusts_10m = Between(0, 90),
                Weather_Code = WeatherCodes[random.Next(WeatherCodes.Length)]
            };

            rest.Points.Add(point);
            grpc.Points.Add(new ForecastPoint
            {
                UnixTime = point.Unix_Time,
                Temperature2M = point.Temperature_2m,
                ApparentTemperature = point.Apparent_Temperature,
                RelativeHumidity2M = point.Relative_Humidity_2m,
                DewPoint2M = point.Dew_Point_2m,
                Precipitation = point.Precipitation,
                PressureMsl = point.Pressure_Msl,
                CloudCover = point.Cloud_Cover,
                WindSpeed10M = point.Wind_Speed_10m,
                WindDirection10M = point.Wind_Direction_10m,
                WindGusts10M = point.Wind_Gusts_10m,
                WeatherCode = point.Weather_Code
            });
        }

        return (grpc, rest);
    }
}
