using Lada.Models;

namespace Lada.Tests.Models;

public sealed class WeatherSnapshotTests
{
    [Theory]
    [InlineData(0, ForecastWeatherKind.Clear)]
    [InlineData(2, ForecastWeatherKind.Cloudy)]
    [InlineData(45, ForecastWeatherKind.Fog)]
    [InlineData(51, ForecastWeatherKind.Rain)]
    [InlineData(65, ForecastWeatherKind.Rain)]
    [InlineData(82, ForecastWeatherKind.Rain)]
    [InlineData(71, ForecastWeatherKind.Snow)]
    [InlineData(86, ForecastWeatherKind.Snow)]
    [InlineData(95, ForecastWeatherKind.Storm)]
    [InlineData(99, ForecastWeatherKind.Storm)]
    public void Classify_MapsWmoCodesToVisualKinds(int code, ForecastWeatherKind expected)
    {
        Assert.Equal(expected, WeatherSnapshot.Classify(code));
    }

    [Theory]
    [InlineData(ForecastDebugWeather.ClearDay, ForecastWeatherKind.Clear, true)]
    [InlineData(ForecastDebugWeather.ClearNight, ForecastWeatherKind.Clear, false)]
    [InlineData(ForecastDebugWeather.Cloudy, ForecastWeatherKind.Cloudy, true)]
    [InlineData(ForecastDebugWeather.Fog, ForecastWeatherKind.Fog, true)]
    [InlineData(ForecastDebugWeather.LightRain, ForecastWeatherKind.Rain, true)]
    [InlineData(ForecastDebugWeather.HeavyRain, ForecastWeatherKind.Rain, false)]
    [InlineData(ForecastDebugWeather.Snow, ForecastWeatherKind.Snow, true)]
    [InlineData(ForecastDebugWeather.Storm, ForecastWeatherKind.Storm, false)]
    public void CreateDebug_BuildsTheRequestedVisualState(
        ForecastDebugWeather debugWeather,
        ForecastWeatherKind expectedKind,
        bool expectedDay)
    {
        var snapshot = WeatherSnapshot.CreateDebug(debugWeather);

        Assert.Equal(expectedKind, snapshot.Kind);
        Assert.Equal(expectedDay, snapshot.IsDay);
    }

    [Fact]
    public void CreateDebug_RejectsRealWeatherBecauseItNeedsLiveData()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WeatherSnapshot.CreateDebug(ForecastDebugWeather.Real));
    }
}
