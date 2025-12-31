namespace LrmCloud.Shared.DTOs.Projects;

/// <summary>
/// Status of sample project creation for a user.
/// </summary>
public class SampleProjectStatusDto
{
    /// <summary>
    /// True if the sample project should be auto-created (first-time user).
    /// </summary>
    public bool ShouldAutoCreate { get; set; }

    /// <summary>
    /// True if the user can manually create a sample project (no projects exist).
    /// </summary>
    public bool CanCreateSample { get; set; }
}
