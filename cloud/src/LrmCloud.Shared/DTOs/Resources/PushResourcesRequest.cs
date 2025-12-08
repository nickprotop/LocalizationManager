namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Request to push resources to the cloud.
/// </summary>
public class PushResourcesRequest
{
    public List<ResourceDto> Resources { get; set; } = new();
    public string? Message { get; set; }
}
