namespace LrmCloud.Shared.DTOs.Admin;

/// <summary>
/// System health status for all services.
/// </summary>
public class SystemHealthDto
{
    public bool IsHealthy { get; set; }
    public ServiceHealthDto Database { get; set; } = new();
    public ServiceHealthDto Redis { get; set; } = new();
    public ServiceHealthDto MinIO { get; set; } = new();
    public ServiceHealthDto? Stripe { get; set; }
    public ServiceHealthDto? PayPal { get; set; }
    public DateTime ServerTime { get; set; }
    public TimeSpan Uptime { get; set; }
    public string Version { get; set; } = "";
}

/// <summary>
/// Health status for an individual service.
/// </summary>
public class ServiceHealthDto
{
    public string Name { get; set; } = "";
    public bool IsHealthy { get; set; }
    public string? Message { get; set; }
    public long? LatencyMs { get; set; }
}
