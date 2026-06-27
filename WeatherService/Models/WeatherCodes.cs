namespace WeatherService.Models;

// as at https://open-meteo.com/en/docs#weathervariables and search for "WMO Weather interpretation codes"
public enum WeatherCode
{
    ClearSky = 0,
    MainlyClear = 1,
    PartlyCloudy = 2,
    Overcast = 3,

    Fog = 45,
    DepositingRimeFog = 48,

    LightDrizzle = 51,
    Drizzle = 53,
    HeavyDrizzle = 55,
    LightFreezingDrizzle = 56,
    FreezingDrizzle = 57,

    LightRain = 61,
    Rain = 63,
    HeavyRain = 65,
    LightFreezingRain = 66,
    FreezingRain = 67,

    LightSnow = 71,
    Snow = 73,
    HeavySnow = 75,
    SnowGrains = 77,

    LightRainShowers = 80,
    RainShowers = 81,
    ViolentRainShowers = 82,

    LightSnowShowers = 85,
    SnowShowers = 86,

    Thunderstorm = 95,
    ThunderstormWithSlightHail = 96,
    ThunderstormWithHeavyHail = 99
}