using Nucleus.Clips;

namespace Nucleus.Repository;

public class ClipCollection
{
    public Guid Id { get; set; }
    
    public Guid CollectionId { get; set; }
    
    public Guid OwnerId { get; set; }
    
    public ClipCategoryEnum CategoryEnum { get; set; }
}