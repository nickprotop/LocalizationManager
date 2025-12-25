namespace LrmCloud.Shared.DTOs.Admin;

public class AdminSendEmailResultDto
{
    public bool Success { get; set; }
    public int RecipientCount { get; set; }
    public string? Error { get; set; }
}
