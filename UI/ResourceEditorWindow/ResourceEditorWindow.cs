// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Data;
using System.Timers;
using LocalizationManager.Core;
using LocalizationManager.Core.Abstractions;
using LocalizationManager.Core.Backup;
using LocalizationManager.Core.Configuration;
using LocalizationManager.Core.Models;
using LocalizationManager.Core.Scanning;
using LocalizationManager.Core.Scanning.Models;
using LocalizationManager.Core.Translation;
using LocalizationManager.UI.Filters;
using Terminal.Gui;

namespace LocalizationManager.UI;

/// <summary>
/// Represents a reference to a specific occurrence of a resource entry.
/// Used to track and manage duplicate keys in the TUI.
/// </summary>
internal class EntryReference
{
    public string Key { get; set; } = string.Empty;
    public int OccurrenceNumber { get; set; }  // 1-based
    public int TotalOccurrences { get; set; }

    public string DisplayKey => TotalOccurrences > 1
        ? $"{Key} [{OccurrenceNumber}]"
        : Key;
}

/// <summary>
/// Interactive TUI window for editing resource files.
/// </summary>
public partial class ResourceEditorWindow : Window
{
    private readonly List<ResourceFile> _resourceFiles;
    private readonly IResourceBackend _backend;
    private readonly ResourceValidator _validator;
    private readonly ResourceFilterService _filterService;
    private readonly string _defaultLanguageCode;
    private readonly ConfigurationModel? _configuration;
    private TableView? _tableView;
    private TextField? _searchField;
    private StatusBar? _statusBar;
    private bool _hasUnsavedChanges = false;
    private string _searchText = string.Empty;
    private DataTable _dataTable;
    private List<string> _allKeys = new();
    private List<EntryReference> _allEntries = new();
    private FilterCriteria _filterCriteria = new();
    private System.Timers.Timer? _searchDebounceTimer;
    private Dictionary<string, List<string>> _extraKeysByLanguage = new();
    private List<CheckBox> _languageCheckboxes = new();
    private CheckBox? _regexCheckBox;
    private bool _showComments = false;
    private Dictionary<string, DuplicateKeyCodeUsage> _caseInsensitiveDuplicates = new();
    private string _resourcePath = string.Empty;
    private string _sourcePath = string.Empty; // Source code path for code scanning
    private Dictionary<int, RowStatus> _rowStatusCache = new(); // Cache for row color determination
    private Label? _matchCounterLabel; // Label showing "X/Y matches"
    private int _currentMatchIndex = -1; // Current highlighted match (0-based)
    private List<int> _matchedRowIndices = new(); // List of row indices that match search
    private ScanResult? _scanResult; // Code scan results
    private bool _isCodeScanned = false; // Whether code has been scanned
    private CheckBox? _filterUnusedCheckBox; // Filter to show only unused in code
    private CheckBox? _filterMissingFromResourcesCheckBox; // Filter to show only missing from .resx
    private OperationHistory _operationHistory = new(); // Undo/redo history
    private HashSet<int> _selectedRowIndices = new(); // Selected row indices for batch operations
    private int _selectionAnchor = -1; // Anchor point for Shift+selection
    private Dictionary<string, EntryReference> _selectedEntries = new(); // Selected entries by DisplayKey for persistence across rebuilds

    /// <summary>
    /// Represents the status of a row for color coding purposes
    /// </summary>
    private enum RowStatus
    {
        Normal,               // Default (no special status)
        Missing,              // Missing translation (red)
        Extra,                // Extra key not in default language (yellow)
        Modified,             // Unsaved changes (green)
        Duplicate,            // Duplicate key (orange/magenta)
        UnusedInCode,         // Exists in .resx but not found in source code (gray)
        MissingFromResources  // Used in code but not defined in .resx (red/critical)
    }

    public ResourceEditorWindow(List<ResourceFile> resourceFiles, IResourceBackend backend, string defaultLanguageCode = "default", string? sourcePath = null, ConfigurationModel? configuration = null)
    {
        _resourceFiles = resourceFiles;
        _backend = backend;
        _validator = new ResourceValidator();
        _filterService = new ResourceFilterService();
        _defaultLanguageCode = defaultLanguageCode;
        _configuration = configuration;

        Title = $"Localization Resource Manager - Interactive Editor ({Application.QuitKey} to quit)";

        // Initialize visible languages (all visible by default)
        _filterCriteria.VisibleLanguageCodes = _resourceFiles.Select(rf => rf.Language.Code).ToList();

        // Build entry references (tracks all occurrences including duplicates)
        BuildEntryReferences();

        // Load keys
        var defaultFile = resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile != null)
        {
            _allKeys = defaultFile.Entries.Select(e => e.Key).Distinct().OrderBy(k => k).ToList();
        }

        // Build DataTable
        _dataTable = BuildDataTable();

        // Detect extra keys in translation files
        DetectAndMarkExtraKeys();

        // Store resource path for code scanning (convert to absolute path, trim trailing slashes)
        var firstFile = resourceFiles.FirstOrDefault();
        if (firstFile != null)
        {
            var resourceDir = Path.GetDirectoryName(firstFile.Language.FilePath) ?? string.Empty;
            if (!string.IsNullOrEmpty(resourceDir))
            {
                _resourcePath = Path.GetFullPath(resourceDir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        // Store source path - use the provided parameter directly (already calculated correctly in EditCommand)
        // The sourcePath parameter is already absolute and points to parent of resource path
        if (!string.IsNullOrEmpty(sourcePath))
        {
            // Source path was explicitly provided or calculated by the command - use it as-is
            _sourcePath = sourcePath;
        }
        else if (!string.IsNullOrEmpty(_resourcePath))
        {
            // Fallback: calculate from resource path if no source path provided
            var parent = Directory.GetParent(_resourcePath);
            _sourcePath = parent?.FullName ?? _resourcePath;
        }
        else
        {
            _sourcePath = string.Empty;
        }

        // Detect case-insensitive duplicates
        DetectCaseInsensitiveDuplicates();

        InitializeComponents();
    }

}
