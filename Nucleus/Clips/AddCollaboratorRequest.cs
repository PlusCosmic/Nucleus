namespace Nucleus.Clips;

public record AddCollaboratorRequest(Guid? UserId = null, string? Email = null, string? Username = null);
