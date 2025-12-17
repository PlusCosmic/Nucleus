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

        // Server Management
        group.MapGet("servers", GetServers).WithName("GetMinecraftServers");
        group.MapPost("servers", CreateServer).WithName("CreateMinecraftServer");
        group.MapGet("servers/{serverId:guid}", GetServer).WithName("GetMinecraftServer");
        group.MapPut("servers/{serverId:guid}", UpdateServer).WithName("UpdateMinecraftServer");
        group.MapDelete("servers/{serverId:guid}", DeleteServer).WithName("DeleteMinecraftServer");

        // Server-scoped endpoints
        RouteGroupBuilder serverGroup = group.MapGroup("servers/{serverId:guid}");

        // Status
        serverGroup.MapGet("status", GetStatus).WithName("GetMinecraftStatus");
        serverGroup.MapGet("players", GetPlayers).WithName("GetMinecraftPlayers");

        // Console (REST)
        serverGroup.MapPost("console/command", SendCommand).WithName("SendMinecraftCommand");
        serverGroup.MapGet("console/history", GetCommandHistory).WithName("GetCommandHistory");

        // Console (WebSocket) - uses HttpContext directly for WebSocket upgrade
        serverGroup.MapGet("console/live", HandleConsoleWebSocket).WithName("ConsoleWebSocket");

        // Files
        serverGroup.MapGet("files", ListFiles).WithName("ListMinecraftFiles");
        serverGroup.MapGet("files/content", GetFileContent).WithName("GetMinecraftFileContent");
        serverGroup.MapPut("files/content", SaveFileContent).WithName("SaveMinecraftFileContent");
        serverGroup.MapDelete("files", DeleteFile).WithName("DeleteMinecraftFile");
        serverGroup.MapPost("files/mkdir", CreateDirectory).WithName("CreateMinecraftDirectory");

        // Backups
        serverGroup.MapGet("backups", GetBackupStatus).WithName("GetBackupStatus");
        serverGroup.MapPost("backups/sync", TriggerBackupSync).WithName("TriggerBackupSync");
    }

    #region Server Management

    private static async Task<Ok<List<MinecraftServer>>> GetServers(
        MinecraftStatements statements,
        AuthenticatedUser user)
    {
        List<MinecraftServer> servers = await statements.GetServersByOwnerAsync(user.Id);
        return TypedResults.Ok(servers);
    }

    private static async Task<Results<Ok<MinecraftServer>, NotFound, ForbidHttpResult>> GetServer(
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId)
    {
        MinecraftServer? server = await statements.GetServerByIdAsync(serverId);

        if (server is null)
            return TypedResults.NotFound();

        if (server.OwnerId != user.Id)
            return TypedResults.Forbid();

        return TypedResults.Ok(server);
    }

    private static async Task<Results<Created<MinecraftServer>, BadRequest<string>, Conflict<string>>> CreateServer(
        MinecraftStatements statements,
        AuthenticatedUser user,
        CreateMinecraftServerRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return TypedResults.BadRequest("Server name cannot be empty");

        if (string.IsNullOrWhiteSpace(request.ContainerName))
            return TypedResults.BadRequest("Container name cannot be empty");

        if (await statements.ContainerNameExistsAsync(request.ContainerName))
            return TypedResults.Conflict($"Container name '{request.ContainerName}' is already in use");

        MinecraftServer server = await statements.CreateServerAsync(user.Id, request);
        return TypedResults.Created($"/minecraft/servers/{server.Id}", server);
    }

    private static async Task<Results<Ok<MinecraftServer>, NotFound, ForbidHttpResult, BadRequest<string>, Conflict<string>>> UpdateServer(
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId,
        UpdateMinecraftServerRequest request)
    {
        MinecraftServer? existing = await statements.GetServerByIdAsync(serverId);

        if (existing is null)
            return TypedResults.NotFound();

        if (existing.OwnerId != user.Id)
            return TypedResults.Forbid();

        if (request.ContainerName is not null &&
            request.ContainerName != existing.ContainerName &&
            await statements.ContainerNameExistsAsync(request.ContainerName, serverId))
        {
            return TypedResults.Conflict($"Container name '{request.ContainerName}' is already in use");
        }

        MinecraftServer? updated = await statements.UpdateServerAsync(serverId, request);
        return TypedResults.Ok(updated!);
    }

    private static async Task<Results<NoContent, NotFound, ForbidHttpResult>> DeleteServer(
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId)
    {
        MinecraftServer? server = await statements.GetServerByIdAsync(serverId);

        if (server is null)
            return TypedResults.NotFound();

        if (server.OwnerId != user.Id)
            return TypedResults.Forbid();

        await statements.DeleteServerAsync(serverId);
        return TypedResults.NoContent();
    }

    #endregion

    #region Helpers

    private static async Task<MinecraftServer?> ValidateServerOwnership(
        MinecraftStatements statements,
        Guid serverId,
        Guid userId)
    {
        MinecraftServer? server = await statements.GetServerByIdAsync(serverId);
        if (server is null || server.OwnerId != userId)
            return null;
        return server;
    }

    #endregion

    #region Console & Status

    private static async Task HandleConsoleWebSocket(
        HttpContext context,
        ConsoleWebSocketHandler handler,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId,
        CancellationToken ct)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsync("WebSocket connection required", ct);
            return;
        }

        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await handler.HandleAsync(webSocket, context.User, server, ct);
    }

    private static async Task<Results<Ok<ServerStatus>, NotFound, ForbidHttpResult>> GetStatus(
        MinecraftStatusService statusService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        ServerStatus status = await statusService.GetServerStatusAsync(server);
        return TypedResults.Ok(status);
    }

    private static async Task<Results<Ok<List<OnlinePlayer>>, NotFound, ForbidHttpResult>> GetPlayers(
        RconService rconService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        List<OnlinePlayer> players = await rconService.GetOnlinePlayersAsync(server);
        return TypedResults.Ok(players);
    }

    private static async Task<Results<Ok<RconResponse>, NotFound, ForbidHttpResult, BadRequest<string>>> SendCommand(
        RconService rconService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId,
        RconCommand request)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(request.Command))
            return TypedResults.BadRequest("Command cannot be empty");

        try
        {
            string response = await rconService.SendCommandAsync(server, request.Command);
            await statements.LogCommand(user.Id, request.Command, response, true, null, serverId);
            return TypedResults.Ok(new RconResponse(true, response, null));
        }
        catch (Exception ex)
        {
            await statements.LogCommand(user.Id, request.Command, null, false, ex.Message, serverId);
            return TypedResults.Ok(new RconResponse(false, null, ex.Message));
        }
    }

    private static async Task<Results<Ok<List<CommandLogEntry>>, NotFound, ForbidHttpResult>> GetCommandHistory(
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId,
        int limit = 50)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        List<CommandLogEntry> history = await statements.GetRecentCommands(user.Id, Math.Min(limit, 100), serverId);
        return TypedResults.Ok(history);
    }

    #endregion

    #region File Operations

    private static async Task<Results<Ok<DirectoryListing>, NotFound, ForbidHttpResult, BadRequest<string>>> ListFiles(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId,
        string path = "")
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        try
        {
            DirectoryListing listing = fileService.ListDirectory(server, path);
            await statements.LogFileOperation(user.Id, "list", path, true, null, serverId);
            return TypedResults.Ok(listing);
        }
        catch (DirectoryNotFoundException)
        {
            await statements.LogFileOperation(user.Id, "list", path, false, "Directory not found", serverId);
            return TypedResults.BadRequest($"Directory not found: {path}");
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "list", path, false, ex.Message, serverId);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok<string>, NotFound, ForbidHttpResult, BadRequest<string>>> GetFileContent(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId,
        string path)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(path))
            return TypedResults.BadRequest("Path cannot be empty");

        try
        {
            string content = await fileService.ReadFileAsync(server, path);
            await statements.LogFileOperation(user.Id, "read", path, true, null, serverId);
            return TypedResults.Ok(content);
        }
        catch (FileNotFoundException)
        {
            await statements.LogFileOperation(user.Id, "read", path, false, "File not found", serverId);
            return TypedResults.BadRequest($"File not found: {path}");
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "read", path, false, ex.Message, serverId);
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await statements.LogFileOperation(user.Id, "read", path, false, ex.Message, serverId);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ForbidHttpResult, BadRequest<string>>> SaveFileContent(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId,
        SaveFileRequest request)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(request.Path))
            return TypedResults.BadRequest("Path cannot be empty");

        try
        {
            await fileService.WriteFileAsync(server, request.Path, request.Content ?? "");
            await statements.LogFileOperation(user.Id, "write", request.Path, true, null, serverId);
            return TypedResults.Ok();
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "write", request.Path, false, ex.Message, serverId);
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await statements.LogFileOperation(user.Id, "write", request.Path, false, ex.Message, serverId);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ForbidHttpResult, BadRequest<string>>> DeleteFile(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId,
        string path)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(path))
            return TypedResults.BadRequest("Path cannot be empty");

        try
        {
            fileService.DeleteFile(server, path);
            await statements.LogFileOperation(user.Id, "delete", path, true, null, serverId);
            return TypedResults.Ok();
        }
        catch (FileNotFoundException)
        {
            await statements.LogFileOperation(user.Id, "delete", path, false, "File not found", serverId);
            return TypedResults.BadRequest($"File not found: {path}");
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "delete", path, false, ex.Message, serverId);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    private static async Task<Results<Ok, NotFound, ForbidHttpResult, BadRequest<string>>> CreateDirectory(
        FileService fileService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId,
        CreateDirectoryRequest request)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        if (string.IsNullOrWhiteSpace(request.Path))
            return TypedResults.BadRequest("Path cannot be empty");

        try
        {
            fileService.CreateDirectory(server, request.Path);
            await statements.LogFileOperation(user.Id, "mkdir", request.Path, true, null, serverId);
            return TypedResults.Ok();
        }
        catch (System.Security.SecurityException ex)
        {
            await statements.LogFileOperation(user.Id, "mkdir", request.Path, false, ex.Message, serverId);
            return TypedResults.BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await statements.LogFileOperation(user.Id, "mkdir", request.Path, false, ex.Message, serverId);
            return TypedResults.BadRequest(ex.Message);
        }
    }

    #endregion

    #region Backups

    private static async Task<Results<Ok<BackupListResult>, NotFound, ForbidHttpResult>> GetBackupStatus(
        BackupService backupService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        BackupListResult status = await backupService.GetBackupStatusAsync(server);
        return TypedResults.Ok(status);
    }

    private static async Task<Results<Ok<BackupSyncResult>, NotFound, ForbidHttpResult>> TriggerBackupSync(
        BackupService backupService,
        MinecraftStatements statements,
        AuthenticatedUser user,
        Guid serverId)
    {
        MinecraftServer? server = await ValidateServerOwnership(statements, serverId, user.Id);
        if (server is null)
            return TypedResults.NotFound();

        BackupSyncResult result = await backupService.SyncBackupsAsync(server);
        return TypedResults.Ok(result);
    }

    #endregion

    public sealed record SaveFileRequest(string Path, string? Content);
    public sealed record CreateDirectoryRequest(string Path);
}
