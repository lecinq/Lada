using System;

namespace Lada.Models;

public enum ForecastWeatherKind
{
    Fallback,
    Clear,
    Cloudy,
    Fog,
    Rain,
    Snow,
    Storm
}

public enum ForecastDebugWeather
{
    Real,
    ClearDay,
    ClearNight,
    Cloudy,
    Fog,
    LightRain,
    HeavyRain,
    Snow,
    Storm
}

public sealed class WeatherSnapshot
{
    public DateTimeOffset ObservedAtUtc { get; set; }
    public int WeatherCode { get; set; }
    public double CloudCover { get; set; }
    public double Precipitation { get; set; }
    public double Rain { get; set; }
    public double Showers { get; set; }
    public double Snowfall { get; set; }
    public double WindSpeed { get; set; }
    public bool IsDay { get; set; }

    public ForecastWeatherKind Kind => Classify(WeatherCode);

    public static ForecastWeatherKind Classify(int weatherCode) => weatherCode switch
    {
        0 => ForecastWeatherKind.Clear,
        1 or 2 or 3 => ForecastWeatherKind.Cloudy,
        45 or 48 => ForecastWeatherKind.Fog,
        >= 51 and <= 67 or >= 80 and <= 82 => ForecastWeatherKind.Rain,
        >= 71 and <= 77 or 85 or 86 => ForecastWeatherKind.Snow,
        >= 95 and <= 99 => ForecastWeatherKind.Storm,
        _ => ForecastWeatherKind.Cloudy
    };

    public static WeatherSnapshot CreateDebug(ForecastDebugWeather weather) => weather switch
    {
        ForecastDebugWeather.ClearDay => Create(0, 4, 0, 0, 0, 0, 3, true),
        ForecastDebugWeather.ClearNight => Create(0, 3, 0, 0, 0, 0, 2, false),
        ForecastDebugWeather.Cloudy => Create(3, 92, 0, 0, 0, 0, 12, true),
        ForecastDebugWeather.Fog => Create(45, 100, 0, 0, 0, 0, 2, true),
        ForecastDebugWeather.LightRain => Create(61, 88, 0.7, 0.7, 0, 0, 9, true),
        ForecastDebugWeather.HeavyRain => Create(65, 100, 5.5, 4.5, 1, 0, 22, false),
        ForecastDebugWeather.Snow => Create(75, 96, 2.4, 0, 0, 2.4, 11, true),
        ForecastDebugWeather.Storm => Create(96, 100, 8, 5, 3, 0, 34, false),
        _ => throw new ArgumentOutOfRangeException(nameof(weather), weather, null)
    };

    private static WeatherSnapshot Create(
        int code,
        double clouds,
        double precipitation,
        double rain,
        double showers,
        double snowfall,
        double wind,
        bool isDay) => new()
    {
        ObservedAtUtc = DateTimeOffset.UtcNow,
        WeatherCode = code,
        CloudCover = clouds,
        Precipitation = precipitation,
        Rain = rain,
        Showers = showers,
        Snowfall = snowfall,
        WindSpeed = wind,
        IsDay = isDay
    };
}
