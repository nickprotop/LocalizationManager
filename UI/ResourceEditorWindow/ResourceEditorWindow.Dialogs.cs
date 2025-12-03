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
/// Dialog Windows and User Interactions
/// </summary>
public partial class ResourceEditorWindow : Window
{
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
            ShowScanningProgressDialog();
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

    private string ShowKeyNameSelectionDialog(List<string> caseVariants)
    {
        var dialog = new Dialog
        {
            Title = "Select Key Name",
            Width = 60,
            Height = 12 + caseVariants.Count
        };

        var message = new Label
        {
            Text = $"Found {caseVariants.Count} case variants.\n" +
                   "Which key name should be used after merge?",
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
            Height = caseVariants.Count + 1
        };

        listView.SetSource(caseVariants);
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

        string? selectedKeyName = null;

        btnSelect.Clicked += () =>
        {
            selectedKeyName = caseVariants[listView.SelectedItem];
            Application.RequestStop();
        };

        btnCancel.Clicked += () =>
        {
            selectedKeyName = null;
            Application.RequestStop();
        };

        dialog.Add(message, listView, btnSelect, btnCancel);
        Application.Run(dialog);
        dialog.Dispose();

        return selectedKeyName!;
    }

    private int ShowOccurrenceSelectionDialog(string languageName, string key, List<ResourceEntry> occurrences, string selectedKeyName)
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
            var preview = value.Length > 60 ? value.Substring(0, 57) + "..." : value;
            // Show key name if it differs from the selected standard
            var keyDisplay = entry.Key != selectedKeyName ? $" ({entry.Key})" : "";
            items.Add($"[{i + 1}] \"{preview}\"{keyDisplay}{comment}");
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
        manager.SetBackend(_backend);

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
                    manager.SetBackend(_backend);
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

    private void ShowTableContextMenu()
    {
        if (_tableView == null || _tableView.SelectedRow < 0)
        {
            return;
        }

        var entryRef = GetEntryReferenceFromSelectedRow(_tableView.SelectedRow);
        if (entryRef == null)
        {
            return;
        }

        // Build context menu items
        var menuItems = new List<string>
        {
            "Edit Key (Enter)",
            "Translate (Ctrl+T)",
            "Copy Value (Ctrl+C)",
            "Delete Key (Del)"
        };

        // Add "View Code References" if code has been scanned
        if (_isCodeScanned && _scanResult != null)
        {
            var hasReferences = _scanResult.AllKeyUsages.Any(ku =>
                ku.Key.Equals(entryRef.Key, StringComparison.OrdinalIgnoreCase) &&
                ku.References.Any());

            if (hasReferences)
            {
                menuItems.Insert(1, "View Code References");
            }
        }

        // Show selection dialog using a Dialog with ListView
        var dialog = new Dialog
        {
            Title = "Actions",
            Width = 50,
            Height = menuItems.Count + 6
        };

        var listView = new ListView
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 3,
            AllowsMarking = false
        };
        listView.SetSource(menuItems);

        var btnOk = new Button("OK")
        {
            X = Pos.Center() - 10,
            Y = Pos.AnchorEnd(1)
        };

        var btnCancel = new Button("Cancel")
        {
            X = Pos.Center() + 2,
            Y = Pos.AnchorEnd(1)
        };

        string? selectedAction = null;

        btnOk.Clicked += () =>
        {
            if (listView.SelectedItem >= 0 && listView.SelectedItem < menuItems.Count)
            {
                selectedAction = menuItems[listView.SelectedItem];
            }
            Application.RequestStop();
        };

        btnCancel.Clicked += () => Application.RequestStop();

        // Handle Enter key on ListView
        listView.KeyPress += (args) =>
        {
            if (args.KeyEvent.Key == Key.Enter)
            {
                if (listView.SelectedItem >= 0 && listView.SelectedItem < menuItems.Count)
                {
                    selectedAction = menuItems[listView.SelectedItem];
                }
                Application.RequestStop();
                args.Handled = true;
            }
        };

        dialog.Add(listView, btnOk, btnCancel);

        Application.Run(dialog);
        dialog.Dispose();

        if (selectedAction == null) return; // Cancelled

        switch (selectedAction)
        {
            case "Edit Key (Enter)":
                EditKey(entryRef.Key, entryRef.OccurrenceNumber);
                break;

            case "View Code References":
                ShowCodeReferences(entryRef.Key);
                break;

            case "Translate (Ctrl+T)":
                TranslateSelection();
                break;

            case "Copy Value (Ctrl+C)":
                CopySelectedValueToClipboard();
                break;

            case "Delete Key (Del)":
                DeleteSelectedKey();
                break;
        }
    }

    private void ShowCodeReferences(string key)
    {
        if (_scanResult == null)
        {
            MessageBox.ErrorQuery("Error", "No scan results available. Run F7 to scan code first.", "OK");
            return;
        }

        var keyUsage = _scanResult.AllKeyUsages.FirstOrDefault(ku =>
            ku.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        if (keyUsage == null || !keyUsage.References.Any())
        {
            MessageBox.Query("Code References",
                $"Key '{key}' has no code references.\n\n" +
                (keyUsage != null && _scanResult.UnusedKeys.Contains(key)
                    ? "This key appears to be unused in the codebase."
                    : "This key was not found in the code scan."),
                "OK");
            return;
        }

        var dialog = new Dialog
        {
            Title = $"Code References: {key}",
            Width = Dim.Percent(80),
            Height = Dim.Percent(70)
        };

        var infoLabel = new Label
        {
            Text = $"Found {keyUsage.ReferenceCount} reference(s) in {keyUsage.References.Select(r => r.FilePath).Distinct().Count()} file(s):",
            X = 1,
            Y = 1
        };

        // Create table for references
        var refTable = new DataTable();
        refTable.Columns.Add("File", typeof(string));
        refTable.Columns.Add("Line", typeof(int));
        refTable.Columns.Add("Pattern", typeof(string));
        refTable.Columns.Add("Confidence", typeof(string));

        foreach (var reference in keyUsage.References.OrderBy(r => r.FilePath).ThenBy(r => r.Line))
        {
            var fileName = Path.GetFileName(reference.FilePath);
            var row = refTable.NewRow();
            row["File"] = fileName;
            row["Line"] = reference.Line;
            row["Pattern"] = reference.Pattern;
            row["Confidence"] = reference.Confidence.ToString();
            refTable.Rows.Add(row);
        }

        var tableView = new TableView
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 3,
            FullRowSelect = true,
            Table = refTable
        };

        var btnClose = new Button("Close")
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1)
        };
        btnClose.Clicked += () => Application.RequestStop();

        dialog.Add(infoLabel, tableView, btnClose);

        Application.Run(dialog);
        dialog.Dispose();
    }

    private void ShowHelp()
    {
        var help = "Keyboard Shortcuts:\n\n" +
                   "Key Management:\n" +
                   "Enter     - Edit selected key\n" +
                   "Ctrl+N    - Add new key\n" +
                   "Del       - Delete selected key\n" +
                   "F8        - Merge duplicate keys\n" +
                   "Ctrl+C    - Copy value to clipboard\n" +
                   "Ctrl+V    - Paste value from clipboard\n" +
                   "Ctrl+Z    - Undo last operation\n" +
                   "Ctrl+Y    - Redo last operation\n\n" +
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
                   "Ctrl+Q    - Quit editor\n\n" +
                   "Code Scanning:\n" +
                   "F7        - Scan source code for key usage\n" +
                   "Ctrl+D    - Show duplicate keys with code refs\n" +
                   "Filters: Use 'Unused in code' and 'Missing from .resx'\n" +
                   "         checkboxes to filter by usage status\n\n" +
                   "Navigation:\n" +
                   "↑/↓       - Move selection\n" +
                   "PgUp/PgDn - Page up/down\n" +
                   "Right-click - Show context menu\n" +
                   "F1        - Show this help\n\n" +
                   "Search Navigation:\n" +
                   "F3        - Next match (or Remove Language if no search)\n" +
                   "Shift+F3  - Previous match\n" +
                   "Note: Match counter shows current/total matches\n\n" +
                   "Status Indicators:\n" +
                   "⚠         - Missing translation\n" +
                   "⭐         - Extra key (not in default language)\n" +
                   "◆         - Duplicate key\n" +
                   "∅         - Unused in code (requires scan)\n" +
                   "✗         - Missing from .resx (requires scan)\n\n" +
                   "Paths:\n" +
                   $"Resource: {_resourcePath}\n" +
                   $"Source:   {_sourcePath}\n" +
                   "Note: Use --source-path argument to specify a custom path\n" +
                   "for code scanning (defaults to parent of resource path).";

        MessageBox.Query("Help", help, "OK");
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

        // Status label and progress bar
        var statusLabel = new Label
        {
            X = 1,
            Y = Pos.AnchorEnd(5),
            Width = Dim.Fill() - 1,
            Text = $"Ready to translate {keysToTranslate.Count} key(s)"
        };

        var progressBar = new ProgressBar
        {
            X = 1,
            Y = Pos.AnchorEnd(4),
            Width = Dim.Fill() - 1,
            Fraction = 0f
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
                progressBar.Fraction = 0f;
                Application.Refresh();

                // Translate each key
                var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
                if (defaultFile == null) return;

                using var cache = new TranslationCache();
                int translated = 0;
                int totalOperations = 0;
                int completedOperations = 0;

                // Calculate total operations (keys × target languages × occurrences)
                foreach (var key in keysToTranslate)
                {
                    var sourceEntries = defaultFile.Entries.Where(e => e.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).ToList();
                    totalOperations += sourceEntries.Count * selectedLangs.Count;
                }

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
                            {
                                completedOperations++;
                                continue;
                            }

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

                            completedOperations++;
                            var progress = totalOperations > 0 ? (float)completedOperations / totalOperations : 0f;
                            progressBar.Fraction = progress;
                            statusLabel.Text = $"Translating... {completedOperations}/{totalOperations} ({progress * 100:F0}%)";
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
        dialog.Add(onlyMissingCheckbox, progressBar, statusLabel, btnTranslate, btnCancel);

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
