namespace Nucleus.Models;

public partial class DiscordUser
{
    public Guid Id { get; set; }

    public string DiscordId { get; set; } = null!;

    public string Username { get; set; } = null!;

    public virtual ICollection<UserFrequentLink> UserFrequentLinks { get; set; } = new List<UserFrequentLink>();
}
