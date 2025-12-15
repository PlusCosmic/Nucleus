using Amazon.S3;
using Amazon.S3.Model;

namespace Nucleus.Minecraft;

public class BackupService
{
    private readonly ILogger<BackupService> _logger;
    private readonly string _backupsPath;
    private readonly IAmazonS3? _s3Client;
    private readonly string? _bucketName;
    private readonly string _bucketPrefix;

    public BackupService(IConfiguration configuration, ILogger<BackupService> logger)
    {
        _logger = logger;

        string? dataPath = configuration["Minecraft:DataPath"];
        if (string.IsNullOrWhiteSpace(dataPath))
        {
            throw new InvalidOperationException("Minecraft:DataPath is not configured");
        }

        _backupsPath = Path.Combine(dataPath, "simplebackups");
        _bucketPrefix = configuration["Backblaze:BucketPrefix"] ?? "minecraft-backups/";

        string? keyId = configuration["Backblaze:KeyId"];
        string? appKey = configuration["Backblaze:ApplicationKey"];
        _bucketName = configuration["Backblaze:BucketName"];
        string? endpoint = configuration["Backblaze:Endpoint"];

        if (!string.IsNullOrWhiteSpace(keyId) &&
            !string.IsNullOrWhiteSpace(appKey) &&
            !string.IsNullOrWhiteSpace(_bucketName) &&
            !string.IsNullOrWhiteSpace(endpoint))
        {
            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                ForcePathStyle = true
            };

            _s3Client = new AmazonS3Client(keyId, appKey, config);
            _logger.LogInformation("BackupService initialized with B2 bucket: {Bucket}", _bucketName);
        }
        else
        {
            _logger.LogWarning("Backblaze B2 not configured - backup sync will be disabled");
        }
    }

    public bool IsConfigured => _s3Client != null && _bucketName != null;

    public async Task<BackupSyncResult> SyncBackupsAsync(CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return new BackupSyncResult(
                Success: false,
                Message: "Backblaze B2 is not configured",
                FilesUploaded: 0,
                FilesSkipped: 0,
                BytesUploaded: 0
            );
        }

        if (!Directory.Exists(_backupsPath))
        {
            _logger.LogWarning("Backups directory does not exist: {Path}", _backupsPath);
            return new BackupSyncResult(
                Success: false,
                Message: $"Backups directory not found: {_backupsPath}",
                FilesUploaded: 0,
                FilesSkipped: 0,
                BytesUploaded: 0
            );
        }

        try
        {
            HashSet<string> existingKeys = await GetExistingKeysAsync(cancellationToken);
            string[] localFiles = Directory.GetFiles(_backupsPath, "*", SearchOption.AllDirectories);

            int uploaded = 0;
            int skipped = 0;
            long bytesUploaded = 0;

            foreach (string localFile in localFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = Path.GetRelativePath(_backupsPath, localFile).Replace('\\', '/');
                string key = _bucketPrefix + relativePath;

                if (existingKeys.Contains(key))
                {
                    _logger.LogDebug("Skipping existing file: {Key}", key);
                    skipped++;
                    continue;
                }

                FileInfo fileInfo = new(localFile);
                await UploadFileAsync(localFile, key, cancellationToken);

                _logger.LogInformation("Uploaded: {File} ({Size:N0} bytes)", relativePath, fileInfo.Length);
                uploaded++;
                bytesUploaded += fileInfo.Length;
            }

            string message = $"Sync complete: {uploaded} uploaded, {skipped} skipped";
            _logger.LogInformation(message);

            return new BackupSyncResult(
                Success: true,
                Message: message,
                FilesUploaded: uploaded,
                FilesSkipped: skipped,
                BytesUploaded: bytesUploaded
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync backups");
            return new BackupSyncResult(
                Success: false,
                Message: $"Sync failed: {ex.Message}",
                FilesUploaded: 0,
                FilesSkipped: 0,
                BytesUploaded: 0
            );
        }
    }

    private async Task<HashSet<string>> GetExistingKeysAsync(CancellationToken cancellationToken)
    {
        HashSet<string> keys = new();

        string? continuationToken = null;
        do
        {
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = _bucketPrefix,
                ContinuationToken = continuationToken
            };

            ListObjectsV2Response response = await _s3Client!.ListObjectsV2Async(request, cancellationToken);

            foreach (S3Object obj in response.S3Objects)
            {
                keys.Add(obj.Key);
            }

            continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
        } while (continuationToken != null);

        _logger.LogDebug("Found {Count} existing files in B2", keys.Count);
        return keys;
    }

    private async Task UploadFileAsync(string localPath, string key, CancellationToken cancellationToken)
    {
        await using FileStream fileStream = File.OpenRead(localPath);

        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = key,
            InputStream = fileStream,
            ContentType = GetContentType(localPath)
        };

        await _s3Client!.PutObjectAsync(request, cancellationToken);
    }

    private static string GetContentType(string filePath)
    {
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".zip" => "application/zip",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",
            ".log" => "text/plain",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    public async Task<BackupListResult> GetBackupStatusAsync(CancellationToken cancellationToken = default)
    {
        List<BackupFileInfo> localFiles = new();
        List<BackupFileInfo> remoteFiles = new();

        if (Directory.Exists(_backupsPath))
        {
            foreach (string file in Directory.GetFiles(_backupsPath, "*", SearchOption.AllDirectories))
            {
                FileInfo info = new(file);
                string relativePath = Path.GetRelativePath(_backupsPath, file).Replace('\\', '/');
                localFiles.Add(new BackupFileInfo(relativePath, info.Length, info.LastWriteTimeUtc));
            }
        }

        if (IsConfigured)
        {
            string? continuationToken = null;
            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = _bucketPrefix,
                    ContinuationToken = continuationToken
                };

                ListObjectsV2Response response = await _s3Client!.ListObjectsV2Async(request, cancellationToken);

                foreach (S3Object obj in response.S3Objects)
                {
                    string relativePath = obj.Key.StartsWith(_bucketPrefix)
                        ? obj.Key[_bucketPrefix.Length..]
                        : obj.Key;
                    remoteFiles.Add(new BackupFileInfo(
                        relativePath,
                        obj.Size ?? 0,
                        obj.LastModified ?? DateTime.MinValue
                    ));
                }

                continuationToken = response.IsTruncated == true ? response.NextContinuationToken : null;
            } while (continuationToken != null);
        }

        HashSet<string> remoteKeys = remoteFiles.Select(f => f.Path).ToHashSet();
        int pendingCount = localFiles.Count(f => !remoteKeys.Contains(f.Path));

        return new BackupListResult(
            IsConfigured: IsConfigured,
            LocalFiles: localFiles.OrderByDescending(f => f.LastModified).ToList(),
            RemoteFiles: remoteFiles.OrderByDescending(f => f.LastModified).ToList(),
            PendingSyncCount: pendingCount
        );
    }
}

public record BackupSyncResult(
    bool Success,
    string Message,
    int FilesUploaded,
    int FilesSkipped,
    long BytesUploaded
);

public record BackupFileInfo(
    string Path,
    long Size,
    DateTime LastModified
);

public record BackupListResult(
    bool IsConfigured,
    List<BackupFileInfo> LocalFiles,
    List<BackupFileInfo> RemoteFiles,
    int PendingSyncCount
);
