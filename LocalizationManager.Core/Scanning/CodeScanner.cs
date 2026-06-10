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
    /// <param name="sourcePath">Path to source code directory</param>
    /// <param name="resourceFiles">Resource files to compare against</param>
    /// <param name="strictMode">If true, only include high-confidence matches</param>
    /// <param name="excludePatterns">Glob patterns to exclude from scanning</param>
    /// <param name="resourceClassNames">Custom resource class names to detect</param>
    /// <param name="localizationMethods">Custom localization method names to detect</param>
    /// <param name="excludeFromMissing">Keys to exclude from "missing keys" report (e.g., file references)</param>
    public ScanResult Scan(
        string sourcePath,
        List<ResourceFile> resourceFiles,
        bool strictMode = false,
        IEnumerable<string>? excludePatterns = null,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null,
        HashSet<string>? excludeFromMissing = null)
    {
        var result = new ScanResult
        {
            SourcePath = sourcePath,
            ResourcePath = Path.GetDirectoryName(resourceFiles.First().Language.FilePath) ?? string.Empty,
            ExcludedPatterns = excludePatterns?.ToList() ?? new List<string>()
        };

        // Get all keys from resource files
        var resourceKeys = GetAllResourceKeys(resourceFiles);
        var keysByGroup = GetKeysByGroup(resourceFiles);

        // Discover source files
        var allExtensions = _scanners.SelectMany(s => s.SupportedExtensions).Distinct();
        var sourceFiles = _fileDiscovery.DiscoverSourceFiles(sourcePath, allExtensions, excludePatterns);
        result.FilesScanned = sourceFiles.Count;

        // Build a map of folder -> injected localizer names declared in that folder's
        // _Imports.razor, so injected names apply folder-wide (and to subfolders).
        var importsMap = BuildImportsMap(sourcePath);

        // Scan all files
        var allReferences = ScanFiles(sourceFiles, strictMode, resourceClassNames, localizationMethods, sourcePath, importsMap);
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
                ExistsInResources = KeyExists(references, key, resourceKeys, keysByGroup),
                DefinedInLanguages = GetLanguagesForKey(resourceFiles, key)
            };

            result.AllKeyUsages.Add(usage);

            // Track missing keys (in code, not in resources)
            // Skip keys that are in the exclude list (e.g., file references)
            if (!usage.ExistsInResources)
            {
                var isExcluded = excludeFromMissing?.Contains(key) ?? false;
                if (!isExcluded)
                {
                    result.MissingKeys.Add(usage);
                }
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
    /// <param name="excludeFromMissing">Keys to exclude from "missing keys" report (e.g., file references)</param>
    /// <returns>Scan result (same format as full scan, but for single file)</returns>
    public ScanResult ScanSingleFile(
        string filePath,
        List<ResourceFile> resourceFiles,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null,
        HashSet<string>? excludeFromMissing = null)
    {
        var result = new ScanResult
        {
            SourcePath = Path.GetDirectoryName(filePath) ?? string.Empty,
            ResourcePath = Path.GetDirectoryName(resourceFiles.First().Language.FilePath) ?? string.Empty,
            FilesScanned = 1
        };

        // Get all keys from resource files
        var resourceKeys = GetAllResourceKeys(resourceFiles);
        var keysByGroup = GetKeysByGroup(resourceFiles);

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
            // Scan the file. For single-file scans we don't have an explicit scan
            // root, so collect injected names from every _Imports.razor in the file's
            // own directory and all ancestor directories.
            var injectedNames = InjectedNamesFromAncestors(filePath);
            var allReferences = scanner.ScanFile(filePath, strictMode, resourceClassNames, localizationMethods, injectedNames);
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
                    ExistsInResources = KeyExists(references, key, resourceKeys, keysByGroup),
                    DefinedInLanguages = GetLanguagesForKey(resourceFiles, key)
                };

                result.AllKeyUsages.Add(usage);

                // Track missing keys (in code, not in resources)
                // Skip keys that are in the exclude list (e.g., file references)
                if (!usage.ExistsInResources)
                {
                    var isExcluded = excludeFromMissing?.Contains(key) ?? false;
                    if (!isExcluded)
                    {
                        result.MissingKeys.Add(usage);
                    }
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
    /// <param name="excludeFromMissing">Keys to exclude from "missing keys" report (e.g., file references)</param>
    /// <returns>Scan result (same format as full scan, but for single file)</returns>
    public ScanResult ScanSingleFileContent(
        string filePath,
        string content,
        List<ResourceFile> resourceFiles,
        bool strictMode = false,
        List<string>? resourceClassNames = null,
        List<string>? localizationMethods = null,
        HashSet<string>? excludeFromMissing = null)
    {
        var result = new ScanResult
        {
            SourcePath = Path.GetDirectoryName(filePath) ?? string.Empty,
            ResourcePath = Path.GetDirectoryName(resourceFiles.First().Language.FilePath) ?? string.Empty,
            FilesScanned = 1
        };

        // Get all keys from resource files
        var resourceKeys = GetAllResourceKeys(resourceFiles);
        var keysByGroup = GetKeysByGroup(resourceFiles);

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
            // Scan the content. As with ScanSingleFile, collect injected names from
            // every _Imports.razor in the file's directory and ancestor directories.
            var injectedNames = InjectedNamesFromAncestors(filePath);
            var allReferences = scanner.ScanContent(filePath, content, strictMode, resourceClassNames, localizationMethods, injectedNames);
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
                    ExistsInResources = KeyExists(references, key, resourceKeys, keysByGroup),
                    DefinedInLanguages = GetLanguagesForKey(resourceFiles, key)
                };

                result.AllKeyUsages.Add(usage);

                // Track missing keys (in code, not in resources)
                // Skip keys that are in the exclude list (e.g., file references)
                if (!usage.ExistsInResources)
                {
                    var isExcluded = excludeFromMissing?.Contains(key) ?? false;
                    if (!isExcluded)
                    {
                        result.MissingKeys.Add(usage);
                    }
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
        List<string>? localizationMethods,
        string sourcePath,
        Dictionary<string, List<string>> importsMap)
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
                    var injectedNames = InjectedNamesFor(filePath, sourcePath, importsMap);
                    var references = scanner.ScanFile(filePath, strictMode, resourceClassNames, localizationMethods, injectedNames);

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

    /// <summary>
    /// Builds a map of folder path -> injected localizer variable names declared in
    /// that folder's <c>_Imports.razor</c> file (Blazor shared imports).
    /// </summary>
    private Dictionary<string, List<string>> BuildImportsMap(string rootPath)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(rootPath))
            return map;

        var options = new EnumerationOptions
        {
            MatchCasing = MatchCasing.CaseInsensitive,
            RecurseSubdirectories = true
        };

        foreach (var imp in Directory.EnumerateFiles(rootPath, "_Imports.razor", options))
        {
            var dir = Path.GetDirectoryName(imp);
            if (dir == null)
                continue;

            try
            {
                map[dir] = InjectedLocalizerExtractor.Extract(File.ReadAllText(imp)).ToList();
            }
            catch
            {
                // Ignore unreadable _Imports.razor files.
            }
        }

        return map;
    }

    /// <summary>
    /// Returns the injected localizer names that apply to <paramref name="filePath"/>:
    /// the union of names from every <c>_Imports.razor</c> at the file's folder or any
    /// ancestor folder up to (and including) the scan root.
    /// </summary>
    private List<string> InjectedNamesFor(string filePath, string rootPath, Dictionary<string, List<string>> importsMap)
    {
        var result = new List<string>();
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));
        var root = Path.GetFullPath(rootPath);

        // Separator-aware prefix so a sibling directory whose name merely shares the
        // root's prefix (e.g. root "/proj/src" vs "/proj/src-gen") is NOT treated as
        // being inside the root.
        var rootWithSep = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;

        while (dir != null &&
               (string.Equals(dir, root, StringComparison.OrdinalIgnoreCase) ||
                dir.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)))
        {
            if (importsMap.TryGetValue(dir, out var names))
                foreach (var n in names)
                    if (!result.Contains(n)) result.Add(n);

            if (string.Equals(dir, root, StringComparison.OrdinalIgnoreCase)) break;
            dir = Path.GetDirectoryName(dir);
        }

        return result;
    }

    /// <summary>
    /// Collects injected localizer names from every <c>_Imports.razor</c> found in the
    /// file's own directory and all ancestor directories. Used by single-file scan paths
    /// (e.g. the web/VS Code APIs) where no explicit scan root is available.
    /// </summary>
    private List<string> InjectedNamesFromAncestors(string filePath)
    {
        var result = new List<string>();
        var dir = Path.GetDirectoryName(Path.GetFullPath(filePath));

        while (dir != null)
        {
            var imp = Path.Combine(dir, "_Imports.razor");
            if (File.Exists(imp))
            {
                try
                {
                    foreach (var n in InjectedLocalizerExtractor.Extract(File.ReadAllText(imp)))
                        if (!result.Contains(n)) result.Add(n);
                }
                catch
                {
                    // Ignore unreadable _Imports.razor files.
                }
            }

            var parent = Path.GetDirectoryName(dir);
            if (parent == dir) break;
            dir = parent;
        }

        return result;
    }

    private HashSet<string> GetAllResourceKeys(List<ResourceFile> resourceFiles)
    {
        // Union the keys from every default language file. In multi-group projects
        // (e.g. CustomerResources, GlassResources, SharedResources) each base resource
        // has its own default file, so taking only the first one would flag keys defined
        // in the other groups as "missing" even though they exist.
        var defaultFiles = resourceFiles.Where(f => f.Language.IsDefault).ToList();

        // Fall back to all files if none are explicitly marked default (defensive).
        if (defaultFiles.Count == 0)
            defaultFiles = resourceFiles;

        return new HashSet<string>(
            defaultFiles.SelectMany(f => f.Entries.Select(e => e.Key)),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Builds a map of resource-group BaseName -> set of keys defined in that group's
    /// default file(s). Used to resolve Data Annotation references that name a specific
    /// <c>ResourceType</c> against the correct group rather than the global union.
    /// </summary>
    private static Dictionary<string, HashSet<string>> GetKeysByGroup(List<ResourceFile> resourceFiles)
    {
        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        var defaultFiles = resourceFiles.Where(f => f.Language.IsDefault).ToList();
        if (defaultFiles.Count == 0)
            defaultFiles = resourceFiles;

        foreach (var file in defaultFiles)
        {
            var baseName = file.Language.BaseName;
            if (string.IsNullOrEmpty(baseName))
                continue;

            if (!map.TryGetValue(baseName, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                map[baseName] = set;
            }

            foreach (var entry in file.Entries)
                set.Add(entry.Key);
        }

        return map;
    }

    /// <summary>
    /// Decides whether a key group exists in resources. References that name a
    /// specific <c>ResourceType</c> (Data Annotations) must be resolved against that
    /// group; untyped references fall back to the global union (<paramref name="allKeys"/>).
    /// </summary>
    private static bool KeyExists(
        IReadOnlyList<KeyReference> references,
        string key,
        HashSet<string> allKeys,
        Dictionary<string, HashSet<string>> keysByGroup)
    {
        // A key can be referenced both ways at once (e.g. `Resources.K` AND a
        // `[Display(..., ResourceType = typeof(R))]`). The key exists if ANY of its
        // references can resolve it:
        //  - an untyped reference resolves against the global union;
        //  - a typed reference resolves against its own group (or the union if that
        //    group's resx is unknown, to avoid spurious "missing" noise).
        var hasUntyped = references.Any(r => string.IsNullOrEmpty(r.ResourceTypeClassName));
        if (hasUntyped && allKeys.Contains(key))
        {
            return true;
        }

        foreach (var boundType in references
                     .Select(r => r.ResourceTypeClassName)
                     .Where(t => !string.IsNullOrEmpty(t))
                     .Distinct())
        {
            var existsInBoundGroup = keysByGroup.TryGetValue(boundType!, out var groupKeys)
                ? groupKeys.Contains(key)
                : allKeys.Contains(key);
            if (existsInBoundGroup)
            {
                return true;
            }
        }

        // No reference could resolve the key. If there were only untyped references we
        // already returned false above via the allKeys check; this also covers the
        // all-typed case where none of the bound groups contain the key.
        return false;
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
