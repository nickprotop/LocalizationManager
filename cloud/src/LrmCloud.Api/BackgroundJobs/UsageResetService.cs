using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using Microsoft.EntityFrameworkCore;

namespace LrmCloud.Api.BackgroundJobs;

/// <summary>
/// Background service that resets monthly usage counters for users.
/// Runs hourly and checks for users whose reset date has passed.
/// </summary>
public class UsageResetService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UsageResetService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public UsageResetService(IServiceProvider serviceProvider, ILogger<UsageResetService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Usage Reset Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ResetExpiredUsageCountersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Usage Reset Service");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Usage Reset Service stopped");
    }

    private async Task ResetExpiredUsageCountersAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var config = scope.ServiceProvider.GetRequiredService<CloudConfiguration>();

        var now = DateTime.UtcNow;

        // Find users whose translation reset date has passed
        var usersToResetTranslation = await db.Users
            .Where(u => u.TranslationCharsResetAt <= now)
            .ToListAsync(stoppingToken);

        if (usersToResetTranslation.Count > 0)
        {
            _logger.LogInformation("Resetting translation usage for {Count} users", usersToResetTranslation.Count);

            foreach (var user in usersToResetTranslation)
            {
                var oldUsage = user.TranslationCharsUsed;
                user.TranslationCharsUsed = 0;
                user.TranslationCharsResetAt = now.AddMonths(1);

                _logger.LogDebug(
                    "Reset translation usage for user {UserId}: {OldUsage} -> 0, next reset at {NextReset}",
                    user.Id, oldUsage, user.TranslationCharsResetAt);
            }

            await db.SaveChangesAsync(stoppingToken);
        }

        // Find users whose other providers reset date has passed
        var usersToResetOther = await db.Users
            .Where(u => u.OtherCharsResetAt <= now)
            .ToListAsync(stoppingToken);

        if (usersToResetOther.Count > 0)
        {
            _logger.LogInformation("Resetting other providers usage for {Count} users", usersToResetOther.Count);

            foreach (var user in usersToResetOther)
            {
                var oldUsage = user.OtherCharsUsed;
                user.OtherCharsUsed = 0;
                user.OtherCharsResetAt = now.AddMonths(1);

                _logger.LogDebug(
                    "Reset other providers usage for user {UserId}: {OldUsage} -> 0, next reset at {NextReset}",
                    user.Id, oldUsage, user.OtherCharsResetAt);
            }

            await db.SaveChangesAsync(stoppingToken);
        }

        // Log summary
        var totalReset = usersToResetTranslation.Count + usersToResetOther.Count;
        if (totalReset > 0)
        {
            _logger.LogInformation(
                "Usage reset complete: {TranslationCount} translation resets, {OtherCount} other provider resets",
                usersToResetTranslation.Count, usersToResetOther.Count);
        }
    }
}
