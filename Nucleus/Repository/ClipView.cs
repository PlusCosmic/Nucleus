namespace Nucleus.Repository;

public class ClipView
{
    public Guid UserId { get; set; }
    
    public Guid ClipId { get; set; }
    
    public DateTime ViewedAt { get; set; }
    
    public Clip Clip { get; set; } = null!;
    
    public DiscordUser User { get; set; } = null!;
}
