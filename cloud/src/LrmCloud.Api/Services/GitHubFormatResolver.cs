using System.Text.Json;
using LrmCloud.Shared.Entities;

namespace LrmCloud.Api.Services;

/// <summary>
/// Resolves the localization format for GitHub operations.
/// Priority: GitHubFormat explicit > lrm.json in repo > auto-detect from files.
/// </summary>
public class GitHubFormatResolver : IGitHubFormatResolver
{
    private readonly IGitHubApiService _githubApi;
    private readonly ILogger<GitHubFormatResolver> _logger;

    public GitHubFormatResolver(
        IGitHubApiService githubApi,
        ILogger<GitHubFormatResolver> logger)
    {
        _githubApi = githubApi;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<string> ResolveFormatAsync(
        Project project,
        int userId,
        string owner,
        string repo,
        string branch,
        string basePath)
    {
        // 1. Check explicit GitHubFormat
        if (!string.IsNullOrEmpty(project.GitHubFormat) &&
            !project.GitHubFormat.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("Using explicit GitHubFormat: {Format}", project.GitHubFormat);
            return project.GitHubFormat;
        }

        // 2. Try to read lrm.json from repo
        var lrmConfig = await TryReadLrmConfigAsync(userId, owner, repo, branch, basePath);
        if (!string.IsNullOrEmpty(lrmConfig?.ResourceFormat))
        {
            _logger.LogDebug("Using format from lrm.json: {Format}", lrmConfig.ResourceFormat);
            return lrmConfig.ResourceFormat;
        }

        // 3. Auto-detect from files in repo using existing detection
        var detectedPaths = await _githubApi.DetectLocalizationFilesAsync(userId, owner, repo, branch, basePath);
        if (detectedPaths.Count > 0)
        {
            var detectedFormat = detectedPaths[0].Format;
            _logger.LogDebug("Auto-detected format: {Format}", detectedFormat);
            return detectedFormat;
        }

        // 4. Default to JSON if nothing detected
        _logger.LogWarning("Could not detect format for {Owner}/{Repo}, defaulting to json", owner, repo);
        return "json";
    }

    /// <summary>
    /// Tries to read and parse lrm.json from the repository.
    /// </summary>
    private async Task<LrmConfig?> TryReadLrmConfigAsync(
        int userId,
        string owner,
        string repo,
        string branch,
        string basePath)
    {
        try
        {
            // Try lrm.json at basePath first, then at repo root
            var paths = new[]
            {
                CombinePaths(basePath, "lrm.json"),
                "lrm.json"
            };

            foreach (var path in paths.Distinct())
            {
                try
                {
                    var content = await _githubApi.GetFileContentAsync(userId, owner, repo, path, branch);

                    if (!string.IsNullOrEmpty(content))
                    {
                        var config = JsonSerializer.Deserialize<LrmConfig>(content, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (config != null)
                        {
                            _logger.LogDebug("Found lrm.json at {Path}", path);
                            return config;
                        }
                    }
                }
                catch (Exception ex) when (ex.Message.Contains("Not Found") || ex.Message.Contains("404"))
                {
                    // File doesn't exist at this path, try next
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error reading lrm.json from {Owner}/{Repo}", owner, repo);
        }

        return null;
    }

    private static string CombinePaths(string basePath, string relativePath)
    {
        if (string.IsNullOrEmpty(basePath) || basePath == ".")
            return relativePath;
        return $"{basePath.TrimEnd('/')}/{relativePath}";
    }

    /// <summary>
    /// Minimal lrm.json config for format detection.
    /// </summary>
    private class LrmConfig
    {
        public string? ResourceFormat { get; set; }
        public string? DefaultLanguageCode { get; set; }
    }
}
