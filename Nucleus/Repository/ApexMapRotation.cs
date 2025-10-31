namespace Nucleus.Repository;

public class ApexMapRotation
{
    public Guid Id { get; set; }
    
    public ApexMap Map { get; set; }
    
    public DateTimeOffset StartTime { get; set; }
    
    public DateTimeOffset EndTime { get; set; }
    
    public ApexGamemode Gamemode { get; set; }
    
}