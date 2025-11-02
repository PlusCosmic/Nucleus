using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Nucleus.Repository;

namespace Nucleus.Auth;

public static class AuthEndpoints
{
    private static string _frontendOrigin = "http://localhost:5173";
    
    public static void MapAuthEndpoints(this WebApplication app)
    {
        string? frontendOrigin = app.Configuration["FrontendOrigin"];
        if (frontendOrigin != null)
        {
            _frontendOrigin = frontendOrigin;
        }

        var group = app.MapGroup("auth");
        
        group.MapGet("discord/login", Login).WithName("Login");
        group.MapGet("post-login-redirect", PostLoginRedirect).WithName("PostLoginRedirect");
        group.MapPost("logout", Logout).WithName("Logout");
    }
    
    public static IResult Login(string? returnUrl)
    {
        // Validate the return URL to prevent open redirect vulnerabilities
        if (!string.IsNullOrEmpty(returnUrl) && !IsValidReturnUrl(returnUrl))
        {
            returnUrl = null;
        }
        
        var props = new AuthenticationProperties
        {
            RedirectUri = "/auth/post-login-redirect" + (returnUrl != null ? $"?returnUrl={Uri.EscapeDataString(returnUrl)}" : "")
        };
        return Results.Challenge(props, new[] { "Discord" });
    }

    public static async Task<IResult> PostLoginRedirect(HttpContext ctx, string? returnUrl,
        ClaimsPrincipal user)
    {
        // Check if the user is already in the database
        var discordId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException("No Discord ID");
        var username = user.FindFirstValue(ClaimTypes.Name) ?? throw new InvalidOperationException("No Discord username");
        /*if (!dbContext.DiscordUsers.Any(u => u.DiscordId == discordId))
        {
            await dbContext.DiscordUsers.AddAsync(new DiscordUser { DiscordId = discordId, Username = username });
            await dbContext.SaveChangesAsync();
        }*/
        // Validate return URL again for safety
        string redirect;
        if (!string.IsNullOrEmpty(returnUrl) && IsValidReturnUrl(returnUrl))
        {
            redirect = returnUrl;
        }
        else
        {
            redirect = _frontendOrigin;
        }
    
        ctx.Response.Redirect(redirect);
        return Results.Empty;
    }

    public static async Task Logout(HttpContext ctx)
    {
        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
    
    private static bool IsValidReturnUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
    
        // Only allow redirect to trusted domains
        if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
            return true;
    
        if (uri.Host == "pluscosmic.dev" || uri.Host.EndsWith(".pluscosmic.dev"))
            return true;
    
        if (uri.Host.EndsWith("pluscosmicdashboard.pages.dev"))
            return true;
    
        return false;
    }
}