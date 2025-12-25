using System.Text.RegularExpressions;

namespace LrmCloud.Web.Services;

/// <summary>
/// Service for fetching data from GitHub (changelog, releases, etc.)
/// Uses raw.githubusercontent.com to avoid CORS issues in Blazor WASM.
/// </summary>
public partial class GitHubService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GitHubService> _logger;

    // Cache for releases
    private List<GitHubRelease>? _cachedReleases;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    // Raw GitHub URLs (CORS-friendly)
    private const string ChangelogUrl = "https://raw.githubusercontent.com/nickprotop/LocalizationManager/main/CHANGELOG.md";

    public GitHubService(HttpClient httpClient, ILogger<GitHubService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Fetches and parses releases from CHANGELOG.md.
    /// </summary>
    public async Task<List<GitHubRelease>> GetReleasesAsync(int count = 20)
    {
        // Return cached data if still valid
        if (_cachedReleases != null && DateTime.UtcNow < _cacheExpiry)
        {
            return _cachedReleases.Take(count).ToList();
        }

        try
        {
            var changelog = await _httpClient.GetStringAsync(ChangelogUrl);
            _cachedReleases = ParseChangelog(changelog);
            _cacheExpiry = DateTime.UtcNow.Add(CacheDuration);

            _logger.LogDebug("Parsed {Count} releases from CHANGELOG.md", _cachedReleases.Count);
            return _cachedReleases.Take(count).ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to fetch CHANGELOG.md from GitHub");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error parsing changelog");
        }

        return _cachedReleases?.Take(count).ToList() ?? new List<GitHubRelease>();
    }

    /// <summary>
    /// Gets the latest release version.
    /// </summary>
    public async Task<string?> GetLatestVersionAsync()
    {
        var releases = await GetReleasesAsync(1);
        return releases.FirstOrDefault()?.Version;
    }

    /// <summary>
    /// Clears the release cache.
    /// </summary>
    public void ClearCache()
    {
        _cachedReleases = null;
        _cacheExpiry = DateTime.MinValue;
    }

    /// <summary>
    /// Parses CHANGELOG.md content into releases.
    /// Expected format: ## [version] - YYYY-MM-DD
    /// </summary>
    private List<GitHubRelease> ParseChangelog(string content)
    {
        var releases = new List<GitHubRelease>();
        var lines = content.Split('\n');

        GitHubRelease? currentRelease = null;
        var bodyLines = new List<string>();

        foreach (var line in lines)
        {
            // Match version header: ## [0.6.25] - 2025-12-06
            var versionMatch = VersionHeaderRegex().Match(line);
            if (versionMatch.Success)
            {
                // Save previous release
                if (currentRelease != null)
                {
                    currentRelease = currentRelease with { Body = string.Join("\n", bodyLines).Trim() };
                    releases.Add(currentRelease);
                    bodyLines.Clear();
                }

                var version = versionMatch.Groups[1].Value;
                var dateStr = versionMatch.Groups[2].Value;
                DateTime.TryParse(dateStr, out var date);

                currentRelease = new GitHubRelease
                {
                    TagName = $"v{version}",
                    Name = $"v{version}",
                    Body = "",
                    PublishedAt = date,
                    HtmlUrl = $"{AppConstants.GitHubReleasesUrl}/tag/v{version}",
                    Prerelease = version.Contains("-")
                };
            }
            else if (currentRelease != null && !string.IsNullOrWhiteSpace(line))
            {
                // Skip headers like ### Added, ### Fixed, etc. but keep content
                if (!line.StartsWith("##"))
                {
                    bodyLines.Add(line);
                }
            }
        }

        // Don't forget the last release
        if (currentRelease != null)
        {
            currentRelease = currentRelease with { Body = string.Join("\n", bodyLines).Trim() };
            releases.Add(currentRelease);
        }

        return releases;
    }

    [GeneratedRegex(@"^## \[(\d+\.\d+\.\d+(?:-\w+)?)\](?: - )?(\d{4}-\d{2}-\d{2})?")]
    private static partial Regex VersionHeaderRegex();
}

/// <summary>
/// Represents a GitHub release parsed from CHANGELOG.md.
/// </summary>
public record GitHubRelease
{
    public required string TagName { get; init; }
    public required string Name { get; init; }
    public required string Body { get; init; }
    public DateTime PublishedAt { get; init; }
    public required string HtmlUrl { get; init; }
    public bool Prerelease { get; init; }

    /// <summary>
    /// Gets the version number from the tag (removes 'v' prefix if present).
    /// </summary>
    public string Version => TagName.TrimStart('v');

    /// <summary>
    /// Parses the body as a list of bullet points.
    /// </summary>
    public List<string> GetChangeItems()
    {
        if (string.IsNullOrWhiteSpace(Body))
            return new List<string>();

        return Body
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("- ") || line.StartsWith("* "))
            .Select(line => line.TrimStart('-', '*', ' '))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();
    }
}
