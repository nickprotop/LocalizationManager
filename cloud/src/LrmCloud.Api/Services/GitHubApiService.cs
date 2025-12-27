using System.Text;
using LrmCloud.Api.Data;
using LrmCloud.Api.Helpers;
using LrmCloud.Shared.Configuration;
using Microsoft.EntityFrameworkCore;
using Octokit;

namespace LrmCloud.Api.Services;

/// <summary>
/// Service for interacting with the GitHub API using user OAuth tokens.
/// </summary>
public class GitHubApiService : IGitHubApiService
{
    private readonly AppDbContext _db;
    private readonly CloudConfiguration _config;
    private readonly ILogger<GitHubApiService> _logger;

    // Known localization file patterns for auto-detection
    private static readonly Dictionary<string, string[]> FormatPatterns = new()
    {
        ["resx"] = new[] { "*.resx" },
        ["json"] = new[] { "strings.json", "strings.*.json", "*.json" },
        ["i18next"] = new[] { "en.json", "*.json" },
        ["android"] = new[] { "values/strings.xml", "values-*/strings.xml" },
        ["ios"] = new[] { "*.lproj/Localizable.strings", "*.lproj/Localizable.stringsdict" }
    };

    public GitHubApiService(
        AppDbContext db,
        CloudConfiguration config,
        ILogger<GitHubApiService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Repository>> ListUserRepositoriesAsync(int userId)
    {
        var client = await GetClientForUserAsync(userId);
        return await GitHubRetryPolicy.ExecuteAsync(
            () => client.Repository.GetAllForCurrent(), _logger);
    }

    public async Task<IReadOnlyList<Branch>> ListBranchesAsync(int userId, string owner, string repo)
    {
        var client = await GetClientForUserAsync(userId);
        return await GitHubRetryPolicy.ExecuteAsync(
            () => client.Repository.Branch.GetAll(owner, repo), _logger);
    }

    public async Task<IReadOnlyList<RepositoryContent>> GetDirectoryContentsAsync(
        int userId, string owner, string repo, string path, string branch)
    {
        var client = await GetClientForUserAsync(userId);
        try
        {
            var contents = await GitHubRetryPolicy.ExecuteAsync(
                () => client.Repository.Content.GetAllContentsByRef(owner, repo, path, branch), _logger);
            return contents;
        }
        catch (NotFoundException)
        {
            return Array.Empty<RepositoryContent>();
        }
    }

    public async Task<string?> GetFileContentAsync(int userId, string owner, string repo, string path, string branch)
    {
        var client = await GetClientForUserAsync(userId);
        try
        {
            var contents = await GitHubRetryPolicy.ExecuteAsync(
                () => client.Repository.Content.GetAllContentsByRef(owner, repo, path, branch), _logger);
            var file = contents.FirstOrDefault();
            if (file == null || file.Type != ContentType.File)
                return null;

            // Content is base64 encoded
            if (!string.IsNullOrEmpty(file.Content))
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(file.Content));
            }

            // If content is too large, we need to fetch it via blob API
            if (!string.IsNullOrEmpty(file.Sha))
            {
                var blob = await GitHubRetryPolicy.ExecuteAsync(
                    () => client.Git.Blob.Get(owner, repo, file.Sha), _logger);
                return Encoding.UTF8.GetString(Convert.FromBase64String(blob.Content));
            }

            return null;
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task<Reference> CreateBranchAsync(int userId, string owner, string repo, string branchName, string fromBranch)
    {
        var client = await GetClientForUserAsync(userId);

        // Get the SHA of the source branch
        var sourceBranch = await GitHubRetryPolicy.ExecuteAsync(
            () => client.Repository.Branch.Get(owner, repo, fromBranch), _logger);
        var sha = sourceBranch.Commit.Sha;

        // Create the new branch
        var newReference = new NewReference($"refs/heads/{branchName}", sha);
        return await GitHubRetryPolicy.ExecuteAsync(
            () => client.Git.Reference.Create(owner, repo, newReference), _logger);
    }

    public async Task CreateOrUpdateFileAsync(
        int userId, string owner, string repo, string path,
        string content, string message, string branch, string? sha = null)
    {
        var client = await GetClientForUserAsync(userId);

        if (sha == null)
        {
            // Try to get existing file SHA
            try
            {
                var existing = await GitHubRetryPolicy.ExecuteAsync(
                    () => client.Repository.Content.GetAllContentsByRef(owner, repo, path, branch), _logger);
                sha = existing.FirstOrDefault()?.Sha;
            }
            catch (NotFoundException)
            {
                // File doesn't exist, that's fine for creation
            }
        }

        if (sha != null)
        {
            // Update existing file
            var updateRequest = new UpdateFileRequest(message, content, sha, branch);
            await GitHubRetryPolicy.ExecuteAsync(
                () => client.Repository.Content.UpdateFile(owner, repo, path, updateRequest), _logger);
        }
        else
        {
            // Create new file
            var createRequest = new CreateFileRequest(message, content, branch);
            await GitHubRetryPolicy.ExecuteAsync(
                () => client.Repository.Content.CreateFile(owner, repo, path, createRequest), _logger);
        }
    }

    public async Task<PullRequest> CreatePullRequestAsync(
        int userId, string owner, string repo, string title,
        string head, string baseBranch, string body)
    {
        var client = await GetClientForUserAsync(userId);
        var newPr = new NewPullRequest(title, head, baseBranch)
        {
            Body = body
        };
        return await GitHubRetryPolicy.ExecuteAsync(
            () => client.PullRequest.Create(owner, repo, newPr), _logger);
    }

    public async Task<(bool Valid, string? ErrorMessage, IReadOnlyList<string>? Scopes)> ValidateTokenAsync(int userId)
    {
        try
        {
            var client = await GetClientForUserAsync(userId);

            // Make a simple API call to validate the token
            var user = await GitHubRetryPolicy.ExecuteAsync(
                () => client.User.Current(), _logger);

            // Check rate limit to get scopes (scopes are in response headers)
            var rateLimit = await GitHubRetryPolicy.ExecuteAsync(
                () => client.RateLimit.GetRateLimits(), _logger);

            // Unfortunately, Octokit doesn't expose the scopes from headers easily
            // We'll just verify the token works and assume scopes are correct
            // A more robust check would require making a raw HTTP call

            _logger.LogInformation("GitHub token validated for user {UserId}, GitHub user: {GitHubLogin}",
                userId, user.Login);

            return (true, null, null);
        }
        catch (AuthorizationException ex)
        {
            _logger.LogWarning(ex, "GitHub token invalid for user {UserId}", userId);
            return (false, "GitHub token is invalid or has been revoked", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating GitHub token for user {UserId}", userId);
            return (false, $"Error validating token: {ex.Message}", null);
        }
    }

    public async Task<Repository?> GetRepositoryAsync(int userId, string owner, string repo)
    {
        var client = await GetClientForUserAsync(userId);
        try
        {
            return await GitHubRetryPolicy.ExecuteAsync(
                () => client.Repository.Get(owner, repo), _logger);
        }
        catch (NotFoundException)
        {
            return null;
        }
    }

    public async Task<List<DetectedLocalizationPath>> DetectLocalizationFilesAsync(
        int userId, string owner, string repo, string branch, string basePath = "")
    {
        var client = await GetClientForUserAsync(userId);
        var results = new List<DetectedLocalizationPath>();

        try
        {
            // Get directory contents
            var path = string.IsNullOrEmpty(basePath) ? "" : basePath;
            var contents = await GetDirectoryContentsRecursiveAsync(client, owner, repo, branch, path, 2);

            // Check for lrm.json
            var hasLrmConfig = contents.Any(c => c.Name == "lrm.json");

            // Detect RESX
            var resxFiles = contents.Where(c => c.Name.EndsWith(".resx")).ToList();
            if (resxFiles.Any())
            {
                var resxPath = GetCommonDirectory(resxFiles.Select(f => f.Path));
                results.Add(new DetectedLocalizationPath(resxPath, "resx", resxFiles.Count, hasLrmConfig));
            }

            // Detect Android
            var androidPath = FindAndroidResources(contents);
            if (androidPath != null)
            {
                var androidFiles = contents.Where(c => c.Path.StartsWith(androidPath) && c.Name == "strings.xml").ToList();
                results.Add(new DetectedLocalizationPath(androidPath, "android", androidFiles.Count, hasLrmConfig));
            }

            // Detect iOS
            var iosPath = FindIosResources(contents);
            if (iosPath != null)
            {
                var iosFiles = contents.Where(c =>
                    c.Path.Contains(".lproj/") &&
                    (c.Name == "Localizable.strings" || c.Name == "Localizable.stringsdict")).ToList();
                results.Add(new DetectedLocalizationPath(iosPath, "ios", iosFiles.Count, hasLrmConfig));
            }

            // Detect JSON / i18next
            var jsonPath = FindJsonLocalization(contents);
            if (jsonPath != null)
            {
                var jsonFiles = contents.Where(c => c.Path.StartsWith(jsonPath) && c.Name.EndsWith(".json")).ToList();
                // Determine if i18next or regular JSON based on naming
                var isI18next = jsonFiles.Any(f =>
                    f.Name == "en.json" || f.Name == "fr.json" || f.Name == "de.json");
                results.Add(new DetectedLocalizationPath(jsonPath,
                    isI18next ? "i18next" : "json",
                    jsonFiles.Count, hasLrmConfig));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting localization files in {Owner}/{Repo}", owner, repo);
        }

        return results;
    }

    // ============================================================================
    // Private Helper Methods
    // ============================================================================

    private async Task<GitHubClient> GetClientForUserAsync(int userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            throw new InvalidOperationException($"User {userId} not found");

        if (string.IsNullOrEmpty(user.GitHubAccessTokenEncrypted))
            throw new InvalidOperationException($"User {userId} does not have a GitHub account linked");

        var accessToken = TokenEncryption.Decrypt(user.GitHubAccessTokenEncrypted, _config.Encryption.TokenKey);

        var client = new GitHubClient(new ProductHeaderValue("LRM-Cloud"))
        {
            Credentials = new Credentials(accessToken)
        };

        return client;
    }

    private async Task<List<RepositoryContent>> GetDirectoryContentsRecursiveAsync(
        GitHubClient client, string owner, string repo, string branch, string path, int maxDepth)
    {
        var results = new List<RepositoryContent>();

        if (maxDepth <= 0) return results;

        try
        {
            IReadOnlyList<RepositoryContent> contents;
            if (string.IsNullOrEmpty(path))
            {
                contents = await client.Repository.Content.GetAllContentsByRef(owner, repo, branch);
            }
            else
            {
                contents = await client.Repository.Content.GetAllContentsByRef(owner, repo, path, branch);
            }

            foreach (var item in contents)
            {
                results.Add(item);

                if (item.Type == ContentType.Dir)
                {
                    var subContents = await GetDirectoryContentsRecursiveAsync(
                        client, owner, repo, branch, item.Path, maxDepth - 1);
                    results.AddRange(subContents);
                }
            }
        }
        catch (NotFoundException)
        {
            // Path doesn't exist
        }

        return results;
    }

    private static string GetCommonDirectory(IEnumerable<string> paths)
    {
        var pathList = paths.ToList();
        if (!pathList.Any()) return "";

        var directories = pathList.Select(p => Path.GetDirectoryName(p)?.Replace('\\', '/') ?? "").ToList();
        var common = directories.FirstOrDefault() ?? "";

        foreach (var dir in directories.Skip(1))
        {
            while (!dir.StartsWith(common) && !string.IsNullOrEmpty(common))
            {
                var lastSlash = common.LastIndexOf('/');
                common = lastSlash >= 0 ? common[..lastSlash] : "";
            }
        }

        return common;
    }

    private static string? FindAndroidResources(List<RepositoryContent> contents)
    {
        // Look for values/strings.xml pattern
        var stringsXml = contents.FirstOrDefault(c =>
            c.Path.EndsWith("values/strings.xml") ||
            c.Path.Contains("values-") && c.Name == "strings.xml");

        if (stringsXml == null) return null;

        // Get parent of values/ directory (e.g., app/src/main/res)
        var path = stringsXml.Path;
        var valuesIndex = path.LastIndexOf("values", StringComparison.OrdinalIgnoreCase);
        if (valuesIndex > 0)
        {
            return path[..(valuesIndex - 1)]; // Remove trailing slash before "values"
        }

        return null;
    }

    private static string? FindIosResources(List<RepositoryContent> contents)
    {
        // Look for *.lproj/Localizable.strings pattern
        var stringsFile = contents.FirstOrDefault(c =>
            c.Path.Contains(".lproj/") &&
            (c.Name == "Localizable.strings" || c.Name == "Localizable.stringsdict"));

        if (stringsFile == null) return null;

        // Get parent of *.lproj/ directory
        var path = stringsFile.Path;
        var lprojIndex = path.IndexOf(".lproj/", StringComparison.OrdinalIgnoreCase);
        if (lprojIndex > 0)
        {
            // Find the directory containing the .lproj folders
            var beforeLproj = path[..lprojIndex];
            var lastSlash = beforeLproj.LastIndexOf('/');
            return lastSlash >= 0 ? beforeLproj[..lastSlash] : "";
        }

        return null;
    }

    private static string? FindJsonLocalization(List<RepositoryContent> contents)
    {
        // Look for typical localization JSON patterns
        var jsonFiles = contents.Where(c =>
            c.Type == ContentType.File &&
            c.Name.EndsWith(".json") &&
            !c.Name.Equals("package.json") &&
            !c.Name.Equals("tsconfig.json") &&
            !c.Name.Equals("lrm.json") &&
            !c.Path.Contains("node_modules")).ToList();

        // Check for i18next pattern (en.json, fr.json, etc.)
        var i18nextPattern = jsonFiles.Where(f =>
            f.Name.Length == 7 && // xx.json
            f.Name[2] == '.').ToList();

        if (i18nextPattern.Count >= 2)
        {
            return GetCommonDirectory(i18nextPattern.Select(f => f.Path));
        }

        // Check for strings.json pattern
        var stringsJson = jsonFiles.FirstOrDefault(f =>
            f.Name == "strings.json" ||
            f.Name.StartsWith("strings."));

        if (stringsJson != null)
        {
            return Path.GetDirectoryName(stringsJson.Path)?.Replace('\\', '/') ?? "";
        }

        // Check for common localization directories
        var localesDirs = new[] { "locales", "lang", "i18n", "translations" };
        foreach (var dir in localesDirs)
        {
            var localeFiles = jsonFiles.Where(f => f.Path.Contains($"/{dir}/")).ToList();
            if (localeFiles.Any())
            {
                var firstFile = localeFiles.First().Path;
                var dirIndex = firstFile.IndexOf($"/{dir}/", StringComparison.OrdinalIgnoreCase);
                if (dirIndex >= 0)
                {
                    return firstFile[..(dirIndex + dir.Length + 1)];
                }
            }
        }

        return null;
    }
}
