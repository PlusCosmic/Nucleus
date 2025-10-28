using System.Text.Json.Serialization;

namespace Nucleus.Models;

public partial class UserFrequentLink
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string? ThumbnailUrl { get; set; }

    [JsonIgnore]
    public virtual DiscordUser User { get; set; } = null!;
}
