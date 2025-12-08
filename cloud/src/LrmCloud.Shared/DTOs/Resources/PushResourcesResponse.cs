namespace LrmCloud.Shared.DTOs.Resources;

/// <summary>
/// Response from pushing resources.
/// </summary>
public class PushResourcesResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int FilesUpdated { get; set; }
    public DateTime PushedAt { get; set; }
}
