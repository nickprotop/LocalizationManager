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

            row["_ActualKey"] = entryRef.Key;
            row["_OccurrenceNumber"] = entryRef.OccurrenceNumber;
            row["_Visible"] = true;
            row["_HasExtraKey"] = false;

            // Get the Nth occurrence from each language file
            foreach (var rf in _resourceFiles)
            {
                var entry = GetNthOccurrence(rf, entryRef.Key, entryRef.OccurrenceNumber);
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
                row[rf.Language.Name] = displayValue;
                row[$"_Comment_{rf.Language.Code}"] = entry?.Comment ?? "";
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
                valueRow[rf.Language.Name] = displayValue;
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

            // Check for missing translations
            foreach (var rf in _resourceFiles)
            {
                if (!rf.Language.IsDefault && row[rf.Language.Name] is string value && string.IsNullOrWhiteSpace(value))
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
