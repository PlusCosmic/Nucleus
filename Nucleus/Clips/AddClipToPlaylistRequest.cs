namespace Nucleus.Clips;

public record AddClipToPlaylistRequest(Guid? ClipId = null, List<Guid>? ClipIds = null);
