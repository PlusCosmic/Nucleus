using Nucleus.Admin;
using Nucleus.Auth;
using Nucleus.Discord;
using Nucleus.Links;

namespace Nucleus;

public static class AppRegistry
{
    public static void MapEndpoints(this WebApplication app)
    {
        app.MapOpenApi();
        app.MapUserEndpoints();
        app.MapAuthEndpoints();
        app.MapLinksEndpoints();
        app.MapAdminEndpoints();
    }
}
