using LocalizationManager.Core.Cloud;
using LocalizationManager.Core.Cloud.Models;
using System.Text.Json;
using Xunit;

namespace LocalizationManager.Tests.UnitTests.Cloud;

public class SyncStateManagerTests : IDisposable
{
    private readonly string _testDirectory;

    public SyncStateManagerTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"lrm_sync_state_test_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    #region LoadAsync Tests

    [Fact]
    public async Task LoadAsync_NoFile_ReturnsNull()
    {
        // Act
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_ValidFile_ReturnsSyncState()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");

        var state = new SyncState
        {
            Timestamp = new DateTime(2023, 12, 1, 12, 0, 0, DateTimeKind.Utc),
            ConfigHash = "abc123",
            Files = new Dictionary<string, string>
            {
                { "Resources.resx", "hash1" },
                { "Resources.de.resx", "hash2" }
            }
        };
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state));

        // Act
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("abc123", result!.ConfigHash);
        Assert.Equal(2, result.Files.Count);
        Assert.Equal("hash1", result.Files["Resources.resx"]);
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ReturnsNull()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");
        await File.WriteAllTextAsync(statePath, "{ invalid json }");

        // Act
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task LoadAsync_EmptyFile_ReturnsNull()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");
        await File.WriteAllTextAsync(statePath, "");

        // Act
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_CreatesLrmDirectory()
    {
        // Arrange
        var state = new SyncState
        {
            Timestamp = DateTime.UtcNow,
            ConfigHash = "test",
            Files = new Dictionary<string, string>()
        };

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, state);

        // Assert
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, ".lrm")));
    }

    [Fact]
    public async Task SaveAsync_WritesValidJson()
    {
        // Arrange
        var state = new SyncState
        {
            Timestamp = new DateTime(2023, 12, 1, 12, 0, 0, DateTimeKind.Utc),
            ConfigHash = "configHash123",
            Files = new Dictionary<string, string>
            {
                { "test.resx", "fileHash456" }
            }
        };

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, state);

        // Assert
        var statePath = Path.Combine(_testDirectory, ".lrm", "sync-state.json");
        Assert.True(File.Exists(statePath));

        var content = await File.ReadAllTextAsync(statePath);
        Assert.Contains("configHash123", content);
        Assert.Contains("test.resx", content);
        Assert.Contains("fileHash456", content);
    }

    [Fact]
    public async Task SaveAsync_OverwritesExisting()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");
        await File.WriteAllTextAsync(statePath, "{\"ConfigHash\": \"old\"}");

        var newState = new SyncState
        {
            Timestamp = DateTime.UtcNow,
            ConfigHash = "new",
            Files = new Dictionary<string, string>()
        };

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, newState);

        // Assert
        var content = await File.ReadAllTextAsync(statePath);
        Assert.Contains("new", content);
        Assert.DoesNotContain("old", content);
    }

    [Fact]
    public async Task SaveAsync_WritesIndentedJson()
    {
        // Arrange
        var state = new SyncState
        {
            Timestamp = DateTime.UtcNow,
            ConfigHash = "hash",
            Files = new Dictionary<string, string>()
        };

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, state);

        // Assert
        var statePath = Path.Combine(_testDirectory, ".lrm", "sync-state.json");
        var content = await File.ReadAllTextAsync(statePath);
        Assert.Contains("\n", content); // Indented JSON has newlines
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_NoFile_DoesNotThrow()
    {
        // Act & Assert - Should not throw
        SyncStateManager.Clear(_testDirectory);
    }

    [Fact]
    public async Task Clear_ExistingFile_DeletesFile()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");
        await File.WriteAllTextAsync(statePath, "{}");

        // Act
        SyncStateManager.Clear(_testDirectory);

        // Assert
        Assert.False(File.Exists(statePath));
    }

    [Fact]
    public async Task Clear_PreservesLrmDirectory()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");
        var otherFile = Path.Combine(lrmDir, "other.json");
        await File.WriteAllTextAsync(statePath, "{}");
        await File.WriteAllTextAsync(otherFile, "{}");

        // Act
        SyncStateManager.Clear(_testDirectory);

        // Assert
        Assert.True(Directory.Exists(lrmDir));
        Assert.True(File.Exists(otherFile));
        Assert.False(File.Exists(statePath));
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public async Task SaveLoad_RoundTrip_PreservesData()
    {
        // Arrange
        var original = new SyncState
        {
            Timestamp = new DateTime(2023, 12, 15, 10, 30, 0, DateTimeKind.Utc),
            ConfigHash = "sha256hash",
            Files = new Dictionary<string, string>
            {
                { "Resources/strings.resx", "hash1" },
                { "Resources/strings.de.resx", "hash2" },
                { "Resources/strings.fr.resx", "hash3" }
            }
        };

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, original);
        var loaded = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(original.ConfigHash, loaded!.ConfigHash);
        Assert.Equal(original.Files.Count, loaded.Files.Count);
        foreach (var kvp in original.Files)
        {
            Assert.True(loaded.Files.ContainsKey(kvp.Key));
            Assert.Equal(kvp.Value, loaded.Files[kvp.Key]);
        }
    }

    [Fact]
    public async Task SaveLoad_EmptyFiles_PreservesData()
    {
        // Arrange
        var original = new SyncState
        {
            Timestamp = DateTime.UtcNow,
            ConfigHash = "hash",
            Files = new Dictionary<string, string>()
        };

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, original);
        var loaded = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.NotNull(loaded);
        Assert.Empty(loaded!.Files);
    }

    [Fact]
    public async Task SaveLoad_NullConfigHash_PreservesData()
    {
        // Arrange
        var original = new SyncState
        {
            Timestamp = DateTime.UtcNow,
            ConfigHash = null,
            Files = new Dictionary<string, string>()
        };

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, original);
        var loaded = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.NotNull(loaded);
        Assert.Null(loaded!.ConfigHash);
    }

    #endregion
}
