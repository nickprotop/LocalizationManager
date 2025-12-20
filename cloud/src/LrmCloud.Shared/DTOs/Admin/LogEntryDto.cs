namespace LrmCloud.Shared.DTOs.Admin;

/// <summary>
/// A log entry from the application logs.
/// </summary>
public class LogEntryDto
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "INF";  // INF, WRN, ERR
    public string Message { get; set; } = "";
    public string? Exception { get; set; }
    public string? SourceContext { get; set; }
    public string? RequestPath { get; set; }
    public int? StatusCode { get; set; }
}

/// <summary>
/// Request for filtering logs.
/// </summary>
public class LogFilterDto
{
    public string? Level { get; set; }  // INF, WRN, ERR, or null for all
    public string? Search { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
}
