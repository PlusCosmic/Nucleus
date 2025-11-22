using MongoDB.Bson;

namespace Nucleus.Dropzone;

public class ShareGroup
{
    public ObjectId Id { get; set; }
    public required string GroupPin { get; set; }
    public List<SharedFile> Files { get; set; } = new();
    public required DateTimeOffset ExpiresAt { get; set; }
}