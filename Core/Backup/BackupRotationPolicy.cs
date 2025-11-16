// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using LocalizationManager.Shared.Models;

namespace LocalizationManager.Core.Backup;

/// <summary>
/// Smart backup rotation policy based on retention rules.
/// </summary>
public class BackupRotationPolicy
{
    /// <summary>
    /// Number of hours to keep all backups (default: 24).
    /// </summary>
    public int KeepAllForHours { get; set; } = 24;

    /// <summary>
    /// Number of days to keep daily backups (default: 7).
    /// </summary>
    public int KeepDailyForDays { get; set; } = 7;

    /// <summary>
    /// Number of weeks to keep weekly backups (default: 4).
    /// </summary>
    public int KeepWeeklyForWeeks { get; set; } = 4;

    /// <summary>
    /// Number of months to keep monthly backups (default: 6).
    /// </summary>
    public int KeepMonthlyForMonths { get; set; } = 6;

    /// <summary>
    /// Maximum total number of backups to keep (default: 100).
    /// </summary>
    public int MaxTotalBackups { get; set; } = 100;

    /// <summary>
    /// Applies the rotation policy to a backup manifest.
    /// </summary>
    /// <param name="manifest">The backup manifest to apply rotation to.</param>
    /// <param name="backupDir">Directory containing the backup files.</param>
    public async Task ApplyRotationAsync(BackupManifest manifest, string backupDir)
    {
        var now = DateTime.UtcNow;
        var backupsToKeep = new HashSet<BackupMetadata>();

        // Sort backups by timestamp descending (newest first)
        var sortedBackups = manifest.Backups.OrderByDescending(b => b.Timestamp).ToList();

        // Keep all backups within the recent time window
        var recentCutoff = now.AddHours(-KeepAllForHours);
        foreach (var backup in sortedBackups)
        {
            if (backup.Timestamp >= recentCutoff)
            {
                backupsToKeep.Add(backup);
            }
        }

        // Keep daily backups
        var dailyCutoff = now.AddDays(-KeepDailyForDays);
        var dailyBackups = new Dictionary<string, BackupMetadata>(); // Key: YYYY-MM-DD
        foreach (var backup in sortedBackups)
        {
            if (backup.Timestamp >= dailyCutoff && backup.Timestamp < recentCutoff)
            {
                var dateKey = backup.Timestamp.ToString("yyyy-MM-dd");
                if (!dailyBackups.ContainsKey(dateKey))
                {
                    dailyBackups[dateKey] = backup;
                    backupsToKeep.Add(backup);
                }
            }
        }

        // Keep weekly backups
        var weeklyCutoff = now.AddDays(-KeepWeeklyForWeeks * 7);
        var weeklyBackups = new Dictionary<string, BackupMetadata>(); // Key: YYYY-Www
        foreach (var backup in sortedBackups)
        {
            if (backup.Timestamp >= weeklyCutoff && backup.Timestamp < dailyCutoff)
            {
                var weekKey = GetIsoWeekKey(backup.Timestamp);
                if (!weeklyBackups.ContainsKey(weekKey))
                {
                    weeklyBackups[weekKey] = backup;
                    backupsToKeep.Add(backup);
                }
            }
        }

        // Keep monthly backups
        var monthlyCutoff = now.AddMonths(-KeepMonthlyForMonths);
        var monthlyBackups = new Dictionary<string, BackupMetadata>(); // Key: YYYY-MM
        foreach (var backup in sortedBackups)
        {
            if (backup.Timestamp >= monthlyCutoff && backup.Timestamp < weeklyCutoff)
            {
                var monthKey = backup.Timestamp.ToString("yyyy-MM");
                if (!monthlyBackups.ContainsKey(monthKey))
                {
                    monthlyBackups[monthKey] = backup;
                    backupsToKeep.Add(backup);
                }
            }
        }

        // If we still have too many backups, remove the oldest
        if (backupsToKeep.Count > MaxTotalBackups)
        {
            var sorted = backupsToKeep.OrderByDescending(b => b.Timestamp).ToList();
            backupsToKeep = sorted.Take(MaxTotalBackups).ToHashSet();
        }

        // Delete backups that are not in the keep set
        var toDelete = manifest.Backups.Where(b => !backupsToKeep.Contains(b)).ToList();
        foreach (var backup in toDelete)
        {
            // Delete backup file
            var backupFilePath = Path.Combine(backupDir, backup.FilePath);
            if (File.Exists(backupFilePath))
            {
                try
                {
                    File.Delete(backupFilePath);
                }
                catch
                {
                    // Ignore deletion errors
                }
            }

            // Remove from manifest
            manifest.Backups.Remove(backup);
        }

        // Sort remaining backups by version descending (newest first)
        manifest.Backups = manifest.Backups.OrderByDescending(b => b.Version).ToList();

        await Task.CompletedTask;
    }

    /// <summary>
    /// Gets the ISO week key for a date (e.g., "2025-W03").
    /// </summary>
    private string GetIsoWeekKey(DateTime date)
    {
        // ISO 8601 week number
        var dayOfWeek = (int)date.DayOfWeek;
        var dayOfYear = date.DayOfYear;

        // Adjust for ISO 8601: Monday = 1, Sunday = 7
        var isoWeekDay = dayOfWeek == 0 ? 7 : dayOfWeek;

        // Find the ISO week number
        var thursday = date.AddDays(4 - isoWeekDay);
        var isoYear = thursday.Year;
        var jan4 = new DateTime(isoYear, 1, 4);
        var isoWeek = (thursday.DayOfYear - jan4.DayOfYear) / 7 + 1;

        return $"{isoYear}-W{isoWeek:D2}";
    }

    /// <summary>
    /// Creates a default rotation policy.
    /// </summary>
    public static BackupRotationPolicy Default => new()
    {
        KeepAllForHours = 24,
        KeepDailyForDays = 7,
        KeepWeeklyForWeeks = 4,
        KeepMonthlyForMonths = 6,
        MaxTotalBackups = 100
    };

    /// <summary>
    /// Creates a minimal rotation policy (keep less history).
    /// </summary>
    public static BackupRotationPolicy Minimal => new()
    {
        KeepAllForHours = 6,
        KeepDailyForDays = 3,
        KeepWeeklyForWeeks = 2,
        KeepMonthlyForMonths = 2,
        MaxTotalBackups = 20
    };

    /// <summary>
    /// Creates an aggressive rotation policy (keep more history).
    /// </summary>
    public static BackupRotationPolicy Aggressive => new()
    {
        KeepAllForHours = 48,
        KeepDailyForDays = 14,
        KeepWeeklyForWeeks = 8,
        KeepMonthlyForMonths = 12,
        MaxTotalBackups = 200
    };
}
