using Dapper;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Nucleus;
using Nucleus.Auth;
using Nucleus.db;

DefaultTypeMap.MatchNamesWithUnderscores = true;
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.RegisterServices();
builder.RegisterDatabases();
builder.ApplyMigrations();
builder.ConfigureDiscordAuth();

WebApplication app = builder.Build();

app.UseHttpsRedirection();
app.UseExceptionHandler();
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

FileExtensionContentTypeProvider provider = new();
provider.Mappings[".avif"] = "image/avif";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<WhitelistMiddleware>();
app.UseAuthenticatedUserResolution();
app.MapEndpoints();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true,
    AllowCachingResponses = false,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
    }
});

app.Run();

namespace Nucleus
{
    public partial class Program { }
}