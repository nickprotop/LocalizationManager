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
/// Data Management and Table Building
/// </summary>
public partial class ResourceEditorWindow : Window
{
    private DataTable BuildDataTable()
    {
        var dt = new DataTable();

        // Add Key column
        dt.Columns.Add("Key", typeof(string));

        // Add a Group column when more than one resource group is present
        if (_isMultiGroup)
        {
            dt.Columns.Add("Group", typeof(string));
        }

        // Add one column per distinct culture code (shared across groups)
        var languageColumns = GetLanguageColumns();
        foreach (var (_, header) in languageColumns)
        {
            dt.Columns.Add(header, typeof(string));
        }

        // Add comment columns for each language (hidden, used for filtering), keyed by code
        foreach (var (code, _) in languageColumns)
        {
            var commentColumn = dt.Columns.Add($"_Comment_{code}", typeof(string));
            commentColumn.ColumnMapping = MappingType.Hidden;
        }

        // Add internal columns for tracking occurrences (hidden from display)
        var baseNameColumn = dt.Columns.Add("_BaseName", typeof(string));
        baseNameColumn.ColumnMapping = MappingType.Hidden;

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

            row["_BaseName"] = entryRef.BaseName;
            row["_ActualKey"] = entryRef.Key;
            row["_OccurrenceNumber"] = entryRef.OccurrenceNumber;
            row["_Visible"] = true;
            row["_HasExtraKey"] = false;
            if (_isMultiGroup)
            {
                row["Group"] = entryRef.BaseName;
            }

            // Get the Nth occurrence from each language column, scoped to this entry's group
            foreach (var (code, header) in languageColumns)
            {
                var entry = GetEntryForCell(entryRef.BaseName, entryRef.Key, entryRef.OccurrenceNumber, code);
                // For plural entries, show a summary; for simple entries, show the value
                string displayValue;
                if (entry?.IsPlural == true && entry.PluralForms != null && entry.PluralForms.Count > 0)
                {
                    // Show plural forms summary, e.g., "one: {0} item, other: {0} items"
                    var forms = entry.PluralForms.Take(2).Select(kv => $"{kv.Key}: {TruncateValue(kv.Value, 20)}");
                    displayValue = $"[plural] {string.Join(", ", forms)}";
                    if (entry.PluralForms.Count > 2) displayValue += ", ...";
                }
                else
                {
                    displayValue = entry?.Value ?? "";
                }
                row[header] = displayValue;
                row[$"_Comment_{code}"] = entry?.Comment ?? "";
            }

            // Build display key with selection marker and status indicator
            var displayKey = entryRef.DisplayKey;

            // Add selection marker if this entry is selected
            if (IsEntrySelected(entryRef))
            {
                displayKey = $"► {displayKey}";
            }

            // Add status indicator based on row status
            var status = DetermineRowStatus(row);
            var statusIcon = GetStatusIcon(status);
            if (!string.IsNullOrEmpty(statusIcon))
            {
                displayKey = $"{statusIcon} {displayKey}";
            }

            row["Key"] = displayKey;

            dt.Rows.Add(row);
        }

        return dt;
    }


    private DataTable BuildDataTableWithDoubleRows()
    {
        var dt = new DataTable();

        // Add Key column
        dt.Columns.Add("Key", typeof(string));

        // Add a Group column when more than one resource group is present
        if (_isMultiGroup)
        {
            dt.Columns.Add("Group", typeof(string));
        }

        // Add one column per distinct culture code (shared across groups)
        var languageColumns = GetLanguageColumns();
        foreach (var (_, header) in languageColumns)
        {
            dt.Columns.Add(header, typeof(string));
        }

        // Add hidden metadata columns
        var rowTypeColumn = dt.Columns.Add("_RowType", typeof(string));
        rowTypeColumn.ColumnMapping = MappingType.Hidden;

        var logicalKeyColumn = dt.Columns.Add("_LogicalKey", typeof(string));
        logicalKeyColumn.ColumnMapping = MappingType.Hidden;

        var doubleBaseNameColumn = dt.Columns.Add("_BaseName", typeof(string));
        doubleBaseNameColumn.ColumnMapping = MappingType.Hidden;

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
            valueRow["_BaseName"] = entryRef.BaseName;
            valueRow["_ActualKey"] = entryRef.Key;
            valueRow["_OccurrenceNumber"] = entryRef.OccurrenceNumber;
            valueRow["_Visible"] = true;
            valueRow["_HasExtraKey"] = false;
            if (_isMultiGroup)
            {
                valueRow["Group"] = entryRef.BaseName;
            }

            // Get the Nth occurrence from each language column, scoped to this entry's group
            foreach (var (code, header) in languageColumns)
            {
                var entry = GetEntryForCell(entryRef.BaseName, entryRef.Key, entryRef.OccurrenceNumber, code);
                // For plural entries, show a summary; for simple entries, show the value
                string displayValue;
                if (entry?.IsPlural == true && entry.PluralForms != null && entry.PluralForms.Count > 0)
                {
                    var forms = entry.PluralForms.Take(2).Select(kv => $"{kv.Key}: {TruncateValue(kv.Value, 20)}");
                    displayValue = $"[plural] {string.Join(", ", forms)}";
                    if (entry.PluralForms.Count > 2) displayValue += ", ...";
                }
                else
                {
                    displayValue = entry?.Value ?? "";
                }
                valueRow[header] = displayValue;
            }
            dt.Rows.Add(valueRow);

            // Comment Row (indented with box-drawing characters)
            var commentRow = dt.NewRow();
            commentRow["Key"] = "  \u2514\u2500 Comment";  // "  └─ Comment"
            commentRow["_RowType"] = "Comment";
            commentRow["_LogicalKey"] = entryRef.DisplayKey;
            commentRow["_BaseName"] = entryRef.BaseName;
            commentRow["_ActualKey"] = entryRef.Key;
            commentRow["_OccurrenceNumber"] = entryRef.OccurrenceNumber;
            commentRow["_Visible"] = true;
            commentRow["_HasExtraKey"] = false;
            if (_isMultiGroup)
            {
                commentRow["Group"] = entryRef.BaseName;
            }

            foreach (var (code, header) in languageColumns)
            {
                var entry = GetEntryForCell(entryRef.BaseName, entryRef.Key, entryRef.OccurrenceNumber, code);
                commentRow[header] = entry?.Comment ?? "";
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

        // One block of entries per resource group, ordered by group then key, so that
        // keys from CustomerResources and GlassResources are both present and never
        // collapsed into each other.
        foreach (var baseName in _groups)
        {
            var defaultFile = _resourceFiles.FirstOrDefault(rf =>
                rf.Language.IsDefault &&
                string.Equals(rf.Language.BaseName, baseName, StringComparison.OrdinalIgnoreCase));

            // Fall back to any file of the group if none is flagged default (defensive).
            defaultFile ??= _resourceFiles.FirstOrDefault(rf =>
                string.Equals(rf.Language.BaseName, baseName, StringComparison.OrdinalIgnoreCase));

            if (defaultFile == null) continue;

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
                    TotalOccurrences = occurrenceCounts[entry.Key],
                    BaseName = baseName
                });
            }
        }
    }

    /// <summary>
    /// The distinct culture columns to display, in default-first order. Each entry pairs the
    /// culture code with the column header. The same code shared by several groups is one column.
    /// </summary>
    private List<(string Code, string Header)> GetLanguageColumns()
    {
        var seen = new HashSet<string>();
        var columns = new List<(string Code, string Header)>();

        foreach (var rf in _resourceFiles
            .OrderBy(rf => rf.Language.IsDefault ? 0 : 1)
            .ThenBy(rf => rf.Language.Code, StringComparer.OrdinalIgnoreCase))
        {
            if (!seen.Add(rf.Language.Code)) continue;
            columns.Add((rf.Language.Code, GetLanguageColumnHeader(rf.Language)));
        }

        return columns;
    }

    /// <summary>
    /// Column header for a language: the configured default code marked "(Default)", else the
    /// culture display name. Independent of the group so the same code maps to one column.
    /// </summary>
    private string GetLanguageColumnHeader(LanguageInfo language)
    {
        if (language.IsDefault)
        {
            var code = string.IsNullOrEmpty(language.Code) ? _defaultLanguageCode : language.Code;
            return $"{code} (Default)";
        }
        return language.Name;
    }

    /// <summary>
    /// All resource files belonging to a resource group (by base name).
    /// </summary>
    private List<ResourceFile> GetGroupFiles(string baseName)
        => _resourceFiles
            .Where(rf => string.Equals(rf.Language.BaseName, baseName, StringComparison.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    /// Determines the group (base name) a key belongs to. With a single group this is just
    /// that group; otherwise the first group whose default file contains the key, falling
    /// back to the first group.
    /// </summary>
    private string ResolveBaseNameForKey(string key)
    {
        if (_groups.Count <= 1)
            return _groups.FirstOrDefault() ?? string.Empty;

        foreach (var baseName in _groups)
        {
            var defaultFile = _resourceFiles.FirstOrDefault(rf =>
                rf.Language.IsDefault &&
                string.Equals(rf.Language.BaseName, baseName, StringComparison.OrdinalIgnoreCase));
            if (defaultFile != null && defaultFile.Entries.Any(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)))
                return baseName;
        }
        return _groups.First();
    }

    /// <summary>
    /// Resolves the value/comment to display for a given (group, key, occurrence, culture code).
    /// Looks only within the entry's own group so values never leak between groups.
    /// </summary>
    private ResourceEntry? GetEntryForCell(string baseName, string key, int occurrenceNumber, string code)
    {
        var groupFiles = GetGroupFiles(baseName);
        var columns = MergedLanguageColumns.Build(groupFiles.Select(f => f.Language));

        // The column code passed in comes from GetLanguageColumns (the raw file Code, which is
        // "" for the suffix-less default). MergedLanguageColumns keys columns by the effective
        // code ("default" when blank). Match against both so either source resolves correctly.
        var effectiveCode = string.IsNullOrEmpty(code) ? "default" : code;
        var column = columns.FirstOrDefault(c =>
            string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(c.Code, effectiveCode, StringComparison.OrdinalIgnoreCase));
        if (column == null) return null;

        ResourceFile? FileFor(string path) =>
            groupFiles.FirstOrDefault(f => (f.Language.FilePath ?? string.Empty) == path);

        var winner = FileFor(column.WinningFilePath);
        var winnerEntry = winner == null ? null : GetNthOccurrence(winner, key, occurrenceNumber);
        if (winnerEntry != null && !string.IsNullOrEmpty(winnerEntry.Value))
            return winnerEntry;

        // Gap-fill only for the primary occurrence: if the default winner has no value for this
        // key, fall back through the colliding culture files to the first non-empty value.
        if (occurrenceNumber == 1)
        {
            foreach (var lp in column.ConflictingFilePaths)
            {
                var lf = FileFor(lp);
                var le = lf == null ? null : GetNthOccurrence(lf, key, 1);
                if (le != null && !string.IsNullOrEmpty(le.Value)) return le;
            }
        }

        // Fall back to the winner entry even if empty/null (preserves prior behavior for empties).
        return winnerEntry;
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

    private DataTable CreateDisplayTable(DataTable sourceTable)
    {
        var columnNames = sourceTable.Columns.Cast<DataColumn>()
            .Where(c => !c.ColumnName.StartsWith("_"))
            .Select(c => c.ColumnName)
            .ToArray();
        return sourceTable.DefaultView.ToTable(false, columnNames);
    }

    /// <summary>
    /// Determines the color status for a table row based on its content
    /// Priority: MissingFromResources > Missing > Extra > Duplicate > Modified > UnusedInCode > Normal
    /// </summary>
    private RowStatus DetermineRowStatus(DataRow row)
    {
        try
        {
            var actualKey = (string)row["_ActualKey"];

            // Check if key is missing from resources but used in code (highest priority)
            if (_isCodeScanned && _scanResult != null)
            {
                var isMissingFromResources = _scanResult.MissingKeys.Any(mk =>
                    mk.Key.Equals(actualKey, StringComparison.OrdinalIgnoreCase));
                if (isMissingFromResources)
                {
                    return RowStatus.MissingFromResources;
                }
            }

            // Check if this is a duplicate key
            var occurrenceNumber = (int)row["_OccurrenceNumber"];
            var entryRef = _allEntries.FirstOrDefault(e =>
                e.Key == (string)row["_ActualKey"] &&
                e.OccurrenceNumber == occurrenceNumber);

            if (entryRef != null && entryRef.TotalOccurrences > 1)
            {
                return RowStatus.Duplicate;
            }

            // Check for missing translations within this row's own group. A blank cell is
            // only "missing" when the group actually has a file for that culture.
            var baseName = row.Table.Columns.Contains("_BaseName") ? row["_BaseName"] as string ?? string.Empty : string.Empty;
            foreach (var (code, header) in GetLanguageColumns())
            {
                var file = _resourceFiles.FirstOrDefault(rf =>
                    string.Equals(rf.Language.BaseName, baseName, StringComparison.OrdinalIgnoreCase) &&
                    rf.Language.Code == code);
                if (file == null || file.Language.IsDefault) continue;
                if (row.Table.Columns.Contains(header) && row[header] is string value && string.IsNullOrWhiteSpace(value))
                {
                    return RowStatus.Missing;
                }
            }

            // Check for extra keys (keys that exist in translation files but not in default)
            if (row["_HasExtraKey"] is bool hasExtraKey && hasExtraKey)
            {
                return RowStatus.Extra;
            }

            // Check if key is unused in code (lowest priority, only if scanned)
            if (_isCodeScanned && _scanResult != null)
            {
                var isUnusedInCode = _scanResult.UnusedKeys.Any(uk =>
                    uk.Equals(actualKey, StringComparison.OrdinalIgnoreCase));
                if (isUnusedInCode)
                {
                    return RowStatus.UnusedInCode;
                }
            }

            return RowStatus.Normal;
        }
        catch
        {
            return RowStatus.Normal;
        }
    }

    /// <summary>
    /// Gets the color scheme for a specific row status
    /// </summary>
    private Terminal.Gui.Attribute GetColorForRowStatus(RowStatus status)
    {
        return status switch
        {
            RowStatus.Missing => Terminal.Gui.Attribute.Make(Color.BrightRed, Color.Black),
            RowStatus.Extra => Terminal.Gui.Attribute.Make(Color.BrightYellow, Color.Black),
            RowStatus.Modified => Terminal.Gui.Attribute.Make(Color.BrightGreen, Color.Black),
            RowStatus.Duplicate => Terminal.Gui.Attribute.Make(Color.BrightMagenta, Color.Black),
            RowStatus.UnusedInCode => Terminal.Gui.Attribute.Make(Color.Gray, Color.Black),
            RowStatus.MissingFromResources => Terminal.Gui.Attribute.Make(Color.BrightRed, Color.Black),
            _ => Terminal.Gui.Attribute.Make(Color.White, Color.Black)
        };
    }

    /// <summary>
    /// Gets the status icon/indicator for a row status (visual indicator since Terminal.Gui 1.19.0 doesn't support row colors)
    /// </summary>
    private string GetStatusIcon(RowStatus status)
    {
        return status switch
        {
            RowStatus.Missing => "⚠",              // Warning sign for missing translations
            RowStatus.Extra => "⭐",                // Star for extra keys
            RowStatus.Duplicate => "◆",            // Diamond for duplicates
            RowStatus.UnusedInCode => "∅",         // Empty set for unused in code
            RowStatus.MissingFromResources => "✗", // Ballot X for missing from .resx
            _ => ""                                // No icon for normal rows
        };
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

        // Rebuild selection indices to map selected entries to current row indices
        RebuildSelectionIndices();
    }

    // Case-Insensitive Duplicate Detection

    private void DetectCaseInsensitiveDuplicates()
    {
        _caseInsensitiveDuplicates.Clear();

        // Inspect every resource group's default file for duplicate keys.
        var defaultFiles = _resourceFiles.Where(rf => rf.Language.IsDefault).ToList();
        if (defaultFiles.Count == 0) return;

        // Find duplicates (case-insensitive per ResX specification) across all groups' defaults
        var duplicateGroups = defaultFiles
            .SelectMany(f => f.Entries)
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

    private void RebuildTableWithVisibleLanguages()
    {
        // Rebuild DataTable with only visible language columns, preserving group and
        // occurrence tracking (one row per entry reference, scoped to its group).
        var dt = new DataTable();

        // Add Key column
        dt.Columns.Add("Key", typeof(string));

        // Add a Group column when more than one resource group is present
        if (_isMultiGroup)
        {
            dt.Columns.Add("Group", typeof(string));
        }

        // Only the culture columns the user has chosen to see
        var languageColumns = GetLanguageColumns()
            .Where(c => _filterCriteria.VisibleLanguageCodes.Contains(c.Code))
            .ToList();
        foreach (var (_, header) in languageColumns)
        {
            dt.Columns.Add(header, typeof(string));
        }

        // Add internal columns (hidden from display)
        var baseNameColumn = dt.Columns.Add("_BaseName", typeof(string));
        baseNameColumn.ColumnMapping = MappingType.Hidden;

        var actualKeyColumn = dt.Columns.Add("_ActualKey", typeof(string));
        actualKeyColumn.ColumnMapping = MappingType.Hidden;

        var occurrenceColumn = dt.Columns.Add("_OccurrenceNumber", typeof(int));
        occurrenceColumn.ColumnMapping = MappingType.Hidden;

        var visibleColumn = dt.Columns.Add("_Visible", typeof(bool));
        visibleColumn.ColumnMapping = MappingType.Hidden;

        var extraKeyColumn = dt.Columns.Add("_HasExtraKey", typeof(bool));
        extraKeyColumn.ColumnMapping = MappingType.Hidden;

        // Populate rows - one per entry reference (group + key + occurrence)
        foreach (var entryRef in _allEntries)
        {
            var row = dt.NewRow();

            var hasExtraKey = IsExtraKey(entryRef);
            var displayKey = hasExtraKey ? $"⚠ {entryRef.DisplayKey}" : entryRef.DisplayKey;

            row["Key"] = displayKey;
            row["_BaseName"] = entryRef.BaseName;
            row["_ActualKey"] = entryRef.Key;
            row["_OccurrenceNumber"] = entryRef.OccurrenceNumber;
            row["_Visible"] = true;
            row["_HasExtraKey"] = hasExtraKey;
            if (_isMultiGroup)
            {
                row["Group"] = entryRef.BaseName;
            }

            foreach (var (code, header) in languageColumns)
            {
                var entry = GetEntryForCell(entryRef.BaseName, entryRef.Key, entryRef.OccurrenceNumber, code);
                row[header] = entry?.Value ?? "";
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

    /// <summary>
    /// Whether an entry is an "extra" key (present in a non-default file of its group but
    /// not in the group's default file).
    /// </summary>
    private bool IsExtraKey(EntryReference entryRef)
    {
        if (_extraKeysByLanguage.Count == 0) return false;
        return _extraKeysByLanguage.Values.Any(keys =>
            keys.Contains(entryRef.Key, StringComparer.OrdinalIgnoreCase));
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
    private void DetectAndMarkExtraKeys()
    {
        _extraKeysByLanguage = new Dictionary<string, List<string>>();

        // Detect extra keys per group: a key in a non-default file of a group that is not
        // present in that same group's default file. Comparing across groups would wrongly
        // flag legitimate keys from other groups as "extra".
        foreach (var baseName in _groups)
        {
            var groupFiles = _resourceFiles
                .Where(rf => string.Equals(rf.Language.BaseName, baseName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            var defaultFile = groupFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile == null) continue;

            var perGroup = ResourceFilterService.DetectExtraKeysInFilteredFiles(defaultFile, groupFiles);
            foreach (var kvp in perGroup)
            {
                // Disambiguate the language bucket by group when multiple groups are present.
                var bucket = _isMultiGroup ? $"{baseName} / {kvp.Key}" : kvp.Key;
                _extraKeysByLanguage[bucket] = kvp.Value;
            }
        }

        // Build a set of all extra keys across all languages/groups
        var allExtraKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var keysList in _extraKeysByLanguage.Values)
        {
            foreach (var key in keysList)
            {
                allExtraKeys.Add(key);
            }
        }

        // Mark rows in DataTable with extra keys (match on the hidden actual key)
        foreach (DataRow row in _dataTable.Rows)
        {
            var actualKey = _dataTable.Columns.Contains("_ActualKey") ? row["_ActualKey"] as string ?? "" : "";
            if (!string.IsNullOrEmpty(actualKey) && allExtraKeys.Contains(actualKey))
            {
                row["_HasExtraKey"] = true;
                // Add warning marker to key name for visual indication
                var key = row["Key"].ToString() ?? "";
                if (!key.StartsWith("⚠"))
                {
                    row["Key"] = $"⚠ {key}";
                }
            }
        }
    }

    // Translation Methods

    /// <summary>
    /// Truncates a string value to the specified max length, adding ellipsis if needed.
    /// </summary>
    private static string TruncateValue(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Length <= maxLength) return value;
        return value.Substring(0, maxLength - 3) + "...";
    }
}
