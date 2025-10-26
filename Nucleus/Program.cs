using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Nucleus.ApexLegends;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<MapService>();
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors();
app.MapGet("/apex-legends/map-rotation", (MapService mapService) => mapService.GetMapRotation())
    .WithName("GetApexMapRotation");

app.Run();