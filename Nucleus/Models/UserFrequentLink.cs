using System;
using System.Collections.Generic;

namespace Nucleus.Models;

public partial class UserFrequentLink
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Url { get; set; } = null!;

    public string? ThumbnailUrl { get; set; }

    public virtual DiscordUser User { get; set; } = null!;
}
