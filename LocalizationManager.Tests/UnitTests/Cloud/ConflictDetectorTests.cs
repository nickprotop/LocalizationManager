using LocalizationManager.Core.Cloud;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

public class ConflictDetectorTests
{
    private readonly ConflictDetector _detector = new();

    [Fact]
    public void DetectResourceConflicts_NoChanges_ReturnsNoConflicts()
    {
        // Arrange
        var local = new List<FileDto>
        {
            new() { Path = "test.resx", Hash = "hash1", Content = "content1" }
        };
        var remote = new List<FileDto>
        {
            new() { Path = "test.resx", Hash = "hash1", Content = "content1" }
        };

        // Act
        var conflicts = _detector.DetectResourceConflicts(local, remote);

        // Assert
        Assert.Empty(conflicts);
    }

    [Fact]
    public void DetectResourceConflicts_BothModified_ReturnsConflict()
    {
        // Arrange
        var local = new List<FileDto>
        {
            new() { Path = "test.resx", Hash = "hash_local", Content = "local content" }
        };
        var remote = new List<FileDto>
        {
            new() { Path = "test.resx", Hash = "hash_remote", Content = "remote content" }
        };

        // Act
        var conflicts = _detector.DetectResourceConflicts(local, remote);

        // Assert
        Assert.Single(conflicts);
        Assert.Equal(ConflictDetector.ConflictType.BothModified, conflicts[0].Type);
        Assert.Equal("test.resx", conflicts[0].Path);
    }

    [Fact]
    public void DetectConfigurationConflict_SameContent_ReturnsNull()
    {
        // Arrange
        var localConfig = "{\"format\":\"resx\",\"defaultLanguage\":\"en\"}";
        var remoteConfig = "{\"format\":\"resx\",\"defaultLanguage\":\"en\"}";

        // Act
        var conflict = _detector.DetectConfigurationConflict(localConfig, remoteConfig);

        // Assert
        Assert.Null(conflict);
    }

    [Fact]
    public void DetectConfigurationConflict_DifferentContent_ReturnsConflict()
    {
        // Arrange
        var localConfig = "{\"format\":\"resx\",\"defaultLanguage\":\"en\"}";
        var remoteConfig = "{\"format\":\"json\",\"defaultLanguage\":\"fr\"}";

        // Act
        var conflict = _detector.DetectConfigurationConflict(localConfig, remoteConfig);

        // Assert
        Assert.NotNull(conflict);
        Assert.Equal(ConflictDetector.ConflictType.ConfigurationConflict, conflict.Type);
        Assert.Contains("lrm.json", conflict.Path);
    }

    [Fact]
    public void GetDiffSummary_NoChanges_ReturnsEmptySummary()
    {
        // Arrange
        var local = new List<FileDto>
        {
            new() { Path = "test.resx", Hash = "hash1", Content = "content" }
        };
        var remote = new List<FileDto>
        {
            new() { Path = "test.resx", Hash = "hash1", Content = "content" }
        };

        // Act
        var diff = _detector.GetDiffSummary(local, remote);

        // Assert
        Assert.Empty(diff.FilesToAdd);
        Assert.Empty(diff.FilesToUpdate);
        Assert.Empty(diff.FilesToDelete);
        Assert.False(diff.HasChanges);
        Assert.Equal(0, diff.TotalChanges);
    }

    [Fact]
    public void GetDiffSummary_NewFilesRemotely_ReturnsFilesToAdd()
    {
        // Arrange
        var local = new List<FileDto>();
        var remote = new List<FileDto>
        {
            new() { Path = "new.resx", Hash = "hash1", Content = "content" }
        };

        // Act
        var diff = _detector.GetDiffSummary(local, remote);

        // Assert
        Assert.Single(diff.FilesToAdd);
        Assert.Contains("new.resx", diff.FilesToAdd);
        Assert.True(diff.HasChanges);
        Assert.Equal(1, diff.TotalChanges);
    }

    [Fact]
    public void GetDiffSummary_DeletedFilesRemotely_ReturnsFilesToDelete()
    {
        // Arrange
        var local = new List<FileDto>
        {
            new() { Path = "deleted.resx", Hash = "hash1", Content = "content" }
        };
        var remote = new List<FileDto>();

        // Act
        var diff = _detector.GetDiffSummary(local, remote);

        // Assert
        Assert.Single(diff.FilesToDelete);
        Assert.Contains("deleted.resx", diff.FilesToDelete);
        Assert.True(diff.HasChanges);
        Assert.Equal(1, diff.TotalChanges);
    }

    [Fact]
    public void GetDiffSummary_ModifiedFiles_ReturnsFilesToUpdate()
    {
        // Arrange
        var local = new List<FileDto>
        {
            new() { Path = "modified.resx", Hash = "old_hash", Content = "old content" }
        };
        var remote = new List<FileDto>
        {
            new() { Path = "modified.resx", Hash = "new_hash", Content = "new content" }
        };

        // Act
        var diff = _detector.GetDiffSummary(local, remote);

        // Assert
        Assert.Single(diff.FilesToUpdate);
        Assert.Contains("modified.resx", diff.FilesToUpdate);
        Assert.True(diff.HasChanges);
        Assert.Equal(1, diff.TotalChanges);
    }

    [Fact]
    public void GetDiffSummary_MultipleChanges_ReturnsCorrectCounts()
    {
        // Arrange
        var local = new List<FileDto>
        {
            new() { Path = "modified.resx", Hash = "old_hash", Content = "old" },
            new() { Path = "deleted.resx", Hash = "hash2", Content = "content2" }
        };
        var remote = new List<FileDto>
        {
            new() { Path = "modified.resx", Hash = "new_hash", Content = "new" },
            new() { Path = "new.resx", Hash = "hash3", Content = "content3" }
        };

        // Act
        var diff = _detector.GetDiffSummary(local, remote);

        // Assert
        Assert.Single(diff.FilesToAdd);
        Assert.Single(diff.FilesToUpdate);
        Assert.Single(diff.FilesToDelete);
        Assert.Contains("new.resx", diff.FilesToAdd);
        Assert.Contains("modified.resx", diff.FilesToUpdate);
        Assert.Contains("deleted.resx", diff.FilesToDelete);
        Assert.True(diff.HasChanges);
        Assert.Equal(3, diff.TotalChanges);
    }
}
