namespace Nucleus.Repository;

public class Tag
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty; // stored normalized (lowercase)

    public ICollection<ClipTag> ClipTags { get; set; } = new List<ClipTag>();
}
