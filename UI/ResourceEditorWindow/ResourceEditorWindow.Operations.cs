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
/// Core Operations (Edit, Delete, Save, Translate)
/// </summary>
public partial class ResourceEditorWindow : Window
{
    private void CopySelectedValueToClipboard()
    {
        if (_tableView == null || _tableView.SelectedRow < 0) return;

        try
        {
            var entryRef = GetEntryReferenceFromSelectedRow(_tableView.SelectedRow);
            if (entryRef == null) return;

            // Get the default language file
            var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile == null) return;

            // Get the value for this key
            var entry = GetNthOccurrence(defaultFile, entryRef.Key, entryRef.OccurrenceNumber);
            var value = entry?.Value ?? string.Empty;

            // Copy to clipboard
            Clipboard.Contents = value;

            // Show brief confirmation in status bar
            if (_statusBar != null)
            {
                var originalStatus = GetStatusText();
                _statusBar.Items = new StatusItem[] {
                    new StatusItem(Key.Null, $"✓ Copied: {value.Substring(0, Math.Min(50, value.Length))}{(value.Length > 50 ? "..." : "")}", null)
                };
                _statusBar.SetNeedsDisplay();

                // Restore status after 2 seconds
                Task.Run(async () =>
                {
                    await Task.Delay(2000);
                    Application.MainLoop.Invoke(() =>
                    {
                        if (_statusBar != null)
                        {
                            _statusBar.Items = new StatusItem[] {
                                new StatusItem(Key.Null, originalStatus, null)
                            };
                            _statusBar.SetNeedsDisplay();
                        }
                    });
                });
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Clipboard Error", $"Failed to copy to clipboard: {ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Pastes clipboard content into the selected row's default language value
    /// </summary>
    private void PasteValueFromClipboard()
    {
        if (_tableView == null || _tableView.SelectedRow < 0) return;

        try
        {
            var clipboardText = Clipboard.Contents?.ToString() ?? string.Empty;
            if (string.IsNullOrEmpty(clipboardText)) return;

            var entryRef = GetEntryReferenceFromSelectedRow(_tableView.SelectedRow);
            if (entryRef == null) return;

            // Get the default language file
            var defaultFile = _resourceFiles.FirstOrDefault(rf => rf.Language.IsDefault);
            if (defaultFile == null) return;

            // Find and update the entry
            var entry = GetNthOccurrence(defaultFile, entryRef.Key, entryRef.OccurrenceNumber);
            if (entry != null)
            {
                entry.Value = clipboardText;
                _hasUnsavedChanges = true;
                RebuildTable();
                UpdateStatus();

                // Show brief confirmation
                if (_statusBar != null)
                {
                    var originalStatus = GetStatusText();
                    _statusBar.Items = new StatusItem[] {
                        new StatusItem(Key.Null, $"✓ Pasted: {clipboardText.Substring(0, Math.Min(50, clipboardText.Length))}{(clipboardText.Length > 50 ? "..." : "")}", null)
                    };
                    _statusBar.SetNeedsDisplay();

                    // Restore status after 2 seconds
                    Task.Run(async () =>
                    {
                        await Task.Delay(2000);
                        Application.MainLoop.Invoke(() =>
                        {
                            if (_statusBar != null)
                            {
                                _statusBar.Items = new StatusItem[] {
                                    new StatusItem(Key.Null, originalStatus, null)
                                };
                                _statusBar.SetNeedsDisplay();
                            }
                        });
                    });
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Clipboard Error", $"Failed to paste from clipboard: {ex.Message}", "OK");
        }
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

    private void PerformFullCodeScan()
    {
        var dialog = new Dialog
        {
            Title = "Scanning Source Code",
            Width = 60,
            Height = 10
        };

        var statusLabel = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 1,
            Text = "Scanning source code for localization key references..."
        };

        var progressBar = new ProgressBar
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 1,
            Fraction = 0f
        };

        var detailLabel = new Label
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill() - 1,
            Text = "Initializing..."
        };

        dialog.Add(statusLabel, progressBar, detailLabel);

        // Perform scanning in background
        Task.Run(() =>
        {
            try
            {
                // Check if source path exists
                if (!Directory.Exists(_sourcePath))
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        detailLabel.Text = $"Source path not found: {_sourcePath}";
                        Application.Refresh();
                    });
                    Thread.Sleep(2000);
                    Application.MainLoop.Invoke(() => Application.RequestStop());
                    return;
                }

                Application.MainLoop.Invoke(() =>
                {
                    progressBar.Fraction = 0.2f;
                    detailLabel.Text = "Discovering source files...";
                    Application.Refresh();
                });

                // Create code scanner and scan
                var codeScanner = new CodeScanner();

                Application.MainLoop.Invoke(() =>
                {
                    progressBar.Fraction = 0.4f;
                    detailLabel.Text = $"Scanning {_sourcePath}...";
                    Application.Refresh();
                });

                _scanResult = codeScanner.Scan(_sourcePath, _resourceFiles, false);
                _isCodeScanned = true;

                Application.MainLoop.Invoke(() =>
                {
                    progressBar.Fraction = 0.9f;
                    detailLabel.Text = $"Processing {_scanResult.FilesScanned} files...";
                    Application.Refresh();
                });

                // Rebuild table to show scan indicators
                Application.MainLoop.Invoke(() =>
                {
                    RebuildTable();
                    UpdateStatus();
                });

                Application.MainLoop.Invoke(() =>
                {
                    progressBar.Fraction = 1f;
                    detailLabel.Text = $"Complete! Scanned {_scanResult.FilesScanned} files, found {_scanResult.TotalReferences} references.";
                    Application.Refresh();
                });

                Thread.Sleep(1000); // Brief pause to show completion
                Application.MainLoop.Invoke(() => Application.RequestStop());
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                {
                    detailLabel.Text = $"Error: {ex.Message}";
                    Application.Refresh();
                });
                Thread.Sleep(2000);
                Application.MainLoop.Invoke(() => Application.RequestStop());
            }
        });

        Application.Run(dialog);
        dialog.Dispose();
    }

    private void ShowScanningProgressDialog()
    {
        var dialog = new Dialog
        {
            Title = "Scanning Code",
            Width = 60,
            Height = 10
        };

        var statusLabel = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 1,
            Text = "Scanning source code for key references..."
        };

        var progressBar = new ProgressBar
        {
            X = 1,
            Y = 3,
            Width = Dim.Fill() - 1,
            Fraction = 0f
        };

        var detailLabel = new Label
        {
            X = 1,
            Y = 4,
            Width = Dim.Fill() - 1,
            Text = "Initializing..."
        };

        dialog.Add(statusLabel, progressBar, detailLabel);

        // Perform scanning in background
        Task.Run(() =>
        {
            try
            {
                if (!_caseInsensitiveDuplicates.Any()) return;

                // Use configured source path
                if (!Directory.Exists(_sourcePath))
                {
                    Application.MainLoop.Invoke(() =>
                    {
                        detailLabel.Text = $"Source path not found: {_sourcePath}";
                        Application.Refresh();
                    });
                    Thread.Sleep(2000);
                    Application.MainLoop.Invoke(() => Application.RequestStop());
                    return;
                }

                Application.MainLoop.Invoke(() =>
                {
                    progressBar.Fraction = 0.2f;
                    detailLabel.Text = "Discovering source files...";
                    Application.Refresh();
                });

                // Create code scanner and scan
                var codeScanner = new CodeScanner();

                Application.MainLoop.Invoke(() =>
                {
                    progressBar.Fraction = 0.4f;
                    detailLabel.Text = $"Scanning {_sourcePath}...";
                    Application.Refresh();
                });

                var scanResult = codeScanner.Scan(_sourcePath, _resourceFiles, false);

                Application.MainLoop.Invoke(() =>
                {
                    progressBar.Fraction = 0.7f;
                    detailLabel.Text = $"Processing {scanResult.FilesScanned} files...";
                    Application.Refresh();
                });

                // Update each duplicate with code references
                int processed = 0;
                int total = _caseInsensitiveDuplicates.Count;

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

                    processed++;
                    var fraction = 0.7f + (0.3f * processed / total);
                    Application.MainLoop.Invoke(() =>
                    {
                        progressBar.Fraction = fraction;
                        detailLabel.Text = $"Analyzing duplicates... {processed}/{total}";
                        Application.Refresh();
                    });
                }

                Application.MainLoop.Invoke(() =>
                {
                    progressBar.Fraction = 1f;
                    detailLabel.Text = $"Complete! Scanned {scanResult.FilesScanned} files, found {scanResult.TotalReferences} references.";
                    Application.Refresh();
                });

                Thread.Sleep(500); // Brief pause to show completion
                Application.MainLoop.Invoke(() => Application.RequestStop());
            }
            catch (Exception ex)
            {
                Application.MainLoop.Invoke(() =>
                {
                    detailLabel.Text = $"Error: {ex.Message}";
                    Application.Refresh();
                });
                Thread.Sleep(2000);
                Application.MainLoop.Invoke(() => Application.RequestStop());
            }
        });

        Application.Run(dialog);
        dialog.Dispose();
    }

    private void ScanCodeForDuplicateUsage()
    {
        if (!_caseInsensitiveDuplicates.Any()) return;

        // Use configured source path
        if (!Directory.Exists(_sourcePath)) return;

        // Create code scanner and scan
        var codeScanner = new CodeScanner();
        var scanResult = codeScanner.Scan(_sourcePath, _resourceFiles, false);

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
                _backend.Writer.Write(rf);
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

        // Check for case variants
        var caseVariants = occurrences.Select(e => e.Key).Distinct().ToList();
        string selectedKeyName;

        if (caseVariants.Count > 1)
        {
            // Ask user which key name to use
            selectedKeyName = ShowKeyNameSelectionDialog(caseVariants);
            if (selectedKeyName == null!)
            {
                return; // User cancelled
            }
        }
        else
        {
            selectedKeyName = caseVariants[0];
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
            var selectedIndex = ShowOccurrenceSelectionDialog(rf.Language.Name, key, langOccurrences, selectedKeyName);
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
            var occurrenceList = rf.Entries
                .Select((e, i) => (Entry: e, Index: i))
                .Where(x => x.Entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (occurrenceList.Count == 0)
                continue;

            // Standardize the key name of the selected occurrence
            var selectedIndex = selectedOccurrence - 1;
            if (selectedIndex >= 0 && selectedIndex < occurrenceList.Count)
            {
                // Use direct list access instead of tuple to ensure modification persists
                var entryIndex = occurrenceList[selectedIndex].Index;
                rf.Entries[entryIndex].Key = selectedKeyName;
            }

            if (occurrenceList.Count <= 1)
                continue;

            // Remove all except the selected one (in reverse to maintain indices)
            for (int i = occurrenceList.Count - 1; i >= 0; i--)
            {
                if (i + 1 != selectedOccurrence)
                {
                    rf.Entries.RemoveAt(occurrenceList[i].Index);
                }
            }
        }

        // Rebuild and refresh
        BuildEntryReferences();
        RebuildTable();
        _hasUnsavedChanges = true;

        var message = caseVariants.Count > 1
            ? $"Successfully merged '{key}' → '{selectedKeyName}'"
            : $"Successfully merged '{key}'";
        MessageBox.Query("Success", message, "OK");
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
                _backend.Writer.Write(rf);
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

    private void Undo()
    {
        if (!_operationHistory.CanUndo)
        {
            return;
        }

        _operationHistory.Undo();
        _hasUnsavedChanges = true;

        // Rebuild UI
        BuildEntryReferences();
        RebuildTable();
        UpdateStatus();
    }

    private void Redo()
    {
        if (!_operationHistory.CanRedo)
        {
            return;
        }

        _operationHistory.Redo();
        _hasUnsavedChanges = true;

        // Rebuild UI
        BuildEntryReferences();
        RebuildTable();
        UpdateStatus();
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

    private void ViewCodeReferencesForSelectedKey()
    {
        if (_tableView == null || _tableView.SelectedRow < 0)
        {
            MessageBox.ErrorQuery("Error", "Please select a key to view code references.", "OK");
            return;
        }

        // Get the key from the selected row
        var key = GetKeyFromSelectedRow(_tableView.SelectedRow);
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        ShowCodeReferences(key);
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

}
