// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LocalizationManager.Core.Backup;
using LocalizationManager.Shared.Models;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Backup;

public class BackupRotationPolicyTests
{
    [Fact]
    public async Task ApplyRotationAsync_KeepsRecentBackups()
    {
        // Arrange
        var policy = new BackupRotationPolicy
        {
            KeepAllForHours = 24,
            MaxTotalBackups = 5
        };

        var manifest = new BackupManifest
        {
            FileName = "TestResource.resx",
            Backups = CreateTestBackups(10, DateTime.UtcNow.AddHours(-2))
        };

        var tempDir = Path.Combine(Path.GetTempPath(), $"rotation-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create dummy backup files
            foreach (var backup in manifest.Backups)
            {
                File.WriteAllText(Path.Combine(tempDir, backup.FilePath), "test");
            }

            // Act
            await policy.ApplyRotationAsync(manifest, tempDir);

            // Assert
            Assert.Equal(5, manifest.Backups.Count); // Should keep only 5 most recent
            Assert.Equal(10, manifest.Backups[0].Version); // Most recent
            Assert.Equal(6, manifest.Backups[4].Version); // Oldest kept
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyRotationAsync_RemovesOldBackups()
    {
        // Arrange
        var policy = new BackupRotationPolicy
        {
            KeepMonthlyForMonths = 1, // Only keep backups from last month
            MaxTotalBackups = 100
        };

        var manifest = new BackupManifest
        {
            FileName = "TestResource.resx",
            Backups = new List<BackupMetadata>
            {
                CreateBackup(1, DateTime.UtcNow.AddDays(-60)), // Too old
                CreateBackup(2, DateTime.UtcNow.AddDays(-45)), // Too old
                CreateBackup(3, DateTime.UtcNow.AddDays(-20)), // Within age limit
                CreateBackup(4, DateTime.UtcNow.AddDays(-10)), // Within age limit
                CreateBackup(5, DateTime.UtcNow.AddDays(-5))   // Within age limit
            }
        };

        var tempDir = Path.Combine(Path.GetTempPath(), $"rotation-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Create dummy backup files
            foreach (var backup in manifest.Backups)
            {
                File.WriteAllText(Path.Combine(tempDir, backup.FilePath), "test");
            }

            // Act
            await policy.ApplyRotationAsync(manifest, tempDir);

            // Assert - Old backups should be removed
            Assert.DoesNotContain(manifest.Backups, b => b.Version == 1);
            Assert.DoesNotContain(manifest.Backups, b => b.Version == 2);
            Assert.Contains(manifest.Backups, b => b.Version == 3);
            Assert.Contains(manifest.Backups, b => b.Version == 4);
            Assert.Contains(manifest.Backups, b => b.Version == 5);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyRotationAsync_KeepsDailyBackups()
    {
        // Arrange
        var policy = new BackupRotationPolicy
        {
            KeepAllForHours = 0,
            KeepDailyForDays = 7,
            KeepWeeklyForWeeks = 0,
            KeepMonthlyForMonths = 0,
            MaxTotalBackups = 100
        };

        var now = DateTime.UtcNow;
        var backups = new List<BackupMetadata>();

        // Create 7 days of backups (one per day)
        for (int i = 0; i < 7; i++)
        {
            backups.Add(CreateBackup(i + 1, now.AddDays(-i)));
        }

        // Add some older backups that should be deleted
        backups.Add(CreateBackup(8, now.AddDays(-8)));
        backups.Add(CreateBackup(9, now.AddDays(-9)));

        var manifest = new BackupManifest
        {
            FileName = "TestResource.resx",
            Backups = backups
        };

        var tempDir = Path.Combine(Path.GetTempPath(), $"rotation-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var backup in manifest.Backups)
            {
                File.WriteAllText(Path.Combine(tempDir, backup.FilePath), "test");
            }

            // Act
            await policy.ApplyRotationAsync(manifest, tempDir);

            // Assert - Should keep the 7 daily backups
            Assert.True(manifest.Backups.Count <= 7);
            Assert.All(manifest.Backups, b => Assert.True(b.Version <= 7));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyRotationAsync_KeepsWeeklyBackups()
    {
        // Arrange
        var policy = new BackupRotationPolicy
        {
            KeepAllForHours = 0,
            KeepDailyForDays = 0,
            KeepWeeklyForWeeks = 4,
            KeepMonthlyForMonths = 0,
            MaxTotalBackups = 100
        };

        var now = DateTime.UtcNow;
        var backups = new List<BackupMetadata>();

        // Create backups for 4 weeks (one per week)
        for (int i = 0; i < 4; i++)
        {
            backups.Add(CreateBackup(i + 1, now.AddDays(-i * 7)));
        }

        var manifest = new BackupManifest
        {
            FileName = "TestResource.resx",
            Backups = backups
        };

        var tempDir = Path.Combine(Path.GetTempPath(), $"rotation-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var backup in manifest.Backups)
            {
                File.WriteAllText(Path.Combine(tempDir, backup.FilePath), "test");
            }

            // Act
            await policy.ApplyRotationAsync(manifest, tempDir);

            // Assert - Should keep the weekly backups
            Assert.Equal(4, manifest.Backups.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyRotationAsync_KeepsMonthlyBackups()
    {
        // Arrange
        var policy = new BackupRotationPolicy
        {
            KeepAllForHours = 0,
            KeepDailyForDays = 0,
            KeepWeeklyForWeeks = 0,
            KeepMonthlyForMonths = 6,
            MaxTotalBackups = 100
        };

        var now = DateTime.UtcNow;
        var backups = new List<BackupMetadata>();

        // Create backups for 6 months (one per month)
        for (int i = 0; i < 6; i++)
        {
            backups.Add(CreateBackup(i + 1, now.AddMonths(-i)));
        }

        var manifest = new BackupManifest
        {
            FileName = "TestResource.resx",
            Backups = backups
        };

        var tempDir = Path.Combine(Path.GetTempPath(), $"rotation-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var backup in manifest.Backups)
            {
                File.WriteAllText(Path.Combine(tempDir, backup.FilePath), "test");
            }

            // Act
            await policy.ApplyRotationAsync(manifest, tempDir);

            // Assert - Should keep the monthly backups
            Assert.Equal(6, manifest.Backups.Count);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyRotationAsync_HandlesEmptyList()
    {
        // Arrange
        var policy = new BackupRotationPolicy
        {
            KeepAllForHours = 24,
            MaxTotalBackups = 5
        };

        var manifest = new BackupManifest
        {
            FileName = "TestResource.resx",
            Backups = new List<BackupMetadata>()
        };

        var tempDir = Path.Combine(Path.GetTempPath(), $"rotation-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // Act
            await policy.ApplyRotationAsync(manifest, tempDir);

            // Assert
            Assert.Empty(manifest.Backups);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task ApplyRotationAsync_ComplexRotationStrategy()
    {
        // Arrange
        var policy = new BackupRotationPolicy
        {
            KeepAllForHours = 0,
            KeepDailyForDays = 7,
            KeepWeeklyForWeeks = 4,
            KeepMonthlyForMonths = 3,
            MaxTotalBackups = 50
        };

        var now = DateTime.UtcNow;
        var backups = new List<BackupMetadata>();
        int version = 1;

        // Daily backups for last 10 days
        for (int i = 0; i < 10; i++)
        {
            backups.Add(CreateBackup(version++, now.AddDays(-i)));
        }

        // Weekly backups for last 8 weeks
        for (int i = 1; i <= 8; i++)
        {
            backups.Add(CreateBackup(version++, now.AddDays(-i * 7)));
        }

        // Monthly backups for last 6 months
        for (int i = 1; i <= 6; i++)
        {
            backups.Add(CreateBackup(version++, now.AddMonths(-i)));
        }

        var manifest = new BackupManifest
        {
            FileName = "TestResource.resx",
            Backups = backups
        };

        var initialCount = manifest.Backups.Count;

        var tempDir = Path.Combine(Path.GetTempPath(), $"rotation-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            foreach (var backup in manifest.Backups)
            {
                File.WriteAllText(Path.Combine(tempDir, backup.FilePath), "test");
            }

            // Act
            await policy.ApplyRotationAsync(manifest, tempDir);

            // Assert - Should have deleted some backups but kept strategic ones
            Assert.True(manifest.Backups.Count < initialCount);
            Assert.True(manifest.Backups.Count > 0);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    private List<BackupMetadata> CreateTestBackups(int count, DateTime baseDate)
    {
        var backups = new List<BackupMetadata>();
        for (int i = 1; i <= count; i++)
        {
            // Create backups where higher version numbers have newer timestamps
            backups.Add(CreateBackup(i, baseDate.AddHours(i - count)));
        }
        return backups;
    }

    private BackupMetadata CreateBackup(int version, DateTime timestamp)
    {
        return new BackupMetadata
        {
            Version = version,
            Timestamp = timestamp,
            FilePath = $"TestResource.v{version:D3}.resx",
            Operation = "test",
            KeyCount = 10,
            Hash = $"hash{version}"
        };
    }
}
