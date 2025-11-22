using Nucleus.ApexLegends;
using Nucleus.ApexLegends.LegendDetection;
using Nucleus.Auth;
using Nucleus.Clips;
using Nucleus.Clips.Bunny;
using Nucleus.Clips.FFmpeg;
using Nucleus.Discord;
using Nucleus.Links;

namespace Nucleus;

public static class AppRegistry
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapUserEndpoints();
        app.MapApexEndpoints();
        app.MapAuthEndpoints();
        app.MapLinksEndpoints();
        app.MapClipsEndpoints();
        app.MapFFmpegEndpoints();
        app.MapBunnyWebhookEndpoints();
        app.MapApexDetectionEndpoints();
        app.MapPlaylistEndpoints();
        //app.MapDropzoneEndpoints();
    }
}