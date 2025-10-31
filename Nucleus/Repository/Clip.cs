using Nucleus.Clips;

namespace Nucleus.Repository;

public class Clip
{
    public Guid Id { get; set; }
    
    public Guid OwnerId { get; set; }
    
    public Guid VideoId { get; set; }
    
    public ClipCategoryEnum CategoryEnum { get; set; }
}