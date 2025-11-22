using Microsoft.AspNetCore.Http.HttpResults;
using MongoDB.Driver;

namespace Nucleus.Dropzone;

public static class DropzoneEndpoints
{
    public static void MapDropzoneEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("dropzone");
        group.MapGet("groups/{group}", GetGroup).WithName("GetGroup");
        group.MapPost("groups/{group}", UploadFile).WithName("UploadFile");
    }

    private static async Task<Results<Ok<ShareGroup>, BadRequest<string>>> GetGroup(IMongoCollection<ShareGroup> collection, DropzoneService dropzoneService, string group)
    {
        if (string.IsNullOrEmpty(group))
        {
            return TypedResults.BadRequest("Group pin cannot be empty");
        }

        if (group.Length != 6)
        {
            return TypedResults.BadRequest("Invalid group pin");
        }

        return TypedResults.Ok(await dropzoneService.GetGroup(collection, group));
    }

    private static Results<Ok<string>, BadRequest<string>> UploadFile(DropzoneService dropzoneService, string group, IFormFile file)
    {
        if (file.Length == 0)
        {
            return TypedResults.BadRequest("File is empty");
        }

        return TypedResults.Ok("File uploaded successfully");
    }
}