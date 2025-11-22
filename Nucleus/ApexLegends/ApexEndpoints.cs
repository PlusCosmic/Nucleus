using Microsoft.AspNetCore.Http.HttpResults;

namespace Nucleus.ApexLegends;

public static class ApexEndpoints
{
    public static void MapApexEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("apex-legends");
        group.MapGet("map-rotation", async (MapService mapService) => await mapService.GetMapRotation())
            .WithName("GetApexMapRotation");
        group.MapPost("assign-account", AssignAccount).WithName("AssignAccount");
    }

    // Platform can be PS4, X1, PC
    public static Results<Ok, BadRequest> AssignAccount(string username, string platform)
    {
        return TypedResults.Ok();
    }
}