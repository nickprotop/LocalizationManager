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
/// Event Handlers
/// </summary>
public partial class ResourceEditorWindow : Window
{
    private void OnKeyPress(KeyEventEventArgs e)
    {
        if (e.KeyEvent.Key == (Key.Z | Key.CtrlMask))
        {
            Undo();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.Y | Key.CtrlMask))
        {
            Redo();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.N | Key.CtrlMask))
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
            // F3 is context-aware: if search matches exist, navigate to next match
            // Otherwise, perform Remove Language action
            if (_matchedRowIndices.Any())
            {
                NavigateToNextMatch();
            }
            else
            {
                RemoveLanguage();
            }
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.F3 | Key.ShiftMask))
        {
            // Shift+F3: Navigate to previous match
            if (_matchedRowIndices.Any())
            {
                NavigateToPreviousMatch();
            }
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
        else if (e.KeyEvent.Key == Key.F7)
        {
            PerformFullCodeScan();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.F7 | Key.ShiftMask))
        {
            ViewCodeReferencesForSelectedKey();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.F8)
        {
            ShowMergeDuplicatesDialog();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.C | Key.CtrlMask))
        {
            CopySelectedValueToClipboard();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.V | Key.CtrlMask))
        {
            PasteValueFromClipboard();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.A | Key.CtrlMask))
        {
            SelectAll();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.Esc && _selectedRowIndices.Any())
        {
            ClearSelection();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == Key.Space)
        {
            ToggleCurrentRowSelection();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.CursorUp | Key.ShiftMask))
        {
            ExtendSelectionUp();
            e.Handled = true;
        }
        else if (e.KeyEvent.Key == (Key.CursorDown | Key.ShiftMask))
        {
            ExtendSelectionDown();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Copies the selected row's default language value to clipboard
    /// </summary>
}
