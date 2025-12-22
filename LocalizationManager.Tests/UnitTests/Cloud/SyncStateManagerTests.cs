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
    public async Task LoadAsync_NoFile_ReturnsNullState()
    {
        // Act
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.Null(result.State);
        Assert.False(result.WasCorrupted);
        Assert.False(result.NeedsMigration);
    }

    [Fact]
    public async Task LoadAsync_ValidV2File_ReturnsSyncState()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");

        var state = new SyncState
        {
            Version = 2,
            Timestamp = new DateTime(2023, 12, 1, 12, 0, 0, DateTimeKind.Utc),
            Entries = new Dictionary<string, Dictionary<string, string>>
            {
                { "WelcomeMessage", new Dictionary<string, string> { { "en", "hash1" }, { "fr", "hash2" } } }
            },
            ConfigProperties = new Dictionary<string, string>
            {
                { "defaultLanguage", "hash3" }
            }
        };
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(state));

        // Act
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.NotNull(result.State);
        Assert.False(result.WasCorrupted);
        Assert.False(result.NeedsMigration);
        Assert.Equal(2, result.State!.Version);
        Assert.Single(result.State.Entries);
        Assert.Equal("hash1", result.State.GetEntryHash("WelcomeMessage", "en"));
    }

    [Fact]
    public async Task LoadAsync_LegacyV1File_DetectsMigration()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");

        // V1 format with Files instead of Entries
        var legacyState = new
        {
            Version = 1,
            Timestamp = new DateTime(2023, 12, 1, 12, 0, 0, DateTimeKind.Utc),
            ConfigHash = "abc123",
            Files = new Dictionary<string, string>
            {
                { "Resources.resx", "hash1" },
                { "Resources.de.resx", "hash2" }
            }
        };
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(legacyState));

        // Act
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.NotNull(result.State);
        Assert.False(result.WasCorrupted);
        Assert.True(result.NeedsMigration);
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ReturnsCorrupted()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");
        await File.WriteAllTextAsync(statePath, "{ invalid json }");

        // Act
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.Null(result.State);
        Assert.True(result.WasCorrupted);
    }

    [Fact]
    public async Task LoadAsync_EmptyFile_ReturnsCorrupted()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");
        await File.WriteAllTextAsync(statePath, "");

        // Act
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.Null(result.State);
        Assert.True(result.WasCorrupted);
    }

    #endregion

    #region SaveAsync Tests

    [Fact]
    public async Task SaveAsync_CreatesLrmDirectory()
    {
        // Arrange
        var state = SyncState.CreateNew();

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, state);

        // Assert
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, ".lrm")));
    }

    [Fact]
    public async Task SaveAsync_WritesV2Format()
    {
        // Arrange
        var state = SyncState.CreateNew();
        state.SetEntryHash("TestKey", "en", "hash123");
        state.ConfigProperties["defaultLanguage"] = "configHash";

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, state);

        // Assert
        var statePath = Path.Combine(_testDirectory, ".lrm", "sync-state.json");
        Assert.True(File.Exists(statePath));

        var content = await File.ReadAllTextAsync(statePath);
        Assert.Contains("\"Version\": 2", content);
        Assert.Contains("TestKey", content);
        Assert.Contains("hash123", content);
        Assert.Contains("defaultLanguage", content);
    }

    [Fact]
    public async Task SaveAsync_OverwritesExisting()
    {
        // Arrange
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");
        await File.WriteAllTextAsync(statePath, "{\"Version\": 1, \"ConfigHash\": \"old\"}");

        var newState = SyncState.CreateNew();
        newState.ConfigProperties["setting"] = "new";

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, newState);

        // Assert
        var content = await File.ReadAllTextAsync(statePath);
        Assert.Contains("\"Version\": 2", content);
        Assert.Contains("new", content);
    }

    [Fact]
    public async Task SaveAsync_WritesIndentedJson()
    {
        // Arrange
        var state = SyncState.CreateNew();

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

    #region SyncState Entry Methods Tests

    [Fact]
    public void SyncState_SetEntryHash_CreatesKeyIfNotExists()
    {
        // Arrange
        var state = SyncState.CreateNew();

        // Act
        state.SetEntryHash("NewKey", "en", "hash123");

        // Assert
        Assert.Equal("hash123", state.GetEntryHash("NewKey", "en"));
    }

    [Fact]
    public void SyncState_SetEntryHash_UpdatesExistingHash()
    {
        // Arrange
        var state = SyncState.CreateNew();
        state.SetEntryHash("Key1", "en", "hash1");

        // Act
        state.SetEntryHash("Key1", "en", "hash2");

        // Assert
        Assert.Equal("hash2", state.GetEntryHash("Key1", "en"));
    }

    [Fact]
    public void SyncState_GetEntryHash_ReturnsNullForMissingKey()
    {
        // Arrange
        var state = SyncState.CreateNew();

        // Act
        var result = state.GetEntryHash("NonExistent", "en");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void SyncState_RemoveEntryHash_RemovesLanguage()
    {
        // Arrange
        var state = SyncState.CreateNew();
        state.SetEntryHash("Key1", "en", "hash1");
        state.SetEntryHash("Key1", "fr", "hash2");

        // Act
        state.RemoveEntryHash("Key1", "en");

        // Assert
        Assert.Null(state.GetEntryHash("Key1", "en"));
        Assert.Equal("hash2", state.GetEntryHash("Key1", "fr"));
    }

    [Fact]
    public void SyncState_RemoveEntryHash_RemovesKeyWhenEmpty()
    {
        // Arrange
        var state = SyncState.CreateNew();
        state.SetEntryHash("Key1", "en", "hash1");

        // Act
        state.RemoveEntryHash("Key1", "en");

        // Assert
        Assert.False(state.Entries.ContainsKey("Key1"));
    }

    [Fact]
    public void SyncState_RemoveEntryHash_RemovesEntireKey()
    {
        // Arrange
        var state = SyncState.CreateNew();
        state.SetEntryHash("Key1", "en", "hash1");
        state.SetEntryHash("Key1", "fr", "hash2");

        // Act
        state.RemoveEntryHash("Key1");

        // Assert
        Assert.False(state.Entries.ContainsKey("Key1"));
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public async Task SaveLoad_RoundTrip_PreservesV2Data()
    {
        // Arrange
        var original = SyncState.CreateNew();
        original.Timestamp = new DateTime(2023, 12, 15, 10, 30, 0, DateTimeKind.Utc);
        original.SetEntryHash("WelcomeMessage", "en", "hash1");
        original.SetEntryHash("WelcomeMessage", "fr", "hash2");
        original.SetEntryHash("SaveButton", "en", "hash3");
        original.ConfigProperties["defaultLanguage"] = "confighash1";
        original.ConfigProperties["translation.provider"] = "confighash2";

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, original);
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.NotNull(result.State);
        Assert.False(result.NeedsMigration);
        Assert.Equal(2, result.State!.Entries.Count);
        Assert.Equal("hash1", result.State.GetEntryHash("WelcomeMessage", "en"));
        Assert.Equal("hash2", result.State.GetEntryHash("WelcomeMessage", "fr"));
        Assert.Equal("hash3", result.State.GetEntryHash("SaveButton", "en"));
        Assert.Equal(2, result.State.ConfigProperties.Count);
    }

    [Fact]
    public async Task SaveLoad_EmptyEntries_PreservesData()
    {
        // Arrange
        var original = SyncState.CreateNew();

        // Act
        await SyncStateManager.SaveAsync(_testDirectory, original);
        var result = await SyncStateManager.LoadAsync(_testDirectory);

        // Assert
        Assert.NotNull(result.State);
        Assert.Empty(result.State!.Entries);
        Assert.Empty(result.State.ConfigProperties);
    }

    #endregion

    #region GetOrCreateAsync Tests

    [Fact]
    public async Task GetOrCreateAsync_NoFile_CreatesNewState()
    {
        // Act
        var state = await SyncStateManager.GetOrCreateAsync(_testDirectory);

        // Assert
        Assert.NotNull(state);
        Assert.Equal(2, state.Version);
        Assert.Empty(state.Entries);
    }

    [Fact]
    public async Task GetOrCreateAsync_ExistingV2_ReturnsExisting()
    {
        // Arrange
        var existing = SyncState.CreateNew();
        existing.SetEntryHash("Key1", "en", "hash1");
        await SyncStateManager.SaveAsync(_testDirectory, existing);

        // Act
        var state = await SyncStateManager.GetOrCreateAsync(_testDirectory);

        // Assert
        Assert.Equal("hash1", state.GetEntryHash("Key1", "en"));
    }

    [Fact]
    public async Task GetOrCreateAsync_LegacyV1_CreatesNewState()
    {
        // Arrange - Create a legacy v1 file
        var lrmDir = Path.Combine(_testDirectory, ".lrm");
        Directory.CreateDirectory(lrmDir);
        var statePath = Path.Combine(lrmDir, "sync-state.json");
        var legacyState = new { Version = 1, ConfigHash = "old", Files = new Dictionary<string, string> { { "test.resx", "hash" } } };
        await File.WriteAllTextAsync(statePath, JsonSerializer.Serialize(legacyState));

        // Act
        var state = await SyncStateManager.GetOrCreateAsync(_testDirectory);

        // Assert
        Assert.Equal(2, state.Version);
        Assert.Empty(state.Entries); // New state, not migrated
    }

    #endregion

    #region Exists Tests

    [Fact]
    public void Exists_NoFile_ReturnsFalse()
    {
        // Act
        var result = SyncStateManager.Exists(_testDirectory);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task Exists_WithFile_ReturnsTrue()
    {
        // Arrange
        var state = SyncState.CreateNew();
        await SyncStateManager.SaveAsync(_testDirectory, state);

        // Act
        var result = SyncStateManager.Exists(_testDirectory);

        // Assert
        Assert.True(result);
    }

    #endregion
}
