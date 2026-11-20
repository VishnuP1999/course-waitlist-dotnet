namespace CourseWaitlist.Api;

/// <summary>Bound from the "Offering" configuration section.</summary>
public sealed class OfferingOptions
{
    public string OfferingId { get; set; } = "NET301";
    public int Capacity { get; set; } = 2;
    public string? RequiredPrerequisite { get; set; } = "CSH101";
}
