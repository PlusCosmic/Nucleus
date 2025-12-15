using System.Net;
using System.Text.RegularExpressions;
using CoreRCON;
using Nucleus.Minecraft.Models;

namespace Nucleus.Minecraft;

public partial class RconService(IConfiguration configuration, ILogger<RconService> logger) : IDisposable
{
    private RCON? _rconClient;
    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private bool _disposed;

    private async Task<RCON> GetConnectedClientAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            if (_rconClient != null && _rconClient.Connected)
            {
                return _rconClient;
            }

            string host = configuration["Minecraft:RconHost"] ?? "localhost";
            int port = int.Parse(configuration["Minecraft:RconPort"] ?? "25575");
            string password = GetRconPassword();

            logger.LogInformation("Connecting to RCON at {Host}:{Port}", host, port);

            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host);
            IPAddress ip = addresses.First();

            _rconClient = new RCON(ip, (ushort)port, password);
            await _rconClient.ConnectAsync();
            logger.LogInformation("Successfully connected to RCON");

            return _rconClient;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<string> SendCommandAsync(string command)
    {
        try
        {
            RCON client = await GetConnectedClientAsync();
            string response = await client.SendCommandAsync(command);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send RCON command: {Command}", command);
            _rconClient?.Dispose();
            _rconClient = null;
            throw;
        }
    }

    public async Task<List<OnlinePlayer>> GetOnlinePlayersAsync()
    {
        try
        {
            string response = await SendCommandAsync("list");
            return ParsePlayerList(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get online players");
            return new List<OnlinePlayer>();
        }
    }

    private static List<OnlinePlayer> ParsePlayerList(string listResponse)
    {
        List<OnlinePlayer> players = new();

        // Expected format: "There are X of a max of Y players online: player1, player2, player3"
        // Or: "There are 0 of a max of 20 players online:"
        Match match = PlayerListRegex().Match(listResponse);
        if (!match.Success)
        {
            return players;
        }

        string playersString = match.Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(playersString))
        {
            return players;
        }

        string[] playerNames = playersString.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (string playerName in playerNames)
        {
            string trimmedName = playerName.Trim();
            if (!string.IsNullOrEmpty(trimmedName))
            {
                // Note: RCON list command doesn't provide UUIDs, so we generate a placeholder
                // In a real implementation, you might query player UUIDs from a different source
                players.Add(new OnlinePlayer(trimmedName, Guid.Empty));
            }
        }

        return players;
    }

    [GeneratedRegex(@"online:\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerListRegex();

    private string GetRconPassword()
    {
        string? dataPath = configuration["Minecraft:DataPath"];
        if (!string.IsNullOrWhiteSpace(dataPath))
        {
            string serverPropertiesPath = Path.Combine(dataPath, "server.properties");
            if (File.Exists(serverPropertiesPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(serverPropertiesPath);
                    foreach (string line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed.StartsWith("rcon.password=", StringComparison.OrdinalIgnoreCase))
                        {
                            string password = trimmed["rcon.password=".Length..];
                            logger.LogDebug("RCON password read from server.properties");
                            return password;
                        }
                    }
                    logger.LogWarning("rcon.password not found in server.properties, falling back to configuration");
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read server.properties, falling back to configuration");
                }
            }
            else
            {
                logger.LogDebug("server.properties not found at {Path}, falling back to configuration", serverPropertiesPath);
            }
        }

        return configuration["Minecraft:RconPassword"] ?? "";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _rconClient?.Dispose();
        _connectionLock.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
