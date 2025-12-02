using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.Discord;
using Nucleus.Exceptions;
using Nucleus.Minecraft.Models;

namespace Nucleus.Minecraft;

public static class MinecraftEndpoints
{
    public static void MapMinecraftEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("minecraft").RequireAuthorization();

        // Status
        group.MapGet("status", GetStatus).WithName("GetMinecraftStatus");
        group.MapGet("players", GetPlayers).WithName("GetMinecraftPlayers");

        // Console (REST)
        group.MapPost("console/command", SendCommand).WithName("SendMinecraftCommand");
        group.MapGet("console/history", GetCommandHistory).WithName("GetCommandHistory");

        // Console (WebSocket) - requires authorization
        group.MapGet("console/live", HandleConsoleWebSocket).WithName("ConsoleWebSocket");

        // Files
        group.MapGet("files", ListFiles).WithName("ListMinecraftFiles");
        group.MapGet("files/content", GetFileContent).WithName("GetMinecraftFileContent");
        group.MapPut("files/content", SaveFileContent).WithName("SaveMinecraftFileContent");
        group.MapDelete("files", DeleteFile).WithName("DeleteMinecraftFile");
        group.MapPost("files/mkdir", CreateDirectory).WithName("CreateMinecraftDirectory");
    }

    private static async Task HandleConsoleWebSocket(
        HttpContext context,
        ConsoleWebSocketHandler handler,
        CancellationToken ct)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket connection required", ct);
            return;
        }

        ClaimsPrincipal user = context.User;
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await handler.HandleAsync(webSocket, user, ct);
    }

    private static async Task<Results<Ok<ServerStatus>, UnauthorizedHttpResult>> GetStatus(
        MinecraftStatusService statusService,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            return TypedResults.Unauthorized();
        }

        ServerStatus status = await statusService.GetServerStatusAsync();
        return TypedResults.Ok(status);
    }

    private static async Task<Results<Ok<List<OnlinePlayer>>, UnauthorizedHttpResult>> GetPlayers(
        RconService rconService,
        ClaimsPrincipal user)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            return TypedResults.Unauthorized();
        }

        List<OnlinePlayer> players = await rconService.GetOnlinePlayersAsync();
        return TypedResults.Ok(players);
    }

    private static async Task<Results<Ok<RconResponse>, UnauthorizedHttpResult, BadRequest<string>>> SendCommand(
        RconService rconService,
        MinecraftStatements statements,
        DiscordStatements discordStatements,
        ClaimsPrincipal user,
        RconCommand request)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return TypedResults.BadRequest("Command cannot be empty");
        }

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        try
        {
            string response = await rconService.SendCommandAsync(request.Command);
            await statements.LogCommand(userId, request.Command, response, true, null);
            return TypedResults.Ok(new RconResponse(true, response, null));
        }
        catch (Exception ex)
        {
            await statements.LogCommand(userId, request.Command, null, false, ex.Message);
            return TypedResults.Ok(new RconResponse(false, null, ex.Message));
        }
    }

    private static async Task<Results<Ok<List<CommandLogEntry>>, UnauthorizedHttpResult>> GetCommandHistory(
        MinecraftStatements statements,
        DiscordStatements discordStatements,
        ClaimsPrincipal user,
        int limit = 50)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            return TypedResults.Unauthorized();
        }

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        List<CommandLogEntry> history = await statements.GetRecentCommands(userId, Math.Min(limit, 100));
        return TypedResults.Ok(history);
    }

    private static async Task<Results<Ok<DirectoryListing>, UnauthorizedHttpResult, BadRequest<string>, NotFound<string>>> ListFiles(
        FileService fileService,
        MinecraftStatements statements,
        DiscordStatements discordStatements,
        ClaimsPrincipal user,
        string path = "")
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            return TypedResults.Unauthorized();
        }

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        try
        {
            DirectoryListing listing = fileService.ListDirectory(path);
            await statements.LogFileOperation(userId, "list", path, true, null);
            return TypedResults.Ok(listing);
        }
        catch (DirectoryNotFoundException)
        {
            await statements.LogFileOperation(userId, "list", path, false, "Directory not found");
            return TypedResults.NotFound($"Directory not found: {path}");
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(userId, "list", path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<string>, UnauthorizedHttpResult, BadRequest<string>, NotFound<string>>> GetFileContent(
        FileService fileService,
        MinecraftStatements statements,
        DiscordStatements discordStatements,
        ClaimsPrincipal user,
        string path)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return TypedResults.BadRequest("Path cannot be empty");
        }

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        try
        {
            string content = await fileService.ReadFileAsync(path);
            await statements.LogFileOperation(userId, "read", path, true, null);
            return TypedResults.Ok(content);
        }
        catch (FileNotFoundException)
        {
            await statements.LogFileOperation(userId, "read", path, false, "File not found");
            return TypedResults.NotFound($"File not found: {path}");
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(userId, "read", path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await statements.LogFileOperation(userId, "read", path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult, BadRequest<string>>> SaveFileContent(
        FileService fileService,
        MinecraftStatements statements,
        DiscordStatements discordStatements,
        ClaimsPrincipal user,
        SaveFileRequest request)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return TypedResults.BadRequest("Path cannot be empty");
        }

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        try
        {
            await fileService.WriteFileAsync(request.Path, request.Content ?? "");
            await statements.LogFileOperation(userId, "write", request.Path, true, null);
            return TypedResults.Ok();
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(userId, "write", request.Path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await statements.LogFileOperation(userId, "write", request.Path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult, BadRequest<string>, NotFound<string>>> DeleteFile(
        FileService fileService,
        MinecraftStatements statements,
        DiscordStatements discordStatements,
        ClaimsPrincipal user,
        string path)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return TypedResults.BadRequest("Path cannot be empty");
        }

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        try
        {
            fileService.DeleteFile(path);
            await statements.LogFileOperation(userId, "delete", path, true, null);
            return TypedResults.Ok();
        }
        catch (FileNotFoundException)
        {
            await statements.LogFileOperation(userId, "delete", path, false, "File not found");
            return TypedResults.NotFound($"File not found: {path}");
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(userId, "delete", path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok, UnauthorizedHttpResult, BadRequest<string>>> CreateDirectory(
        FileService fileService,
        MinecraftStatements statements,
        DiscordStatements discordStatements,
        ClaimsPrincipal user,
        CreateDirectoryRequest request)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is null)
        {
            return TypedResults.Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return TypedResults.BadRequest("Path cannot be empty");
        }

        DiscordStatements.DiscordUserRow discordUser = await discordStatements.GetUserByDiscordId(discordId)
                                                       ?? throw new UnauthorizedException("User not found");
        Guid userId = discordUser.Id;

        try
        {
            fileService.CreateDirectory(request.Path);
            await statements.LogFileOperation(userId, "mkdir", request.Path, true, null);
            return TypedResults.Ok();
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(userId, "mkdir", request.Path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await statements.LogFileOperation(userId, "mkdir", request.Path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public sealed record SaveFileRequest(string Path, string? Content);
    public sealed record CreateDirectoryRequest(string Path);
}
