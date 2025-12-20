using System.Diagnostics;
using LrmCloud.Api.Data;
using LrmCloud.Shared.Configuration;
using LrmCloud.Shared.DTOs.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Minio;
using Minio.DataModel.Args;
using Npgsql;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for superadmin operations.
/// </summary>
public class AdminService : IAdminService
{
    private readonly AppDbContext _db;
    private readonly CloudConfiguration _config;
    private readonly IDistributedCache _cache;
    private readonly IMinioClient _minio;
    private readonly ILogger<AdminService> _logger;
    private static readonly DateTime _startTime = DateTime.UtcNow;

    public AdminService(
        AppDbContext db,
        CloudConfiguration config,
        IDistributedCache cache,
        IMinioClient minio,
        ILogger<AdminService> logger)
    {
        _db = db;
        _config = config;
        _cache = cache;
        _minio = minio;
        _logger = logger;
    }

    public async Task<AdminStatsDto> GetStatsAsync()
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        // Get user stats
        var totalUsers = await _db.Users.CountAsync();
        var activeUsers = await _db.Users.CountAsync(u => u.LastLoginAt >= thirtyDaysAgo);
        var paidUsers = await _db.Users.CountAsync(u => u.Plan != "free" && u.SubscriptionStatus == "active");

        // Get org and project counts
        var totalOrgs = await _db.Organizations.CountAsync();
        var totalProjects = await _db.Projects.CountAsync();

        // Get translation counts
        var totalTranslations = await _db.Translations.LongCountAsync();
        var totalTranslationChars = await _db.Users.SumAsync(u => (long)u.TranslationCharsUsed);
        var totalOtherChars = await _db.Users.SumAsync(u => u.OtherCharsUsed);

        // Get users by plan
        var usersByPlan = await _db.Users
            .GroupBy(u => u.Plan)
            .Select(g => new { Plan = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Plan, x => x.Count);

        // Get users by auth type
        var usersByAuthType = await _db.Users
            .GroupBy(u => u.AuthType)
            .Select(g => new { AuthType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.AuthType, x => x.Count);

        // Get database size (PostgreSQL specific)
        long dbSizeBytes = 0;
        try
        {
            var dbName = _db.Database.GetDbConnection().Database;
            var connection = _db.Database.GetDbConnection();
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT pg_database_size('{dbName}')";
            var result = await command.ExecuteScalarAsync();
            if (result != null && result != DBNull.Value)
            {
                dbSizeBytes = Convert.ToInt64(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get database size");
        }

        // Get storage size from MinIO (approximate)
        long storageSizeBytes = 0;
        try
        {
            var listArgs = new ListObjectsArgs()
                .WithBucket(_config.Storage.Bucket)
                .WithRecursive(true);

            await foreach (var item in _minio.ListObjectsEnumAsync(listArgs))
            {
                storageSizeBytes += (long)item.Size;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get storage size");
        }

        return new AdminStatsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            PaidUsers = paidUsers,
            TotalOrganizations = totalOrgs,
            TotalProjects = totalProjects,
            TotalTranslations = totalTranslations,
            TotalTranslationCharsUsed = totalTranslationChars,
            TotalOtherCharsUsed = totalOtherChars,
            DatabaseSizeBytes = dbSizeBytes,
            StorageSizeBytes = storageSizeBytes,
            UsersByPlan = usersByPlan,
            UsersByAuthType = usersByAuthType
        };
    }

    public async Task<SystemHealthDto> GetHealthAsync()
    {
        var health = new SystemHealthDto
        {
            ServerTime = DateTime.UtcNow,
            Uptime = DateTime.UtcNow - _startTime,
            Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"
        };

        // Check Database
        health.Database = await CheckDatabaseHealthAsync();

        // Check Redis
        health.Redis = await CheckRedisHealthAsync();

        // Check MinIO
        health.MinIO = await CheckMinioHealthAsync();

        // Check Stripe (if enabled)
        if (_config.Payment?.Stripe?.Enabled == true)
        {
            health.Stripe = new ServiceHealthDto
            {
                Name = "Stripe",
                IsHealthy = _config.Payment.Stripe.IsConfigured,
                Message = _config.Payment.Stripe.IsConfigured ? "Configured" : "Not configured"
            };
        }

        // Check PayPal (if enabled)
        if (_config.Payment?.PayPal?.Enabled == true)
        {
            health.PayPal = new ServiceHealthDto
            {
                Name = "PayPal",
                IsHealthy = _config.Payment.PayPal.IsConfigured,
                Message = _config.Payment.PayPal.IsConfigured ? "Configured" : "Not configured"
            };
        }

        // Overall health
        health.IsHealthy = health.Database.IsHealthy && health.Redis.IsHealthy && health.MinIO.IsHealthy;

        return health;
    }

    private async Task<ServiceHealthDto> CheckDatabaseHealthAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT 1");
            sw.Stop();
            return new ServiceHealthDto
            {
                Name = "PostgreSQL",
                IsHealthy = true,
                Message = "Connected",
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ServiceHealthDto
            {
                Name = "PostgreSQL",
                IsHealthy = false,
                Message = ex.Message,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
    }

    private async Task<ServiceHealthDto> CheckRedisHealthAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var testKey = "__health_check__";
            await _cache.SetStringAsync(testKey, "ok", new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(5)
            });
            var result = await _cache.GetStringAsync(testKey);
            sw.Stop();

            return new ServiceHealthDto
            {
                Name = "Redis",
                IsHealthy = result == "ok",
                Message = result == "ok" ? "Connected" : "Read/Write failed",
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ServiceHealthDto
            {
                Name = "Redis",
                IsHealthy = false,
                Message = ex.Message,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
    }

    private async Task<ServiceHealthDto> CheckMinioHealthAsync()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var bucketExists = await _minio.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_config.Storage.Bucket));
            sw.Stop();

            return new ServiceHealthDto
            {
                Name = "MinIO",
                IsHealthy = bucketExists,
                Message = bucketExists ? "Connected" : "Bucket not found",
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            return new ServiceHealthDto
            {
                Name = "MinIO",
                IsHealthy = false,
                Message = ex.Message,
                LatencyMs = sw.ElapsedMilliseconds
            };
        }
    }

    public async Task<List<LogEntryDto>> GetLogsAsync(LogFilterDto? filter, int page = 1, int pageSize = 100)
    {
        // Determine log directory
        var logPath = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
            ? "/var/log/lrmcloud/api"
            : Path.Combine(Directory.GetCurrentDirectory(), "logs");

        var logs = new List<LogEntryDto>();

        if (!Directory.Exists(logPath))
        {
            return logs;
        }

        // Get log files (most recent first)
        var logFiles = Directory.GetFiles(logPath, "api-*.log")
            .OrderByDescending(f => f)
            .Take(5) // Only check last 5 log files
            .ToList();

        foreach (var logFile in logFiles)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(logFile);

                foreach (var line in lines.Reverse()) // Most recent first
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var entry = ParseLogLine(line);
                    if (entry == null) continue;

                    // Apply filters
                    if (filter != null)
                    {
                        if (!string.IsNullOrEmpty(filter.Level) &&
                            !entry.Level.Equals(filter.Level, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (!string.IsNullOrEmpty(filter.Search) &&
                            !entry.Message.Contains(filter.Search, StringComparison.OrdinalIgnoreCase))
                            continue;

                        if (filter.From.HasValue && entry.Timestamp < filter.From.Value)
                            continue;

                        if (filter.To.HasValue && entry.Timestamp > filter.To.Value)
                            continue;
                    }

                    logs.Add(entry);

                    // Early exit if we have enough for pagination
                    if (logs.Count >= page * pageSize)
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read log file {File}", logFile);
            }

            if (logs.Count >= page * pageSize)
                break;
        }

        // Apply pagination
        return logs
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
    }

    private static LogEntryDto? ParseLogLine(string line)
    {
        // Parse format: "2024-01-15 10:30:45.123 +00:00 [INF] Message here"
        try
        {
            // Find the timestamp end
            var bracketIndex = line.IndexOf('[');
            if (bracketIndex < 0) return null;

            var closeBracketIndex = line.IndexOf(']', bracketIndex);
            if (closeBracketIndex < 0) return null;

            var timestampStr = line[..bracketIndex].Trim();
            var level = line.Substring(bracketIndex + 1, closeBracketIndex - bracketIndex - 1);
            var message = line[(closeBracketIndex + 1)..].Trim();

            // Try to parse timestamp
            if (!DateTime.TryParse(timestampStr, out var timestamp))
            {
                timestamp = DateTime.UtcNow;
            }

            return new LogEntryDto
            {
                Timestamp = timestamp,
                Level = level,
                Message = message
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<(List<AdminUserDto> Users, int TotalCount)> GetUsersAsync(string? search, string? plan, int page = 1, int pageSize = 20)
    {
        var query = _db.Users.AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLowerInvariant();
            query = query.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                u.Username.ToLower().Contains(searchLower) ||
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(searchLower)));
        }

        // Apply plan filter
        if (!string.IsNullOrWhiteSpace(plan))
        {
            query = query.Where(u => u.Plan == plan.ToLowerInvariant());
        }

        // Get total count
        var totalCount = await query.CountAsync();

        // Get paginated results
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminUserDto
            {
                Id = u.Id,
                Email = u.Email,
                Username = u.Username,
                DisplayName = u.DisplayName,
                AvatarUrl = u.AvatarUrl,
                AuthType = u.AuthType,
                Plan = u.Plan,
                SubscriptionStatus = u.SubscriptionStatus,
                IsSuperAdmin = u.IsSuperAdmin,
                EmailVerified = u.EmailVerified,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync();

        return (users, totalCount);
    }

    public async Task<AdminUserDetailDto?> GetUserAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        // Get organization IDs where user is a member
        var userOrgIds = await _db.OrganizationMembers
            .Where(om => om.UserId == userId)
            .Select(om => om.OrganizationId)
            .ToListAsync();

        // Count personal projects + organization projects (where user is a member)
        var projectCount = await _db.Projects.CountAsync(p =>
            p.UserId == userId ||
            (p.OrganizationId.HasValue && userOrgIds.Contains(p.OrganizationId.Value)));

        var orgCount = userOrgIds.Count;

        return new AdminUserDetailDto
        {
            Id = user.Id,
            Email = user.Email,
            Username = user.Username,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl,
            AuthType = user.AuthType,
            Plan = user.Plan,
            SubscriptionStatus = user.SubscriptionStatus,
            IsSuperAdmin = user.IsSuperAdmin,
            EmailVerified = user.EmailVerified,
            CreatedAt = user.CreatedAt,
            LastLoginAt = user.LastLoginAt,
            PaymentProvider = user.PaymentProvider,
            PaymentCustomerId = user.PaymentCustomerId,
            PaymentSubscriptionId = user.PaymentSubscriptionId,
            SubscriptionCurrentPeriodEnd = user.SubscriptionCurrentPeriodEnd,
            CancelAtPeriodEnd = user.CancelAtPeriodEnd,
            TranslationCharsUsed = user.TranslationCharsUsed,
            TranslationCharsLimit = user.TranslationCharsLimit,
            OtherCharsUsed = user.OtherCharsUsed,
            OtherCharsLimit = user.OtherCharsLimit,
            TranslationCharsResetAt = user.TranslationCharsResetAt,
            OtherCharsResetAt = user.OtherCharsResetAt,
            ProjectCount = projectCount,
            OrganizationCount = orgCount,
            FailedLoginAttempts = user.FailedLoginAttempts,
            LockedUntil = user.LockedUntil,
            DeletedAt = user.DeletedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateUserAsync(int userId, AdminUpdateUserDto dto)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return (false, "User not found");

        if (dto.Plan != null)
        {
            var validPlans = new[] { "free", "team", "enterprise" };
            if (!validPlans.Contains(dto.Plan.ToLowerInvariant()))
                return (false, "Invalid plan. Must be: free, team, or enterprise");

            user.Plan = dto.Plan.ToLowerInvariant();

            // Update limits based on new plan
            user.TranslationCharsLimit = _config.Limits.GetTranslationCharsLimit(user.Plan);
            user.OtherCharsLimit = _config.Limits.GetOtherCharsLimit(user.Plan);
        }

        if (dto.TranslationCharsLimit.HasValue)
            user.TranslationCharsLimit = dto.TranslationCharsLimit.Value;

        if (dto.OtherCharsLimit.HasValue)
            user.OtherCharsLimit = dto.OtherCharsLimit.Value;

        if (dto.IsSuperAdmin.HasValue)
            user.IsSuperAdmin = dto.IsSuperAdmin.Value;

        if (dto.EmailVerified.HasValue)
            user.EmailVerified = dto.EmailVerified.Value;

        if (dto.SubscriptionStatus != null)
        {
            var validStatuses = new[] { "none", "active", "past_due", "canceled", "incomplete" };
            if (!validStatuses.Contains(dto.SubscriptionStatus.ToLowerInvariant()))
                return (false, "Invalid subscription status");

            user.SubscriptionStatus = dto.SubscriptionStatus.ToLowerInvariant();
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin updated user {UserId}: {@Changes}", userId, dto);

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return (false, "User not found");

        if (user.IsSuperAdmin)
            return (false, "Cannot delete a superadmin user");

        // Soft delete
        user.DeletedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin soft-deleted user {UserId} ({Email})", userId, user.Email);

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> ResetUserUsageAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return (false, "User not found");

        user.TranslationCharsUsed = 0;
        user.OtherCharsUsed = 0;
        user.TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1);
        user.OtherCharsResetAt = DateTime.UtcNow.AddMonths(1);
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin reset usage for user {UserId} ({Email})", userId, user.Email);

        return (true, null);
    }

    public async Task<(List<AdminOrganizationDto> Organizations, int TotalCount)> GetOrganizationsAsync(string? search, int page = 1, int pageSize = 20)
    {
        var query = _db.Organizations
            .Include(o => o.Members)
            .AsQueryable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchLower = search.ToLowerInvariant();
            query = query.Where(o =>
                o.Name.ToLower().Contains(searchLower) ||
                o.Slug.ToLower().Contains(searchLower));
        }

        // Get total count
        var totalCount = await query.CountAsync();

        // Get paginated results with owner info
        var orgs = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                Org = o,
                MemberCount = o.Members.Count,
                Owner = _db.Users.FirstOrDefault(u => u.Id == o.OwnerId)
            })
            .ToListAsync();

        var projectCounts = await _db.Projects
            .Where(p => p.OrganizationId != null && orgs.Select(o => o.Org.Id).Contains(p.OrganizationId.Value))
            .GroupBy(p => p.OrganizationId)
            .Select(g => new { OrgId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.OrgId ?? 0, x => x.Count);

        var result = orgs.Select(o => new AdminOrganizationDto
        {
            Id = o.Org.Id,
            Name = o.Org.Name,
            Slug = o.Org.Slug,
            Description = o.Org.Description,
            MemberCount = o.MemberCount,
            ProjectCount = projectCounts.GetValueOrDefault(o.Org.Id, 0),
            OwnerId = o.Org.OwnerId,
            OwnerEmail = o.Owner?.Email,
            OwnerUsername = o.Owner?.Username,
            CreatedAt = o.Org.CreatedAt,
            UpdatedAt = o.Org.UpdatedAt
        }).ToList();

        return (result, totalCount);
    }

    public async Task<(List<AdminWebhookEventDto> Events, int TotalCount)> GetWebhookEventsAsync(int page = 1, int pageSize = 50)
    {
        var totalCount = await _db.WebhookEvents.CountAsync();

        var events = await _db.WebhookEvents
            .OrderByDescending(e => e.ProcessedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                Event = e,
                User = e.UserId.HasValue ? _db.Users.FirstOrDefault(u => u.Id == e.UserId) : null
            })
            .ToListAsync();

        var result = events.Select(e => new AdminWebhookEventDto
        {
            Id = e.Event.Id,
            ProviderEventId = e.Event.ProviderEventId,
            ProviderName = e.Event.ProviderName,
            EventType = e.Event.EventType,
            UserId = e.Event.UserId,
            UserEmail = e.User?.Email,
            ProcessedAt = e.Event.ProcessedAt,
            Success = e.Event.Success,
            ErrorMessage = e.Event.ErrorMessage
        }).ToList();

        return (result, totalCount);
    }

    // ===== Analytics Methods =====

    // Plan prices for MRR calculation
    private static readonly Dictionary<string, decimal> PlanPrices = new()
    {
        { "team", 9.00m },
        { "enterprise", 29.00m }
    };

    public async Task<RevenueAnalyticsDto> GetRevenueAnalyticsAsync(int months = 12)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        // Current MRR: Count active subscribers by plan
        var currentPaidUsers = await _db.Users
            .Where(u => (u.Plan == "team" || u.Plan == "enterprise") && u.SubscriptionStatus == "active")
            .GroupBy(u => u.Plan)
            .Select(g => new { Plan = g.Key, Count = g.Count() })
            .ToListAsync();

        var currentMrr = currentPaidUsers.Sum(p => p.Count * PlanPrices.GetValueOrDefault(p.Plan, 0));
        var revenueByPlan = currentPaidUsers.ToDictionary(p => p.Plan, p => p.Count * PlanPrices.GetValueOrDefault(p.Plan, 0));

        // For previous MRR, we'd need historical data. For now, estimate from current data.
        // In a real system, you'd track plan changes in a PlanChangeLog table.
        var previousMrr = currentMrr; // Placeholder - would need historical data

        // Build MRR history (approximation based on user creation dates)
        var mrrHistory = new List<MrrDataPoint>();
        for (int i = months - 1; i >= 0; i--)
        {
            var monthStart = currentMonthStart.AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);

            // Count users who had active subscriptions as of that month
            // This is an approximation - real implementation would use PlanChangeLog
            var paidUsersInMonth = await _db.Users
                .Where(u => (u.Plan == "team" || u.Plan == "enterprise")
                    && u.SubscriptionStatus == "active"
                    && u.CreatedAt < monthEnd)
                .CountAsync();

            // Estimate MRR based on average plan price
            var estimatedMrr = paidUsersInMonth * 15m; // Average of team ($9) and enterprise ($29)

            mrrHistory.Add(new MrrDataPoint
            {
                Month = monthStart,
                Mrr = estimatedMrr,
                PaidUsers = paidUsersInMonth
            });
        }

        var mrrGrowthPercent = previousMrr > 0 ? ((currentMrr - previousMrr) / previousMrr) * 100 : 0;

        return new RevenueAnalyticsDto
        {
            CurrentMrr = currentMrr,
            PreviousMrr = previousMrr,
            MrrGrowthPercent = mrrGrowthPercent,
            MrrHistory = mrrHistory,
            RevenueByPlan = revenueByPlan
        };
    }

    public async Task<UserAnalyticsDto> GetUserAnalyticsAsync(int months = 12)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var previousMonthStart = currentMonthStart.AddMonths(-1);
        var thirtyDaysAgo = now.AddDays(-30);

        var totalUsers = await _db.Users.CountAsync();

        // New users this month
        var newUsersThisMonth = await _db.Users
            .Where(u => u.CreatedAt >= currentMonthStart)
            .CountAsync();

        // New users last month
        var newUsersLastMonth = await _db.Users
            .Where(u => u.CreatedAt >= previousMonthStart && u.CreatedAt < currentMonthStart)
            .CountAsync();

        // Churn this month (users who went to free plan or canceled)
        // Without PlanChangeLog, we can't accurately track this
        var churnedUsersThisMonth = 0; // Placeholder

        // Growth rate
        var growthRate = totalUsers > 0 ? ((decimal)newUsersThisMonth / totalUsers) * 100 : 0;

        // Build growth history
        var growthHistory = new List<UserGrowthDataPoint>();
        for (int i = months - 1; i >= 0; i--)
        {
            var monthStart = currentMonthStart.AddMonths(-i);
            var monthEnd = monthStart.AddMonths(1);

            var newInMonth = await _db.Users
                .Where(u => u.CreatedAt >= monthStart && u.CreatedAt < monthEnd)
                .CountAsync();

            var totalAtMonthEnd = await _db.Users
                .Where(u => u.CreatedAt < monthEnd)
                .CountAsync();

            var activeInMonth = await _db.Users
                .Where(u => u.LastLoginAt >= monthStart && u.LastLoginAt < monthEnd)
                .CountAsync();

            growthHistory.Add(new UserGrowthDataPoint
            {
                Month = monthStart,
                NewUsers = newInMonth,
                ChurnedUsers = 0, // Would need PlanChangeLog
                TotalUsers = totalAtMonthEnd,
                ActiveUsers = activeInMonth
            });
        }

        // Conversion funnel (plan upgrades)
        // Without PlanChangeLog, we can estimate from current distribution
        var planCounts = await _db.Users
            .GroupBy(u => u.Plan)
            .Select(g => new { Plan = g.Key, Count = g.Count() })
            .ToDictionaryAsync(p => p.Plan, p => p.Count);

        var freeCount = planCounts.GetValueOrDefault("free", 0);
        var teamCount = planCounts.GetValueOrDefault("team", 0);
        var enterpriseCount = planCounts.GetValueOrDefault("enterprise", 0);

        var conversionFunnel = new List<ConversionDataPoint>();
        if (freeCount > 0)
        {
            conversionFunnel.Add(new ConversionDataPoint
            {
                FromPlan = "free",
                ToPlan = "team",
                Count = teamCount,
                Rate = (decimal)teamCount / (freeCount + teamCount + enterpriseCount) * 100
            });
        }
        if (teamCount > 0)
        {
            conversionFunnel.Add(new ConversionDataPoint
            {
                FromPlan = "team",
                ToPlan = "enterprise",
                Count = enterpriseCount,
                Rate = (decimal)enterpriseCount / (teamCount + enterpriseCount) * 100
            });
        }

        return new UserAnalyticsDto
        {
            TotalUsers = totalUsers,
            NewUsersThisMonth = newUsersThisMonth,
            NewUsersLastMonth = newUsersLastMonth,
            ChurnedUsersThisMonth = churnedUsersThisMonth,
            ChurnRate = 0, // Would need PlanChangeLog
            GrowthRate = growthRate,
            GrowthHistory = growthHistory,
            ConversionFunnel = conversionFunnel
        };
    }

    public async Task<UsageAnalyticsDto> GetUsageAnalyticsAsync(int days = 30)
    {
        var now = DateTime.UtcNow;
        var startDate = now.AddDays(-days);
        var previousPeriodStart = startDate.AddDays(-days);

        // Total chars this period
        var totalLrmChars = await _db.Users.SumAsync(u => (long)u.TranslationCharsUsed);
        var totalByokChars = await _db.Users.SumAsync(u => u.OtherCharsUsed);
        var totalCharsThisMonth = totalLrmChars + totalByokChars;

        // Get usage from TranslationUsageHistory grouped by period
        var usageHistory = await _db.TranslationUsageHistory
            .Where(h => h.PeriodStart >= startDate)
            .GroupBy(h => h.PeriodStart.Date)
            .Select(g => new
            {
                Date = g.Key,
                LrmChars = g.Where(x => x.ProviderName == "lrm").Sum(x => x.CharsUsed),
                ByokChars = g.Where(x => x.ProviderName != "lrm").Sum(x => x.CharsUsed),
                ApiCalls = g.Sum(x => x.ApiCalls)
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        var dailyTrends = usageHistory.Select(d => new UsageTrendDataPoint
        {
            Date = d.Date,
            LrmChars = d.LrmChars,
            ByokChars = d.ByokChars,
            ApiCalls = d.ApiCalls
        }).ToList();

        // Usage by provider
        var usageByProvider = await _db.TranslationUsageHistory
            .Where(h => h.PeriodStart >= startDate)
            .GroupBy(h => h.ProviderName)
            .Select(g => new { Provider = g.Key, Chars = g.Sum(x => x.CharsUsed) })
            .ToDictionaryAsync(p => p.Provider, p => p.Chars);

        // Calculate growth (comparing to previous period would need more data)
        var growthPercent = 0m; // Placeholder

        return new UsageAnalyticsDto
        {
            TotalCharsThisMonth = totalCharsThisMonth,
            TotalCharsLastMonth = 0, // Would need historical aggregation
            GrowthPercent = growthPercent,
            TotalLrmChars = totalLrmChars,
            TotalByokChars = totalByokChars,
            DailyTrends = dailyTrends,
            UsageByProvider = usageByProvider
        };
    }

    // ===== Bulk Action Methods =====

    public async Task<BulkActionResult> BulkChangePlanAsync(BulkChangePlanRequest request)
    {
        var result = new BulkActionResult();
        var validPlans = new[] { "free", "team", "enterprise" };

        if (!validPlans.Contains(request.NewPlan.ToLowerInvariant()))
        {
            result.Errors.Add(new BulkActionError
            {
                UserId = 0,
                ErrorMessage = $"Invalid plan '{request.NewPlan}'. Must be: free, team, or enterprise"
            });
            return result;
        }

        foreach (var userId in request.UserIds)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    result.FailureCount++;
                    result.Errors.Add(new BulkActionError { UserId = userId, ErrorMessage = "User not found" });
                    continue;
                }

                user.Plan = request.NewPlan.ToLowerInvariant();
                user.TranslationCharsLimit = _config.Limits.GetTranslationCharsLimit(user.Plan);
                user.OtherCharsLimit = _config.Limits.GetOtherCharsLimit(user.Plan);
                user.UpdatedAt = DateTime.UtcNow;

                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add(new BulkActionError { UserId = userId, ErrorMessage = ex.Message });
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Bulk plan change: {SuccessCount} succeeded, {FailureCount} failed",
            result.SuccessCount, result.FailureCount);

        return result;
    }

    public async Task<BulkActionResult> BulkVerifyEmailsAsync(BulkActionRequest request)
    {
        var result = new BulkActionResult();

        foreach (var userId in request.UserIds)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    result.FailureCount++;
                    result.Errors.Add(new BulkActionError { UserId = userId, ErrorMessage = "User not found" });
                    continue;
                }

                if (user.EmailVerified)
                {
                    // Already verified, count as success
                    result.SuccessCount++;
                    continue;
                }

                user.EmailVerified = true;
                user.EmailVerificationTokenHash = null;
                user.EmailVerificationExpiresAt = null;
                user.UpdatedAt = DateTime.UtcNow;

                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add(new BulkActionError { UserId = userId, ErrorMessage = ex.Message });
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Bulk email verification: {SuccessCount} succeeded, {FailureCount} failed",
            result.SuccessCount, result.FailureCount);

        return result;
    }

    public async Task<BulkActionResult> BulkResetUsageAsync(BulkActionRequest request)
    {
        var result = new BulkActionResult();

        foreach (var userId in request.UserIds)
        {
            try
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
                if (user == null)
                {
                    result.FailureCount++;
                    result.Errors.Add(new BulkActionError { UserId = userId, ErrorMessage = "User not found" });
                    continue;
                }

                user.TranslationCharsUsed = 0;
                user.OtherCharsUsed = 0;
                user.TranslationCharsResetAt = DateTime.UtcNow.AddMonths(1);
                user.OtherCharsResetAt = DateTime.UtcNow.AddMonths(1);
                user.UpdatedAt = DateTime.UtcNow;

                result.SuccessCount++;
            }
            catch (Exception ex)
            {
                result.FailureCount++;
                result.Errors.Add(new BulkActionError { UserId = userId, ErrorMessage = ex.Message });
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Bulk usage reset: {SuccessCount} succeeded, {FailureCount} failed",
            result.SuccessCount, result.FailureCount);

        return result;
    }

    // ===== Organization Detail Methods =====

    public async Task<AdminOrganizationDetailDto?> GetOrganizationAsync(int orgId)
    {
        var org = await _db.Organizations
            .Include(o => o.Owner)
            .Include(o => o.Members)
                .ThenInclude(m => m.User)
            .Include(o => o.Projects)
            .Where(o => o.Id == orgId && o.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (org == null) return null;

        return new AdminOrganizationDetailDto
        {
            Id = org.Id,
            Name = org.Name,
            Slug = org.Slug,
            Description = org.Description,
            OwnerId = org.OwnerId,
            OwnerEmail = org.Owner?.Email,
            OwnerUsername = org.Owner?.Username,
            Plan = org.Plan,
            TranslationCharsUsed = org.TranslationCharsUsed,
            TranslationCharsLimit = org.TranslationCharsLimit,
            PaymentProvider = org.PaymentProvider,
            PaymentCustomerId = org.PaymentCustomerId,
            MemberCount = org.Members.Count,
            ProjectCount = org.Projects.Count,
            CreatedAt = org.CreatedAt,
            UpdatedAt = org.UpdatedAt,
            Members = org.Members
                .Where(m => m.User != null)
                .Select(m => new AdminOrgMemberDto
                {
                    UserId = m.UserId,
                    Email = m.User!.Email ?? "",
                    Username = m.User.Username,
                    DisplayName = m.User.DisplayName,
                    AvatarUrl = m.User.AvatarUrl,
                    Role = m.Role,
                    JoinedAt = m.JoinedAt ?? m.CreatedAt,
                    IsOwner = m.UserId == org.OwnerId
                })
                .OrderByDescending(m => m.IsOwner)
                .ThenBy(m => m.Email)
                .ToList(),
            Projects = org.Projects
                .Select(p => new AdminOrgProjectDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    LanguageCount = 0, // Would need a subquery for this
                    KeyCount = 0, // Would need a subquery for this
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .OrderBy(p => p.Name)
                .ToList()
        };
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateOrganizationAsync(int orgId, AdminUpdateOrganizationDto dto)
    {
        var org = await _db.Organizations
            .Where(o => o.Id == orgId && o.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (org == null)
            return (false, "Organization not found");

        if (!string.IsNullOrWhiteSpace(dto.Name))
            org.Name = dto.Name;

        if (!string.IsNullOrWhiteSpace(dto.Plan))
        {
            var validPlans = new[] { "team", "enterprise" };
            if (!validPlans.Contains(dto.Plan.ToLower()))
                return (false, "Invalid plan. Must be 'team' or 'enterprise'");
            org.Plan = dto.Plan.ToLower();
        }

        if (dto.TranslationCharsLimit.HasValue)
            org.TranslationCharsLimit = dto.TranslationCharsLimit.Value;

        if (dto.ResetUsage == true)
            org.TranslationCharsUsed = 0;

        org.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation("Admin updated organization {OrgId}: {OrgName}", org.Id, org.Name);

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> TransferOrganizationOwnershipAsync(int orgId, AdminTransferOwnershipRequest request)
    {
        var org = await _db.Organizations
            .Include(o => o.Members)
            .Where(o => o.Id == orgId && o.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (org == null)
            return (false, "Organization not found");

        var newOwner = await _db.Users
            .Where(u => u.Id == request.NewOwnerId && u.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (newOwner == null)
            return (false, "New owner not found");

        // Check if new owner is a member
        var membership = org.Members.FirstOrDefault(m => m.UserId == request.NewOwnerId);
        if (membership == null)
            return (false, "New owner must be a member of the organization");

        // Update owner
        var previousOwnerId = org.OwnerId;
        org.OwnerId = request.NewOwnerId;
        org.UpdatedAt = DateTime.UtcNow;

        // Update member roles
        membership.Role = "owner";
        var previousOwnerMembership = org.Members.FirstOrDefault(m => m.UserId == previousOwnerId);
        if (previousOwnerMembership != null)
            previousOwnerMembership.Role = "admin";

        await _db.SaveChangesAsync();
        _logger.LogInformation("Admin transferred organization {OrgId} ownership from user {PreviousOwnerId} to {NewOwnerId}",
            orgId, previousOwnerId, request.NewOwnerId);

        return (true, null);
    }
}
