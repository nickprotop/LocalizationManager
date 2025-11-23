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
/// Search and Filter Operations
/// </summary>
public partial class ResourceEditorWindow : Window
{
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

            // Apply additional code usage filters if code is scanned
            if (_isCodeScanned && _scanResult != null)
            {
                var filteredIndices = new List<int>();

                foreach (var idx in matchingIndices)
                {
                    var row = _dataTable.Rows[idx];
                    var actualKey = (string)row["_ActualKey"];

                    // Check if filters are active
                    bool showUnusedOnly = _filterUnusedCheckBox?.Checked ?? false;
                    bool showMissingOnly = _filterMissingFromResourcesCheckBox?.Checked ?? false;

                    // If no usage filters are checked, include all rows
                    if (!showUnusedOnly && !showMissingOnly)
                    {
                        filteredIndices.Add(idx);
                        continue;
                    }

                    bool isUnused = _scanResult.UnusedKeys.Any(uk =>
                        uk.Equals(actualKey, StringComparison.OrdinalIgnoreCase));
                    bool isMissing = _scanResult.MissingKeys.Any(mk =>
                        mk.Key.Equals(actualKey, StringComparison.OrdinalIgnoreCase));

                    // Include row if it matches any active filter
                    if ((showUnusedOnly && isUnused) || (showMissingOnly && isMissing))
                    {
                        filteredIndices.Add(idx);
                    }
                }

                matchingIndices = filteredIndices;
            }

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

            // Update matched row indices for search navigation
            // Convert to 0-based indices in the filtered/displayed table
            _matchedRowIndices = Enumerable.Range(0, filteredTable.Rows.Count).ToList();
            _currentMatchIndex = _matchedRowIndices.Any() ? 0 : -1;
            UpdateMatchCounter();
        }
        catch (Exception ex)
        {
            // If filtering fails (e.g., invalid regex), show all rows
            _dataTable.DefaultView.RowFilter = string.Empty;

            // Create display table without internal columns
            var allRowsTable = CreateDisplayTable(_dataTable);

            _tableView.Table = allRowsTable;
            _tableView.SetNeedsDisplay();

            // Clear match navigation on error
            _matchedRowIndices.Clear();
            _currentMatchIndex = -1;
            UpdateMatchCounter();

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
        // Strip status icons (⚠, ⭐, ◆, ∅, ✗) when matching
        var matchingDataRow = _dataTable.Rows.Cast<DataRow>()
            .FirstOrDefault(r =>
            {
                var keyVal = r["Key"]?.ToString();
                return keyVal == displayedKeyValue ||
                       keyVal == displayedKeyValue.TrimStart('⚠', '⭐', '◆', '∅', '✗', ' ');
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

    /// <summary>
    /// Updates the match counter label showing "X/Y matches"
    /// </summary>
    private void UpdateMatchCounter()
    {
        if (_matchCounterLabel == null) return;

        if (_matchedRowIndices.Any())
        {
            var currentMatch = _currentMatchIndex >= 0 ? _currentMatchIndex + 1 : 1;
            var totalMatches = _matchedRowIndices.Count;
            _matchCounterLabel.Text = $"{currentMatch}/{totalMatches}";
        }
        else
        {
            _matchCounterLabel.Text = "";
        }
        _matchCounterLabel.SetNeedsDisplay();
    }

    /// <summary>
    /// Navigates to the next search match
    /// </summary>
    private void NavigateToNextMatch()
    {
        if (!_matchedRowIndices.Any() || _tableView == null) return;

        _currentMatchIndex = (_currentMatchIndex + 1) % _matchedRowIndices.Count;
        var targetRow = _matchedRowIndices[_currentMatchIndex];

        _tableView.SelectedRow = targetRow;
        _tableView.EnsureSelectedCellIsVisible();
        _tableView.SetNeedsDisplay();
        UpdateMatchCounter();
    }

    /// <summary>
    /// Navigates to the previous search match
    /// </summary>
    private void NavigateToPreviousMatch()
    {
        if (!_matchedRowIndices.Any() || _tableView == null) return;

        _currentMatchIndex--;
        if (_currentMatchIndex < 0)
        {
            _currentMatchIndex = _matchedRowIndices.Count - 1;
        }

        var targetRow = _matchedRowIndices[_currentMatchIndex];
        _tableView.SelectedRow = targetRow;
        _tableView.EnsureSelectedCellIsVisible();
        _tableView.SetNeedsDisplay();
        UpdateMatchCounter();
    }

    private void UpdateStatus()
    {
        if (_statusBar == null) return;
        _statusBar.Items = new StatusItem[] {
            new StatusItem(Key.Null, GetStatusText(), null)
        };
        _statusBar.SetNeedsDisplay();
    }

    private string GetStatusText()
    {
        var filteredCount = _dataTable.DefaultView.Count;
        var totalCount = _dataTable.Rows.Count;
        var langCount = _resourceFiles.Count;
        var status = $"Keys: {filteredCount}/{totalCount} | Languages: {langCount}";

        // Add selection count if any rows are selected
        if (_selectedEntries.Any())
        {
            status += $" | 📋 Selected: {_selectedEntries.Count}";
        }

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

        // Add code scan information
        if (_isCodeScanned && _scanResult != null)
        {
            status += $" | 🔍 Scanned: {_scanResult.FilesScanned} files, {_scanResult.TotalReferences} refs";
            if (_scanResult.UnusedKeys.Any())
            {
                status += $" | Unused: {_scanResult.UnusedKeys.Count}";
            }
            if (_scanResult.MissingKeys.Any())
            {
                status += $" | Missing: {_scanResult.MissingKeys.Count}";
            }
        }
        else if (Directory.Exists(_sourcePath))
        {
            status += " | 🔍 Not scanned (F7 to scan)";
        }

        if (_hasUnsavedChanges) status += " [MODIFIED]";

        // Add help shortcuts
        status += " | Ctrl+T=Translate  F4=Auto-Translate  F6=Validate  Ctrl+S=Save  F1=Help";

        return status;
    }

}
