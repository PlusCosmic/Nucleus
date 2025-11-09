namespace Nucleus.ApexLegends;

public static class ApexEndpoints
{
    public static void MapApexEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("apex-legends");
        group.MapGet("map-rotation", async (MapService mapService) => await mapService.GetMapRotation())
            .WithName("GetApexMapRotation");
    }
}