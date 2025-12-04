using System.Net.WebSockets;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Nucleus.Auth;
using Nucleus.Minecraft.Models;

namespace Nucleus.Minecraft;

public static class MinecraftEndpoints
{
    public static void MapMinecraftEndpoints(this WebApplication app)
    {
        RouteGroupBuilder group = app.MapGroup("minecraft")
            .RequireAuthorization();

        // Status
        group.MapGet("status", GetStatus).WithName("GetMinecraftStatus");
        group.MapGet("players", GetPlayers).WithName("GetMinecraftPlayers");

        // Console (REST)
        group.MapPost("console/command", SendCommand).WithName("SendMinecraftCommand");
        group.MapGet("console/history", GetCommandHistory).WithName("GetCommandHistory");

        // Console (WebSocket) - uses HttpContext directly for WebSocket upgrade
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
        AuthenticatedUser user,
        CancellationToken ct)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket connection required", ct);
            return;
        }

        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await handler.HandleAsync(webSocket, context.User, ct);
    }

    private static async Task<Ok<ServerStatus>> GetStatus(
        MinecraftStatusService statusService,
        AuthenticatedUser user)
    {
        ServerStatus status = await statusService.GetServerStatusAsync();
        return TypedResults.Ok(status);
    }

    private static async Task<Ok<List<OnlinePlayer>>> GetPlayers(
        RconService rconService,
        AuthenticatedUser user)
    {
        List<OnlinePlayer> players = await rconService.GetOnlinePlayersAsync();
        return TypedResults.Ok(players);
    }

    private static async Task<Results<Ok<RconResponse>, BadRequest<string>>> SendCommand(
        RconService rconService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        RconCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return TypedResults.BadRequest("Command cannot be empty");
        }

        try
        {
            string response = await rconService.SendCommandAsync(request.Command);
            await statements.LogCommand(user.Id, request.Command, response, true, null);
            return TypedResults.Ok(new RconResponse(true, response, null));
        }
        catch (Exception ex)
        {
            await statements.LogCommand(user.Id, request.Command, null, false, ex.Message);
            return TypedResults.Ok(new RconResponse(false, null, ex.Message));
        }
    }

    private static async Task<Ok<List<CommandLogEntry>>> GetCommandHistory(
        MinecraftStatements statements,
        AuthenticatedUser user,
        int limit = 50)
    {
        List<CommandLogEntry> history = await statements.GetRecentCommands(user.Id, Math.Min(limit, 100));
        return TypedResults.Ok(history);
    }

    private static async Task<Results<Ok<DirectoryListing>, BadRequest<string>, NotFound<string>>> ListFiles(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        string path = "")
    {
        try
        {
            DirectoryListing listing = fileService.ListDirectory(path);
            await statements.LogFileOperation(user.Id, "list", path, true, null);
            return TypedResults.Ok(listing);
        }
        catch (DirectoryNotFoundException)
        {
            await statements.LogFileOperation(user.Id, "list", path, false, "Directory not found");
            return TypedResults.NotFound($"Directory not found: {path}");
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "list", path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<string>, BadRequest<string>, NotFound<string>>> GetFileContent(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return TypedResults.BadRequest("Path cannot be empty");
        }

        try
        {
            string content = await fileService.ReadFileAsync(path);
            await statements.LogFileOperation(user.Id, "read", path, true, null);
            return TypedResults.Ok(content);
        }
        catch (FileNotFoundException)
        {
            await statements.LogFileOperation(user.Id, "read", path, false, "File not found");
            return TypedResults.NotFound($"File not found: {path}");
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "read", path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await statements.LogFileOperation(user.Id, "read", path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok, BadRequest<string>>> SaveFileContent(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        SaveFileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return TypedResults.BadRequest("Path cannot be empty");
        }

        try
        {
            await fileService.WriteFileAsync(request.Path, request.Content ?? "");
            await statements.LogFileOperation(user.Id, "write", request.Path, true, null);
            return TypedResults.Ok();
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "write", request.Path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await statements.LogFileOperation(user.Id, "write", request.Path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok, BadRequest<string>, NotFound<string>>> DeleteFile(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return TypedResults.BadRequest("Path cannot be empty");
        }

        try
        {
            fileService.DeleteFile(path);
            await statements.LogFileOperation(user.Id, "delete", path, true, null);
            return TypedResults.Ok();
        }
        catch (FileNotFoundException)
        {
            await statements.LogFileOperation(user.Id, "delete", path, false, "File not found");
            return TypedResults.NotFound($"File not found: {path}");
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "delete", path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok, BadRequest<string>>> CreateDirectory(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        CreateDirectoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Path))
        {
            return TypedResults.BadRequest("Path cannot be empty");
        }

        try
        {
            fileService.CreateDirectory(request.Path);
            await statements.LogFileOperation(user.Id, "mkdir", request.Path, true, null);
            return TypedResults.Ok();
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "mkdir", request.Path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await statements.LogFileOperation(user.Id, "mkdir", request.Path, false, ex.Message);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    public sealed record SaveFileRequest(string Path, string? Content);
    public sealed record CreateDirectoryRequest(string Path);
}
