using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Nucleus.ApexLegends;
using Nucleus.Auth;
using Nucleus.Discord;
using Nucleus.Endpoints;
using Nucleus.Links;
using Nucleus.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MapService>();
builder.Services.AddScoped<LinksService>();
builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:5173";

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                    return false;
                
                // Allow localhost on any port
                if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                    return true;
                
                // Allow *.pluscosmic.dev
                if (uri.Host == "pluscosmic.dev" || uri.Host.EndsWith(".pluscosmic.dev"))
                    return true;
                
                // Allow previews from cloudflare
                if (uri.Host.EndsWith("pluscosmicdashboard.pages.dev"))
                    return true;
                
                return false;
            })
            .AllowAnyMethod()
            .AllowCredentials()
            .AllowAnyHeader());
});

builder.ConfigureDiscordAuth();

builder.Services.AddDbContextPool<NucleusDbContext>(opt => 
    opt.UseNpgsql(builder.Configuration["DatabaseConnectionString"] ?? throw new InvalidOperationException("DatabaseConnectionString not configured")));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapUserEndpoints();
app.MapApexEndpoints();
app.MapAuthEndpoints();
app.MapLinksEndpoints();
app.Run();