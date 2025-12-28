// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LocalizationManager.JsonLocalization.Ota;

/// <summary>
/// Background service that periodically refreshes OTA translations.
/// Implements IHostedService for ASP.NET Core integration.
/// </summary>
public class OtaRefreshService : BackgroundService
{
    private readonly OtaClient _client;
    private readonly OtaOptions _options;
    private readonly ILogger<OtaRefreshService> _logger;

    /// <summary>
    /// Creates a new OTA refresh service.
    /// </summary>
    public OtaRefreshService(
        OtaClient client,
        OtaOptions options,
        ILogger<OtaRefreshService>? logger = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? NullLogger<OtaRefreshService>.Instance;
    }

    /// <summary>
    /// Executes the background refresh loop.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OTA refresh service started for project {Project}", _options.Project);

        // Initial fetch (non-blocking, don't fail startup)
        try
        {
            await _client.RefreshAsync(force: true, stoppingToken);
            _logger.LogInformation("Initial OTA bundle loaded successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Initial OTA fetch failed - will use fallback resources until successful");
        }

        // Periodic refresh loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.RefreshInterval, stoppingToken);

                // Check version first for efficiency
                var hasNewVersion = await _client.CheckVersionAsync(stoppingToken);
                if (hasNewVersion)
                {
                    _logger.LogDebug("New OTA version detected, refreshing bundle");
                    await _client.RefreshAsync(force: true, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "OTA refresh cycle failed");
                // Continue running - circuit breaker will handle repeated failures
            }
        }

        _logger.LogInformation("OTA refresh service stopped");
    }
}
