using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Lada.Models;
using Windows.Devices.Geolocation;

namespace Lada.Services;

public enum WeatherActivationResult
{
    Updated,
    PermissionDenied,
    Unavailable
}

// One shared weather source for every Forecast surface. The Windows location
// request happens only after an explicit user action. Only coordinates rounded
// to two decimals (roughly city-level) are persisted and sent to Open-Meteo.
public sealed class WeatherService : IDisposable
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(12)
    };

    private readonly string _cachePath;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Timer _refreshTimer;
    private WeatherCache _cache;
    private WeatherSnapshot? _liveCurrent;
    private ForecastDebugWeather _debugWeather = ForecastDebugWeather.Real;
    private bool _disposed;

    public WeatherService(string cachePath)
    {
        _cachePath = cachePath;
        _cache = LoadCache(cachePath);
        _liveCurrent = _cache.Snapshot;
        _refreshTimer = new Timer(OnRefreshTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public event Action? Updated;

    public WeatherSnapshot? Current => _debugWeather == ForecastDebugWeather.Real
        ? _liveCurrent
        : WeatherSnapshot.CreateDebug(_debugWeather);
    public ForecastDebugWeather DebugWeather => _debugWeather;
    public bool HasCachedLocation => _cache.Latitude is not null && _cache.Longitude is not null;

    public static string GetDefaultPath()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Lada");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "weather.json");
    }

    public void SetDebugWeather(ForecastDebugWeather weather)
    {
        _debugWeather = weather;
        if (weather != ForecastDebugWeather.Real && !_disposed)
            _refreshTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        Updated?.Invoke();
    }

    public async Task<WeatherActivationResult> ActivateAsync(bool requestLocation)
    {
        if (_disposed)
            return WeatherActivationResult.Unavailable;

        if (requestLocation)
        {
            try
            {
                var access = await Geolocator.RequestAccessAsync();
                if (access != GeolocationAccessStatus.Allowed)
                    return WeatherActivationResult.PermissionDenied;

                var locator = new Geolocator { DesiredAccuracyInMeters = 3000 };
                var position = await locator.GetGeopositionAsync(
                    maximumAge: TimeSpan.FromMinutes(15),
                    timeout: TimeSpan.FromSeconds(15));

                _cache.Latitude = Math.Round(position.Coordinate.Point.Position.Latitude, 2, MidpointRounding.AwayFromZero);
                _cache.Longitude = Math.Round(position.Coordinate.Point.Position.Longitude, 2, MidpointRounding.AwayFromZero);
                SaveCache();
            }
            catch (Exception ex)
            {
                Logger.LogError("Forecast location", ex);
                return WeatherActivationResult.Unavailable;
            }
        }
        else if (!HasCachedLocation)
        {
            return WeatherActivationResult.Unavailable;
        }

        var updated = await RefreshAsync();
        if (!_disposed)
        {
            _refreshTimer.Change(TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(15));
        }

        return updated ? WeatherActivationResult.Updated : WeatherActivationResult.Unavailable;
    }

    private async Task<bool> RefreshAsync()
    {
        if (_cache.Latitude is not double latitude || _cache.Longitude is not double longitude)
            return false;

        await _refreshGate.WaitAsync();
        try
        {
            var query = string.Create(CultureInfo.InvariantCulture,
                $"https://api.open-meteo.com/v1/forecast?latitude={latitude:F2}&longitude={longitude:F2}&current=weather_code,cloud_cover,precipitation,rain,showers,snowfall,wind_speed_10m,is_day&timezone=auto&forecast_days=1");
            var json = await HttpClient.GetStringAsync(query);
            var response = JsonSerializer.Deserialize<OpenMeteoResponse>(json);
            if (response?.Current is null)
                return false;

            _liveCurrent = new WeatherSnapshot
            {
                ObservedAtUtc = DateTimeOffset.UtcNow,
                WeatherCode = response.Current.WeatherCode,
                CloudCover = Math.Clamp(response.Current.CloudCover, 0, 100),
                Precipitation = Math.Max(0, response.Current.Precipitation),
                Rain = Math.Max(0, response.Current.Rain),
                Showers = Math.Max(0, response.Current.Showers),
                Snowfall = Math.Max(0, response.Current.Snowfall),
                WindSpeed = Math.Max(0, response.Current.WindSpeed),
                IsDay = response.Current.IsDay == 1
            };
            _cache.Snapshot = _liveCurrent;
            SaveCache();
            Updated?.Invoke();
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or IOException)
        {
            Logger.LogError("Forecast weather refresh", ex);
            return false;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async void OnRefreshTimer(object? state)
    {
        if (_disposed)
            return;

        await RefreshAsync();
    }

    private static WeatherCache LoadCache(string cachePath)
    {
        try
        {
            if (!File.Exists(cachePath))
                return new WeatherCache();

            return JsonSerializer.Deserialize<WeatherCache>(File.ReadAllText(cachePath), LadaJson.Options)
                   ?? new WeatherCache();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            Logger.LogError("Forecast weather cache load", ex);
            return new WeatherCache();
        }
    }

    private void SaveCache()
    {
        try
        {
            var directory = Path.GetDirectoryName(_cachePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _cachePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(_cache, LadaJson.Options));
            if (File.Exists(_cachePath))
                File.Replace(temporaryPath, _cachePath, null);
            else
                File.Move(temporaryPath, _cachePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Logger.LogError("Forecast weather cache save", ex);
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _refreshTimer.Dispose();
    }

    private sealed class WeatherCache
    {
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public WeatherSnapshot? Snapshot { get; set; }
    }

    private sealed class OpenMeteoResponse
    {
        [JsonPropertyName("current")]
        public OpenMeteoCurrent? Current { get; set; }
    }

    private sealed class OpenMeteoCurrent
    {
        [JsonPropertyName("weather_code")]
        public int WeatherCode { get; set; }

        [JsonPropertyName("cloud_cover")]
        public double CloudCover { get; set; }

        [JsonPropertyName("precipitation")]
        public double Precipitation { get; set; }

        [JsonPropertyName("rain")]
        public double Rain { get; set; }

        [JsonPropertyName("showers")]
        public double Showers { get; set; }

        [JsonPropertyName("snowfall")]
        public double Snowfall { get; set; }

        [JsonPropertyName("wind_speed_10m")]
        public double WindSpeed { get; set; }

        [JsonPropertyName("is_day")]
        public int IsDay { get; set; }
    }
}
