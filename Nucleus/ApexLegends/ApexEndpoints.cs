using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.ApexLegends.Models;

namespace Nucleus.ApexLegends;

public static class ApexEndpoints
{
    public static void MapApexEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("apex-legends");
        group.MapGet("map-rotation", (MapService mapService) => mapService.GetMapRotation())
            .WithName("GetApexMapRotation");
    }
    
    public static async Task<Results<Ok<CurrentMapRotation>, ProblemHttpResult>> GetApexMapRotation(this MapService mapService)
    {
        try
        {
            return TypedResults.Ok(await mapService.GetMapRotation());
        }
        catch (Exception)
        {
            return TypedResults.Problem(title: "Apex Legends Status Unavailable", statusCode: 503, detail: "The API to fetch the map rotation is currently unavailable.");
        }
    }
}