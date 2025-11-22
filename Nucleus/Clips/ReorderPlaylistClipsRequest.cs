namespace Nucleus.Clips;

public record ReorderPlaylistClipsRequest(Guid? ClipId = null, int? NewPosition = null, List<Guid>? ClipOrdering = null);
