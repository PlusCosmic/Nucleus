using System.Collections.Concurrent;
using System.Text;
using System.Threading.Channels;

namespace Nucleus.Minecraft;

/// <summary>
/// Tails the Minecraft server log file and broadcasts new lines to subscribers.
/// Runs as a background service that continuously watches for log changes.
/// </summary>
public class LogTailerService : BackgroundService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<LogTailerService> _logger;
    private readonly ConcurrentDictionary<Guid, Channel<LogEntry>> _subscribers = new();
    private string? _logFilePath;

    public LogTailerService(IConfiguration configuration, ILogger<LogTailerService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public record LogEntry(string Text, DateTimeOffset Timestamp, LogLevel Level);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string dataPath = _configuration["Minecraft:DataPath"] ?? "./data";
        _logFilePath = Path.Combine(dataPath, "logs", "latest.log");

        _logger.LogInformation("LogTailerService starting, watching: {LogPath}", _logFilePath);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TailLogFileAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error tailing log file, retrying in 5 seconds");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("LogTailerService stopping");
    }

    private async Task TailLogFileAsync(CancellationToken ct)
    {
        if (!File.Exists(_logFilePath))
        {
            _logger.LogWarning("Log file not found: {Path}, waiting...", _logFilePath);
            await Task.Delay(TimeSpan.FromSeconds(10), ct);
            return;
        }

        await using FileStream fs = new(
            _logFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

        // Start from end of file
        fs.Seek(0, SeekOrigin.End);

        using StreamReader reader = new(fs, Encoding.UTF8);

        while (!ct.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(ct);

            if (line != null)
            {
                LogEntry entry = ParseLogLine(line);
                await BroadcastAsync(entry);
            }
            else
            {
                // No new content, wait a bit before checking again
                await Task.Delay(100, ct);
            }
        }
    }

    private static LogEntry ParseLogLine(string line)
    {
        // Minecraft log format: [HH:mm:ss] [Thread/LEVEL]: Message
        // Example: [12:34:56] [Server thread/INFO]: Player joined the game
        LogLevel level = LogLevel.Information;

        if (line.Contains("/WARN]") || line.Contains("/WARNING]"))
            level = LogLevel.Warning;
        else if (line.Contains("/ERROR]") || line.Contains("/SEVERE]") || line.Contains("/FATAL]"))
            level = LogLevel.Error;
        else if (line.Contains("/DEBUG]"))
            level = LogLevel.Debug;

        return new LogEntry(line, DateTimeOffset.UtcNow, level);
    }

    private async Task BroadcastAsync(LogEntry entry)
    {
        foreach (var (id, channel) in _subscribers)
        {
            try
            {
                // Non-blocking write, drop if channel is full
                channel.Writer.TryWrite(entry);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write to subscriber {Id}", id);
            }
        }

        await Task.CompletedTask;
    }

    public Guid Subscribe(out Channel<LogEntry> channel)
    {
        Guid id = Guid.NewGuid();
        channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });
        _subscribers.TryAdd(id, channel);
        _logger.LogDebug("Subscriber {Id} added, total: {Count}", id, _subscribers.Count);
        return id;
    }

    public void Unsubscribe(Guid id)
    {
        if (_subscribers.TryRemove(id, out var channel))
        {
            channel.Writer.TryComplete();
            _logger.LogDebug("Subscriber {Id} removed, total: {Count}", id, _subscribers.Count);
        }
    }

    /// <summary>
    /// Gets recent log lines by reading the tail of the log file.
    /// Used to provide initial context when a client connects.
    /// </summary>
    public async Task<List<LogEntry>> GetRecentLinesAsync(int count = 100)
    {
        if (_logFilePath == null || !File.Exists(_logFilePath))
            return new List<LogEntry>();

        try
        {
            string[] allLines = await File.ReadAllLinesAsync(_logFilePath);
            return allLines
                .TakeLast(count)
                .Select(ParseLogLine)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read recent log lines");
            return new List<LogEntry>();
        }
    }
}
