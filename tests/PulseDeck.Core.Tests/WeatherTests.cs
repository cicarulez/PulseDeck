using System.Net;
using System.Text;
using PulseDeck.Core;
using Xunit;

public class WeatherTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1789657200);
    private static readonly WeatherLocation Rome = new("Roma", 41.89193, 12.51133);
    private static string Json(long? time = null, string temperature = "21.5", string unit = "°C") => $$$"""
        {"utc_offset_seconds":7200,"current_units":{"temperature_2m":"{{{unit}}}","wind_speed_10m":"km/h"},
         "current":{"time":{{{time ?? Now.ToUnixTimeSeconds()}}},"temperature_2m":{{{temperature}}},"apparent_temperature":20,"relative_humidity_2m":60,"wind_speed_10m":9,"weather_code":2,"is_day":1},
         "daily":{"temperature_2m_min":[15],"temperature_2m_max":[24]}}
        """;

    [Fact]
    public void ValidatesCoordinatesAndKeepsLegacyConfigurationOffline()
    {
        Assert.Null(new DeckConfig().WeatherLocation);
        Assert.Null((new DeckConfig { Layout = "weather", WeatherLocation = Rome }).Validate());
        Assert.NotNull((new DeckConfig { WeatherLocation = Rome with { Latitude = 91 } }).Validate());
        Assert.NotNull((new DeckConfig { WeatherLocation = Rome with { Longitude = double.NaN } }).Validate());
        Assert.NotNull((new DeckConfig { WeatherLocation = Rome with { Name = "" } }).Validate());
    }

    [Fact]
    public void ParsesRealUnitsAndSourceTimeWithoutInventingMissingReadings()
    {
        var weather = WeatherFeed.Parse(Encoding.UTF8.GetBytes(Json()), "Roma", Now);
        Assert.Equal(21.5, weather.Temperature); Assert.Equal(15, weather.Minimum); Assert.Equal(24, weather.Maximum);
        Assert.Equal(TimeSpan.FromHours(2), weather.ModelTime!.Value.Offset);
        Assert.Equal("Parzialmente nuvoloso", WeatherConditions.Describe(weather.Code, weather.IsDay));
        Assert.Throws<InvalidDataException>(() => WeatherFeed.Parse(Encoding.UTF8.GetBytes(Json(temperature: "null")), "Roma", Now));
        Assert.Throws<InvalidDataException>(() => WeatherFeed.Parse(Encoding.UTF8.GetBytes(Json(unit: "°F")), "Roma", Now));
        Assert.Throws<InvalidDataException>(() => WeatherFeed.Parse(Encoding.UTF8.GetBytes(Json(time: Now.AddHours(-3).ToUnixTimeSeconds())), "Roma", Now));
        Assert.Equal("Condizioni non disponibili", WeatherConditions.Describe(4, true));
    }

    [Fact]
    public async Task CachesForFifteenMinutesAndClearsValuesOnFailureOrDisable()
    {
        var clock = new Clock(); var calls = 0;
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(++calls == 1
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json()) }
            : new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))));
        using var feed = new WeatherFeed(client, clock);
        Assert.Equal("not-configured", feed.Read(Rome, false, default).Status); Assert.Equal(0, calls);
        feed.Read(Rome, true, default);
        var live = await Until(feed, Rome, s => s.Status == "connected"); Assert.Equal(21.5, live.Temperature);
        clock.Now = Now.AddMinutes(14); feed.Read(Rome, true, default); Assert.Equal(1, calls);
        clock.Now = Now.AddMinutes(16); feed.Read(Rome, true, default);
        var failed = await Until(feed, Rome, s => s.Status == "unavailable"); Assert.Null(failed.Temperature); Assert.Equal(2, calls);
        feed.Read(Rome, true, default); Assert.Equal(2, calls);
        Assert.Equal("not-configured", feed.Read(Rome, false, default).Status);
    }

    [Fact]
    public async Task SlowRequestDoesNotBlockAndCannotOverwriteNewLocation()
    {
        var first = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var client = new HttpClient(new Handler((_, _) => ++calls == 1 ? first.Task
            : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json(temperature: "10")) })));
        using var feed = new WeatherFeed(client, new Clock());
        Assert.Equal("loading", feed.Read(Rome, true, default).Status);
        Assert.False(first.Task.IsCompleted);
        feed.Read(Rome, true, default); Assert.Equal(1, calls);
        var paris = new WeatherLocation("Paris", 48.86, 2.35);
        Assert.Equal("Paris", feed.Read(paris, true, default).LocationName);
        first.SetResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json()) });
        var result = await Until(feed, paris, s => s.Status == "connected");
        Assert.Equal("Paris", result.LocationName); Assert.Equal(10, result.Temperature);
    }

    [Fact]
    public async Task RejectsOversizedResponsesAndExpiresAfterSuspend()
    {
        var clock = new Clock(); var calls = 0;
        using var client = new HttpClient(new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(++calls == 1 ? Json() : new string(' ', 65537)) })));
        using var feed = new WeatherFeed(client, clock);
        feed.Read(Rome, true, default); await Until(feed, Rome, s => s.Status == "connected");
        clock.Now = Now.AddHours(1);
        Assert.Null(feed.Read(Rome, true, default).Temperature);
        var failed = await Until(feed, Rome, s => s.Status == "unavailable"); Assert.Null(failed.Temperature);
    }

    private static async Task<WeatherSnapshot> Until(WeatherFeed feed, WeatherLocation location, Func<WeatherSnapshot, bool> predicate)
    {
        for (var i = 0; i < 100; i++) { var state = feed.Read(location, true, default); if (predicate(state)) return state; await Task.Delay(5); }
        throw new TimeoutException("Weather request did not settle.");
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = WeatherTests.Now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken); }
}
