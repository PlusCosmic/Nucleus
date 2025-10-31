namespace Nucleus.Clips;

public enum ClipCategoryEnum
{
    ApexLegends,
    CallOfDutyWarzone,
    Snowboarding
}

public record ClipCategory(string Name, ClipCategoryEnum categoryEnum, string ArtUrl);