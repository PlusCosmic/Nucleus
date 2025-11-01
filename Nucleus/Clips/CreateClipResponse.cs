namespace Nucleus.Clips;

public record CreateClipResponse(string Signature, long Expiration, string LibraryId, Guid VideoId, Guid CollectionId)
{
    
}