using LocalizationManager.Core.Models;
using LocalizationManager.Core.Scanning.Models;
using LocalizationManager.Core.Scanning.Scanners;

namespace LocalizationManager.Core.Scanning;

/// <summary>
/// Orchestrates code scanning for localization key references
/// </summary>
public class CodeScanner
{
    private readonly SourceFileDiscovery _fileDiscovery;
    private readonly List<PatternMatcher> _scanners;

    public CodeScanner()
    {
        _fileDiscovery = new SourceFileDiscovery();
        _scanners = new List<PatternMatcher>
        {
            new CSharpScanner(),
            new RazorScanner(),
            new XamlScanner()
        };
    }

    /// <summary>
    /// Scan source code for localization key references and compare with resource files
    /// </summary>
    public ScanResult Scan(
        string sourcePath,
        List<ResourceFile> resourceFiles,
        bool strictMode = false,
        IEnumerable<string>? excludePatterns = null,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null)
    {
        var result = new ScanResult
        {
            SourcePath = sourcePath,
            ResourcePath = Path.GetDirectoryName(resourceFiles.First().Language.FilePath) ?? string.Empty,
            ExcludedPatterns = excludePatterns?.ToList() ?? new List<string>()
        };

        // Get all keys from resource files
        var resourceKeys = GetAllResourceKeys(resourceFiles);

        // Discover source files
        var allExtensions = _scanners.SelectMany(s => s.SupportedExtensions).Distinct();
        var sourceFiles = _fileDiscovery.DiscoverSourceFiles(sourcePath, allExtensions, excludePatterns);
        result.FilesScanned = sourceFiles.Count;

        // Scan all files
        var allReferences = ScanFiles(sourceFiles, strictMode, resourceClassNames, localizationMethods);
        result.TotalReferences = allReferences.Count;

        // Separate dynamic keys from regular keys
        var dynamicReferences = allReferences.Where(r => r.Key == "<dynamic>").ToList();
        var regularReferences = allReferences.Where(r => r.Key != "<dynamic>").ToList();

        // Group regular references by key (case-sensitive to preserve exact casing for code reference tracking)
        var keyGroups = regularReferences
            .GroupBy(r => r.Key)
            .ToList();

        result.UniqueKeysFound = keyGroups.Count;

        // Create key usage information for regular keys
        foreach (var group in keyGroups)
        {
            var key = group.Key;
            var references = group.ToList();

            var usage = new KeyUsage
            {
                Key = key,
                ReferenceCount = references.Count,
                References = references,
                ExistsInResources = resourceKeys.Contains(key),
                DefinedInLanguages = GetLanguagesForKey(resourceFiles, key)
            };

            result.AllKeyUsages.Add(usage);

            // Track missing keys (in code, not in resources)
            if (!usage.ExistsInResources)
            {
                result.MissingKeys.Add(usage);
            }
        }

        // Add dynamic keys as a single group if any exist
        if (dynamicReferences.Any())
        {
            var dynamicUsage = new KeyUsage
            {
                Key = "<dynamic>",
                ReferenceCount = dynamicReferences.Count,
                References = dynamicReferences,
                ExistsInResources = false,
                DefinedInLanguages = new List<string>()
            };

            result.AllKeyUsages.Add(dynamicUsage);
            // Don't add dynamic keys to MissingKeys - they're warnings, not errors
        }

        // Track unused keys (in resources, not in code) - case-insensitive
        var usedKeys = new HashSet<string>(keyGroups.Select(g => g.Key), StringComparer.OrdinalIgnoreCase);
        result.UnusedKeys = resourceKeys.Where(k => !usedKeys.Contains(k)).OrderBy(k => k).ToList();

        // Count warnings
        result.WarningCount = allReferences.Count(r => r.Confidence == ConfidenceLevel.Low);

        return result;
    }

    /// <summary>
    /// Scan a single source code file for localization key references
    /// </summary>
    /// <param name="filePath">Path to the file to scan</param>
    /// <param name="resourceFiles">Resource files to compare against</param>
    /// <param name="strictMode">If true, only include high-confidence matches</param>
    /// <param name="resourceClassNames">Custom resource class names to detect</param>
    /// <param name="localizationMethods">Custom localization method names to detect</param>
    /// <returns>Scan result (same format as full scan, but for single file)</returns>
    public ScanResult ScanSingleFile(
        string filePath,
        List<ResourceFile> resourceFiles,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null)
    {
        var result = new ScanResult
        {
            SourcePath = Path.GetDirectoryName(filePath) ?? string.Empty,
            ResourcePath = Path.GetDirectoryName(resourceFiles.First().Language.FilePath) ?? string.Empty,
            FilesScanned = 1
        };

        // Get all keys from resource files
        var resourceKeys = GetAllResourceKeys(resourceFiles);

        // Detect file type and get appropriate scanner
        var extension = Path.GetExtension(filePath);
        var scanner = _scanners.FirstOrDefault(s => s.SupportedExtensions.Contains(extension));

        if (scanner == null)
        {
            // Unsupported file type - return empty result
            return result;
        }

        try
        {
            // Scan the file
            var allReferences = scanner.ScanFile(filePath, strictMode, resourceClassNames, localizationMethods);
            result.TotalReferences = allReferences.Count;

            // Separate dynamic keys from regular keys
            var dynamicReferences = allReferences.Where(r => r.Key == "<dynamic>").ToList();
            var regularReferences = allReferences.Where(r => r.Key != "<dynamic>").ToList();

            // Group regular references by key
            var keyGroups = regularReferences
                .GroupBy(r => r.Key)
                .ToList();

            result.UniqueKeysFound = keyGroups.Count;

            // Create key usage information for regular keys
            foreach (var group in keyGroups)
            {
                var key = group.Key;
                var references = group.ToList();

                var usage = new KeyUsage
                {
                    Key = key,
                    ReferenceCount = references.Count,
                    References = references,
                    ExistsInResources = resourceKeys.Contains(key),
                    DefinedInLanguages = GetLanguagesForKey(resourceFiles, key)
                };

                result.AllKeyUsages.Add(usage);

                // Track missing keys (in code, not in resources)
                if (!usage.ExistsInResources)
                {
                    result.MissingKeys.Add(usage);
                }
            }

            // Add dynamic keys as a single group if any exist
            if (dynamicReferences.Any())
            {
                var dynamicUsage = new KeyUsage
                {
                    Key = "<dynamic>",
                    ReferenceCount = dynamicReferences.Count,
                    References = dynamicReferences,
                    ExistsInResources = false,
                    DefinedInLanguages = new List<string>()
                };

                result.AllKeyUsages.Add(dynamicUsage);
            }

            // For single file scan, we don't calculate unused keys (would need full codebase scan)
            result.UnusedKeys = new List<string>();

            // Count warnings
            result.WarningCount = allReferences.Count(r => r.Confidence == ConfidenceLevel.Low);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error scanning {filePath}: {ex.Message}");
        }

        return result;
    }

    /// <summary>
    /// Scan file content (string) for localization key references
    /// </summary>
    /// <param name="filePath">Path to the file (used for extension detection and KeyReference.FilePath)</param>
    /// <param name="content">The file content to scan</param>
    /// <param name="resourceFiles">Resource files to compare against</param>
    /// <param name="strictMode">If true, only include high-confidence matches</param>
    /// <param name="resourceClassNames">Custom resource class names to detect</param>
    /// <param name="localizationMethods">Custom localization method names to detect</param>
    /// <returns>Scan result (same format as full scan, but for single file)</returns>
    public ScanResult ScanSingleFileContent(
        string filePath,
        string content,
        List<ResourceFile> resourceFiles,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null)
    {
        var result = new ScanResult
        {
            SourcePath = Path.GetDirectoryName(filePath) ?? string.Empty,
            ResourcePath = Path.GetDirectoryName(resourceFiles.First().Language.FilePath) ?? string.Empty,
            FilesScanned = 1
        };

        // Get all keys from resource files
        var resourceKeys = GetAllResourceKeys(resourceFiles);

        // Detect file type and get appropriate scanner
        var extension = Path.GetExtension(filePath);
        var scanner = _scanners.FirstOrDefault(s => s.SupportedExtensions.Contains(extension));

        if (scanner == null)
        {
            // Unsupported file type - return empty result
            return result;
        }

        try
        {
            // Scan the content
            var allReferences = scanner.ScanContent(filePath, content, strictMode, resourceClassNames, localizationMethods);
            result.TotalReferences = allReferences.Count;

            // Separate dynamic keys from regular keys
            var dynamicReferences = allReferences.Where(r => r.Key == "<dynamic>").ToList();
            var regularReferences = allReferences.Where(r => r.Key != "<dynamic>").ToList();

            // Group regular references by key
            var keyGroups = regularReferences
                .GroupBy(r => r.Key)
                .ToList();

            result.UniqueKeysFound = keyGroups.Count;

            // Create key usage information for regular keys
            foreach (var group in keyGroups)
            {
                var key = group.Key;
                var references = group.ToList();

                var usage = new KeyUsage
                {
                    Key = key,
                    ReferenceCount = references.Count,
                    References = references,
                    ExistsInResources = resourceKeys.Contains(key),
                    DefinedInLanguages = GetLanguagesForKey(resourceFiles, key)
                };

                result.AllKeyUsages.Add(usage);

                // Track missing keys (in code, not in resources)
                if (!usage.ExistsInResources)
                {
                    result.MissingKeys.Add(usage);
                }
            }

            // Add dynamic keys as a single group if any exist
            if (dynamicReferences.Any())
            {
                var dynamicUsage = new KeyUsage
                {
                    Key = "<dynamic>",
                    ReferenceCount = dynamicReferences.Count,
                    References = dynamicReferences,
                    ExistsInResources = false,
                    DefinedInLanguages = new List<string>()
                };

                result.AllKeyUsages.Add(dynamicUsage);
            }

            // For single file scan, we don't calculate unused keys (would need full codebase scan)
            result.UnusedKeys = new List<string>();

            // Count warnings
            result.WarningCount = allReferences.Count(r => r.Confidence == ConfidenceLevel.Low);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error scanning {filePath}: {ex.Message}");
        }

        return result;
    }

    private List<KeyReference> ScanFiles(
        List<string> sourceFiles,
        bool strictMode,
        List<string>? resourceClassNames,
        List<string>? localizationMethods)
    {
        var allReferences = new List<KeyReference>();

        // Use parallel processing for better performance
        var lockObject = new object();

        Parallel.ForEach(sourceFiles, filePath =>
        {
            var extension = Path.GetExtension(filePath);
            var scanner = _scanners.FirstOrDefault(s => s.SupportedExtensions.Contains(extension));

            if (scanner != null)
            {
                try
                {
                    var references = scanner.ScanFile(filePath, strictMode, resourceClassNames, localizationMethods);

                    lock (lockObject)
                    {
                        allReferences.AddRange(references);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Error scanning {filePath}: {ex.Message}");
                }
            }
        });

        return allReferences.OrderBy(r => r.FilePath).ThenBy(r => r.Line).ToList();
    }

    private HashSet<string> GetAllResourceKeys(List<ResourceFile> resourceFiles)
    {
        // Get keys from default language file
        var defaultFile = resourceFiles.FirstOrDefault(f => f.Language.IsDefault);

        if (defaultFile == null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return new HashSet<string>(defaultFile.Entries.Select(e => e.Key), StringComparer.OrdinalIgnoreCase);
    }

    private List<string> GetLanguagesForKey(List<ResourceFile> resourceFiles, string key)
    {
        var languages = new List<string>();

        foreach (var file in resourceFiles)
        {
            if (file.Entries.Any(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
            {
                languages.Add(file.Language.GetDisplayCode());
            }
        }

        return languages;
    }
}
