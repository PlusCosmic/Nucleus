using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Nucleus.Discord;

namespace Nucleus.Minecraft;

/// <summary>
/// Handles WebSocket connections for the Minecraft console.
/// Provides bidirectional communication for:
/// - Sending RCON commands to the server
/// - Receiving real-time server log output
/// </summary>
public class ConsoleWebSocketHandler
{
    private readonly RconService _rconService;
    private readonly LogTailerService _logTailerService;
    private readonly MinecraftStatements _statements;
    private readonly DiscordStatements _discordStatements;
    private readonly ILogger<ConsoleWebSocketHandler> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ConsoleWebSocketHandler(
        RconService rconService,
        LogTailerService logTailerService,
        MinecraftStatements statements,
        DiscordStatements discordStatements,
        ILogger<ConsoleWebSocketHandler> logger)
    {
        _rconService = rconService;
        _logTailerService = logTailerService;
        _statements = statements;
        _discordStatements = discordStatements;
        _logger = logger;
    }

    public async Task HandleAsync(WebSocket webSocket, ClaimsPrincipal user, CancellationToken ct)
    {
        string? discordId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId == null)
        {
            await CloseWithErrorAsync(webSocket, "Unauthorized", ct);
            return;
        }

        DiscordStatements.DiscordUserRow? discordUser = await _discordStatements.GetUserByDiscordId(discordId);
        if (discordUser == null)
        {
            await CloseWithErrorAsync(webSocket, "User not found", ct);
            return;
        }

        Guid userId = discordUser.Id;
        _logger.LogInformation("WebSocket console connected for user {UserId}", userId);

        // Subscribe to log stream
        Guid subscriptionId = _logTailerService.Subscribe(out Channel<LogTailerService.LogEntry> logChannel);

        try
        {
            // Send recent log history first
            List<LogTailerService.LogEntry> recentLogs = await _logTailerService.GetRecentLinesAsync(50);
            foreach (var entry in recentLogs)
            {
                await SendLogEntryAsync(webSocket, entry, ct);
            }

            // Send connected message
            await SendMessageAsync(webSocket, new WsMessage
            {
                Type = "connected",
                Message = "Connected to Minecraft console"
            }, ct);

            // Start concurrent tasks for reading commands and streaming logs
            using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            Task readTask = ReadCommandsAsync(webSocket, userId, linkedCts.Token);
            Task writeTask = StreamLogsAsync(webSocket, logChannel, linkedCts.Token);

            // Wait for either task to complete (connection closed or error)
            await Task.WhenAny(readTask, writeTask);

            // Cancel the other task
            await linkedCts.CancelAsync();
        }
        catch (OperationCanceledException)
        {
            // Normal disconnection
        }
        catch (WebSocketException ex)
        {
            _logger.LogWarning(ex, "WebSocket error for user {UserId}", userId);
        }
        finally
        {
            _logTailerService.Unsubscribe(subscriptionId);
            _logger.LogInformation("WebSocket console disconnected for user {UserId}", userId);

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", CancellationToken.None);
            }
        }
    }

    private async Task ReadCommandsAsync(WebSocket webSocket, Guid userId, CancellationToken ct)
    {
        byte[] buffer = new byte[4096];

        while (webSocket.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            using MemoryStream ms = new();

            do
            {
                result = await webSocket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Text)
            {
                string json = Encoding.UTF8.GetString(ms.ToArray());
                await ProcessCommandAsync(webSocket, json, userId, ct);
            }
        }
    }

    private async Task ProcessCommandAsync(WebSocket webSocket, string json, Guid userId, CancellationToken ct)
    {
        try
        {
            WsMessage? message = JsonSerializer.Deserialize<WsMessage>(json, JsonOptions);
            if (message?.Type != "command" || string.IsNullOrWhiteSpace(message.Command))
            {
                return;
            }

            string command = message.Command;
            _logger.LogInformation("User {UserId} executing command: {Command}", userId, command);

            try
            {
                string response = await _rconService.SendCommandAsync(command);
                await _statements.LogCommand(userId, command, response, true, null);

                await SendMessageAsync(webSocket, new WsMessage
                {
                    Type = "response",
                    Command = command,
                    Response = response,
                    Success = true,
                    Timestamp = DateTimeOffset.UtcNow
                }, ct);
            }
            catch (Exception ex)
            {
                await _statements.LogCommand(userId, command, null, false, ex.Message);

                await SendMessageAsync(webSocket, new WsMessage
                {
                    Type = "response",
                    Command = command,
                    Response = null,
                    Error = ex.Message,
                    Success = false,
                    Timestamp = DateTimeOffset.UtcNow
                }, ct);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON received from client");
        }
    }

    private async Task StreamLogsAsync(WebSocket webSocket, Channel<LogTailerService.LogEntry> logChannel, CancellationToken ct)
    {
        await foreach (var entry in logChannel.Reader.ReadAllAsync(ct))
        {
            if (webSocket.State != WebSocketState.Open)
                break;

            await SendLogEntryAsync(webSocket, entry, ct);
        }
    }

    private async Task SendLogEntryAsync(WebSocket webSocket, LogTailerService.LogEntry entry, CancellationToken ct)
    {
        await SendMessageAsync(webSocket, new WsMessage
        {
            Type = "log",
            Text = entry.Text,
            Level = entry.Level.ToString().ToLowerInvariant(),
            Timestamp = entry.Timestamp
        }, ct);
    }

    private static async Task SendMessageAsync(WebSocket webSocket, WsMessage message, CancellationToken ct)
    {
        if (webSocket.State != WebSocketState.Open)
            return;

        string json = JsonSerializer.Serialize(message, JsonOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static async Task CloseWithErrorAsync(WebSocket webSocket, string error, CancellationToken ct)
    {
        await SendMessageAsync(webSocket, new WsMessage
        {
            Type = "error",
            Error = error
        }, ct);
        await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, error, ct);
    }

    public class WsMessage
    {
        public string Type { get; set; } = "";
        public string? Command { get; set; }
        public string? Response { get; set; }
        public string? Error { get; set; }
        public string? Message { get; set; }
        public string? Text { get; set; }
        public string? Level { get; set; }
        public bool? Success { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
