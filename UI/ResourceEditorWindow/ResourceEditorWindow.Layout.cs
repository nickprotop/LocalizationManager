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
/// UI Layout and Component Initialization
/// </summary>
public partial class ResourceEditorWindow : Window
{
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
                new MenuItem("_Undo", "Undo last operation", () => Undo(), null, null, Key.Z | Key.CtrlMask),
                new MenuItem("_Redo", "Redo last operation", () => Redo(), null, null, Key.Y | Key.CtrlMask),
                null!, // separator
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
                new MenuItem("_Merge Duplicates", "Merge duplicate keys", () => ShowMergeDuplicatesDialog(), null, null, Key.F8),
                null!, // separator
                new MenuItem("Select _All", "Select all visible keys", () => SelectAll(), null, null, Key.A | Key.CtrlMask),
                new MenuItem("Clear Se_lection", "Clear all selections", () => ClearSelection(), null, null, Key.Esc),
                new MenuItem("_Bulk Translate", "Translate selected keys", () => BulkTranslate(), null, null, (Key)0),
                new MenuItem("Bulk D_elete", "Delete selected keys", () => BulkDelete(), null, null, (Key)0),
                null!, // separator
                new MenuItem("_Copy Value", "Copy selected value to clipboard", () => CopySelectedValueToClipboard(), null, null, Key.C | Key.CtrlMask),
                new MenuItem("_Paste Value", "Paste value from clipboard", () => PasteValueFromClipboard(), null, null, Key.V | Key.CtrlMask)
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
            new MenuBarItem("_Tools", new MenuItem[]
            {
                new MenuItem("_Scan Source Code", "Scan code for key usage", () => PerformFullCodeScan(), null, null, Key.F7),
                new MenuItem("_View Code References", "View code references for selected key", () => {
                    if (_tableView?.SelectedRow >= 0)
                    {
                        var entryRef = GetEntryReferenceFromSelectedRow(_tableView.SelectedRow);
                        if (entryRef != null)
                        {
                            ShowCodeReferences(entryRef.Key);
                        }
                    }
                    else
                    {
                        MessageBox.ErrorQuery("Error", "Please select a key first.", "OK");
                    }
                }, null, null, (Key)0),
                new MenuItem("Show _Duplicates", "Show duplicate keys with code references", () => ShowDuplicatesDialog(), null, null, Key.D | Key.CtrlMask)
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

        // Clear button (X) next to search field
        var clearButton = new Button
        {
            Text = "✕",
            X = Pos.Right(_searchField) + 1,
            Y = 2
        };
        clearButton.Clicked += () =>
        {
            _searchField.Text = string.Empty;
            _searchText = string.Empty;
            _currentMatchIndex = -1;
            _matchedRowIndices.Clear();
            ApplyFilters();
            UpdateMatchCounter();
        };

        // Match counter label (shows "X/Y matches")
        _matchCounterLabel = new Label
        {
            Text = "",
            X = Pos.Right(clearButton) + 1,
            Y = 2,
            Width = 15
        };

        // Case-sensitive checkbox (on same line as search)
        var caseSensitiveCheckBox = new CheckBox
        {
            Text = "Case-sensitive",
            X = Pos.Right(_matchCounterLabel) + 1,
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

        // Code scanning controls (row 4)
        var scanButton = new Button
        {
            Text = "Scan Code (F7)",
            X = 1,
            Y = 4
        };
        scanButton.Clicked += () => PerformFullCodeScan();

        _filterUnusedCheckBox = new CheckBox
        {
            Text = "Unused in code",
            X = Pos.Right(scanButton) + 2,
            Y = 4,
            Checked = false
        };
        _filterUnusedCheckBox.Toggled += (prev) =>
        {
            ApplyFilters();
        };

        _filterMissingFromResourcesCheckBox = new CheckBox
        {
            Text = "Missing from .resx",
            X = Pos.Right(_filterUnusedCheckBox) + 2,
            Y = 4,
            Checked = false
        };
        _filterMissingFromResourcesCheckBox.Toggled += (prev) =>
        {
            ApplyFilters();
        };

        // TableView for keys and translations (adjusted Y position for new controls)
        _tableView = new TableView
        {
            X = 1,
            Y = 5,  // Y=5 now (menu=0, search=2, languages=3, scan=4, table=5)
            Width = Dim.Fill() - 1,
            Height = Dim.Fill() - 2,
            FullRowSelect = true,
            MultiSelect = false,
            Table = CreateDisplayTable(_dataTable)
        };

        // Note: Terminal.Gui 1.19.0 doesn't support RowColorGetter
        // Color coding is implemented through status indicators in the Key column
        // (⚠ for missing, ⭐ for extra, ◆ for duplicates)

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

        // Add right-click context menu support
        _tableView.MouseClick += (args) =>
        {
            // Check if right-click (Button 3)
            if (args.MouseEvent.Flags.HasFlag(MouseFlags.Button3Clicked))
            {
                ShowTableContextMenu();
                args.Handled = true;
            }
        };

        // Status bar (upgraded from Label to StatusBar widget)
        _statusBar = new StatusBar(new StatusItem[] {
            new StatusItem(Key.Null, GetStatusText(), null)
        });

        Add(searchLabel, _searchField, clearButton, _matchCounterLabel, caseSensitiveCheckBox, scopeButton, _regexCheckBox);
        Add(langLabel);
        foreach (var checkbox in _languageCheckboxes)
        {
            Add(checkbox);
        }
        Add(moreButton, showCommentsCheckBox);
        Add(scanButton, _filterUnusedCheckBox, _filterMissingFromResourcesCheckBox);
        Add(_tableView);
        Add(_statusBar);

        // Keyboard shortcuts
        KeyPress += OnKeyPress;
    }

}
