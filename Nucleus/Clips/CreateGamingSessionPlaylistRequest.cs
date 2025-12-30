namespace Nucleus.Clips;

public record CreateGamingSessionPlaylistRequest(List<Guid> Participants, Guid CategoryId)
{
}
