namespace Nucleus.Dropzone;

public class SharedFile
{
    public required string S3Url { get; set; }
    public required string FileName { get; set; }
    public required string FileType { get; set; }
    public required int FileSize { get; set; }
    public required string FileHash { get; set; }
    public required string UploaderName { get; set; }
}