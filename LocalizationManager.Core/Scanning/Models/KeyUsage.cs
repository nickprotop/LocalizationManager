namespace LocalizationManager.Core.Scanning.Models;

/// <summary>
/// Represents usage information for a single localization key
/// </summary>
public class KeyUsage
{
    /// <summary>
    /// The localization key
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Number of references found in code
    /// </summary>
    public int ReferenceCount { get; set; }

    /// <summary>
    /// List of all references to this key
    /// </summary>
    public List<KeyReference> References { get; set; } = new();

    /// <summary>
    /// Whether this key exists in .resx files
    /// </summary>
    public bool ExistsInResources { get; set; }

    /// <summary>
    /// Languages where this key is defined
    /// </summary>
    public List<string> DefinedInLanguages { get; set; } = new();
}
