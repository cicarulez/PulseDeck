using System.Globalization;
using System.Text.Json;

namespace PulseDeck.Core;

public sealed record WeatherLocation(string Name, double Latitude, double Longitude)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsValid => !string.IsNullOrWhiteSpace(Name) && Name.Length <= 120 && !Name.Any(char.IsControl)
        && double.IsFinite(Latitude) && Latitude is >= -90 and <= 90
        && double.IsFinite(Longitude) && Longitude is >= -180 and <= 180;
}

public sealed record WeatherSnapshot
{
    public string Status { get; init; } = "not-configured";
    public string LocationName { get; init; } = "";
    public double? Temperature { get; init; }
    public double? FeelsLike { get; init; }
    public double? Humidity { get; init; }
    public double? WindSpeed { get; init; }
    public double? Minimum { get; init; }
    public double? Maximum { get; init; }
    public int? Code { get; init; }
    public bool IsDay { get; init; }
    public DateTimeOffset? ModelTime { get; init; }
    public DateTimeOffset? FetchedAt { get; init; }
}

public static class WeatherConditions
{
    public static string Describe(int? code, bool day) => code switch
    {
        0 => day ? "Sereno" : "Notte serena", 1 => "Prevalentemente sereno", 2 => "Parzialmente nuvoloso", 3 => "Coperto",
        45 or 48 => "Nebbia", 51 or 53 or 55 => "Pioviggine", 56 or 57 => "Pioviggine gelata",
        61 or 63 or 65 => "Pioggia", 66 or 67 => "Pioggia gelata", 71 or 73 or 75 or 77 => "Neve",
        80 or 81 or 82 => "Rovesci di pioggia", 85 or 86 => "Rovesci di neve",
        95 or 96 or 99 => "Temporale", _ => "Condizioni non disponibili"
    };
}

/// <summary>Non-blocking, single-flight weather polling. Failed/changed locations never reuse old readings.</summary>
public sealed class WeatherFeed(HttpClient client, TimeProvider? timeProvider = null) : IDisposable
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private WeatherLocation? location;
    private WeatherSnapshot snapshot = new();
    private Task<WeatherSnapshot>? pending;
    private CancellationTokenSource? request;
    private DateTimeOffset nextFetch;

    // Owned by the single render loop; no background task mutates its state.
    public WeatherSnapshot Read(WeatherLocation? selected, bool enabled, CancellationToken stoppingToken)
    {
        var target = enabled && selected?.IsValid == true ? selected : null;
        if (target != location)
        {
            request?.Cancel(); request?.Dispose(); request = null; pending = null;
            location = target;
            snapshot = new() { Status = target is null ? "not-configured" : "loading", LocationName = target?.Name ?? "" };
            nextFetch = DateTimeOffset.MinValue;
        }
        if (target is null || stoppingToken.IsCancellationRequested) return snapshot;
        var now = clock.GetUtcNow();
        if (pending?.IsCompleted == true)
        {
            snapshot = pending.GetAwaiter().GetResult(); pending = null;
            request?.Dispose(); request = null;
            nextFetch = now.AddMinutes(snapshot.Status == "connected" ? 15 : 2);
        }
        // Do not present an old response as current after clock jumps/suspend.
        if (snapshot.FetchedAt is { } fetched && now - fetched > TimeSpan.FromMinutes(30))
        {
            snapshot = new() { Status = "unavailable", LocationName = target.Name };
            nextFetch = DateTimeOffset.MinValue;
        }
        if (pending is null && now >= nextFetch)
        {
            request = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            request.CancelAfter(TimeSpan.FromSeconds(5));
            pending = Fetch(target, request.Token);
        }
        return snapshot;
    }

    private async Task<WeatherSnapshot> Fetch(WeatherLocation target, CancellationToken cancellationToken)
    {
        try
        {
            var latitude = target.Latitude.ToString("R", CultureInfo.InvariantCulture);
            var longitude = target.Longitude.ToString("R", CultureInfo.InvariantCulture);
            var url = $"https://api.open-meteo.com/v1/forecast?latitude={latitude}&longitude={longitude}"
                + "&current=temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,weather_code,is_day"
                + "&daily=temperature_2m_min,temperature_2m_max&forecast_days=1&timezone=auto&timeformat=unixtime"
                + "&temperature_unit=celsius&wind_speed_unit=kmh";
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var bytes = new byte[65537]; var length = 0;
            while (length < bytes.Length)
            {
                var read = await stream.ReadAsync(bytes.AsMemory(length), cancellationToken);
                if (read == 0) break;
                length += read;
            }
            if (length > 65536) throw new InvalidDataException("Weather response too large.");
            return Parse(bytes.AsMemory(0, length), target.Name, clock.GetUtcNow());
        }
        catch { return new() { Status = "unavailable", LocationName = target.Name }; }
    }

    public static WeatherSnapshot Parse(ReadOnlyMemory<byte> json, string locationName, DateTimeOffset now)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement; var current = root.GetProperty("current");
        var units = root.GetProperty("current_units");
        if (units.GetProperty("temperature_2m").GetString() != "°C" || units.GetProperty("wind_speed_10m").GetString() != "km/h")
            throw new InvalidDataException("Unexpected weather units.");
        static double? Value(JsonElement node, string key, double min, double max)
        {
            if (!node.TryGetProperty(key, out var value) || !value.TryGetDoubleSafe(out var number)
                || !double.IsFinite(number) || number < min || number > max) return null;
            return number;
        }
        var temperature = Value(current, "temperature_2m", -100, 70) ?? throw new InvalidDataException("Missing temperature.");
        var modelTime = DateTimeOffset.FromUnixTimeSeconds(current.GetProperty("time").GetInt64());
        if (now - modelTime > TimeSpan.FromHours(2) || modelTime - now > TimeSpan.FromMinutes(30))
            throw new InvalidDataException("Outdated weather model time.");
        var offset = root.GetProperty("utc_offset_seconds").GetInt32();
        var localTime = modelTime.ToOffset(TimeSpan.FromSeconds(offset));
        var code = Value(current, "weather_code", 0, 99);
        double? Daily(string name)
        {
            if (!root.TryGetProperty("daily", out var daily) || !daily.TryGetProperty(name, out var values)
                || values.ValueKind != JsonValueKind.Array || values.GetArrayLength() == 0
                || !values[0].TryGetDoubleSafe(out var value) || !double.IsFinite(value) || value is < -100 or > 70) return null;
            return value;
        }
        return new() { Status = "connected", LocationName = locationName, Temperature = temperature,
            FeelsLike = Value(current, "apparent_temperature", -130, 100), Humidity = Value(current, "relative_humidity_2m", 0, 100),
            WindSpeed = Value(current, "wind_speed_10m", 0, 500), Code = code is { } c && c == Math.Truncate(c) ? (int)c : null,
            IsDay = Value(current, "is_day", 0, 1) == 1, Minimum = Daily("temperature_2m_min"), Maximum = Daily("temperature_2m_max"),
            ModelTime = localTime, FetchedAt = now };
    }

    public void Dispose() { request?.Cancel(); request?.Dispose(); }
}

internal static class WeatherJsonExtensions
{
    public static bool TryGetDoubleSafe(this JsonElement element, out double value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value);
    }
}
