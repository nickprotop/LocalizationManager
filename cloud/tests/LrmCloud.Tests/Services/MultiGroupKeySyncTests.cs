// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using LrmCloud.Api.Data;
using LrmCloud.Api.Services;
using LrmCloud.Shared.DTOs.Sync;
using LrmCloud.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LrmCloud.Tests.Services;

public class MultiGroupKeySyncTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly KeySyncService _sut;
    private readonly Mock<IProjectService> _projectService;
    private readonly Mock<ISyncHistoryService> _historyService;
    private readonly Mock<IResourceService> _resourceService;

    public MultiGroupKeySyncTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _db = new AppDbContext(options);

        _projectService = new Mock<IProjectService>();
        _projectService.Setup(p => p.CanManageResourcesAsync(It.IsAny<int>(), It.IsAny<int>())).ReturnsAsync(true);

        _historyService = new Mock<ISyncHistoryService>();
        _historyService.Setup(h => h.RecordPushAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string?>(),
                It.IsAny<List<SyncChangeEntry>>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SyncHistory
            {
                ProjectId = 1,
                HistoryId = "h-1",
                OperationType = "push",
                Source = "cli",
                EntriesAdded = 0,
                EntriesModified = 0,
                EntriesDeleted = 0,
                CreatedAt = DateTime.UtcNow
            });

        _resourceService = new Mock<IResourceService>();
        _resourceService.Setup(r => r.InvalidateValidationCacheAsync(It.IsAny<int>())).Returns(Task.CompletedTask);

        _sut = new KeySyncService(
            _db,
            _projectService.Object,
            _historyService.Object,
            _resourceService.Object,
            new Mock<ILogger<KeySyncService>>().Object);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    [Fact]
    public async Task PushAsync_SameKeyNameInTwoGroups_StoresBothRows()
    {
        var project = new Project
        {
            Slug = "p", Name = "P", UserId = 1, DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var request = new KeySyncPushRequest
        {
            Entries = new List<EntryChangeDto>
            {
                new() { Key = "OK", BaseName = "CustomerResources", Lang = "", Value = "Confirm" },
                new() { Key = "OK", BaseName = "SharedResources",   Lang = "", Value = "OK" }
            }
        };

        var response = await _sut.PushAsync(project.Id, userId: 1, request);

        Assert.Equal(2, response.Applied);
        Assert.Equal(2, response.Added);
        Assert.Empty(response.Conflicts);

        var rows = await _db.ResourceKeys
            .Where(rk => rk.ProjectId == project.Id && rk.KeyName == "OK")
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Contains(rows, r => r.BaseName == "CustomerResources");
        Assert.Contains(rows, r => r.BaseName == "SharedResources");

        // The grouped hash map should expose both
        Assert.True(response.NewEntryHashesByGroup.ContainsKey("CustomerResources"));
        Assert.True(response.NewEntryHashesByGroup.ContainsKey("SharedResources"));
    }

    [Fact]
    public async Task PushAsync_LegacyEmptyBaseName_StoresWithEmptyBaseName()
    {
        var project = new Project
        {
            Slug = "p", Name = "P", UserId = 1, DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        var request = new KeySyncPushRequest
        {
            Entries = new List<EntryChangeDto>
            {
                // No BaseName set -> defaults to ""
                new() { Key = "Hello", Lang = "", Value = "Hello" }
            }
        };

        var response = await _sut.PushAsync(project.Id, userId: 1, request);
        Assert.Equal(1, response.Applied);

        var row = await _db.ResourceKeys.SingleAsync(rk => rk.ProjectId == project.Id && rk.KeyName == "Hello");
        Assert.Equal(string.Empty, row.BaseName);
    }

    [Fact]
    public async Task MigrateGroupsAsync_RekeysLegacyEmptyBaseNameRows()
    {
        var project = new Project
        {
            Slug = "p", Name = "P", UserId = 1, DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // Simulate legacy single-group state: two keys under BaseName="".
        _db.ResourceKeys.AddRange(
            new ResourceKey { ProjectId = project.Id, KeyName = "Hello",   BaseName = "" },
            new ResourceKey { ProjectId = project.Id, KeyName = "Goodbye", BaseName = "" });
        await _db.SaveChangesAsync();

        var result = await _sut.MigrateGroupsAsync(project.Id, userId: 1,
            new MigrateGroupsRequest { FromBaseName = "", ToBaseName = "SharedResources" });

        Assert.Equal(2, result.RowsUpdated);
        Assert.Empty(result.ConflictingKeys);

        var rows = await _db.ResourceKeys.Where(rk => rk.ProjectId == project.Id).ToListAsync();
        Assert.All(rows, r => Assert.Equal("SharedResources", r.BaseName));
    }

    [Fact]
    public async Task MigrateGroupsAsync_DetectsConflictsAndRollsBack()
    {
        var project = new Project
        {
            Slug = "p", Name = "P", UserId = 1, DefaultLanguage = "en",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();

        // One row in the source group AND a same-named row already in the target group.
        _db.ResourceKeys.AddRange(
            new ResourceKey { ProjectId = project.Id, KeyName = "Hello",  BaseName = "" },
            new ResourceKey { ProjectId = project.Id, KeyName = "Hello",  BaseName = "SharedResources" },
            new ResourceKey { ProjectId = project.Id, KeyName = "Solo",   BaseName = "" });
        await _db.SaveChangesAsync();

        var result = await _sut.MigrateGroupsAsync(project.Id, userId: 1,
            new MigrateGroupsRequest { FromBaseName = "", ToBaseName = "SharedResources" });

        Assert.Equal(0, result.RowsUpdated);
        Assert.Contains("Hello", result.ConflictingKeys);

        // Source-group rows are unchanged after rollback.
        var emptyGroupRows = await _db.ResourceKeys
            .Where(rk => rk.ProjectId == project.Id && rk.BaseName == "")
            .CountAsync();
        Assert.Equal(2, emptyGroupRows);
    }
}
