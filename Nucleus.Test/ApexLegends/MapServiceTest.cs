using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Nucleus.ApexLegends;
using Nucleus.ApexLegends.Models;

namespace Nucleus.Test.ApexLegends;

public class MapServiceTests
{
    private const string SampleJson = """
{
    "battle_royale": {
        "current": {
            "start": 1761471000,
            "end": 1761476400,
            "readableDate_start": "2025-10-26 09:30:00",
            "readableDate_end": "2025-10-26 11:00:00",
            "map": "E-District",
            "code": "edistrict_rotation",
            "DurationInSecs": 5400,
            "DurationInMinutes": 90,
            "asset": "https://apexlegendsstatus.com/assets/maps/E-District.png",
            "remainingSecs": 539,
            "remainingMins": 9,
            "remainingTimer": "00:08:59"
        },
        "next": {
            "start": 1761476400,
            "end": 1761481800,
            "readableDate_start": "2025-10-26 11:00:00",
            "readableDate_end": "2025-10-26 12:30:00",
            "map": "Kings Canyon",
            "code": "kings_canyon_rotation",
            "DurationInSecs": 5400,
            "DurationInMinutes": 90,
            "asset": "https://apexlegendsstatus.com/assets/maps/Kings_Canyon.png"
        }
    },
    "ranked": {
        "current": {
            "start": 1761411600,
            "end": 1761498000,
            "readableDate_start": "2025-10-25 17:00:00",
            "readableDate_end": "2025-10-26 17:00:00",
            "map": "Olympus",
            "code": "olympus_rotation",
            "DurationInSecs": 86400,
            "DurationInMinutes": 1440,
            "asset": "https://apexlegendsstatus.com/assets/maps/Olympus.png",
            "remainingSecs": 22139,
            "remainingMins": 369,
            "remainingTimer": "00:08:59"
        },
        "next": {
            "start": 1761498000,
            "end": 1761584400,
            "readableDate_start": "2025-10-26 17:00:00",
            "readableDate_end": "2025-10-27 17:00:00",
            "map": "E-District",
            "code": "edistrict_rotation",
            "DurationInSecs": 86400,
            "DurationInMinutes": 1440,
            "asset": "https://apexlegendsstatus.com/assets/maps/E-District.png"
        }
    },
    "ltm": {
        "current": {
            "start": 1761475500,
            "end": 1761476400,
            "readableDate_start": "2025-10-26 10:45:00",
            "readableDate_end": "2025-10-26 11:00:00",
            "map": "Skulltown",
            "code": "freedm_gungame_skulltown",
            "DurationInSecs": 900,
            "DurationInMinutes": 15,
            "isActive": true,
            "eventName": "Gun Run",
            "asset": "https://apexlegendsstatus.com/assets/maps/Arena_Skulltown.png",
            "remainingSecs": 539,
            "remainingMins": 9,
            "remainingTimer": "00:08:59"
        },
        "next": {
            "start": 1761476400,
            "end": 1761477300,
            "readableDate_start": "2025-10-26 11:00:00",
            "readableDate_end": "2025-10-26 11:15:00",
            "map": "Fragment",
            "code": "freedm_tdm_fragment",
            "DurationInSecs": 900,
            "DurationInMinutes": 15,
            "isActive": true,
            "eventName": "TDM",
            "asset": "https://apexlegendsstatus.com/assets/maps/Worlds_Edge.png"
        }
    }
}
""";

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Always return the provided JSON regardless of request
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleJson, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }

    [Fact]
    public async Task GetMapRotation_ParsesResponseIntoCurrentMapRotation()
    {
        // Arrange
        var httpClient = new HttpClient(new StubHttpMessageHandler());
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                {"ApexLegendsApiKey", "dummy"}
            })
            .Build();

        var service = new MapService(httpClient, config);

        // Act
        CurrentMapRotation result = await service.GetMapRotation();

        // Assert
        Assert.NotNull(result);

        // Standard maps (battle royale)
        Assert.Equal("E-District", result.StandardMap.Name);
        Assert.Equal("Kings Canyon", result.StandardMapNext.Name);

        // Ranked maps
        Assert.Equal("Olympus", result.RankedMap.Name);
        Assert.Equal("E-District", result.RankedMapNext.Name);

        // Verify unix time conversion
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1761471000), result.StandardMap.MapStart);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1761476400), result.StandardMap.MapEnd);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1761411600), result.RankedMap.MapStart);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1761498000), result.RankedMap.MapEnd);

        // CorrectAsOf should be near "now"
        Assert.True(result.CorrectAsOf > DateTimeOffset.UtcNow.AddMinutes(-5));
        Assert.True(result.CorrectAsOf < DateTimeOffset.UtcNow.AddMinutes(5));
    }
}
