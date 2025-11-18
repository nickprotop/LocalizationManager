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
public class ResourceEditorWindow : Window
{
    private readonly List<ResourceFile> _resourceFiles;
    private readonly ResourceFileParser _parser;
    private readonly ResourceValidator _validator;
    private readonly ResourceFilterService _filterService;
    private readonly string _defaultLanguageCode;
    private readonly ConfigurationModel? _configuration;
    private TableView? _tableView;
    private TextField? _searchField;
    private Label? _statusLabel;
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

    public ResourceEditorWindow(List<ResourceFile> resourceFiles, ResourceFileParser parser, string defaultLanguageCode = "default", ConfigurationModel? configuration = null)
    {
        _resourceFiles = resourceFiles;
        _parser = parser;
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

        // Store resource path for code scanning
        var firstFile = resourceFiles.FirstOrDefault();
        if (firstFile != null)
        {
            _resourcePath = Path.GetDirectoryName(firstFile.Language.FilePath) ?? string.Empty;
        }

        // Detect case-insensitive duplicates
        DetectCaseInsensitiveDuplicates();

        InitializeComponents();
    }

    private DataTable BuildDataTable()
    {
        var dt = new DataTable();

        // Add Key column
        dt.Columns.Add("Key", typeof(string));

        // Add column for each language
        foreach (var rf in _resourceFiles)
        {
            dt.Columns.Add(rf.Language.Name, typeof(string));
        }

        // Add comment columns for each language (hidden, used for filtering)
        foreach (var rf in _resourceFiles)
        {
            var commentColumn = dt.Columns.Add($"_Comment_{rf.Language.Code}", typeof(string));
            commentColumn.ColumnMapping = MappingType.Hidden;
        }

        // Add internal columns for tracking occurrences (hidden from display)
        var actualKeyColumn = dt.Columns.Add("_ActualKey", typeof(string));
        actualKeyColumn.ColumnMapping = MappingType.Hidden;

        var occurrenceColumn = dt.Columns.Add("_OccurrenceNumber", typeof(int));
        occurrenceColumn.ColumnMapping = MappingType.Hidden;

        var visibleColumn = dt.Columns.Add("_Visible", typeof(bool));
        visibleColumn.ColumnMapping = MappingType.Hidden;

        var extraKeyColumn = dt.Columns.Add("_HasExtraKey", typeof(bool));
        extraKeyColumn.ColumnMapping = MappingType.Hidden;

        // Populate rows - one row per entry reference (including all duplicate occurrences)
        foreach (var entryRef in _allEntries)
        {
            var row = dt.NewRow();

            // Display key with [N] suffix if duplicates exist
            row["Key"] = entryRef.DisplayKey;
            row["_ActualKey"] = entryRef.Key;
            row["_OccurrenceNumber"] = entryRef.OccurrenceNumber;
            row["_Visible"] = true;
            row["_HasExtraKey"] = false;

            // Get the Nth occurrence from each language file
            foreach (var rf in _resourceFiles)
            {
                var entry = GetNthOccurrence(rf, entryRef.Key, entryRef.OccurrenceNumber);
                row[rf.Language.Name] = entry?.Value ?? "";
                row[$"_Comment_{rf.Language.Code}"] = entry?.Comment ?? "";
            }

            dt.Rows.Add(row);
        }

        return dt;
    }


    private DataTable BuildDataTableWithDoubleRows()
    {
        var dt = new DataTable();

        // Add Key column
        dt.Columns.Add("Key", typeof(string));

        // Add column for each language
        foreach (var rf in _resourceFiles)
        {
            dt.Columns.Add(rf.Language.Name, typeof(string));
        }

        // Add hidden metadata columns
        var rowTypeColumn = dt.Columns.Add("_RowType", typeof(string));
        rowTypeColumn.ColumnMapping = MappingType.Hidden;

        var logicalKeyColumn = dt.Columns.Add("_LogicalKey", typeof(string));
        logicalKeyColumn.ColumnMapping = MappingType.Hidden;

        var actualKeyColumn = dt.Columns.Add("_ActualKey", typeof(string));
        actualKeyColumn.ColumnMapping = MappingType.Hidden;

        var occurrenceColumn = dt.Columns.Add("_OccurrenceNumber", typeof(int));
        occurrenceColumn.ColumnMapping = MappingType.Hidden;

        var visibleColumn = dt.Columns.Add("_Visible", typeof(bool));
        visibleColumn.ColumnMapping = MappingType.Hidden;

        var extraKeyColumn = dt.Columns.Add("_HasExtraKey", typeof(bool));
        extraKeyColumn.ColumnMapping = MappingType.Hidden;

        // Populate rows - 2 rows per entry reference (value + comment, including all duplicate occurrences)
        foreach (var entryRef in _allEntries)
        {
            // Value Row
            var valueRow = dt.NewRow();
            valueRow["Key"] = entryRef.DisplayKey;
            valueRow["_RowType"] = "Value";
            valueRow["_LogicalKey"] = entryRef.DisplayKey;
            valueRow["_ActualKey"] = entryRef.Key;
            valueRow["_OccurrenceNumber"] = entryRef.OccurrenceNumber;
            valueRow["_Visible"] = true;
            valueRow["_HasExtraKey"] = false;

            // Get the Nth occurrence from each language file
            foreach (var rf in _resourceFiles)
            {
                var entry = GetNthOccurrence(rf, entryRef.Key, entryRef.OccurrenceNumber);
                valueRow[rf.Language.Name] = entry?.Value ?? "";
            }
            dt.Rows.Add(valueRow);

            // Comment Row (indented with box-drawing characters)
            var commentRow = dt.NewRow();
            commentRow["Key"] = "  \u2514\u2500 Comment";  // "  └─ Comment"
            commentRow["_RowType"] = "Comment";
            commentRow["_LogicalKey"] = entryRef.DisplayKey;
            commentRow["_ActualKey"] = entryRef.Key;
            commentRow["_OccurrenceNumber"] = entryRef.OccurrenceNumber;
            commentRow["_Visible"] = true;
            commentRow["_HasExtraKey"] = false;

            foreach (var rf in _resourceFiles)
            {
                var entry = GetNthOccurrence(rf, entryRef.Key, entryRef.OccurrenceNumber);
                commentRow[rf.Language.Name] = entry?.Comment ?? "";
            }
            dt.Rows.Add(commentRow);
        }

        return dt;
    }

    /// <summary>
    /// Builds a list of entry references from the default file, tracking all occurrences of each key.
    /// </summary>
    private void BuildEntryReferences()
    {
        _allEntries.Clear();

        var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile == null) return;

        // Count occurrences of each key (case-insensitive per ResX specification)
        var occurrenceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in defaultFile.Entries)
        {
            if (!occurrenceCounts.ContainsKey(entry.Key))
            {
                occurrenceCounts[entry.Key] = 0;
            }
            occurrenceCounts[entry.Key]++;
        }

        // Sort entries so case-variants appear together (e.g., Devices, devices)
        var sortedEntries = defaultFile.Entries
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

        // Build entry references with occurrence numbers (case-insensitive)
        var occurrenceIndices = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in sortedEntries)
        {
            if (!occurrenceIndices.ContainsKey(entry.Key))
            {
                occurrenceIndices[entry.Key] = 0;
            }
            occurrenceIndices[entry.Key]++;

            _allEntries.Add(new EntryReference
            {
                Key = entry.Key,
                OccurrenceNumber = occurrenceIndices[entry.Key],
                TotalOccurrences = occurrenceCounts[entry.Key]
            });
        }
    }

    /// <summary>
    /// Gets the Nth occurrence of a key from a resource file.
    /// </summary>
    /// <param name="resourceFile">The resource file to search</param>
    /// <param name="key">The key to find</param>
    /// <param name="occurrenceNumber">The occurrence number (1-based)</param>
    /// <returns>The entry, or null if not found</returns>
    private ResourceEntry? GetNthOccurrence(ResourceFile resourceFile, string key, int occurrenceNumber)
    {
        var occurrences = resourceFile.Entries.Where(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
        if (occurrenceNumber < 1 || occurrenceNumber > occurrences.Count)
        {
            return null;
        }
        return occurrences[occurrenceNumber - 1];
    }

    private void InitializeComponents()
    {
        // Menu bar
        var menu = new MenuBar(new MenuBarItem[]
        {
            new MenuBarItem("_File", new MenuItem[]
            {
                new MenuItem("_Save", "Save all changes", () => SaveChanges(), null, null, Key.S | Key.CtrlMask),
                new MenuItem("_Validate", "Run validation", () => ShowValidation(), null, null, Key.F6),
                new MenuItem("_Backups", "Manage backups", () => ShowBackupManager(), null, null, Key.F7),
                null!, // separator
                new MenuItem("_Quit", "Exit editor", () => { if (ConfirmQuit()) Application.RequestStop(); }, null, null, Key.Q | Key.CtrlMask)
            }),
            new MenuBarItem("_Edit", new MenuItem[]
            {
                new MenuItem("_Add Key", "Add new translation key", () => AddNewKey(), null, null, Key.N | Key.CtrlMask),
                new MenuItem("_Edit Key", "Edit selected key", () => {
                    if (_tableView?.SelectedRow >= 0)
                    {
                        var entryRef = GetEntryReferenceFromSelectedRow(_tableView.SelectedRow);
                        if (entryRef != null)
                        {
                            EditKey(entryRef.Key, entryRef.OccurrenceNumber);
                        }
                    }
                }, null, null, Key.Enter),
                new MenuItem("_Delete Key", "Delete selected key", () => DeleteSelectedKey(), null, null, Key.DeleteChar),
                new MenuItem("_Merge Duplicates", "Merge duplicate keys", () => ShowMergeDuplicatesDialog(), null, null, Key.F8)
            }),
            new MenuBarItem("_Languages", new MenuItem[]
            {
                new MenuItem("_List", "Show all languages", () => ShowLanguageList(), null, null, Key.L | Key.CtrlMask),
                new MenuItem("_Add New", "Add new language", () => AddLanguage(), null, null, Key.F2),
                new MenuItem("_Remove", "Remove language", () => RemoveLanguage(), null, null, Key.F3)
            }),
            new MenuBarItem("_Translation", new MenuItem[]
            {
                new MenuItem("_Translate Selection", "Translate selected key", () => TranslateSelection(), null, null, Key.T | Key.CtrlMask),
                new MenuItem("Translate _Missing", "Translate all missing values", () => TranslateMissing(), null, null, Key.F4),
                new MenuItem("_Configure Providers", "Configure translation providers", () => ConfigureTranslation(), null, null, Key.F5)
            }),
            new MenuBarItem("_Help", new MenuItem[]
            {
                new MenuItem("_Shortcuts", "Show keyboard shortcuts", () => ShowHelp(), null, null, Key.F1)
            })
        });

        Add(menu);

        // Search bar (adjusted Y position for menu)
        var searchLabel = new Label
        {
            Text = "Search:",
            X = 1,
            Y = 2
        };

        _searchField = new TextField
        {
            X = Pos.Right(searchLabel) + 1,
            Y = 2,
            Width = 40,
            Text = ""
        };

        // Case-sensitive checkbox (on same line as search)
        var caseSensitiveCheckBox = new CheckBox
        {
            Text = "Case-sensitive",
            X = Pos.Right(_searchField) + 2,
            Y = 2,
            Checked = false
        };
        caseSensitiveCheckBox.Toggled += (prev) =>
        {
            _filterCriteria.CaseSensitive = caseSensitiveCheckBox.Checked;
            Application.MainLoop.Invoke(() => ApplyFilters());
        };

        // Search scope cycle (Keys+Values → Keys Only → Comments → All) - on same line as search
        var scopeButton = new Button
        {
            Text = "Keys+Values",
            X = Pos.Right(caseSensitiveCheckBox) + 2,
            Y = 2
        };
        scopeButton.Clicked += () =>
        {
            // Cycle through all search scopes
            _filterCriteria.Scope = _filterCriteria.Scope switch
            {
                SearchScope.KeysAndValues => SearchScope.KeysOnly,
                SearchScope.KeysOnly => SearchScope.Comments,
                SearchScope.Comments => SearchScope.All,
                SearchScope.All => SearchScope.KeysAndValues,
                _ => SearchScope.KeysAndValues
            };

            scopeButton.Text = _filterCriteria.Scope switch
            {
                SearchScope.KeysAndValues => "Keys+Values",
                SearchScope.KeysOnly => "Keys Only",
                SearchScope.Comments => "Comments",
                SearchScope.All => "All",
                _ => "Keys+Values"
            };

            ApplyFilters();
        };

        // Regex checkbox (on same line as search)
        _regexCheckBox = new CheckBox
        {
            Text = "Regex",
            X = Pos.Right(scopeButton) + 2,
            Y = 2,
            Checked = false
        };

        _regexCheckBox.Toggled += (prev) =>
        {
            // When checked: use Regex mode
            // When unchecked: use Wildcard mode (with auto-detection of wildcards vs substring)
            _filterCriteria.Mode = _regexCheckBox.Checked ? FilterMode.Regex : FilterMode.Wildcard;
            Application.MainLoop.Invoke(() => ApplyFilters());
        };

        _searchField.TextChanged += (oldValue) =>
        {
            _searchText = _searchField.Text.ToString() ?? string.Empty;

            // Debounce search input (300ms delay)
            _searchDebounceTimer?.Stop();
            _searchDebounceTimer?.Dispose();
            _searchDebounceTimer = new System.Timers.Timer(300);
            _searchDebounceTimer.Elapsed += (s, e) =>
            {
                Application.MainLoop.Invoke(() => FilterKeys());
                _searchDebounceTimer?.Dispose();
                _searchDebounceTimer = null;
            };
            _searchDebounceTimer.AutoReset = false;
            _searchDebounceTimer.Start();
        };

        // Language visibility controls (Y=3)
        var langLabel = new Label
        {
            Text = "Show languages:",
            X = 1,
            Y = 3
        };

        // Add checkboxes for first 3-4 languages
        var maxVisibleLangs = Math.Min(4, _resourceFiles.Count);
        var currentX = Pos.Right(langLabel) + 1;

        for (int i = 0; i < maxVisibleLangs; i++)
        {
            var rf = _resourceFiles[i];
            var displayName = string.IsNullOrEmpty(rf.Language.Code)
                ? _defaultLanguageCode
                : rf.Language.Code;

            var checkbox = new CheckBox
            {
                Text = displayName,
                X = currentX,
                Y = 3,
                Checked = true // All visible by default
            };

            var languageCode = rf.Language.Code; // Capture for closure
            checkbox.Toggled += (prev) =>
            {
                if (checkbox.Checked)
                {
                    if (!_filterCriteria.VisibleLanguageCodes.Contains(languageCode))
                    {
                        _filterCriteria.VisibleLanguageCodes.Add(languageCode);
                    }
                }
                else
                {
                    _filterCriteria.VisibleLanguageCodes.Remove(languageCode);
                }
                RebuildTableWithVisibleLanguages();
            };

            _languageCheckboxes.Add(checkbox);
            currentX = Pos.Right(checkbox) + 2;
        }

        // "More..." button to open full language filter dialog
        var moreButton = new Button
        {
            Text = "More...",
            X = currentX,
            Y = 3
        };
        moreButton.Clicked += () => ShowLanguageFilterDialog();

        // Show Comments checkbox (toggle double-row display)
        var showCommentsCheckBox = new CheckBox
        {
            Text = "Show Comments",
            X = Pos.Right(moreButton) + 3,
            Y = 3,
            Checked = _showComments
        };
        showCommentsCheckBox.Toggled += (prev) =>
        {
            _showComments = showCommentsCheckBox.Checked;
            RebuildTableWithCommentRows();
        };

        // TableView for keys and translations (adjusted Y position for new controls)
        _tableView = new TableView
        {
            X = 1,
            Y = 4,  // Y=4 now (menu=0, search=2, languages=3, table=4)
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 2,
            FullRowSelect = true,
            MultiSelect = false,
            Table = CreateDisplayTable(_dataTable)
        };

        // Intercept F8 for merge duplicates
        _tableView.KeyPress += (args) =>
        {
            if (args.KeyEvent.Key == Key.F8)
            {
                ShowMergeDuplicatesDialog();
                args.Handled = true;
            }
        };

        _tableView.CellActivated += (args) =>
        {
            // Get the entry reference from the selected row
            var entryRef = GetEntryReferenceFromSelectedRow(args.Row);
            if (entryRef != null)
            {
                EditKey(entryRef.Key, entryRef.OccurrenceNumber);
            }
        };

        // Status bar (with help text)
        _statusLabel = new Label
        {
            Text = GetStatusText(),
            X = 1,
            Y = Pos.AnchorEnd(1),
            Width = Dim.Fill()
        };

        Add(searchLabel, _searchField, caseSensitiveCheckBox, scopeButton, _regexCheckBox);
        Add(langLabel);
        foreach (var checkbox in _languageCheckboxes)
        {
            Add(checkbox);
        }
        Add(moreButton, showCommentsCheckBox);
        Add(_tableView, _statusLabel);

        // Keyboard shortcuts
        KeyPress += OnKeyPress;
    }

    private void OnKeyPress(KeyEventEventArgs e)
    {
        if (e.KeyEvent.Key == (Key.N | Key.CtrlMask))
        {
            AddNewKey();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.DeleteChar || e.KeyEvent.Key == Key.Backspace)
        {
            DeleteSelectedKey();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.S | Key.CtrlMask))
        {
            SaveChanges();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.F1)
        {
            ShowHelp();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.F6)
        {
            ShowValidation();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.Q | Key.CtrlMask))
        {
            if (ConfirmQuit())
            {
                Application.RequestStop();
            }
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.L | Key.CtrlMask))
        {
            ShowLanguageList();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.F2)
        {
            AddLanguage();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.F3)
        {
            RemoveLanguage();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.T | Key.CtrlMask))
        {
            TranslateSelection();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.F4)
        {
            TranslateMissing();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.F5)
        {
            ConfigureTranslation();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.D | Key.CtrlMask))
        {
            ShowDuplicatesDialog();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.F8)
        {
            ShowMergeDuplicatesDialog();
            e.Handled = true;
        }
    }

    private void FilterKeys()
    {
        if (_tableView == null) return;

        // Update filter criteria with current search text
        _filterCriteria.SearchText = _searchText;

        // Auto-detect wildcards and update mode (matching view command behavior)
        // Only if user hasn't checked the Regex checkbox
        if (_regexCheckBox != null && !_regexCheckBox.Checked)
        {
            // User is in Wildcard mode (unchecked)
            if (!string.IsNullOrEmpty(_searchText) && ResourceFilterService.IsWildcardPattern(_searchText))
            {
                // Has wildcards - use wildcard mode
                _filterCriteria.Mode = FilterMode.Wildcard;
            }
            else
            {
                // No wildcards - use substring mode
                _filterCriteria.Mode = FilterMode.Substring;
            }
        }
        // If user checked Regex checkbox, keep it as regex

        // Apply filters using the new service
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (_tableView == null) return;

        try
        {
            // Get matching row indices from filter service
            var matchingIndices = _filterService.FilterRows(_dataTable, _filterCriteria);

            // Update _Visible column for each row
            for (int i = 0; i < _dataTable.Rows.Count; i++)
            {
                _dataTable.Rows[i]["_Visible"] = matchingIndices.Contains(i);
            }

            // Apply the row filter to show only visible rows
            _dataTable.DefaultView.RowFilter = "_Visible = True";

            // Create display table without internal columns
            var filteredTable = CreateDisplayTable(_dataTable);

            // Assign filtered table to TableView
            _tableView.Table = filteredTable;
            _tableView.SetNeedsDisplay();
        }
        catch (Exception ex)
        {
            // If filtering fails (e.g., invalid regex), show all rows
            _dataTable.DefaultView.RowFilter = string.Empty;

            // Create display table without internal columns
            var allRowsTable = CreateDisplayTable(_dataTable);

            _tableView.Table = allRowsTable;
            _tableView.SetNeedsDisplay();

            // Optionally show error to user
            if (_filterCriteria.Mode == FilterMode.Regex)
            {
                MessageBox.ErrorQuery("Invalid Pattern",
                    $"Invalid regex pattern: {ex.Message}", "OK");
            }
        }

        UpdateStatus();
    }

    /// <summary>
    /// Creates a display table from the source table, excluding internal columns (those starting with _)
    /// </summary>
    private DataTable CreateDisplayTable(DataTable sourceTable)
    {
        var columnNames = sourceTable.Columns.Cast<DataColumn>()
            .Where(c => !c.ColumnName.StartsWith("_"))
            .Select(c => c.ColumnName)
            .ToArray();
        return sourceTable.DefaultView.ToTable(false, columnNames);
    }

    private void EditKey(string key, int occurrenceNumber = 1)
    {
        // Check if this is a duplicate key
        var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        var totalOccurrences = defaultFile?.Entries.Count(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? 0;
        var titleSuffix = totalOccurrences > 1 ? $" [{occurrenceNumber}]" : "";

        var dialog = new Dialog
        {
            Title = $"Edit: {key}{titleSuffix}",
            Width = Dim.Percent(80),
            Height = Dim.Percent(70)
        };

        var fields = new Dictionary<string, TextField>();
        var commentFields = new Dictionary<string, TextField>();
        var yPos = 1;

        foreach (var rf in _resourceFiles)
        {
            // Get the Nth occurrence of this key
            var entry = GetNthOccurrence(rf, key, occurrenceNumber);
            var currentValue = entry?.Value ?? string.Empty;
            var currentComment = entry?.Comment ?? string.Empty;

            // Value label and field
            var valueLabel = new Label
            {
                Text = $"{rf.Language.Name}:",
                X = 1,
                Y = yPos
            };

            var valueField = new TextField
            {
                Text = currentValue,
                X = 1,
                Y = yPos + 1,
                Width = Dim.Fill() - 1
            };

            // Comment label and field
            var commentLabel = new Label
            {
                Text = "  Comment:",
                X = 1,
                Y = yPos + 2
            };

            var commentField = new TextField
            {
                Text = currentComment,
                X = 1,
                Y = yPos + 3,
                Width = Dim.Fill() - 1
            };

            fields[rf.Language.Code] = valueField;
            commentFields[rf.Language.Code] = commentField;
            dialog.Add(valueLabel, valueField, commentLabel, commentField);
            yPos += 5;
        }

        // Auto-Translate button (left side)
        var btnAutoTranslate = new Button
        {
            Text = "Auto-Translate (Ctrl+T)",
            X = 1,
            Y = Pos.AnchorEnd(2)
        };

        btnAutoTranslate.Clicked += () =>
        {
            Application.RequestStop(); // Close edit dialog
            Application.MainLoop.Invoke(() => ShowTranslateDialog(new List<string> { key }));
        };

        var btnSave = new Button
        {
            Text = "Save",
            X = Pos.Center() - 10,
            Y = Pos.AnchorEnd(2),
            IsDefault = true
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 10,
            Y = Pos.AnchorEnd(2)
        };

        btnSave.Clicked += () =>
        {
            // Save values - update the Nth occurrence
            foreach (var kvp in fields)
            {
                var rf = _resourceFiles.FirstOrDefault(r => r.Language.Code == kvp.Key);
                if (rf != null)
                {
                    var entry = GetNthOccurrence(rf, key, occurrenceNumber);
                    if (entry != null)
                    {
                        entry.Value = kvp.Value.Text.ToString();
                        _hasUnsavedChanges = true;
                    }
                }
            }

            // Save comments
            foreach (var kvp in commentFields)
            {
                var rf = _resourceFiles.FirstOrDefault(r => r.Language.Code == kvp.Key);
                if (rf != null)
                {
                    var entry = GetNthOccurrence(rf, key, occurrenceNumber);
                    if (entry != null)
                    {
                        entry.Comment = kvp.Value.Text.ToString();
                        _hasUnsavedChanges = true;
                    }
                }
            }

            // Rebuild DataTable to reflect changes
            if (_showComments)
            {
                _dataTable = BuildDataTableWithDoubleRows();
            }
            else
            {
                _dataTable = BuildDataTable();
            }

            if (_tableView != null)
            {
                _tableView.Table = _dataTable;
            }

            UpdateStatus();
            FilterKeys();
            Application.RequestStop();
        };

        btnCancel.Clicked += () =>
        {
            Application.RequestStop();
        };

        dialog.Add(btnAutoTranslate, btnSave, btnCancel);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void AddNewKey()
    {
        var dialog = new Dialog
        {
            Title = "Add New Key",
            Width = 60,
            Height = 10 + _resourceFiles.Count * 5  // Increased for comment fields
        };

        var keyLabel = new Label
        {
            Text = "Key name:",
            X = 1,
            Y = 1
        };

        var keyField = new TextField
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 1,
            Text = ""
        };

        dialog.Add(keyLabel, keyField);

        var valueFields = new Dictionary<string, TextField>();
        var commentFields = new Dictionary<string, TextField>();
        var yPos = 4;

        foreach (var rf in _resourceFiles)
        {
            // Value label and field
            var valueLabel = new Label
            {
                Text = $"{rf.Language.Name}:",
                X = 1,
                Y = yPos
            };

            var valueField = new TextField
            {
                X = 1,
                Y = yPos + 1,
                Width = Dim.Fill() - 1,
                Text = ""
            };

            // Comment label and field
            var commentLabel = new Label
            {
                Text = "  Comment:",
                X = 1,
                Y = yPos + 2
            };

            var commentField = new TextField
            {
                X = 1,
                Y = yPos + 3,
                Width = Dim.Fill() - 1,
                Text = ""
            };

            valueFields[rf.Language.Code] = valueField;
            commentFields[rf.Language.Code] = commentField;
            dialog.Add(valueLabel, valueField, commentLabel, commentField);
            yPos += 5;
        }

        var btnAdd = new Button
        {
            Text = "Add",
            X = Pos.Center() - 10,
            Y = Pos.AnchorEnd(2),
            IsDefault = true
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            X = Pos.Center() + 5,
            Y = Pos.AnchorEnd(2)
        };

        btnAdd.Clicked += () =>
        {
            var key = keyField.Text.ToString();
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.ErrorQuery("Error", "Key name is required", "OK");
                return;
            }

            if (_allKeys.Contains(key))
            {
                MessageBox.ErrorQuery("Error", "Key already exists", "OK");
                return;
            }

            foreach (var kvp in valueFields)
            {
                var rf = _resourceFiles.FirstOrDefault(r => r.Language.Code == kvp.Key);
                if (rf != null)
                {
                    var comment = commentFields.ContainsKey(kvp.Key)
                        ? commentFields[kvp.Key].Text.ToString()
                        : "";

                    rf.Entries.Add(new ResourceEntry
                    {
                        Key = key,
                        Value = kvp.Value.Text.ToString(),
                        Comment = comment
                    });
                }
            }

            _allKeys.Add(key);
            _allKeys = _allKeys.OrderBy(k => k).ToList();

            // Rebuild the entire table to account for new key
            // This handles both single-row and double-row modes correctly
            if (_showComments)
            {
                _dataTable = BuildDataTableWithDoubleRows();
            }
            else
            {
                _dataTable = BuildDataTable();
            }

            if (_tableView != null)
            {
                _tableView.Table = _dataTable;
            }

            _hasUnsavedChanges = true;
            FilterKeys();
            Application.RequestStop();
        };

        btnCancel.Clicked += () =>
        {
            Application.RequestStop();
        };

        dialog.Add(btnAdd, btnCancel);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void DeleteSelectedKey()
    {
        if (_tableView == null || _tableView.SelectedRow < 0) return;

        // Get the entry reference from the selected row
        var entryRef = GetEntryReferenceFromSelectedRow(_tableView.SelectedRow);
        if (entryRef == null) return;

        // Check if this key has duplicates
        if (entryRef.TotalOccurrences > 1)
        {
            // Show options dialog for handling duplicates
            ShowDeleteDuplicateDialog(entryRef);
        }
        else
        {
            // Single occurrence - simple deletion
            var message = $"Delete key '{entryRef.Key}' from all languages?";
            var result = MessageBox.Query("Confirm Delete", message, "Yes", "No");

            if (result == 0)
            {
                DeleteAllOccurrences(entryRef.Key);
            }
        }
    }

    private void ShowDeleteDuplicateDialog(EntryReference entryRef)
    {
        var dialog = new Dialog
        {
            Title = "Delete Duplicate Key",
            Width = 60,
            Height = 16
        };

        var message = new Label
        {
            Text = $"Key '{entryRef.Key}' has {entryRef.TotalOccurrences} occurrences.\n\n" +
                   $"You are viewing occurrence [{entryRef.OccurrenceNumber}].\n\n" +
                   "What would you like to do?",
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 1,
            Height = 5
        };

        var btnDeleteThis = new Button
        {
            Text = $"Delete This [{entryRef.OccurrenceNumber}]",
            X = 1,
            Y = 7
        };

        var btnDeleteAll = new Button
        {
            Text = "Delete All",
            X = 1,
            Y = 8
        };

        var btnMerge = new Button
        {
            Text = "Merge Duplicates",
            X = 1,
            Y = 9
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            X = 1,
            Y = 11
        };

        btnDeleteThis.Clicked += () =>
        {
            Application.RequestStop();
            var confirmMessage = $"Delete occurrence [{entryRef.OccurrenceNumber}] of '{entryRef.Key}' from all languages?";
            var result = MessageBox.Query("Confirm Delete", confirmMessage, "Yes", "No");
            if (result == 0)
            {
                DeleteSpecificOccurrence(entryRef.Key, entryRef.OccurrenceNumber);
            }
        };

        btnDeleteAll.Clicked += () =>
        {
            Application.RequestStop();
            var confirmMessage = $"Delete ALL {entryRef.TotalOccurrences} occurrences of '{entryRef.Key}' from all languages?";
            var result = MessageBox.Query("Confirm Delete", confirmMessage, "Yes", "No");
            if (result == 0)
            {
                DeleteAllOccurrences(entryRef.Key);
            }
        };

        btnMerge.Clicked += () =>
        {
            Application.RequestStop();
            Application.MainLoop.Invoke(() => PerformMerge(entryRef.Key));
        };

        btnCancel.Clicked += () => Application.RequestStop();

        dialog.Add(message, btnDeleteThis, btnDeleteAll, btnMerge, btnCancel);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void DeleteSpecificOccurrence(string key, int occurrenceNumber)
    {
        // Delete the Nth occurrence from all language files
        foreach (var rf in _resourceFiles)
        {
            var occurrences = rf.Entries
                .Select((e, i) => (Entry: e, Index: i))
                .Where(x => x.Entry.Key == key)
                .ToList();

            if (occurrenceNumber > 0 && occurrenceNumber <= occurrences.Count)
            {
                rf.Entries.RemoveAt(occurrences[occurrenceNumber - 1].Index);
            }
        }

        // Rebuild entry references and table
        BuildEntryReferences();
        RebuildTable();
        _hasUnsavedChanges = true;
    }

    private void DeleteAllOccurrences(string key)
    {
        // Delete all occurrences from all files
        foreach (var rf in _resourceFiles)
        {
            rf.Entries.RemoveAll(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        }

        // Rebuild entry references and table
        BuildEntryReferences();
        RebuildTable();
        _hasUnsavedChanges = true;
    }

    private void RebuildTable()
    {
        // Rebuild DataTable to reflect changes
        if (_showComments)
        {
            _dataTable = BuildDataTableWithDoubleRows();
        }
        else
        {
            _dataTable = BuildDataTable();
        }

        if (_tableView != null)
        {
            _tableView.Table = _dataTable;
        }

        FilterKeys();
    }

    // Case-Insensitive Duplicate Detection

    private void DetectCaseInsensitiveDuplicates()
    {
        _caseInsensitiveDuplicates.Clear();

        var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile == null) return;

        // Find duplicates (case-insensitive per ResX specification)
        var duplicateGroups = defaultFile.Entries
            .GroupBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        if (!duplicateGroups.Any()) return;

        // For each duplicate group, create usage info
        foreach (var group in duplicateGroups)
        {
            var normalizedKey = group.Key.ToLowerInvariant();
            var usage = new DuplicateKeyCodeUsage
            {
                NormalizedKey = normalizedKey,
                CodeScanned = false
            };

            // Find all case variants across all resource files
            var variants = _resourceFiles
                .SelectMany(rf => rf.Entries)
                .Where(e => e.Key.Equals(normalizedKey, StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Key)
                .Distinct()
                .ToList();

            usage.ResourceVariants = variants;

            _caseInsensitiveDuplicates[normalizedKey] = usage;
        }
    }

    private void ScanCodeForDuplicateUsage()
    {
        if (!_caseInsensitiveDuplicates.Any()) return;

        // Determine source path (parent of resource path)
        var sourcePath = Directory.GetParent(_resourcePath)?.FullName ?? _resourcePath;
        if (!Directory.Exists(sourcePath)) return;

        // Create code scanner and scan
        var codeScanner = new CodeScanner();
        var scanResult = codeScanner.Scan(sourcePath, _resourceFiles, false);

        // Update each duplicate with code references
        foreach (var kvp in _caseInsensitiveDuplicates)
        {
            var usage = kvp.Value;
            usage.CodeScanned = true;

            foreach (var variant in usage.ResourceVariants)
            {
                var references = scanResult.AllKeyUsages
                    .Where(ku => ku.Key == variant) // Exact match
                    .SelectMany(ku => ku.References)
                    .ToList();

                usage.CodeReferences[variant] = references;
            }
        }
    }

    private void ShowDuplicatesDialog()
    {
        if (!_caseInsensitiveDuplicates.Any())
        {
            MessageBox.Query("No Duplicates", "No case-insensitive duplicate keys found.", "OK");
            return;
        }

        // Scan code if not already scanned
        if (_caseInsensitiveDuplicates.Values.Any(u => !u.CodeScanned))
        {
            ScanCodeForDuplicateUsage();
        }

        var dialog = new Dialog
        {
            Title = "Case-Insensitive Duplicates",
            Width = 80,
            Height = 24
        };

        var infoLabel = new Label
        {
            Text = $"Found {_caseInsensitiveDuplicates.Count} duplicate key(s) that differ only by case.\n" +
                   "MSBuild treats these as duplicates and will discard one at compile time.",
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 2,
            Height = 2
        };

        // Create a list view of duplicates
        var duplicatesList = new ListView
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill() - 2,
            Height = 12
        };

        var listItems = new List<string>();
        var duplicateKeys = _caseInsensitiveDuplicates.Keys.ToList();

        foreach (var key in duplicateKeys)
        {
            var usage = _caseInsensitiveDuplicates[key];
            var variants = string.Join(" / ", usage.ResourceVariants);
            var usedCount = usage.UsedVariants.Count;
            var unusedCount = usage.UnusedVariants.Count;

            var status = "";
            if (usage.CodeScanned)
            {
                if (unusedCount > 0 && usedCount > 0)
                {
                    status = $" [{usedCount} used, {unusedCount} unused]";
                }
                else if (unusedCount > 0)
                {
                    status = $" [all {unusedCount} unused]";
                }
                else
                {
                    status = $" [all {usedCount} used]";
                }
            }

            listItems.Add($"{variants}{status}");
        }

        duplicatesList.SetSource(listItems);

        // Details label
        var detailsLabel = new Label
        {
            X = 1,
            Y = 17,
            Width = Dim.Fill() - 2,
            Height = 2,
            Text = "Select a duplicate to see details"
        };

        duplicatesList.SelectedItemChanged += (args) =>
        {
            if (args.Item >= 0 && args.Item < duplicateKeys.Count)
            {
                var key = duplicateKeys[args.Item];
                var usage = _caseInsensitiveDuplicates[key];

                var details = new List<string>();
                foreach (var variant in usage.ResourceVariants)
                {
                    var refs = usage.CodeReferences.GetValueOrDefault(variant, new List<KeyReference>());
                    if (refs.Any())
                    {
                        var locs = string.Join(", ", refs.Take(2).Select(r => $"{Path.GetFileName(r.FilePath)}:{r.Line}"));
                        if (refs.Count > 2) locs += $" (+{refs.Count - 2})";
                        details.Add($"✓ \"{variant}\" in code: {locs}");
                    }
                    else
                    {
                        details.Add($"✗ \"{variant}\" not found in code");
                    }
                }

                // Add guidance based on usage
                if (usage.UsedVariants.Count > 1)
                {
                    details.Add("⚠ Multiple variants used! Standardize casing in code first.");
                }
                else if (usage.UsedVariants.Count == 1 && usage.UnusedVariants.Any())
                {
                    details.Add($"💡 Use F8 to merge and keep \"{usage.UsedVariants.First()}\"");
                }

                detailsLabel.Text = string.Join("\n", details);
            }
        };

        // Check if there are any unused variants to delete
        var hasUnusedVariants = _caseInsensitiveDuplicates.Values
            .Any(u => u.CodeScanned && u.UnusedVariants.Any());

        // Check if there are duplicates where all variants are used (problematic)
        var hasAllUsedDuplicates = _caseInsensitiveDuplicates.Values
            .Any(u => u.CodeScanned && u.UsedVariants.Count > 1);

        // Buttons
        var btnDeleteUnused = new Button
        {
            Text = "Delete Unused Variants",
            X = 1,
            Y = 20,
            Enabled = hasUnusedVariants
        };

        var btnClose = new Button
        {
            Text = "Close",
            X = Pos.Right(btnDeleteUnused) + 2,
            Y = 20
        };

        // Check if there are simple cases (one variant used, others unused)
        var hasSimpleCases = _caseInsensitiveDuplicates.Values
            .Any(u => u.CodeScanned && u.UsedVariants.Count == 1 && u.UnusedVariants.Any());

        // Warning/guidance label for duplicates
        var warningLabel = new Label
        {
            X = 1,
            Y = 22,
            Width = Dim.Fill() - 2,
            Height = 1,
            Text = hasAllUsedDuplicates
                ? "⚠ Some duplicates have multiple variants used in code - fix code casing first!"
                : hasSimpleCases
                    ? "💡 Use F8 (Merge Duplicates) to resolve simple cases."
                    : hasUnusedVariants
                        ? ""
                        : "No unused variants to delete."
        };

        btnDeleteUnused.Clicked += () =>
        {
            var result = MessageBox.Query(
                "Delete Unused",
                "Delete all key variants that are not found in code?\n" +
                "This will remove entries from all language files.\n\n" +
                "Note: Variants used in multiple places in code will NOT be deleted.",
                "Delete", "Cancel");

            if (result == 0)
            {
                DeleteUnusedDuplicateVariants();
                Application.RequestStop();
            }
        };

        btnClose.Clicked += () => Application.RequestStop();

        dialog.Add(infoLabel, duplicatesList, detailsLabel, btnDeleteUnused, btnClose, warningLabel);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void DeleteUnusedDuplicateVariants()
    {
        var deletedCount = 0;

        foreach (var kvp in _caseInsensitiveDuplicates)
        {
            var usage = kvp.Value;
            if (!usage.CodeScanned) continue;

            var unusedVariants = usage.UnusedVariants;
            if (!unusedVariants.Any()) continue;

            // Delete unused variants from all resource files
            foreach (var rf in _resourceFiles)
            {
                foreach (var variant in unusedVariants)
                {
                    var removed = rf.Entries.RemoveAll(e => e.Key == variant);
                    if (removed > 0) deletedCount += removed;
                }
            }
        }

        if (deletedCount > 0)
        {
            // Save changes
            foreach (var rf in _resourceFiles)
            {
                _parser.Write(rf);
            }

            // Rebuild everything
            BuildEntryReferences();
            RebuildTable();
            DetectCaseInsensitiveDuplicates();
            _hasUnsavedChanges = false;
            UpdateStatus();

            MessageBox.Query("Deleted", $"Removed {deletedCount} unused variant(s) from resource files.", "OK");
        }
        else
        {
            MessageBox.Query("No Changes", "No unused variants to delete.", "OK");
        }
    }

    // Merge Duplicates Functionality

    private void ShowMergeDuplicatesDialog()
    {
        // Check if a key with duplicates is selected
        EntryReference? selectedEntry = null;
        if (_tableView != null && _tableView.SelectedRow >= 0)
        {
            selectedEntry = GetEntryReferenceFromSelectedRow(_tableView.SelectedRow);
        }

        var dialog = new Dialog
        {
            Title = "Merge Duplicates",
            Width = 70,
            Height = 18
        };

        var yPos = 1;

        // Show selection info if a duplicate is selected
        if (selectedEntry != null && selectedEntry.TotalOccurrences > 1)
        {
            var selectionInfo = new Label
            {
                Text = $"Selected: '{selectedEntry.Key}' (has {selectedEntry.TotalOccurrences} occurrences)",
                X = 1,
                Y = yPos,
                Width = Dim.Fill() - 1
            };
            dialog.Add(selectionInfo);
            yPos += 2;
        }

        var message = new Label
        {
            Text = "Choose an option to merge duplicate keys:",
            X = 1,
            Y = yPos,
            Width = Dim.Fill() - 1
        };

        var btnMergeSelected = new Button
        {
            Text = selectedEntry != null && selectedEntry.TotalOccurrences > 1
                ? $"Merge '{selectedEntry.Key}'"
                : "Merge Selected (no duplicate selected)",
            X = 1,
            Y = yPos + 2,
            Enabled = selectedEntry != null && selectedEntry.TotalOccurrences > 1
        };

        var btnMergeAll = new Button
        {
            Text = "Merge All Duplicate Keys",
            X = 1,
            Y = yPos + 4
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            X = 1,
            Y = yPos + 6
        };

        var helpText = new Label
        {
            Text = "Merging allows you to consolidate duplicate occurrences\n" +
                   "by choosing which value to keep for each language.",
            X = 1,
            Y = yPos + 8,
            Width = Dim.Fill() - 1,
            Height = 3
        };

        btnMergeSelected.Clicked += () =>
        {
            if (selectedEntry != null)
            {
                Application.RequestStop();
                PerformMerge(selectedEntry.Key);
            }
        };

        btnMergeAll.Clicked += () =>
        {
            Application.RequestStop();
            PerformMergeAll();
        };

        btnCancel.Clicked += () => Application.RequestStop();

        dialog.Add(message, btnMergeSelected, btnMergeAll, btnCancel, helpText);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void PerformMerge(string key)
    {
        var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile == null) return;

        var occurrences = defaultFile.Entries.Where(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
        if (occurrences.Count <= 1)
        {
            MessageBox.Query("No Duplicates", $"Key '{key}' has only one occurrence.", "OK");
            return;
        }

        // Collect selections for each language
        var selections = new Dictionary<string, int>(); // language code -> selected occurrence index (1-based)

        foreach (var rf in _resourceFiles)
        {
            var langOccurrences = rf.Entries.Where(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();

            if (langOccurrences.Count == 0)
            {
                continue; // Key not found in this language
            }

            if (langOccurrences.Count == 1)
            {
                selections[rf.Language.Code] = 1; // Only one occurrence
                continue;
            }

            // Multiple occurrences: ask user
            var selectedIndex = ShowOccurrenceSelectionDialog(rf.Language.Name, key, langOccurrences);
            if (selectedIndex == -1)
            {
                // User cancelled
                return;
            }

            selections[rf.Language.Code] = selectedIndex;
        }

        // Apply the merge
        foreach (var rf in _resourceFiles)
        {
            if (!selections.ContainsKey(rf.Language.Code))
                continue;

            var selectedOccurrence = selections[rf.Language.Code];
            var indices = rf.Entries
                .Select((e, i) => (Entry: e, Index: i))
                .Where(x => x.Entry.Key == key)
                .Select(x => x.Index)
                .ToList();

            if (indices.Count <= 1)
                continue;

            // Remove all except the selected one (in reverse to maintain indices)
            for (int i = indices.Count - 1; i >= 0; i--)
            {
                if (i + 1 != selectedOccurrence)
                {
                    rf.Entries.RemoveAt(indices[i]);
                }
            }
        }

        // Rebuild and refresh
        BuildEntryReferences();
        RebuildTable();
        _hasUnsavedChanges = true;

        MessageBox.Query("Success", $"Successfully merged '{key}'", "OK");
    }

    private int ShowOccurrenceSelectionDialog(string languageName, string key, List<ResourceEntry> occurrences)
    {
        var dialog = new Dialog
        {
            Title = $"Select Occurrence for {languageName}",
            Width = Dim.Percent(80),
            Height = Dim.Percent(60)
        };

        var message = new Label
        {
            Text = $"Key '{key}' has {occurrences.Count} occurrences in {languageName}.\n" +
                   "Select which one to keep:",
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 1,
            Height = 3
        };

        var listView = new ListView
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 7
        };

        var items = new List<string>();
        for (int i = 0; i < occurrences.Count; i++)
        {
            var entry = occurrences[i];
            var value = entry.Value ?? "";
            var comment = !string.IsNullOrWhiteSpace(entry.Comment) ? $" // {entry.Comment}" : "";
            var preview = value.Length > 80 ? value.Substring(0, 77) + "..." : value;
            items.Add($"[{i + 1}] \"{preview}\"{comment}");
        }

        listView.SetSource(items);
        listView.SelectedItem = 0;

        var btnSelect = new Button
        {
            Text = "Select",
            X = 1,
            Y = Pos.AnchorEnd(2),
            IsDefault = true
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            X = Pos.Right(btnSelect) + 2,
            Y = Pos.AnchorEnd(2)
        };

        int selectedIndex = -1;

        btnSelect.Clicked += () =>
        {
            selectedIndex = listView.SelectedItem + 1; // Convert to 1-based
            Application.RequestStop();
        };

        btnCancel.Clicked += () =>
        {
            selectedIndex = -1;
            Application.RequestStop();
        };

        dialog.Add(message, listView, btnSelect, btnCancel);
        Application.Run(dialog);
        dialog.Dispose();

        return selectedIndex;
    }

    private void PerformMergeAll()
    {
        var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile == null) return;

        // Find all keys with duplicates
        var keysToMerge = defaultFile.Entries
            .GroupBy(e => e.Key)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (keysToMerge.Count == 0)
        {
            MessageBox.Query("No Duplicates", "No duplicate keys found.", "OK");
            return;
        }

        var confirmMessage = $"Found {keysToMerge.Count} key(s) with duplicates.\n\n" +
                           "You will be asked to select which occurrence to keep\n" +
                           "for each language with multiple occurrences.\n\n" +
                           "Proceed?";

        var result = MessageBox.Query("Confirm Merge All", confirmMessage, "Yes", "No");
        if (result != 0)
        {
            return;
        }

        // Merge each key
        foreach (var key in keysToMerge)
        {
            PerformMerge(key);
        }
    }

    private void SaveChanges()
    {
        if (!_hasUnsavedChanges)
        {
            MessageBox.Query("Save", "No changes to save", "OK");
            return;
        }

        var result = MessageBox.Query("Save Changes",
            "Create backups before saving?", "Yes", "No", "Cancel");

        if (result == 2) return;

        try
        {
            if (result == 0)
            {
                var backupManager = new BackupVersionManager(10);
                var basePath = Path.GetDirectoryName(_resourceFiles.First().Language.FilePath) ?? Environment.CurrentDirectory;
                foreach (var rf in _resourceFiles)
                {
                    backupManager.CreateBackupAsync(rf.Language.FilePath, "tui-save", basePath)
                        .GetAwaiter().GetResult();
                }
            }

            foreach (var rf in _resourceFiles)
            {
                _parser.Write(rf);
            }

            _hasUnsavedChanges = false;
            UpdateStatus();
            MessageBox.Query("Success", "Changes saved successfully", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    private void ShowValidation()
    {
        var result = _validator.Validate(_resourceFiles);

        var message = result.IsValid
            ? "All validations passed!"
            : $"Found {result.TotalIssues} issue(s):\n\n" +
              $"Missing: {result.MissingKeys.Sum(kv => kv.Value.Count)}\n" +
              $"Extra: {result.ExtraKeys.Sum(kv => kv.Value.Count)}\n" +
              $"Duplicates: {result.DuplicateKeys.Sum(kv => kv.Value.Count)}\n" +
              $"Empty: {result.EmptyValues.Sum(kv => kv.Value.Count)}\n" +
              $"Placeholder Mismatches: {result.PlaceholderMismatches.Sum(kv => kv.Value.Count)}";

        MessageBox.Query("Validation", message, "OK");
    }

    private void ShowBackupManager()
    {
        var basePath = Path.GetDirectoryName(_resourceFiles.First().Language.FilePath) ?? Environment.CurrentDirectory;
        var backupWindow = new BackupManagerWindow(basePath, _resourceFiles);
        Application.Run(backupWindow);
        backupWindow.Dispose();
    }

    private void ShowLanguageList()
    {
        var dialog = new Dialog
        {
            Title = "Manage Languages",
            Width = Dim.Percent(70),
            Height = Dim.Percent(60)
        };

        var languageList = _resourceFiles.Select(rf =>
        {
            var code = string.IsNullOrEmpty(rf.Language.Code) ? $"({_defaultLanguageCode})" : rf.Language.Code;
            var isDefault = rf.Language.IsDefault ? " [DEFAULT]" : "";
            return $"{code,-12} {rf.Language.Name,-25} ({rf.Entries.Count,4} entries){isDefault}";
        }).ToList();

        var listView = new ListView(languageList)
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 4
        };

        var btnAdd = new Button("Add New (F2)")
        {
            X = 1,
            Y = Pos.AnchorEnd(2)
        };
        btnAdd.Clicked += () => { Application.RequestStop(); AddLanguage(); };

        var btnRemove = new Button("Remove (F3)")
        {
            X = Pos.Right(btnAdd) + 2,
            Y = Pos.AnchorEnd(2)
        };
        btnRemove.Clicked += () => { Application.RequestStop(); RemoveLanguage(); };

        var btnClose = new Button("Close")
        {
            X = Pos.Right(btnRemove) + 2,
            Y = Pos.AnchorEnd(2)
        };
        btnClose.Clicked += () => Application.RequestStop();

        dialog.Add(listView, btnAdd, btnRemove, btnClose);
        Application.Run(dialog);
    }

    private void AddLanguage()
    {
        var dialog = new Dialog
        {
            Title = "Add New Language",
            Width = 65,
            Height = 16
        };

        var cultureLabel = new Label("Culture code (e.g., fr, de-DE, ja):") { X = 1, Y = 1 };
        var cultureField = new TextField { X = 1, Y = 2, Width = 20 };
        var statusLabel = new Label { X = 22, Y = 2, Width = Dim.Fill() - 1, ColorScheme = Colors.Base };

        var copyFromLabel = new Label("Copy entries from:") { X = 1, Y = 4 };
        var languageOptions = _resourceFiles.Select(rf =>
            string.IsNullOrEmpty(rf.Language.Code) ? "Default" : $"{rf.Language.Code} ({rf.Language.Name})"
        ).ToList();

        var copyFromCombo = new ComboBox
        {
            X = 1,
            Y = 5,
            Width = 40,
            Height = 5
        };
        copyFromCombo.SetSource(languageOptions);

        var emptyCheckbox = new CheckBox("Create empty (no entries)")
        {
            X = 1,
            Y = 7
        };

        var manager = new LanguageFileManager();
        var discovery = new ResourceDiscovery();

        cultureField.TextChanged += (oldValue) =>
        {
            var code = cultureField.Text.ToString();
            if (string.IsNullOrWhiteSpace(code))
            {
                statusLabel.Text = "";
                return;
            }

            if (manager.IsValidCultureCode(code, out var culture))
            {
                statusLabel.Text = $"✓ {culture!.DisplayName}";
                statusLabel.ColorScheme = Colors.Dialog;
            }
            else
            {
                statusLabel.Text = "✗ Invalid code";
                statusLabel.ColorScheme = Colors.Error;
            }
        };

        var btnCreate = new Button("Create")
        {
            X = 1,
            Y = Pos.AnchorEnd(2)
        };

        btnCreate.Clicked += () =>
        {
            var code = cultureField.Text.ToString();
            if (string.IsNullOrWhiteSpace(code))
            {
                MessageBox.ErrorQuery("Error", "Culture code is required.", "OK");
                return;
            }

            if (!manager.IsValidCultureCode(code, out var culture))
            {
                MessageBox.ErrorQuery("Error", $"Invalid culture code: {code}", "OK");
                return;
            }

            var baseName = _resourceFiles[0].Language.BaseName;
            var resourcePath = Path.GetDirectoryName(_resourceFiles[0].Language.FilePath) ?? "";

            if (manager.LanguageFileExists(baseName, code, resourcePath))
            {
                MessageBox.ErrorQuery("Error", $"Language '{code}' already exists.", "OK");
                return;
            }

            try
            {
                // Get source file
                ResourceFile? sourceFile = null;
                if (!emptyCheckbox.Checked)
                {
                    var selectedIdx = copyFromCombo.SelectedItem;
                    sourceFile = _resourceFiles[selectedIdx];
                }

                // Create new language file
                var newFile = manager.CreateLanguageFile(
                    baseName,
                    code,
                    resourcePath,
                    sourceFile,
                    copyEntries: !emptyCheckbox.Checked
                );

                // Add to resource files list
                _resourceFiles.Add(newFile);

                // Rebuild DataTable with new column
                var newDataTable = BuildDataTable();
                _dataTable = newDataTable;
                if (_tableView != null)
                {
                    _tableView.Table = _dataTable;

                }

                UpdateStatus();

                MessageBox.Query("Success",
                    $"Added {culture!.DisplayName} ({code}) language\n" +
                    $"File: {Path.GetFileName(newFile.Language.FilePath)}",
                    "OK");

                Application.RequestStop();
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to create language:\n{ex.Message}", "OK");
            }
        };

        var btnCancel = new Button("Cancel")
        {
            X = Pos.Right(btnCreate) + 2,
            Y = Pos.AnchorEnd(2)
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.Add(cultureLabel, cultureField, statusLabel, copyFromLabel, copyFromCombo, emptyCheckbox, btnCreate, btnCancel);
        Application.Run(dialog);
    }

    private void RemoveLanguage()
    {
        var removableLanguages = _resourceFiles
            .Where(rf => !rf.Language.IsDefault)
            .ToList();

        if (!removableLanguages.Any())
        {
            MessageBox.ErrorQuery("Error", "No languages to remove.\nCannot delete the default language.", "OK");
            return;
        }

        var dialog = new Dialog
        {
            Title = "Remove Language",
            Width = 65,
            Height = 18
        };

        var label = new Label("Select language to remove:") { X = 1, Y = 1 };

        var languageList = removableLanguages.Select(rf =>
            $"{rf.Language.Code,-12} {rf.Language.Name,-25} ({rf.Entries.Count,4} entries)"
        ).ToList();

        var listView = new ListView(languageList)
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 5
        };

        var noBackupCheckbox = new CheckBox("Skip backup (not recommended)")
        {
            X = 1,
            Y = Pos.AnchorEnd(3)
        };

        var btnRemove = new Button("Remove")
        {
            X = 1,
            Y = Pos.AnchorEnd(2)
        };

        btnRemove.Clicked += () =>
        {
            var selectedIdx = listView.SelectedItem;
            if (selectedIdx < 0 || selectedIdx >= removableLanguages.Count)
            {
                MessageBox.ErrorQuery("Error", "Please select a language to remove.", "OK");
                return;
            }

            var rf = removableLanguages[selectedIdx];
            var result = MessageBox.Query("Confirm Delete",
                $"Delete {rf.Language.Name} ({rf.Language.Code})?\n\n" +
                $"{rf.Entries.Count} entries will be lost.\n" +
                $"File: {Path.GetFileName(rf.Language.FilePath)}",
                "Delete", "Cancel");

            if (result == 0)
            {
                try
                {
                    // Create backup if requested
                    if (!noBackupCheckbox.Checked)
                    {
                        var backup = new BackupVersionManager(10);
                        var basePath = Path.GetDirectoryName(rf.Language.FilePath) ?? Environment.CurrentDirectory;
                        backup.CreateBackupAsync(rf.Language.FilePath, "tui-delete-language", basePath)
                            .GetAwaiter().GetResult();
                    }

                    // Delete the file
                    var manager = new LanguageFileManager();
                    manager.DeleteLanguageFile(rf.Language);

                    // Remove from list
                    _resourceFiles.Remove(rf);

                    // Rebuild DataTable without this column
                    var newDataTable = BuildDataTable();
                    _dataTable = newDataTable;
                    if (_tableView != null)
                    {
                        _tableView.Table = _dataTable;
                    }

                    UpdateStatus();

                    MessageBox.Query("Success",
                        $"Removed {rf.Language.Name} ({rf.Language.Code})",
                        "OK");

                    Application.RequestStop();
                }
                catch (Exception ex)
                {
                    MessageBox.ErrorQuery("Error", $"Failed to remove language:\n{ex.Message}", "OK");
                }
            }
        };

        var btnCancel = new Button("Cancel")
        {
            X = Pos.Right(btnRemove) + 2,
            Y = Pos.AnchorEnd(2)
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.Add(label, listView, noBackupCheckbox, btnRemove, btnCancel);
        Application.Run(dialog);
    }

    private void ShowHelp()
    {
        var help = "Keyboard Shortcuts:\n\n" +
                   "Key Management:\n" +
                   "Enter     - Edit selected key\n" +
                   "Ctrl+N    - Add new key\n" +
                   "Del       - Delete selected key\n" +
                   "F8        - Merge duplicate keys\n\n" +
                   "Language Management:\n" +
                   "Ctrl+L    - List languages\n" +
                   "F2        - Add new language\n" +
                   "F3        - Remove language\n\n" +
                   "Translation:\n" +
                   "Ctrl+T    - Translate selected key\n" +
                   "F4        - Translate missing values\n" +
                   "F5        - Configure providers\n\n" +
                   "File Operations:\n" +
                   "Ctrl+S    - Save changes\n" +
                   "F6        - Run validation\n" +
                   "F7        - Manage backups\n" +
                   "Ctrl+Q    - Quit editor\n\n" +
                   "Navigation:\n" +
                   "↑/↓       - Move selection\n" +
                   "PgUp/PgDn - Page up/down\n" +
                   "F1        - Show this help";

        MessageBox.Query("Help", help, "OK");
    }

    private bool ConfirmQuit()
    {
        if (!_hasUnsavedChanges) return true;

        var result = MessageBox.Query("Unsaved Changes",
            "Save before quitting?", "Save", "Discard", "Cancel");

        if (result == 0)
        {
            SaveChanges();
            return !_hasUnsavedChanges;
        }

        return result == 1;
    }

    private void UpdateStatus()
    {
        if (_statusLabel == null) return;
        _statusLabel.Text = GetStatusText();
    }

    private string GetStatusText()
    {
        var filteredCount = _dataTable.DefaultView.Count;
        var totalCount = _dataTable.Rows.Count;
        var langCount = _resourceFiles.Count;
        var status = $"Keys: {filteredCount}/{totalCount} | Languages: {langCount}";

        // Add extra keys warning if any found
        if (_extraKeysByLanguage.Any())
        {
            var totalExtraKeys = _extraKeysByLanguage.Sum(kvp => kvp.Value.Count);
            var affectedLangs = string.Join(", ", _extraKeysByLanguage.Keys.Take(2).Select(k =>
                k.Contains("(") ? k.Substring(k.LastIndexOf('(') + 1).TrimEnd(')') : k));
            if (_extraKeysByLanguage.Count > 2) affectedLangs += "...";
            status += $" | ⚠ Extra: {totalExtraKeys} ({affectedLangs})";
        }

        // Add case-insensitive duplicates warning
        if (_caseInsensitiveDuplicates.Any())
        {
            status += $" | ⚠ Duplicates: {_caseInsensitiveDuplicates.Count} (Ctrl+D)";
        }

        if (_hasUnsavedChanges) status += " [MODIFIED]";

        // Add help shortcuts
        status += " | Ctrl+T=Translate  F4=Auto-Translate  F6=Validate  Ctrl+S=Save  F1=Help";

        return status;
    }

    private void RebuildTableWithVisibleLanguages()
    {
        // Rebuild DataTable with only visible language columns
        var dt = new DataTable();

        // Add Key column
        dt.Columns.Add("Key", typeof(string));

        // Add columns only for visible languages
        var visibleResourceFiles = _resourceFiles
            .Where(rf => _filterCriteria.VisibleLanguageCodes.Contains(rf.Language.Code))
            .ToList();

        foreach (var rf in visibleResourceFiles)
        {
            dt.Columns.Add(rf.Language.Name, typeof(string));
        }

        // Add internal columns for filtering (hidden from display)
        var visibleColumn = dt.Columns.Add("_Visible", typeof(bool));
        visibleColumn.ColumnMapping = MappingType.Hidden;

        var extraKeyColumn = dt.Columns.Add("_HasExtraKey", typeof(bool));
        extraKeyColumn.ColumnMapping = MappingType.Hidden;

        // Populate rows
        foreach (var key in _allKeys)
        {
            var row = dt.NewRow();

            // Check if this key has extra key warning
            var hasExtraKey = false;
            var displayKey = key;

            // Find original row to get extra key status
            var originalRow = _dataTable.Rows.Cast<DataRow>()
                .FirstOrDefault(r => r["Key"].ToString()?.TrimStart('⚠', ' ') == key);
            if (originalRow != null)
            {
                hasExtraKey = (bool)originalRow["_HasExtraKey"];
                displayKey = hasExtraKey ? $"⚠ {key}" : key;
            }

            row["Key"] = displayKey;
            row["_Visible"] = true;
            row["_HasExtraKey"] = hasExtraKey;

            foreach (var rf in visibleResourceFiles)
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                row[rf.Language.Name] = entry?.Value ?? "";
            }

            dt.Rows.Add(row);
        }

        // Replace DataTable
        _dataTable = dt;
        if (_tableView != null)
        {
            _tableView.Table = _dataTable;
        }

        // Reapply filters
        ApplyFilters();
    }

    private void RebuildTableWithCommentRows()
    {
        // Rebuild DataTable based on _showComments state
        if (_showComments)
        {
            // Use double-row layout (value + comment rows)
            _dataTable = BuildDataTableWithDoubleRows();
        }
        else
        {
            // Use standard single-row layout
            _dataTable = BuildDataTable();
        }

        // Update TableView
        if (_tableView != null)
        {
            _tableView.Table = _dataTable;
        }

        // Reapply filters
        ApplyFilters();
    }

    /// <summary>
    /// Gets the key from a selected row
    /// </summary>
    /// <returns>The key, or null if not found</returns>
    private string? GetKeyFromSelectedRow(int rowIndex)
    {
        var displayedTable = _tableView?.Table;
        if (displayedTable == null || rowIndex < 0 || rowIndex >= displayedTable.Rows.Count)
        {
            return null;
        }

        // Get the key from the displayed row
        var displayedKeyValue = displayedTable.Rows[rowIndex]["Key"]?.ToString();
        if (string.IsNullOrEmpty(displayedKeyValue))
        {
            return null;
        }

        // Strip warning marker if present
        var key = displayedKeyValue.TrimStart('⚠', ' ');

        // Strip [N] suffix for duplicates (e.g., "AppName [2]" -> "AppName")
        var bracketIndex = key.LastIndexOf(" [");
        if (bracketIndex > 0 && key.EndsWith("]"))
        {
            key = key.Substring(0, bracketIndex);
        }

        return key;
    }

    /// <summary>
    /// Gets the EntryReference from a selected row (includes occurrence tracking)
    /// </summary>
    /// <returns>The entry reference, or null if not found</returns>
    private EntryReference? GetEntryReferenceFromSelectedRow(int rowIndex)
    {
        // Find the row in _dataTable that corresponds to the displayed row
        var displayedTable = _tableView?.Table;
        if (displayedTable == null || rowIndex < 0 || rowIndex >= displayedTable.Rows.Count)
        {
            return null;
        }

        var displayedKeyValue = displayedTable.Rows[rowIndex]["Key"]?.ToString();
        if (string.IsNullOrEmpty(displayedKeyValue))
        {
            return null;
        }

        // In the filtered/displayed table, we need to map back to _dataTable
        // The displayed table doesn't have the hidden columns, so we match by visible key value
        var matchingDataRow = _dataTable.Rows.Cast<DataRow>()
            .FirstOrDefault(r =>
            {
                var keyVal = r["Key"]?.ToString();
                return keyVal == displayedKeyValue ||
                       keyVal == displayedKeyValue.TrimStart('⚠', ' ');
            });

        if (matchingDataRow == null)
        {
            return null;
        }

        // Extract from hidden columns
        var actualKey = matchingDataRow["_ActualKey"]?.ToString();
        var occurrenceNumber = matchingDataRow["_OccurrenceNumber"] as int? ?? 1;

        if (string.IsNullOrEmpty(actualKey))
        {
            return null;
        }

        // Find the matching entry reference
        return _allEntries.FirstOrDefault(e =>
            e.Key == actualKey && e.OccurrenceNumber == occurrenceNumber);
    }

    private void ShowLanguageFilterDialog()
    {
        var dialog = new Dialog
        {
            Title = "Filter Languages",
            Width = Dim.Percent(60),
            Height = Dim.Percent(60)
        };

        var label = new Label
        {
            Text = "Select languages to display:",
            X = 1,
            Y = 1
        };

        var checkboxes = new List<CheckBox>();
        var yPos = 3;

        foreach (var rf in _resourceFiles)
        {
            var displayName = string.IsNullOrEmpty(rf.Language.Code)
                ? $"{_defaultLanguageCode} ({rf.Language.Name})"
                : $"{rf.Language.Code} ({rf.Language.Name})";

            var checkbox = new CheckBox
            {
                Text = displayName,
                X = 1,
                Y = yPos,
                Checked = _filterCriteria.VisibleLanguageCodes.Contains(rf.Language.Code)
            };

            checkboxes.Add(checkbox);
            yPos++;
        }

        var scrollView = new ScrollView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 5,
            ContentSize = new Size(50, yPos),
            ShowVerticalScrollIndicator = true
        };

        foreach (var cb in checkboxes)
        {
            scrollView.Add(cb);
        }

        var btnSelectAll = new Button
        {
            Text = "Select All",
            X = 1,
            Y = Pos.AnchorEnd(2)
        };
        btnSelectAll.Clicked += () =>
        {
            foreach (var cb in checkboxes)
            {
                cb.Checked = true;
            }
        };

        var btnSelectNone = new Button
        {
            Text = "Select None",
            X = Pos.Right(btnSelectAll) + 2,
            Y = Pos.AnchorEnd(2)
        };
        btnSelectNone.Clicked += () =>
        {
            foreach (var cb in checkboxes)
            {
                cb.Checked = false;
            }
        };

        var btnApply = new Button
        {
            Text = "Apply",
            X = Pos.Right(btnSelectNone) + 2,
            Y = Pos.AnchorEnd(2),
            IsDefault = true
        };
        btnApply.Clicked += () =>
        {
            // Update visible language codes
            _filterCriteria.VisibleLanguageCodes.Clear();
            for (int i = 0; i < checkboxes.Count; i++)
            {
                if (checkboxes[i].Checked)
                {
                    _filterCriteria.VisibleLanguageCodes.Add(_resourceFiles[i].Language.Code);
                }
            }

            // Update quick checkboxes in main UI
            for (int i = 0; i < _languageCheckboxes.Count && i < _resourceFiles.Count; i++)
            {
                _languageCheckboxes[i].Checked = _filterCriteria.VisibleLanguageCodes.Contains(_resourceFiles[i].Language.Code);
            }

            // Rebuild table
            RebuildTableWithVisibleLanguages();

            Application.RequestStop();
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            X = Pos.Right(btnApply) + 2,
            Y = Pos.AnchorEnd(2)
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.Add(label, scrollView, btnSelectAll, btnSelectNone, btnApply, btnCancel);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void DetectAndMarkExtraKeys()
    {
        var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
        if (defaultFile == null) return;

        // Detect extra keys using the filter service
        _extraKeysByLanguage = ResourceFilterService.DetectExtraKeysInFilteredFiles(defaultFile, _resourceFiles);

        // Build a set of all extra keys across all languages
        var allExtraKeys = new HashSet<string>();
        foreach (var keysList in _extraKeysByLanguage.Values)
        {
            foreach (var key in keysList)
            {
                allExtraKeys.Add(key);
            }
        }

        // Mark rows in DataTable with extra keys
        foreach (DataRow row in _dataTable.Rows)
        {
            var key = row["Key"].ToString() ?? "";
            if (allExtraKeys.Contains(key))
            {
                row["_HasExtraKey"] = true;
                // Add warning marker to key name for visual indication
                row["Key"] = $"⚠ {key}";
            }
        }
    }

    // Translation Methods

    private void TranslateSelection()
    {
        if (_tableView == null || _tableView.SelectedRow < 0)
        {
            MessageBox.ErrorQuery("Error", "Please select a key to translate.", "OK");
            return;
        }

        // Get the key from the selected row
        var key = GetKeyFromSelectedRow(_tableView.SelectedRow);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        ShowTranslateDialog(new List<string> { key });
    }

    private void TranslateMissing()
    {
        // Find all keys with missing values
        var keysToTranslate = new List<string>();

        foreach (var key in _allKeys)
        {
            foreach (var rf in _resourceFiles.Where(r => !r.Language.IsDefault))
            {
                var entry = rf.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                {
                    if (!keysToTranslate.Contains(key))
                    {
                        keysToTranslate.Add(key);
                    }
                    break;
                }
            }
        }

        if (keysToTranslate.Count == 0)
        {
            MessageBox.Query("Translation", "All keys have translations.", "OK");
            return;
        }

        var result = MessageBox.Query("Translate Missing",
            $"Found {keysToTranslate.Count} keys with missing translations.\n\nProceed with automatic translation?",
            "Yes", "No");

        if (result == 0)
        {
            ShowTranslateDialog(keysToTranslate);
        }
    }

    private void ShowTranslateDialog(List<string> keysToTranslate)
    {
        var dialog = new Dialog
        {
            Title = "Translate Keys",
            Width = Dim.Percent(80),
            Height = Dim.Percent(70)
        };

        var yPos = 1;

        // Show context information if translating a single key
        if (keysToTranslate.Count == 1)
        {
            var key = keysToTranslate[0];
            var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            var entry = defaultFile?.Entries.FirstOrDefault(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

            var contextLabel = new Label("Translation Context:")
            {
                X = 1,
                Y = yPos,
                ColorScheme = new ColorScheme
                {
                    Normal = Terminal.Gui.Attribute.Make(Color.BrightCyan, Color.Black)
                }
            };

            var keyLabel = new Label($"Key: {key}")
            {
                X = 1,
                Y = yPos + 1
            };

            var valueLabel = new Label($"Source Text: {entry?.Value ?? "(empty)"}")
            {
                X = 1,
                Y = yPos + 2,
                Width = Dim.Fill() - 1
            };

            dialog.Add(contextLabel, keyLabel, valueLabel);
            yPos += 3;

            // Add comment if present
            if (!string.IsNullOrWhiteSpace(entry?.Comment))
            {
                var commentLabel = new Label($"Comment: {entry.Comment}")
                {
                    X = 1,
                    Y = yPos,
                    Width = Dim.Fill() - 1,
                    ColorScheme = new ColorScheme
                    {
                        Normal = Terminal.Gui.Attribute.Make(Color.BrightYellow, Color.Black)
                    }
                };
                dialog.Add(commentLabel);
                yPos++;
            }

            // Add separator
            var separator = new Label(new string('─', 60))
            {
                X = 1,
                Y = yPos
            };
            dialog.Add(separator);
            yPos++;
        }

        // Provider selection
        var providerLabel = new Label("Translation Provider:") { X = 1, Y = yPos };
        var providers = TranslationProviderFactory.GetSupportedProviders();
        var defaultProvider = _configuration?.Translation?.DefaultProvider ?? "google";
        var selectedProviderIdx = Array.IndexOf(providers, defaultProvider);
        if (selectedProviderIdx < 0) selectedProviderIdx = 0;

        var providerCombo = new ComboBox
        {
            X = 1,
            Y = yPos + 1,
            Width = 20,
            Height = 8
        };
        providerCombo.SetSource(providers);
        providerCombo.SelectedItem = selectedProviderIdx;

        yPos += 3; // Move past provider label and combo

        // Target languages selection
        var langLabel = new Label("Target Languages:") { X = 1, Y = yPos };
        var targetLanguages = _resourceFiles
            .Where(rf => !rf.Language.IsDefault)
            .Select(rf => rf.Language.Code)
            .ToList();

        var langCheckboxes = new List<CheckBox>();
        yPos++;

        foreach (var lang in targetLanguages)
        {
            var checkbox = new CheckBox
            {
                Text = lang,
                X = 1,
                Y = yPos,
                Checked = true
            };
            langCheckboxes.Add(checkbox);
            yPos++;
        }

        // Only missing values checkbox
        var onlyMissingCheckbox = new CheckBox("Only translate missing values")
        {
            X = 1,
            Y = yPos + 1,
            Checked = false  // Default to translating all (user can check this to skip existing values)
        };

        // Status label
        var statusLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(4),
            Width = Dim.Fill() - 1,
            Text = $"Ready to translate {keysToTranslate.Count} key(s)"
        };

        var btnTranslate = new Button("Translate")
        {
            X = 1,
            Y = Pos.AnchorEnd(2)
        };

        var btnCancel = new Button("Cancel")
        {
            X = Pos.Right(btnTranslate) + 2,
            Y = Pos.AnchorEnd(2)
        };

        btnTranslate.Clicked += async () =>
        {
            var provider = providers[providerCombo.SelectedItem];
            var selectedLangs = langCheckboxes.Where(cb => cb.Checked)
                .Select(cb => cb.Text.ToString())
                .ToList();

            if (selectedLangs.Count == 0)
            {
                MessageBox.ErrorQuery("Error", "Please select at least one target language.", "OK");
                return;
            }

            try
            {
                // Create translation provider
                var translationProvider = TranslationProviderFactory.Create(provider, _configuration);
                if (!translationProvider.IsConfigured())
                {
                    var configHelp = GetProviderConfigurationHelp(provider);
                    MessageBox.ErrorQuery("Provider Not Configured",
                        $"Translation provider '{provider}' is not configured.\n\n{configHelp}",
                        "OK");
                    return;
                }

                statusLabel.Text = "Translating...";
                Application.Refresh();

                // Translate each key
                var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
                if (defaultFile == null) return;

                using var cache = new TranslationCache();
                int translated = 0;

                foreach (var key in keysToTranslate)
                {
                    // Get all occurrences of this key in the default file
                    var sourceEntries = defaultFile.Entries.Where(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (sourceEntries.Count == 0)
                        continue;

                    foreach (var targetLang in selectedLangs)
                    {
                        var targetFile = _resourceFiles.FirstOrDefault(rf => rf.Language.Code == targetLang);
                        if (targetFile == null) continue;

                        // Get all occurrences in target language
                        var targetEntries = targetFile.Entries.Where(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();

                        // If source has more occurrences than target, we need to add entries
                        // If source has fewer, we only update existing ones
                        for (int i = 0; i < sourceEntries.Count; i++)
                        {
                            var sourceEntry = sourceEntries[i];
                            if (string.IsNullOrWhiteSpace(sourceEntry.Value))
                                continue;

                            ResourceEntry? targetEntry = i < targetEntries.Count ? targetEntries[i] : null;

                            // Skip if only missing and value exists
                            if (onlyMissingCheckbox.Checked && targetEntry != null && !string.IsNullOrWhiteSpace(targetEntry.Value))
                                continue;

                            var request = new TranslationRequest
                            {
                                SourceText = sourceEntry.Value,
                                SourceLanguage = null, // Auto-detect
                                TargetLanguage = targetLang!
                            };

                            // Try cache first
                            TranslationResponse? response = null;
                            if (cache.TryGet(request, provider, out var cachedResponse) && cachedResponse != null)
                            {
                                response = cachedResponse;
                            }
                            else
                            {
                                response = await translationProvider.TranslateAsync(request);
                                cache.Store(request, response);
                            }

                            // Update or add entry
                            if (response != null && !string.IsNullOrWhiteSpace(response.TranslatedText))
                            {
                                if (targetEntry != null)
                                {
                                    targetEntry.Value = response.TranslatedText;
                                    if (!string.IsNullOrWhiteSpace(sourceEntry.Comment) && string.IsNullOrWhiteSpace(targetEntry.Comment))
                                    {
                                        targetEntry.Comment = sourceEntry.Comment;
                                    }
                                    translated++;
                                }
                                else
                                {
                                    targetFile.Entries.Add(new ResourceEntry
                                    {
                                        Key = key,
                                        Value = response.TranslatedText,
                                        Comment = sourceEntry.Comment
                                    });
                                    translated++;
                                }
                            }

                            statusLabel.Text = $"Translated {translated}...";
                            Application.Refresh();
                        }
                    }
                }

                // Only mark as changed and rebuild if something was actually translated
                if (translated > 0)
                {
                    _hasUnsavedChanges = true;

                    // Rebuild table to show translated values
                    RebuildTable();
                    UpdateStatus();

                    MessageBox.Query("Success",
                        $"Translated {translated} value(s) successfully.",
                        "OK");
                }
                else
                {
                    MessageBox.Query("No Changes",
                        "No values were translated.\n\n" +
                        "Possible reasons:\n" +
                        "- Target language(s) already have values and 'Only translate missing values' is checked\n" +
                        "- Translation provider returned empty results\n" +
                        "- Source values are empty",
                        "OK");
                }

                Application.RequestStop();
            }
            catch (TranslationException ex)
            {
                MessageBox.ErrorQuery("Translation Error",
                    $"Translation failed: {ex.Message}\n\nError Code: {ex.ErrorCode}",
                    "OK");
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error",
                    $"Translation failed: {ex.Message}",
                    "OK");
            }
        };

        btnCancel.Clicked += () => Application.RequestStop();

        dialog.Add(providerLabel, providerCombo, langLabel);
        foreach (var cb in langCheckboxes)
        {
            dialog.Add(cb);
        }
        dialog.Add(onlyMissingCheckbox, statusLabel, btnTranslate, btnCancel);

        Application.Run(dialog);
        dialog.Dispose();
    }

    private void ConfigureTranslation()
    {
        var dialog = new Dialog
        {
            Title = "Translation Configuration",
            Width = Dim.Percent(70),
            Height = 20
        };

        var message = "Translation Provider Configuration:\n\n" +
                      "API keys can be configured via:\n\n" +
                      "1. Environment Variables (recommended):\n" +
                      "   LRM_GOOGLE_API_KEY\n" +
                      "   LRM_DEEPL_API_KEY\n" +
                      "   LRM_LIBRETRANSLATE_API_KEY\n" +
                      "   LRM_OLLAMA_API_KEY\n" +
                      "   LRM_OPENAI_API_KEY\n" +
                      "   LRM_CLAUDE_API_KEY\n" +
                      "   LRM_AZUREOPENAI_API_KEY\n" +
                      "   LRM_AZURETRANSLATOR_API_KEY\n\n" +
                      "2. Secure Credential Store:\n" +
                      "   lrm config set-api-key --provider <name> --key <key>\n\n" +
                      "3. Configuration File (lrm.json):\n" +
                      "   Add to Translation section\n\n" +
                      "IMPORTANT:\n" +
                      "- Azure OpenAI requires: API key + Endpoint + Deployment name\n" +
                      "- AI providers (OpenAI, Claude, Ollama) support optional model selection\n" +
                      "- Configure these in lrm.json (see docs/TRANSLATION.md for details)";

        var textView = new TextView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 3,
            ReadOnly = true,
            Text = message
        };

        var btnClose = new Button("Close")
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1)
        };
        btnClose.Clicked += () => Application.RequestStop();

        dialog.Add(textView, btnClose);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private static string GetProviderConfigurationHelp(string provider)
    {
        return provider.ToLower() switch
        {
            "google" => "Required: LRM_GOOGLE_API_KEY\n\n" +
                       "Set via environment variable or add to lrm.json:\n" +
                       "\"Translation\": { \"Google\": { \"ApiKey\": \"your-key\" } }",

            "deepl" => "Required: LRM_DEEPL_API_KEY\n\n" +
                      "Set via environment variable or add to lrm.json:\n" +
                      "\"Translation\": { \"DeepL\": { \"ApiKey\": \"your-key\" } }",

            "libretranslate" => "Optional: LRM_LIBRETRANSLATE_API_KEY\n\n" +
                               "Also configure API URL if using custom instance:\n" +
                               "\"Translation\": { \"LibreTranslate\": { \"ApiUrl\": \"url\" } }",

            "ollama" => "Required: Ollama API URL (default: http://localhost:11434)\n" +
                       "Optional: Model name (default: llama3.2)\n\n" +
                       "Configure in lrm.json:\n" +
                       "\"Translation\": { \"Ollama\": { \"ApiUrl\": \"url\", \"Model\": \"model\" } }",

            "openai" => "Required: LRM_OPENAI_API_KEY\n" +
                       "Optional: Model (default: gpt-4o-mini)\n\n" +
                       "Set via environment variable or add to lrm.json:\n" +
                       "\"Translation\": { \"OpenAI\": { \"ApiKey\": \"key\", \"Model\": \"model\" } }",

            "claude" => "Required: LRM_CLAUDE_API_KEY\n" +
                       "Optional: Model (default: claude-3-5-sonnet-20241022)\n\n" +
                       "Set via environment variable or add to lrm.json:\n" +
                       "\"Translation\": { \"Claude\": { \"ApiKey\": \"key\", \"Model\": \"model\" } }",

            "azureopenai" => "Required:\n" +
                            "- LRM_AZUREOPENAI_API_KEY\n" +
                            "- Endpoint URL (e.g., https://your-resource.openai.azure.com)\n" +
                            "- Deployment name\n\n" +
                            "Configure in lrm.json:\n" +
                            "\"Translation\": { \"AzureOpenAI\": {\n" +
                            "  \"ApiKey\": \"key\",\n" +
                            "  \"Endpoint\": \"url\",\n" +
                            "  \"DeploymentName\": \"name\"\n" +
                            "} }",

            "azuretranslator" => "Required: LRM_AZURETRANSLATOR_API_KEY\n" +
                                "Optional: Region (for regional endpoint)\n\n" +
                                "Set via environment variable or add to lrm.json:\n" +
                                "\"Translation\": { \"AzureTranslator\": { \"ApiKey\": \"key\", \"Region\": \"region\" } }",

            _ => "Please configure API keys in lrm.json or environment variables.\n" +
                "See documentation for provider-specific requirements."
        };
    }
}
