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
/// Batch Operations
/// </summary>
public partial class ResourceEditorWindow : Window
{
    /// <summary>
    /// Toggles selection for the current row
    /// </summary>
    private void ToggleCurrentRowSelection()
    {
        if (_tableView == null || _tableView.SelectedRow < 0)
            return;

        var rowIndex = _tableView.SelectedRow;
        var entryRef = GetEntryReferenceFromSelectedRow(rowIndex);
        if (entryRef == null)
            return;

        var displayKey = entryRef.DisplayKey;

        if (_selectedEntries.ContainsKey(displayKey))
        {
            _selectedEntries.Remove(displayKey);
            _selectedRowIndices.Remove(rowIndex);
        }
        else
        {
            _selectedEntries[displayKey] = entryRef;
            _selectedRowIndices.Add(rowIndex);
            _selectionAnchor = rowIndex;
        }

        UpdateStatus();
        RebuildTable(); // Refresh to show selection markers
    }

    /// <summary>
    /// Selects all visible rows
    /// </summary>
    private void SelectAll()
    {
        if (_tableView == null)
            return;

        _selectedRowIndices.Clear();
        _selectedEntries.Clear();

        // Select all rows in the current table view
        for (int i = 0; i < _tableView.Table.Rows.Count; i++)
        {
            var entryRef = GetEntryReferenceFromSelectedRow(i);
            if (entryRef != null)
            {
                _selectedRowIndices.Add(i);
                _selectedEntries[entryRef.DisplayKey] = entryRef;
            }
        }

        if (_selectedRowIndices.Any())
        {
            _selectionAnchor = 0;
        }

        UpdateStatus();
        RebuildTable();
    }

    /// <summary>
    /// Clears all selections
    /// </summary>
    private void ClearSelection()
    {
        _selectedRowIndices.Clear();
        _selectedEntries.Clear();
        _selectionAnchor = -1;
        UpdateStatus();
        RebuildTable();
    }

    /// <summary>
    /// Extends selection upward from current position
    /// </summary>
    private void ExtendSelectionUp()
    {
        if (_tableView == null || _tableView.SelectedRow < 0)
            return;

        var currentRow = _tableView.SelectedRow;

        // Set anchor if not set
        if (_selectionAnchor < 0)
        {
            _selectionAnchor = currentRow;
        }

        // Move selection up one row
        if (currentRow > 0)
        {
            var newRow = currentRow - 1;
            _tableView.SelectedRow = newRow;

            // Select range from anchor to new position
            SelectRange(_selectionAnchor, newRow);
        }
    }

    /// <summary>
    /// Extends selection downward from current position
    /// </summary>
    private void ExtendSelectionDown()
    {
        if (_tableView == null || _tableView.SelectedRow < 0)
            return;

        var currentRow = _tableView.SelectedRow;

        // Set anchor if not set
        if (_selectionAnchor < 0)
        {
            _selectionAnchor = currentRow;
        }

        // Move selection down one row
        if (currentRow < _tableView.Table.Rows.Count - 1)
        {
            var newRow = currentRow + 1;
            _tableView.SelectedRow = newRow;

            // Select range from anchor to new position
            SelectRange(_selectionAnchor, newRow);
        }
    }

    /// <summary>
    /// Selects a range of rows between start and end (inclusive)
    /// </summary>
    private void SelectRange(int start, int end)
    {
        _selectedRowIndices.Clear();
        _selectedEntries.Clear();

        var minRow = Math.Min(start, end);
        var maxRow = Math.Max(start, end);

        for (int i = minRow; i <= maxRow; i++)
        {
            var entryRef = GetEntryReferenceFromSelectedRow(i);
            if (entryRef != null)
            {
                _selectedRowIndices.Add(i);
                _selectedEntries[entryRef.DisplayKey] = entryRef;
            }
        }

        UpdateStatus();
        RebuildTable();
    }

    /// <summary>
    /// Gets the list of selected EntryReferences
    /// </summary>
    private List<EntryReference> GetSelectedEntries()
    {
        return _selectedEntries.Values.ToList();
    }

    /// <summary>
    /// Bulk translate selected keys
    /// </summary>
    private void BulkTranslate()
    {
        var selectedEntries = GetSelectedEntries();

        if (!selectedEntries.Any())
        {
            MessageBox.ErrorQuery("No Selection", "No keys selected. Select keys using Space or Ctrl+A.", "OK");
            return;
        }

        var keysToTranslate = selectedEntries.Select(e => e.DisplayKey).ToList();
        ShowTranslateDialog(keysToTranslate);
    }

    /// <summary>
    /// Bulk delete selected keys
    /// </summary>
    private void BulkDelete()
    {
        var selectedEntries = GetSelectedEntries();

        if (!selectedEntries.Any())
        {
            MessageBox.ErrorQuery("No Selection", "No keys selected. Select keys using Space or Ctrl+A.", "OK");
            return;
        }

        var keyCount = selectedEntries.Count;
        var uniqueKeys = selectedEntries.Select(e => e.Key).Distinct().Count();

        var result = MessageBox.Query(
            "Confirm Bulk Delete",
            $"Delete {keyCount} selected entries ({uniqueKeys} unique keys)?",
            "Delete", "Cancel"
        );

        if (result == 0) // Delete
        {
            try
            {
                // Create backup before bulk delete
                var backupManager = new BackupVersionManager(10);
                var basePath = Path.GetDirectoryName(_resourceFiles.First().Language.FilePath) ?? Environment.CurrentDirectory;
                foreach (var rf in _resourceFiles)
                {
                    backupManager.CreateBackupAsync(rf.Language.FilePath, "tui-bulk-delete", basePath)
                        .GetAwaiter().GetResult();
                }

                // Delete each selected entry
                foreach (var entryRef in selectedEntries)
                {
                    DeleteSpecificOccurrence(entryRef.Key, entryRef.OccurrenceNumber);
                }

                _hasUnsavedChanges = true;
                ClearSelection();
                BuildEntryReferences();
                RebuildTable();
                UpdateStatus();

                MessageBox.Query("Success", $"Deleted {keyCount} entries.", "OK");
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Error", $"Failed to delete entries: {ex.Message}", "OK");
            }
        }
    }

    /// <summary>
    /// Checks if a row index is selected
    /// </summary>
    private bool IsRowSelected(int rowIndex)
    {
        return _selectedRowIndices.Contains(rowIndex);
    }

    /// <summary>
    /// Rebuilds the row index mapping after table rebuild
    /// Maps _selectedEntries back to current row indices
    /// </summary>
    private void RebuildSelectionIndices()
    {
        _selectedRowIndices.Clear();

        if (_tableView == null || !_selectedEntries.Any())
            return;

        // Find current row indices for each selected entry
        for (int i = 0; i < _tableView.Table.Rows.Count; i++)
        {
            var entryRef = GetEntryReferenceFromSelectedRow(i);
            if (entryRef != null && _selectedEntries.ContainsKey(entryRef.DisplayKey))
            {
                _selectedRowIndices.Add(i);
            }
        }
    }

    /// <summary>
    /// Checks if an EntryReference is selected
    /// </summary>
    private bool IsEntrySelected(EntryReference entryRef)
    {
        return _selectedEntries.ContainsKey(entryRef.DisplayKey);
    }
}
