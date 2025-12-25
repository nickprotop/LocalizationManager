namespace LrmCloud.Shared.DTOs.Admin;

public class AdminSendEmailDto
{
    public AdminEmailRecipientType RecipientType { get; set; }
    public int? UserId { get; set; }
    public string? PlanFilter { get; set; }
    public bool? EmailVerifiedFilter { get; set; }
    public required string Subject { get; set; }
    public required string HtmlBody { get; set; }
}

public enum AdminEmailRecipientType
{
    SingleUser,
    FilteredUsers,
    AllUsers
}
