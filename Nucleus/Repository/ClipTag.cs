namespace Nucleus.Repository;

public class ClipTag
{
    public Guid ClipId { get; set; }
    public Clip Clip { get; set; } = null!;

    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
