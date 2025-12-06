using System.Text.RegularExpressions;

namespace LocalizationManager.Core.Scanning;

/// <summary>
/// Discovers source code files for scanning
/// </summary>
public class SourceFileDiscovery
{
    private static readonly string[] DefaultExcludePatterns = new[]
    {
        "**/bin/**",
        "**/obj/**",
        "**/node_modules/**",
        "**/.git/**",
        "**/.vs/**",
        "**/*.g.cs",
        "**/*.designer.cs",
        "**/*.Designer.cs",
        "**/*.g.i.cs"
    };

    /// <summary>
    /// Discover source files in a directory
    /// </summary>
    /// <param name="sourcePath">Root directory to search</param>
    /// <param name="fileExtensions">File extensions to include (e.g., ".cs", ".razor")</param>
    /// <param name="excludePatterns">Glob patterns to exclude</param>
    /// <returns>List of discovered file paths</returns>
    public List<string> DiscoverSourceFiles(
        string sourcePath,
        IEnumerable<string> fileExtensions,
        IEnumerable<string>? excludePatterns = null)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"Source path not found: {sourcePath}");
        }

        var allExcludePatterns = DefaultExcludePatterns
            .Concat(excludePatterns ?? Enumerable.Empty<string>())
            .ToList();

        var sourceFiles = new List<string>();

        foreach (var extension in fileExtensions)
        {
            var pattern = extension.StartsWith(".") ? $"*{extension}" : $"*.{extension}";

            try
            {
                var enumerationOptions = new EnumerationOptions
                {
                    MatchCasing = MatchCasing.CaseInsensitive,
                    RecurseSubdirectories = true
                };

                var files = Directory.EnumerateFiles(sourcePath, pattern, enumerationOptions);

                foreach (var file in files)
                {
                    try
                    {
                        if (!ShouldExcludeFile(file, sourcePath, allExcludePatterns))
                        {
                            sourceFiles.Add(file);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip files we don't have permission to access
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip directories we don't have permission to access
                continue;
            }
        }

        return sourceFiles.OrderBy(f => f).ToList();
    }

    private bool ShouldExcludeFile(string filePath, string basePath, List<string> excludePatterns)
    {
        var relativePath = Path.GetRelativePath(basePath, filePath).Replace(Path.DirectorySeparatorChar, '/');

        foreach (var pattern in excludePatterns)
        {
            if (MatchesGlobPattern(relativePath, pattern))
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesGlobPattern(string path, string pattern)
    {
        // Convert glob pattern to regex
        // ** matches any number of directories
        // * matches any characters except /
        // ? matches single character

        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*\\*/", "(?:.*/)?")  // **/ matches zero or more directories
            .Replace("\\*\\*", ".*")          // ** at end/start matches everything
            .Replace("\\*", "[^/]*")          // * matches filename characters
            .Replace("\\?", ".")              // ? matches single character
            + "$";

        return Regex.IsMatch(path, regexPattern, RegexOptions.IgnoreCase);
    }
}
