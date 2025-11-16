// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LocalizationManager.Core.Backup;
using LocalizationManager.Core.Models;
using LocalizationManager.Shared.Models;
using Terminal.Gui;

namespace LocalizationManager.UI;

/// <summary>
/// Advanced backup management window with restore, diff, and prune capabilities.
/// </summary>
public class BackupManagerWindow : Dialog
{
    private readonly string _basePath;
    private readonly List<ResourceFile> _resourceFiles;
    private readonly BackupVersionManager _backupManager;
    private ListView? _fileListView;
    private ListView? _backupListView;
    private Label? _statusLabel;
    private Dictionary<string, List<BackupMetadata>> _backupsByFile;

    public BackupManagerWindow(string basePath, List<ResourceFile> resourceFiles)
    {
        _basePath = basePath;
        _resourceFiles = resourceFiles;
        _backupManager = new BackupVersionManager(10);
        _backupsByFile = new Dictionary<string, List<BackupMetadata>>();

        Title = "Backup Manager (F7)";
        Width = Dim.Percent(90);
        Height = Dim.Percent(85);

        InitializeComponents();
        LoadBackups();
    }

    private void InitializeComponents()
    {
        // File list (left panel)
        var fileLabel = new Label("Resource Files:") { X = 1, Y = 1 };

        var fileNames = _resourceFiles
            .Select(rf => Path.GetFileName(rf.Language.FilePath))
            .Distinct()
            .OrderBy(f => f)
            .ToList();

        _fileListView = new ListView(fileNames)
        {
            X = 1,
            Y = 2,
            Width = 30,
            Height = Dim.Fill() - 8
        };

        _fileListView.SelectedItemChanged += (args) => OnFileSelected();

        // Backup list (right panel)
        var backupLabel = new Label("Backups:") { X = 32, Y = 1 };

        _backupListView = new ListView(new List<string>())
        {
            X = 32,
            Y = 2,
            Width = Dim.Fill() - 32,
            Height = Dim.Fill() - 8
        };

        // Status bar
        _statusLabel = new Label("Select a file to view backups")
        {
            X = 1,
            Y = Pos.AnchorEnd(6),
            Width = Dim.Fill() - 1,
            ColorScheme = Colors.Dialog
        };

        // Buttons
        var btnRestore = new Button("Restore")
        {
            X = 1,
            Y = Pos.AnchorEnd(4)
        };
        btnRestore.Clicked += OnRestore;

        var btnDiff = new Button("View Diff")
        {
            X = Pos.Right(btnRestore) + 2,
            Y = Pos.AnchorEnd(4)
        };
        btnDiff.Clicked += OnViewDiff;

        var btnInfo = new Button("Details")
        {
            X = Pos.Right(btnDiff) + 2,
            Y = Pos.AnchorEnd(4)
        };
        btnInfo.Clicked += OnViewDetails;

        var btnDelete = new Button("Delete")
        {
            X = Pos.Right(btnInfo) + 2,
            Y = Pos.AnchorEnd(4)
        };
        btnDelete.Clicked += OnDelete;

        var btnPrune = new Button("Prune Old")
        {
            X = Pos.Right(btnDelete) + 2,
            Y = Pos.AnchorEnd(4)
        };
        btnPrune.Clicked += OnPrune;

        var btnRefresh = new Button("Refresh")
        {
            X = Pos.Right(btnPrune) + 2,
            Y = Pos.AnchorEnd(4)
        };
        btnRefresh.Clicked += () => LoadBackups();

        var btnClose = new Button("Close")
        {
            X = Pos.AnchorEnd(10),
            Y = Pos.AnchorEnd(2)
        };
        btnClose.Clicked += () => Application.RequestStop();

        Add(fileLabel, _fileListView, backupLabel, _backupListView, _statusLabel,
            btnRestore, btnDiff, btnInfo, btnDelete, btnPrune, btnRefresh, btnClose);
    }

    private void LoadBackups()
    {
        _backupsByFile.Clear();

        var fileNames = _resourceFiles
            .Select(rf => Path.GetFileName(rf.Language.FilePath))
            .Distinct()
            .ToList();

        foreach (var fileName in fileNames)
        {
            try
            {
                var backups = _backupManager.ListBackupsAsync(fileName, _basePath)
                    .GetAwaiter()
                    .GetResult()
                    .OrderByDescending(b => b.Version)
                    .ToList();

                _backupsByFile[fileName] = backups;
            }
            catch
            {
                _backupsByFile[fileName] = new List<BackupMetadata>();
            }
        }

        if (_fileListView != null && _fileListView.Source.Count > 0)
        {
            OnFileSelected();
        }

        UpdateStatus();
    }

    private void OnFileSelected()
    {
        if (_fileListView == null || _backupListView == null)
            return;

        var selectedIdx = _fileListView.SelectedItem;
        if (selectedIdx < 0)
            return;

        var fileName = _fileListView.Source.ToList()[selectedIdx].ToString();
        if (string.IsNullOrEmpty(fileName) || fileName == null || !_backupsByFile.ContainsKey(fileName))
            return;

        var backups = _backupsByFile[fileName];
        var displayList = backups.Select(b =>
            $"v{b.Version:D3} | {b.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss} | {b.Operation,-18} | {b.KeyCount,4} keys | {b.Hash[..8]}"
        ).ToList();

        if (!displayList.Any())
        {
            displayList.Add("(No backups found)");
        }

        _backupListView.SetSource(displayList);
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        if (_statusLabel == null)
            return;

        var totalBackups = _backupsByFile.Values.Sum(list => list.Count);
        var totalFiles = _backupsByFile.Count;

        _statusLabel.Text = $"Total: {totalBackups} backups across {totalFiles} files";
    }

    private void OnRestore()
    {
        var backup = GetSelectedBackup();
        if (backup == null)
            return;

        var fileName = GetSelectedFileName();
        if (string.IsNullOrEmpty(fileName))
            return;

        // Show restore options dialog
        var dialog = new Dialog
        {
            Title = "Restore Backup",
            Width = 70,
            Height = 18
        };

        var infoLabel = new Label($"Restore from backup v{backup.Version:D3}?")
        {
            X = 1,
            Y = 1
        };

        var detailsLabel = new Label(
            $"File: {fileName}\n" +
            $"Date: {backup.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n" +
            $"Operation: {backup.Operation}\n" +
            $"Keys: {backup.KeyCount}")
        {
            X = 1,
            Y = 3
        };

        var partialCheckbox = new CheckBox("Partial restore (select keys)")
        {
            X = 1,
            Y = 8
        };

        var previewCheckbox = new CheckBox("Preview changes before restoring", true)
        {
            X = 1,
            Y = 9
        };

        var btnRestore = new Button("Restore")
        {
            X = 1,
            Y = Pos.AnchorEnd(2)
        };

        btnRestore.Clicked += () =>
        {
            Application.RequestStop();
            PerformRestore(fileName, backup, partialCheckbox.Checked, previewCheckbox.Checked);
        };

        var btnCancel = new Button("Cancel")
        {
            X = Pos.Right(btnRestore) + 2,
            Y = Pos.AnchorEnd(2)
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.Add(infoLabel, detailsLabel, partialCheckbox, previewCheckbox, btnRestore, btnCancel);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void PerformRestore(string fileName, BackupMetadata backup, bool partial, bool preview)
    {
        try
        {
            var filePath = Path.Combine(_basePath, fileName);
            var restoreService = new BackupRestoreService(_backupManager);

            if (preview)
            {
                // Show preview dialog
                var diffResult = restoreService.PreviewRestoreAsync(fileName, backup.Version, filePath, _basePath)
                    .GetAwaiter()
                    .GetResult();

                ShowRestorePreview(diffResult, () =>
                {
                    ExecuteRestore(fileName, backup, partial, restoreService);
                });
            }
            else
            {
                ExecuteRestore(fileName, backup, partial, restoreService);
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Restore Error", $"Failed to restore backup:\n{ex.Message}", "OK");
        }
    }

    private void ExecuteRestore(string fileName, BackupMetadata backup, bool partial, BackupRestoreService restoreService)
    {
        try
        {
            if (partial)
            {
                // TODO: Show key selection dialog
                MessageBox.Query("Info", "Partial restore not yet implemented.", "OK");
                return;
            }

            var filePath = Path.Combine(_basePath, fileName);

            // Perform restore (creates backup before restore by default)
            restoreService.RestoreAsync(fileName, backup.Version, filePath, _basePath, createBackupBeforeRestore: true)
                .GetAwaiter()
                .GetResult();

            MessageBox.Query("Success", $"Backup v{backup.Version:D3} restored successfully!", "OK");
            LoadBackups();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Restore Error", $"Failed to restore:\n{ex.Message}", "OK");
        }
    }

    private void ShowRestorePreview(BackupDiffResult diffResult, Action onConfirm)
    {
        var dialog = new Dialog
        {
            Title = "Restore Preview",
            Width = Dim.Percent(80),
            Height = Dim.Percent(70)
        };

        var label = new Label($"Preview of {diffResult.Statistics.TotalChanges} changes:")
        {
            X = 1,
            Y = 1
        };

        var changeList = diffResult.Changes.Select(c =>
        {
            var action = c.Type.ToString();
            var oldVal = c.OldValue != null && c.OldValue.Length > 20
                ? c.OldValue.Substring(0, 20)
                : c.OldValue ?? "null";
            var newVal = c.NewValue != null && c.NewValue.Length > 20
                ? c.NewValue.Substring(0, 20)
                : c.NewValue ?? "null";
            return $"{action,-10} | {c.Key,-30} | Old: {oldVal,-20} | New: {newVal,-20}";
        }).ToList();

        var listView = new ListView(changeList)
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 5
        };

        var btnConfirm = new Button("Confirm Restore")
        {
            X = 1,
            Y = Pos.AnchorEnd(2)
        };
        btnConfirm.Clicked += () =>
        {
            Application.RequestStop();
            onConfirm();
        };

        var btnCancel = new Button("Cancel")
        {
            X = Pos.Right(btnConfirm) + 2,
            Y = Pos.AnchorEnd(2)
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.Add(label, listView, btnConfirm, btnCancel);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void OnViewDiff()
    {
        var backup = GetSelectedBackup();
        if (backup == null)
            return;

        var fileName = GetSelectedFileName();
        if (string.IsNullOrEmpty(fileName))
            return;

        try
        {
            var filePath = Path.Combine(_basePath, fileName);
            var backupFilePath = _backupManager.GetBackupFilePathAsync(fileName, backup.Version, _basePath)
                .GetAwaiter()
                .GetResult();

            if (backupFilePath == null || !File.Exists(backupFilePath))
            {
                MessageBox.ErrorQuery("Error", "Backup file not found.", "OK");
                return;
            }

            var diffService = new BackupDiffService();
            var diff = diffService.CompareWithCurrentAsync(backup, backupFilePath, filePath, includeUnchanged: false)
                .GetAwaiter()
                .GetResult();

            var diffWindow = new BackupDiffWindow(diff, backup);
            Application.Run(diffWindow);
            diffWindow.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Diff Error", $"Failed to generate diff:\n{ex.Message}", "OK");
        }
    }

    private void OnViewDetails()
    {
        var backup = GetSelectedBackup();
        if (backup == null)
            return;

        var details = $"Backup Details\n\n" +
                     $"Version: {backup.Version}\n" +
                     $"Timestamp: {backup.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n" +
                     $"Operation: {backup.Operation}\n" +
                     $"Key Count: {backup.KeyCount}\n" +
                     $"Changed Keys: {backup.ChangedKeys}\n" +
                     $"Hash: {backup.Hash}\n" +
                     $"File Path: {backup.FilePath}";

        MessageBox.Query("Backup Details", details, "OK");
    }

    private void OnDelete()
    {
        var backup = GetSelectedBackup();
        if (backup == null)
            return;

        var fileName = GetSelectedFileName();
        if (string.IsNullOrEmpty(fileName))
            return;

        var result = MessageBox.Query("Confirm Delete",
            $"Delete backup v{backup.Version:D3}?\n\n" +
            $"Date: {backup.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}\n" +
            $"Operation: {backup.Operation}",
            "Delete", "Cancel");

        if (result == 0)
        {
            try
            {
                var backupFilePath = _backupManager.GetBackupFilePathAsync(fileName, backup.Version, _basePath)
                    .GetAwaiter()
                    .GetResult();

                if (backupFilePath != null && File.Exists(backupFilePath))
                {
                    File.Delete(backupFilePath);
                    LoadBackups();
                    MessageBox.Query("Success", "Backup deleted.", "OK");
                }
                else
                {
                    MessageBox.ErrorQuery("Error", "Backup file not found.", "OK");
                }
            }
            catch (Exception ex)
            {
                MessageBox.ErrorQuery("Delete Error", $"Failed to delete backup:\n{ex.Message}", "OK");
            }
        }
    }

    private void OnPrune()
    {
        var fileName = GetSelectedFileName();
        if (string.IsNullOrEmpty(fileName))
            return;

        var dialog = new Dialog
        {
            Title = "Prune Old Backups",
            Width = 60,
            Height = 15
        };

        var label = new Label($"Prune old backups for {fileName}:")
        {
            X = 1,
            Y = 1
        };

        var keepLabel = new Label("Keep most recent:")
        {
            X = 1,
            Y = 3
        };

        var keepField = new TextField("10")
        {
            X = Pos.Right(keepLabel) + 1,
            Y = 3,
            Width = 10
        };

        var btnPrune = new Button("Prune")
        {
            X = 1,
            Y = Pos.AnchorEnd(2)
        };

        btnPrune.Clicked += () =>
        {
            if (int.TryParse(keepField.Text.ToString(), out int keepCount))
            {
                Application.RequestStop();
                PerformPrune(fileName, keepCount);
            }
            else
            {
                MessageBox.ErrorQuery("Error", "Invalid number", "OK");
            }
        };

        var btnCancel = new Button("Cancel")
        {
            X = Pos.Right(btnPrune) + 2,
            Y = Pos.AnchorEnd(2)
        };
        btnCancel.Clicked += () => Application.RequestStop();

        dialog.Add(label, keepLabel, keepField, btnPrune, btnCancel);
        Application.Run(dialog);
        dialog.Dispose();
    }

    private void PerformPrune(string fileName, int keepCount)
    {
        try
        {
            if (!_backupsByFile.ContainsKey(fileName))
                return;

            var backups = _backupsByFile[fileName]
                .OrderByDescending(b => b.Version)
                .Skip(keepCount)
                .ToList();

            if (!backups.Any())
            {
                MessageBox.Query("Info", "No backups to prune.", "OK");
                return;
            }

            var result = MessageBox.Query("Confirm Prune",
                $"Delete {backups.Count} old backup(s)?",
                "Delete", "Cancel");

            if (result == 0)
            {
                foreach (var backup in backups)
                {
                    var backupFilePath = _backupManager.GetBackupFilePathAsync(fileName, backup.Version, _basePath)
                        .GetAwaiter()
                        .GetResult();

                    if (backupFilePath != null && File.Exists(backupFilePath))
                    {
                        File.Delete(backupFilePath);
                    }
                }

                LoadBackups();
                MessageBox.Query("Success", $"Pruned {backups.Count} backup(s).", "OK");
            }
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Prune Error", $"Failed to prune backups:\n{ex.Message}", "OK");
        }
    }

    private BackupMetadata? GetSelectedBackup()
    {
        if (_backupListView == null)
            return null;

        var fileName = GetSelectedFileName();
        if (string.IsNullOrEmpty(fileName))
            return null;

        var selectedIdx = _backupListView.SelectedItem;
        if (selectedIdx < 0 || !_backupsByFile.ContainsKey(fileName))
            return null;

        var backups = _backupsByFile[fileName];
        if (selectedIdx >= backups.Count)
            return null;

        return backups[selectedIdx];
    }

    private string? GetSelectedFileName()
    {
        if (_fileListView == null)
            return null;

        var selectedIdx = _fileListView.SelectedItem;
        if (selectedIdx < 0)
            return null;

        var sourceList = _fileListView.Source.ToList();
        if (selectedIdx >= sourceList.Count)
            return null;

        return sourceList[selectedIdx]?.ToString();
    }
}
