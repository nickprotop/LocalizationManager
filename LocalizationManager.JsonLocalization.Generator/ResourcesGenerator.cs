// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License

using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LocalizationManager.JsonLocalization.Generator;

/// <summary>
/// Incremental source generator that generates strongly-typed resource accessors from JSON files.
/// </summary>
[Generator]
public class ResourcesGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Find all JSON files in AdditionalFiles
        var jsonFiles = context.AdditionalTextsProvider
            .Where(file => file.Path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            .Where(file => !Path.GetFileName(file.Path).StartsWith("_")); // Skip files starting with underscore

        // Get the assembly name for namespace
        var assemblyName = context.CompilationProvider
            .Select((c, _) => c.AssemblyName ?? "Resources");

        // Combine and generate
        var combined = jsonFiles.Collect().Combine(assemblyName);

        context.RegisterSourceOutput(combined, (ctx, source) =>
        {
            var (files, rootNamespace) = source;
            GenerateResources(ctx, files, rootNamespace);
        });
    }

    private void GenerateResources(
        SourceProductionContext context,
        ImmutableArray<AdditionalText> files,
        string? rootNamespace)
    {
        // Group files by base name (e.g., "strings.json" and "strings.fr.json")
        var fileGroups = files
            .GroupBy(f => GetBaseName(Path.GetFileName(f.Path)))
            .ToList();

        foreach (var group in fileGroups)
        {
            var baseName = group.Key;

            // Find the default/base file (without culture code)
            var baseFile = group.FirstOrDefault(f =>
                Path.GetFileNameWithoutExtension(f.Path).Equals(baseName, StringComparison.OrdinalIgnoreCase));

            if (baseFile == null)
            {
                // Try to find any file in the group to extract keys
                baseFile = group.First();
            }

            var content = baseFile.GetText(context.CancellationToken)?.ToString();
            if (string.IsNullOrEmpty(content))
                continue;

            try
            {
                var keys = ExtractKeys(content);
                var source = GenerateResourceClass(baseName, keys, rootNamespace ?? "Resources");
                context.AddSource($"{baseName}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
            catch (JsonException)
            {
                // Skip invalid JSON files
                continue;
            }
        }
    }

    /// <summary>
    /// Gets the base name from a filename (e.g., "strings" from "strings.fr.json").
    /// </summary>
    private static string GetBaseName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var dotIndex = name.LastIndexOf('.');

        // Check if the part after the dot looks like a culture code
        if (dotIndex > 0)
        {
            var suffix = name.Substring(dotIndex + 1);
            // Simple heuristic: culture codes are 2-5 chars (e.g., "en", "fr", "en-US", "zh-Hans")
            if (suffix.Length >= 2 && suffix.Length <= 10 && !suffix.Contains(" "))
            {
                return name.Substring(0, dotIndex);
            }
        }

        return name;
    }

    /// <summary>
    /// Extracts keys from JSON content.
    /// </summary>
    private static List<ResourceKey> ExtractKeys(string jsonContent)
    {
        var keys = new List<ResourceKey>();

        using var doc = JsonDocument.Parse(jsonContent, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        ExtractKeysFromElement(doc.RootElement, "", keys);

        return keys;
    }

    /// <summary>
    /// Recursively extracts keys from a JSON element.
    /// </summary>
    private static void ExtractKeysFromElement(JsonElement element, string prefix, List<ResourceKey> keys)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return;

        foreach (var prop in element.EnumerateObject())
        {
            // Skip internal/meta properties
            if (prop.Name.StartsWith("_"))
                continue;

            var fullKey = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            var propertyName = SanitizePropertyName(prop.Name);

            switch (prop.Value.ValueKind)
            {
                case JsonValueKind.String:
                    keys.Add(new ResourceKey(fullKey, propertyName, false));
                    break;

                case JsonValueKind.Object:
                    // Check if it's a plural entry, value with metadata, or nested object
                    if (prop.Value.TryGetProperty("_plural", out _) ||
                        HasPluralForms(prop.Value))
                    {
                        keys.Add(new ResourceKey(fullKey, propertyName, true));
                    }
                    else if (prop.Value.TryGetProperty("_value", out _))
                    {
                        keys.Add(new ResourceKey(fullKey, propertyName, false));
                    }
                    else
                    {
                        // Nested object - add as a nested class
                        keys.Add(new ResourceKey(fullKey, propertyName, false, true));
                        ExtractKeysFromElement(prop.Value, fullKey, keys);
                    }
                    break;

                default:
                    // Numbers, booleans, etc.
                    keys.Add(new ResourceKey(fullKey, propertyName, false));
                    break;
            }
        }
    }

    /// <summary>
    /// Checks if an object has plural form keys.
    /// </summary>
    private static bool HasPluralForms(JsonElement element)
    {
        var pluralKeys = new[] { "zero", "one", "two", "few", "many", "other" };
        var found = 0;

        foreach (var prop in element.EnumerateObject())
        {
            if (pluralKeys.Contains(prop.Name, StringComparer.OrdinalIgnoreCase))
                found++;
        }

        return found >= 2;
    }

    /// <summary>
    /// Sanitizes a key to be a valid C# property name.
    /// </summary>
    private static string SanitizePropertyName(string key)
    {
        var sb = new StringBuilder();
        var capitalizeNext = true;

        foreach (var c in key)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
                capitalizeNext = false;
            }
            else
            {
                capitalizeNext = true;
            }
        }

        var result = sb.ToString();

        // Ensure it doesn't start with a digit
        if (result.Length > 0 && char.IsDigit(result[0]))
        {
            result = "_" + result;
        }

        // Handle reserved keywords
        if (IsReservedKeyword(result))
        {
            result = "@" + result;
        }

        return result;
    }

    /// <summary>
    /// Checks if a name is a C# reserved keyword.
    /// </summary>
    private static bool IsReservedKeyword(string name)
    {
        var keywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
            "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
            "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
            "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
            "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
            "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
            "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
            "void", "volatile", "while"
        };

        return keywords.Contains(name);
    }

    /// <summary>
    /// Generates the source code for a resource class.
    /// </summary>
    private static string GenerateResourceClass(string baseName, List<ResourceKey> keys, string rootNamespace)
    {
        var className = SanitizePropertyName(baseName);
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by LocalizationManager.JsonLocalization.Generator");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {rootNamespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// Strongly-typed resource accessors for {baseName}.json");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    public static partial class {className}");
        sb.AppendLine("    {");

        // Generate localizer property
        sb.AppendLine("        private static global::LocalizationManager.JsonLocalization.JsonLocalizer? _localizer;");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Gets or sets the localizer instance used for all resource access.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static global::LocalizationManager.JsonLocalization.JsonLocalizer Localizer");
        sb.AppendLine("        {");
        sb.AppendLine("            get => _localizer ?? throw new global::System.InvalidOperationException(");
        sb.AppendLine($"                \"Localizer not initialized. Call {className}.Initialize() first.\");");
        sb.AppendLine("            set => _localizer = value;");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate Initialize method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes the resource accessor with a file system loader.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public static void Initialize(string resourcesPath)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _localizer = new global::LocalizationManager.JsonLocalization.JsonLocalizer(resourcesPath, \"{baseName}\");");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate Initialize with assembly method
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Initializes the resource accessor with embedded resources.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public static void Initialize(global::System.Reflection.Assembly assembly, string resourceNamespace)");
        sb.AppendLine("        {");
        sb.AppendLine($"            _localizer = new global::LocalizationManager.JsonLocalization.JsonLocalizer(assembly, resourceNamespace, \"{baseName}\");");
        sb.AppendLine("        }");
        sb.AppendLine();

        // Generate properties for top-level keys
        var topLevelKeys = keys.Where(k => !k.Key.Contains('.')).ToList();
        foreach (var key in topLevelKeys)
        {
            GenerateProperty(sb, key, "        ");
        }

        // Generate nested classes
        var nestedGroups = keys
            .Where(k => k.Key.Contains('.'))
            .GroupBy(k => k.Key.Split('.')[0])
            .ToList();

        foreach (var group in nestedGroups)
        {
            GenerateNestedClass(sb, group.Key, group.ToList(), "        ");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Generates a property for a resource key.
    /// </summary>
    private static void GenerateProperty(StringBuilder sb, ResourceKey key, string indent)
    {
        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Gets the localized string for key \"{key.Key}\".");
        sb.AppendLine($"{indent}/// </summary>");

        if (key.IsPlural)
        {
            sb.AppendLine($"{indent}public static string {key.PropertyName}(int count) => Localizer.Plural(\"{key.Key}\", count);");
        }
        else if (!key.IsNested)
        {
            sb.AppendLine($"{indent}public static string {key.PropertyName} => Localizer[\"{key.Key}\"];");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Generates a nested class for grouped keys.
    /// </summary>
    private static void GenerateNestedClass(StringBuilder sb, string className, List<ResourceKey> keys, string indent)
    {
        var sanitizedName = SanitizePropertyName(className);

        sb.AppendLine($"{indent}/// <summary>");
        sb.AppendLine($"{indent}/// Nested resource accessors for \"{className}\" keys.");
        sb.AppendLine($"{indent}/// </summary>");
        sb.AppendLine($"{indent}public static class {sanitizedName}");
        sb.AppendLine($"{indent}{{");

        // Generate properties for this level
        foreach (var key in keys)
        {
            var parts = key.Key.Split('.');
            if (parts.Length == 2)
            {
                GenerateProperty(sb, key, indent + "    ");
            }
        }

        // Handle deeper nesting if needed
        var deeperKeys = keys.Where(k => k.Key.Split('.').Length > 2).ToList();
        if (deeperKeys.Any())
        {
            var subGroups = deeperKeys
                .GroupBy(k =>
                {
                    var parts = k.Key.Split('.');
                    return parts.Length > 1 ? parts[1] : "";
                })
                .ToList();

            foreach (var subGroup in subGroups)
            {
                var adjustedKeys = subGroup
                    .Select(k => new ResourceKey(
                        string.Join(".", k.Key.Split('.').Skip(1)),
                        k.PropertyName,
                        k.IsPlural,
                        k.IsNested))
                    .ToList();

                GenerateNestedClass(sb, subGroup.Key, adjustedKeys, indent + "    ");
            }
        }

        sb.AppendLine($"{indent}}}");
        sb.AppendLine();
    }

    /// <summary>
    /// Represents a resource key with its metadata.
    /// </summary>
    private class ResourceKey
    {
        public string Key { get; }
        public string PropertyName { get; }
        public bool IsPlural { get; }
        public bool IsNested { get; }

        public ResourceKey(string key, string propertyName, bool isPlural, bool isNested = false)
        {
            Key = key;
            PropertyName = propertyName;
            IsPlural = isPlural;
            IsNested = isNested;
        }
    }
}
