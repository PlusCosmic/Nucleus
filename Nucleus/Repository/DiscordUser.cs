using System.ComponentModel.DataAnnotations;

namespace Nucleus.Repository;

public partial class DiscordUser
{
    public Guid Id { get; set; }

    [MaxLength(100)]
    public required string DiscordId { get; set; }
    
    [MaxLength(100)]
    public required string Username { get; set; }
    
    [MaxLength(100)]
    public string? Avatar { get; set; }

    public virtual ICollection<UserFrequentLink> UserFrequentLinks { get; set; } = new List<UserFrequentLink>();
}
