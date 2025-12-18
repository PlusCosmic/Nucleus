using System.Security.Claims;

namespace Nucleus.Auth;

public class WhitelistMiddleware
{
    private readonly ILogger<WhitelistMiddleware> _logger;
    private readonly RequestDelegate _next;
    private readonly WhitelistService _whitelistService;

    public WhitelistMiddleware(RequestDelegate next, WhitelistService whitelistService, ILogger<WhitelistMiddleware> logger)
    {
        _next = next;
        _whitelistService = whitelistService;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip whitelist check for health endpoint
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        // Skip whitelist check for auth endpoints (login/logout)
        if (context.Request.Path.StartsWithSegments("/auth"))
        {
            await _next(context);
            return;
        }

        // Skip whitelist check for dropzone endpoint (file upload)
        if (context.Request.Path.StartsWithSegments("/dropzone"))
        {
            await _next(context);
            return;
        }

        // Skip whitelist check for apex-legends endpoint (public endpoint)
        if (context.Request.Path.StartsWithSegments("/apex-legends"))
        {
            await _next(context);
            return;
        }

        // Skip whitelist check for webhooks (external service callbacks)
        if (context.Request.Path.StartsWithSegments("/webhooks"))
        {
            _logger.LogInformation("[WHITELIST] Bypassing whitelist check for webhook path: {Path}, IP: {RemoteIp}",
                context.Request.Path, context.Connection.RemoteIpAddress);
            await _next(context);
            return;
        }

        // Check if user is authenticated
        if (!context.User.Identity?.IsAuthenticated ?? true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication required" });
            return;
        }

        // Get Discord user ID from claims
        string? discordUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(discordUserId))
        {
            _logger.LogWarning("Authenticated user has no Discord ID claim");
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid user claims" });
            return;
        }

        // Check if user is whitelisted
        if (!_whitelistService.IsWhitelisted(discordUserId))
        {
            _logger.LogWarning("Discord user {DiscordUserId} is not whitelisted", discordUserId);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { error = "Access denied: User not whitelisted" });
            return;
        }

        await _next(context);
    }
}