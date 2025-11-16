// Copyright (c) 2025 Nikolaos Protopapas
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using LocalizationManager.Core.Backup;
using LocalizationManager.Core.Models;
using LocalizationManager.Shared.Models;
using Terminal.Gui;

namespace LocalizationManager.UI;

/// <summary>
/// Visual diff viewer window for comparing backup versions.
/// </summary>
public class BackupDiffWindow : Dialog
{
    private readonly BackupDiffResult _diff;
    private readonly BackupMetadata _backup;
    private ListView? _changeListView;
    private TextView? _detailsView;
    private Label? _statsLabel;

    public BackupDiffWindow(BackupDiffResult diff, BackupMetadata backup)
    {
        _diff = diff;
        _backup = backup;

        Title = $"Backup Diff - v{backup.Version:D3}";
        Width = Dim.Percent(90);
        Height = Dim.Percent(80);

        InitializeComponents();
        LoadDiff();
    }

    private void InitializeComponents()
    {
        // Header info
        var headerLabel = new Label(
            $"Comparing: Backup v{_backup.Version:D3} ({_backup.Timestamp.ToLocalTime():yyyy-MM-dd HH:mm:ss}) vs Current")
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill() - 1
        };

        // Stats label
        _statsLabel = new Label("Loading...")
        {
            X = 1,
            Y = 2,
            Width = Dim.Fill() - 1,
            ColorScheme = Colors.Dialog
        };

        // Filter buttons
        var btnAll = new Button("All")
        {
            X = 1,
            Y = 4
        };
        btnAll.Clicked += () => FilterChanges(null);

        var btnAdded = new Button("Added")
        {
            X = Pos.Right(btnAll) + 2,
            Y = 4
        };
        btnAdded.Clicked += () => FilterChanges("Added");

        var btnModified = new Button("Modified")
        {
            X = Pos.Right(btnAdded) + 2,
            Y = 4
        };
        btnModified.Clicked += () => FilterChanges("Modified");

        var btnRemoved = new Button("Removed")
        {
            X = Pos.Right(btnModified) + 2,
            Y = 4
        };
        btnRemoved.Clicked += () => FilterChanges("Removed");

        // Change list (top panel)
        var changeListLabel = new Label("Changes:")
        {
            X = 1,
            Y = 6
        };

        _changeListView = new ListView(new List<string>())
        {
            X = 1,
            Y = 7,
            Width = Dim.Fill() - 1,
            Height = Dim.Percent(50)
        };
        _changeListView.SelectedItemChanged += (args) => OnChangeSelected();

        // Details panel (bottom)
        var detailsLabel = new Label("Details:")
        {
            X = 1,
            Y = Pos.Bottom(_changeListView) + 1
        };

        _detailsView = new TextView
        {
            X = 1,
            Y = Pos.Bottom(detailsLabel),
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 4,
            ReadOnly = true,
            WordWrap = true
        };

        // Close button
        var btnClose = new Button("Close")
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(2)
        };
        btnClose.Clicked += () => Application.RequestStop();

        Add(headerLabel, _statsLabel, btnAll, btnAdded, btnModified, btnRemoved,
            changeListLabel, _changeListView, detailsLabel, _detailsView, btnClose);
    }

    private void LoadDiff()
    {
        FilterChanges(null);
    }

    private void FilterChanges(string? changeType)
    {
        if (_changeListView == null || _statsLabel == null)
            return;

        var changes = _diff.Changes.AsEnumerable();

        if (!string.IsNullOrEmpty(changeType))
        {
            changes = changes.Where(c => c.Type.ToString() == changeType);
        }

        var changeList = changes.Select(c =>
        {
            var typeStr = c.Type.ToString();
            var color = GetChangeColor(typeStr);
            var key = c.Key.Length > 35 ? c.Key.Substring(0, 32) + "..." : c.Key;
            var oldVal = c.OldValue != null && c.OldValue.Length > 25
                ? c.OldValue.Substring(0, 22) + "..."
                : c.OldValue ?? "";
            var newVal = c.NewValue != null && c.NewValue.Length > 25
                ? c.NewValue.Substring(0, 22) + "..."
                : c.NewValue ?? "";

            return $"{typeStr,-10} | {key,-35} | {oldVal,-25} | {newVal,-25}";
        }).ToList();

        if (!changeList.Any())
        {
            changeList.Add("(No changes)");
        }

        _changeListView.SetSource(changeList);

        // Update stats
        var stats = $"Added: {_diff.Statistics.AddedCount}  |  Modified: {_diff.Statistics.ModifiedCount}  |  " +
                   $"Deleted: {_diff.Statistics.DeletedCount}  |  Total: {_diff.Statistics.TotalChanges}";

        if (!string.IsNullOrEmpty(changeType))
        {
            stats += $"  |  Showing: {changeList.Count} {changeType}";
        }

        _statsLabel.Text = stats;
    }

    private string GetChangeColor(string changeType)
    {
        return changeType switch
        {
            "Added" => "green",
            "Modified" => "yellow",
            "Removed" => "red",
            _ => "white"
        };
    }

    private void OnChangeSelected()
    {
        if (_changeListView == null || _detailsView == null)
            return;

        var selectedIdx = _changeListView.SelectedItem;
        if (selectedIdx < 0 || selectedIdx >= _diff.Changes.Count)
        {
            _detailsView.Text = "";
            return;
        }

        var change = _diff.Changes[selectedIdx];

        var details = $"Change Type: {change.Type}\n\n" +
                     $"Key: {change.Key}\n\n";

        if (change.Type == LocalizationManager.Shared.Enums.ChangeType.Added)
        {
            details += $"New Value:\n{change.NewValue ?? "(empty)"}\n\n";

            if (!string.IsNullOrWhiteSpace(change.NewComment))
            {
                details += $"Comment:\n{change.NewComment}\n";
            }
        }
        else if (change.Type == LocalizationManager.Shared.Enums.ChangeType.Deleted)
        {
            details += $"Old Value:\n{change.OldValue ?? "(empty)"}\n\n";

            if (!string.IsNullOrWhiteSpace(change.OldComment))
            {
                details += $"Comment:\n{change.OldComment}\n";
            }
        }
        else if (change.Type == LocalizationManager.Shared.Enums.ChangeType.Modified)
        {
            details += "OLD VALUE:\n" +
                      "─────────────────────────────────────────\n" +
                      $"{change.OldValue ?? "(empty)"}\n\n" +
                      "NEW VALUE:\n" +
                      "─────────────────────────────────────────\n" +
                      $"{change.NewValue ?? "(empty)"}\n\n";

            if (change.OldComment != change.NewComment)
            {
                details += "COMMENT CHANGED:\n" +
                          "─────────────────────────────────────────\n" +
                          $"Old: {change.OldComment ?? "(none)"}\n" +
                          $"New: {change.NewComment ?? "(none)"}\n";
            }
            else if (!string.IsNullOrWhiteSpace(change.OldComment))
            {
                details += $"Comment: {change.OldComment}\n";
            }
        }

        _detailsView.Text = details;
    }
}
