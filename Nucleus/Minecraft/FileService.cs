using System.Security;
using Nucleus.Minecraft.Models;

namespace Nucleus.Minecraft;

public class FileService
{
    private readonly string _basePath;
    private readonly ILogger<FileService> _logger;
    private const long MaxFileSize = 5 * 1024 * 1024; // 5MB

    public FileService(IConfiguration configuration, ILogger<FileService> logger)
    {
        _logger = logger;
        string? configuredPath = configuration["Minecraft:DataPath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new InvalidOperationException("Minecraft:DataPath is not configured");
        }

        _basePath = Path.GetFullPath(configuredPath);
        _logger.LogInformation("FileService initialized with base path: {BasePath}", _basePath);

        if (!Directory.Exists(_basePath))
        {
            _logger.LogWarning("Base path does not exist: {BasePath}", _basePath);
        }
    }

    private string GetSafePath(string relativePath)
    {
        // Normalize the relative path
        string normalizedRelativePath = relativePath.Replace('\\', '/').TrimStart('/');

        // Combine with base path
        string combinedPath = Path.Combine(_basePath, normalizedRelativePath);

        // Get the full normalized path
        string fullPath = Path.GetFullPath(combinedPath);

        // Verify the resulting path is still within the base path
        if (!fullPath.StartsWith(_basePath, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Path traversal attempt detected: {RelativePath} -> {FullPath}", relativePath, fullPath);
            throw new SecurityException("Access to the specified path is denied");
        }

        return fullPath;
    }

    public DirectoryListing ListDirectory(string relativePath)
    {
        string safePath = GetSafePath(relativePath);

        if (!Directory.Exists(safePath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {relativePath}");
        }

        List<FileEntry> entries = new();

        // Add directories
        foreach (string dir in Directory.GetDirectories(safePath))
        {
            DirectoryInfo dirInfo = new(dir);
            string dirName = dirInfo.Name;
            string dirPath = Path.Combine(relativePath, dirName).Replace('\\', '/');

            entries.Add(new FileEntry(
                Name: dirName,
                Path: dirPath,
                IsDirectory: true,
                Size: 0,
                LastModified: dirInfo.LastWriteTimeUtc
            ));
        }

        // Add files
        foreach (string file in Directory.GetFiles(safePath))
        {
            FileInfo fileInfo = new(file);
            string fileName = fileInfo.Name;
            string filePath = Path.Combine(relativePath, fileName).Replace('\\', '/');

            entries.Add(new FileEntry(
                Name: fileName,
                Path: filePath,
                IsDirectory: false,
                Size: fileInfo.Length,
                LastModified: fileInfo.LastWriteTimeUtc
            ));
        }

        // Sort: directories first, then by name
        entries = entries.OrderBy(e => !e.IsDirectory).ThenBy(e => e.Name).ToList();

        return new DirectoryListing(relativePath, entries);
    }

    public async Task<string> ReadFileAsync(string relativePath)
    {
        string safePath = GetSafePath(relativePath);

        if (!File.Exists(safePath))
        {
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        FileInfo fileInfo = new(safePath);
        if (fileInfo.Length > MaxFileSize)
        {
            throw new InvalidOperationException($"File is too large to read (max {MaxFileSize / 1024 / 1024}MB)");
        }

        return await File.ReadAllTextAsync(safePath);
    }

    public async Task WriteFileAsync(string relativePath, string content)
    {
        string safePath = GetSafePath(relativePath);

        // Check content size
        long contentSize = System.Text.Encoding.UTF8.GetByteCount(content);
        if (contentSize > MaxFileSize)
        {
            throw new InvalidOperationException($"Content is too large to write (max {MaxFileSize / 1024 / 1024}MB)");
        }

        // Ensure parent directory exists
        string? parentDir = Path.GetDirectoryName(safePath);
        if (parentDir != null && !Directory.Exists(parentDir))
        {
            Directory.CreateDirectory(parentDir);
        }

        await File.WriteAllTextAsync(safePath, content);
        _logger.LogInformation("File written: {Path}", relativePath);
    }

    public void DeleteFile(string relativePath)
    {
        string safePath = GetSafePath(relativePath);

        if (!File.Exists(safePath))
        {
            throw new FileNotFoundException($"File not found: {relativePath}");
        }

        File.Delete(safePath);
        _logger.LogInformation("File deleted: {Path}", relativePath);
    }

    public void CreateDirectory(string relativePath)
    {
        string safePath = GetSafePath(relativePath);

        if (Directory.Exists(safePath))
        {
            throw new InvalidOperationException($"Directory already exists: {relativePath}");
        }

        Directory.CreateDirectory(safePath);
        _logger.LogInformation("Directory created: {Path}", relativePath);
    }
}
