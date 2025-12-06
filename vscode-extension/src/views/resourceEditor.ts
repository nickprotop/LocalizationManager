import * as vscode from 'vscode';
import * as path from 'path';
import { ApiClient } from '../backend/apiClient';
import { CacheService } from '../backend/cacheService';

export class ResourceEditorPanel {
    public static currentPanel: ResourceEditorPanel | undefined;
    private readonly _panel: vscode.WebviewPanel;
    private _disposables: vscode.Disposable[] = [];
    private apiClient: ApiClient;
    private cacheService: CacheService;

    public static createOrShow(extensionUri: vscode.Uri, apiClient: ApiClient, cacheService: CacheService, options?: { selectKey?: string; openTranslate?: boolean }) {
        const column = vscode.window.activeTextEditor
            ? vscode.window.activeTextEditor.viewColumn
            : undefined;

        // If we already have a panel, show it
        if (ResourceEditorPanel.currentPanel) {
            ResourceEditorPanel.currentPanel._panel.reveal(column);
            // If options provided, send message to select key
            if (options?.selectKey) {
                ResourceEditorPanel.currentPanel.selectKeyAndTranslate(options.selectKey, options.openTranslate);
            }
            return;
        }

        // Otherwise, create a new panel
        const panel = vscode.window.createWebviewPanel(
            'lrmResourceEditor',
            'LRM Resource Editor',
            column || vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: true,
                localResourceRoots: [vscode.Uri.joinPath(extensionUri, 'out')]
            }
        );

        ResourceEditorPanel.currentPanel = new ResourceEditorPanel(panel, extensionUri, apiClient, cacheService, options);
    }

    /**
     * Select a key in the editor and optionally open translate dialog
     */
    public selectKeyAndTranslate(keyName: string, openTranslate?: boolean) {
        this._panel.webview.postMessage({
            command: 'selectKeyAndTranslate',
            key: keyName,
            openTranslate: openTranslate ?? false
        });
    }

    private _pendingKeySelection?: { key: string; openTranslate: boolean };

    private constructor(panel: vscode.WebviewPanel, _extensionUri: vscode.Uri, apiClient: ApiClient, cacheService: CacheService, options?: { selectKey?: string; openTranslate?: boolean }) {
        this._panel = panel;
        this.apiClient = apiClient;
        this.cacheService = cacheService;

        // Store pending key selection to apply after resources load
        if (options?.selectKey) {
            this._pendingKeySelection = { key: options.selectKey, openTranslate: options.openTranslate ?? false };
        }

        // Set the webview's initial html content
        this._update();

        // Listen for when the panel is disposed
        this._panel.onDidDispose(() => this.dispose(), null, this._disposables);

        // Handle messages from the webview
        this._panel.webview.onDidReceiveMessage(
            async message => {
                switch (message.command) {
                    case 'loadResources':
                        await this.handleLoadResources();
                        return;
                    case 'updateKey':
                        // Support both inline edit (single language) and modal edit (all languages)
                        if (message.values && typeof message.values === 'object' && !message.language) {
                            // Modal edit - multiple languages in values object
                            await this.handleUpdateKeyMultiple(message.key, message.values);
                        } else {
                            // Inline edit - single language
                            await this.handleUpdateKey(message.key, message.language, message.value, message.comment);
                        }
                        return;
                    case 'addKey':
                        await this.handleAddKey(message.key, message.values);
                        return;
                    case 'deleteKey':
                        await this.handleDeleteKey(message.key);
                        return;
                    case 'getKeyDetails':
                        await this.handleGetKeyDetails(message.key);
                        return;
                    case 'translateKey':
                        await this.handleTranslateKey(message.key, message.provider, message.languages, message.onlyMissing);
                        return;
                    case 'translateAll':
                        await this.handleTranslateAll(message.provider, message.languages, message.onlyMissing);
                        return;
                    case 'getProviders':
                        await this.handleGetProviders();
                        return;
                    case 'getLanguages':
                        await this.handleGetLanguages();
                        return;
                    case 'scanCode':
                        await this.handleScanCode();
                        return;
                    case 'openFile':
                        await this.handleOpenFile(message.filePath, message.line);
                        return;
                    case 'search':
                        await this.handleSearch(message.pattern, message.scope);
                        return;
                    case 'searchEnhanced':
                        await this.handleSearchEnhanced(message.pattern, message.filterMode, message.caseSensitive, message.searchScope);
                        return;
                    case 'getKeyReferences':
                        await this.handleGetKeyReferences(message.key);
                        return;
                }
            },
            null,
            this._disposables
        );
    }

    private async handleLoadResources() {
        try {
            console.log('Loading resources from cache...');
            const keys = await this.cacheService.getKeys();
            console.log(`Loaded ${keys.length} keys from cache`);

            this._panel.webview.postMessage({
                command: 'resourcesLoaded',
                data: keys
            });

            // Check if we have cached scan results and send them to populate reference counts
            const cachedScanResults = this.cacheService.getCachedScanResults();
            if (cachedScanResults) {
                console.log('Sending cached scan results to webview');
                this._panel.webview.postMessage({
                    command: 'scanCodeComplete',
                    data: cachedScanResults
                });
            }

            // If there's a pending key selection, apply it now
            if (this._pendingKeySelection) {
                // Small delay to ensure webview has processed the resources
                setTimeout(() => {
                    if (this._pendingKeySelection) {
                        this.selectKeyAndTranslate(this._pendingKeySelection.key, this._pendingKeySelection.openTranslate);
                        this._pendingKeySelection = undefined;
                    }
                }, 100);
            }
        } catch (error: any) {
            console.error('Failed to load resources:', error);
            this._panel.webview.postMessage({
                command: 'error',
                message: `Failed to load resources: ${error.message}`
            });
        }
    }

    private async handleUpdateKey(key: string, language: string, value: string, comment?: string) {
        try {
            // Normalize empty string to "default" for the default language
            const langCode = language || 'default';
            console.log(`Updating key "${key}" for language "${langCode}" with value "${value}"`);
            await this.apiClient.updateKey(key, {
                values: {
                    [langCode]: { value, comment: comment ?? undefined }
                }
            });

            // Invalidate cache for this key
            this.cacheService.invalidateKey(key);

            this._panel.webview.postMessage({
                command: 'updateSuccess',
                key
            });

            // Reload resources to reflect changes
            await this.handleLoadResources();
        } catch (error: any) {
            console.error('Failed to update key:', error);
            this._panel.webview.postMessage({
                command: 'error',
                message: `Failed to update key: ${error.message}`
            });
        }
    }

    private async handleUpdateKeyMultiple(key: string, values: { [language: string]: { value: string; comment?: string | null } }) {
        try {
            console.log(`Updating key "${key}" for multiple languages:`, Object.keys(values));
            // Convert to ResourceValueUpdate format for backend API
            // Normalize empty string to "default" for the default language
            const resourceValues: { [language: string]: { value: string; comment?: string } } = {};
            for (const [lang, data] of Object.entries(values)) {
                const langCode = lang || 'default';
                resourceValues[langCode] = {
                    value: data.value,
                    comment: data.comment ?? undefined
                };
            }
            await this.apiClient.updateKey(key, { values: resourceValues });

            // Invalidate cache for this key
            this.cacheService.invalidateKey(key);

            this._panel.webview.postMessage({
                command: 'updateSuccess',
                key
            });

            // Reload resources to reflect changes
            await this.handleLoadResources();
        } catch (error: any) {
            console.error('Failed to update key (multiple):', error);
            this._panel.webview.postMessage({
                command: 'error',
                message: `Failed to update key: ${error.message}`
            });
        }
    }

    private async handleAddKey(key: string, values: { [language: string]: string }) {
        try {
            await this.apiClient.addKey({ key, values });

            // Invalidate entire cache since new key affects key list
            this.cacheService.invalidate();

            this._panel.webview.postMessage({
                command: 'addSuccess',
                key
            });

            await this.handleLoadResources();
        } catch (error: any) {
            this._panel.webview.postMessage({
                command: 'error',
                message: `Failed to add key: ${error.message}`
            });
        }
    }

    private async handleDeleteKey(key: string) {
        try {
            await this.apiClient.deleteKey(key);

            // Invalidate entire cache since deleted key affects key list
            this.cacheService.invalidate();

            this._panel.webview.postMessage({
                command: 'deleteSuccess',
                key
            });

            await this.handleLoadResources();
        } catch (error: any) {
            this._panel.webview.postMessage({
                command: 'error',
                message: `Failed to delete key: ${error.message}`
            });
        }
    }

    private async handleTranslateKey(key: string, provider: string, languages: string[], onlyMissing: boolean = true, dryRun: boolean = true) {
        try {
            console.log(`Translating key: ${key}, provider: ${provider}, languages: ${languages.join(', ')}, dryRun: ${dryRun}`);

            const result = await this.apiClient.translate({
                keys: [key],
                provider,
                targetLanguages: languages,
                onlyMissing,
                dryRun  // Don't save directly - populate dialog fields instead
            });

            console.log('Translation result:', result);

            // Transform backend response (Results array) to frontend format (translations object)
            const translations: { [language: string]: string } = {};
            if (result.results) {
                result.results.forEach((r: any) => {
                    if (r.success && r.translatedValue) {
                        translations[r.language] = r.translatedValue;
                    }
                });
            }

            // Send translation results back to WebView (don't save yet)
            this._panel.webview.postMessage({
                command: 'singleKeyTranslated',
                data: translations
            });
        } catch (error: any) {
            console.error('Translation error:', error);
            this._panel.webview.postMessage({
                command: 'error',
                message: `Translation failed: ${error.message || error}`
            });
        }
    }

    private async handleGetKeyDetails(keyName: string) {
        try {
            const keyDetails = await this.cacheService.getKeyDetails(keyName);
            this._panel.webview.postMessage({
                command: 'keyDetailsLoaded',
                data: keyDetails
            });
        } catch (error: any) {
            this._panel.webview.postMessage({
                command: 'error',
                message: `Failed to load key details: ${error.message}`
            });
        }
    }

    private async handleTranslateAll(provider: string, languages: string[], onlyMissing: boolean = true) {
        try {
            // Get all keys first (use cache)
            const keys = await this.cacheService.getKeys();

            // Filter keys based on onlyMissing
            const keysToTranslate = onlyMissing
                ? keys.filter(key => languages.some(lang => !key.values[lang] || key.values[lang].trim() === ''))
                : keys;

            const total = keysToTranslate.length;
            let completed = 0;

            // Send initial progress
            this._panel.webview.postMessage({
                command: 'translationProgress',
                data: { completed: 0, total, currentKey: null }
            });

            // Translate each key
            for (const key of keysToTranslate) {
                try {
                    // Filter languages based on onlyMissing for this specific key
                    const targetLangs = onlyMissing
                        ? languages.filter(lang => !key.values[lang] || key.values[lang].trim() === '')
                        : languages;

                    if (targetLangs.length > 0) {
                        await this.apiClient.translate({
                            keys: [key.key],
                            provider,
                            targetLanguages: targetLangs,
                            onlyMissing,
                            dryRun: false  // Actually save translations when translating all
                        });
                    }

                    completed++;

                    // Send progress update
                    this._panel.webview.postMessage({
                        command: 'translationProgress',
                        data: { completed, total, currentKey: key.key }
                    });
                } catch (error: any) {
                    console.error(`Failed to translate key ${key.key}:`, error);
                    // Continue with other keys even if one fails
                }
            }

            // Invalidate entire cache after batch translation
            this.cacheService.invalidate();

            this._panel.webview.postMessage({
                command: 'translateAllSuccess'
            });

            await this.handleLoadResources();
        } catch (error: any) {
            this._panel.webview.postMessage({
                command: 'error',
                message: `Batch translation failed: ${error.message}`
            });
        }
    }

    private async handleGetProviders() {
        try {
            const providers = await this.apiClient.getTranslationProviders();
            this._panel.webview.postMessage({
                command: 'providersLoaded',
                data: providers
            });
        } catch (error: any) {
            this._panel.webview.postMessage({
                command: 'error',
                message: error.message
            });
        }
    }

    private async handleGetLanguages() {
        try {
            console.log('Fetching languages from API...');
            const languages = await this.apiClient.getLanguages();
            console.log(`Loaded ${languages.length} languages:`, languages);
            this._panel.webview.postMessage({
                command: 'languagesLoaded',
                data: languages
            });
        } catch (error: any) {
            console.error('Failed to load languages:', error);
            this._panel.webview.postMessage({
                command: 'error',
                message: error.message
            });
        }
    }

    private async handleScanCode() {
        try {
            console.log('Starting code scan (using cache)...');
            const scanResult = await this.cacheService.getScanResults();
            console.log('Scan complete (from cache):', scanResult);

            this._panel.webview.postMessage({
                command: 'scanCodeComplete',
                data: scanResult
            });
        } catch (error: any) {
            console.error('Code scan failed:', error);
            this._panel.webview.postMessage({
                command: 'error',
                message: `Code scan failed: ${error.message}`
            });
        }
    }

    private async handleOpenFile(filePath: string, line: number) {
        try {
            // Resolve the file path relative to workspace
            const workspaceFolders = vscode.workspace.workspaceFolders;
            if (!workspaceFolders || workspaceFolders.length === 0) {
                throw new Error('No workspace folder open');
            }

            // If filePath is relative, resolve it against workspace root
            let fullPath = filePath;
            if (!path.isAbsolute(filePath)) {
                fullPath = path.join(workspaceFolders[0].uri.fsPath, filePath);
            }

            const document = await vscode.workspace.openTextDocument(fullPath);
            const editor = await vscode.window.showTextDocument(document);

            // Navigate to the specified line
            const position = new vscode.Position(line - 1, 0); // line is 1-indexed, Position is 0-indexed
            editor.selection = new vscode.Selection(position, position);
            editor.revealRange(new vscode.Range(position, position), vscode.TextEditorRevealType.InCenter);
        } catch (error: any) {
            console.error('Failed to open file:', error);
            vscode.window.showErrorMessage(`Failed to open file: ${error.message}`);
        }
    }

    private async handleSearch(pattern: string, scope: string) {
        try {
            const result = await this.apiClient.search({
                pattern,
                filterMode: 'substring',
                searchScope: scope as any
            });

            this._panel.webview.postMessage({
                command: 'searchResults',
                data: result.results
            });
        } catch (error: any) {
            this._panel.webview.postMessage({
                command: 'error',
                message: error.message
            });
        }
    }

    private async handleSearchEnhanced(pattern: string, filterMode: string, caseSensitive: boolean, searchScope: string) {
        try {
            const result = await this.apiClient.search({
                pattern,
                filterMode: filterMode as any,
                caseSensitive: caseSensitive,
                searchScope: searchScope as any
            });

            this._panel.webview.postMessage({
                command: 'searchResults',
                data: result.results
            });
        } catch (error: any) {
            this._panel.webview.postMessage({
                command: 'error',
                message: error.message
            });
        }
    }

    private async handleGetKeyReferences(keyName: string) {
        try {
            const keyUsage = await this.cacheService.getKeyReferences(keyName);
            this._panel.webview.postMessage({
                command: 'keyReferencesLoaded',
                data: keyUsage
            });
        } catch (error: any) {
            this._panel.webview.postMessage({
                command: 'error',
                message: `Failed to load references: ${error.message}`
            });
        }
    }

    public dispose() {
        ResourceEditorPanel.currentPanel = undefined;

        this._panel.dispose();

        while (this._disposables.length) {
            const disposable = this._disposables.pop();
            if (disposable) {
                disposable.dispose();
            }
        }
    }

    private _update() {
        this._panel.title = 'LRM Resource Editor';
        this._panel.webview.html = this._getHtmlForWebview();
    }

    private _getHtmlForWebview() {
        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>LRM Resource Editor</title>
    <style>
        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }

        body {
            font-family: var(--vscode-font-family);
            font-size: var(--vscode-font-size);
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
            padding: 20px;
        }

        .header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            padding-bottom: 10px;
            border-bottom: 1px solid var(--vscode-panel-border);
        }

        .toolbar {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
            margin-bottom: 20px;
        }

        button {
            background-color: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border: none;
            padding: 6px 14px;
            cursor: pointer;
            border-radius: 2px;
            font-size: 13px;
        }

        button:hover {
            background-color: var(--vscode-button-hoverBackground);
        }

        button.secondary {
            background-color: var(--vscode-button-secondaryBackground);
            color: var(--vscode-button-secondaryForeground);
        }

        button.secondary:hover {
            background-color: var(--vscode-button-secondaryHoverBackground);
        }

        input[type="text"], input[type="search"], select {
            background-color: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            border: 1px solid var(--vscode-input-border);
            padding: 6px 8px;
            font-size: 13px;
            border-radius: 2px;
        }

        input[type="text"]:focus, input[type="search"]:focus, select:focus {
            outline: 1px solid var(--vscode-focusBorder);
        }

        .search-box {
            flex: 1;
            max-width: 400px;
        }

        table {
            width: 100%;
            border-collapse: collapse;
            background-color: var(--vscode-editor-background);
        }

        th {
            background-color: var(--vscode-editor-background);
            color: var(--vscode-foreground);
            text-align: left;
            padding: 8px;
            border-bottom: 1px solid var(--vscode-panel-border);
            font-weight: 600;
            position: sticky;
            top: 0;
        }

        td {
            padding: 8px;
            border-bottom: 1px solid var(--vscode-panel-border);
        }

        tr:hover {
            background-color: var(--vscode-list-hoverBackground);
        }

        .editable-cell {
            cursor: text;
            min-height: 20px;
        }

        .editable-cell:hover {
            background-color: var(--vscode-list-hoverBackground);
        }

        .editable-cell:focus {
            outline: 1px solid var(--vscode-focusBorder);
            background-color: var(--vscode-input-background);
        }

        .key-column {
            font-family: var(--vscode-editor-font-family);
            font-weight: 500;
        }

        .empty-value {
            color: var(--vscode-errorForeground);
            font-style: italic;
        }

        .status-bar {
            position: fixed;
            bottom: 0;
            left: 0;
            right: 0;
            background-color: var(--vscode-statusBar-background);
            color: var(--vscode-statusBar-foreground);
            padding: 4px 20px;
            font-size: 12px;
            display: flex;
            justify-content: space-between;
        }

        .loading {
            text-align: center;
            padding: 40px;
            color: var(--vscode-descriptionForeground);
        }

        .modal {
            display: none;
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background-color: rgba(0, 0, 0, 0.5);
            z-index: 1000;
        }

        .modal-content {
            background-color: var(--vscode-editor-background);
            margin: 10% auto;
            padding: 20px;
            border: 1px solid var(--vscode-panel-border);
            width: 80%;
            max-width: 600px;
            border-radius: 4px;
        }

        .modal-header {
            margin-bottom: 20px;
            font-size: 16px;
            font-weight: 600;
        }

        .modal-body {
            margin-bottom: 20px;
        }

        .modal-footer {
            display: flex;
            justify-content: flex-end;
            gap: 10px;
        }

        .form-group {
            margin-bottom: 15px;
        }

        .form-group label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
        }

        .form-group input, .form-group select {
            width: 100%;
        }

        .actions {
            display: flex;
            gap: 5px;
        }

        .icon-button {
            background: none;
            border: none;
            cursor: pointer;
            padding: 4px;
            color: var(--vscode-foreground);
            opacity: 0.7;
        }

        .icon-button:hover {
            opacity: 1;
            background-color: var(--vscode-list-hoverBackground);
        }
    </style>
</head>
<body>
    <div class="header">
        <h1>Resource Editor</h1>
        <div>
            <span id="stats"></span>
        </div>
    </div>

    <div class="toolbar">
        <div style="display: flex; gap: 8px; flex: 1; align-items: center;">
            <input type="search" id="searchInput" class="search-box" placeholder="Search keys and values..." style="flex: 1; min-width: 200px;">
            <select id="searchMode" style="padding: 6px; background: var(--vscode-dropdown-background); color: var(--vscode-dropdown-foreground); border: 1px solid var(--vscode-dropdown-border);" onchange="performSearch()">
                <option value="substring">Substring</option>
                <option value="wildcard">Wildcard</option>
                <option value="regex">Regex</option>
            </select>
            <label style="display: flex; align-items: center; gap: 4px; white-space: nowrap;">
                <input type="checkbox" id="caseSensitive" onchange="performSearch()">
                <span>Case</span>
            </label>
            <select id="searchScope" style="padding: 6px; background: var(--vscode-dropdown-background); color: var(--vscode-dropdown-foreground); border: 1px solid var(--vscode-dropdown-border);" onchange="performSearch()">
                <option value="keysAndValues">Keys & Values</option>
                <option value="keys">Keys Only</option>
                <option value="values">Values Only</option>
                <option value="comments">Comments</option>
                <option value="all">All</option>
            </select>
        </div>
        <button onclick="addNewKey()">Add Key</button>
        <button onclick="showTranslateModal()" class="secondary">Translate All Missing</button>
        <button onclick="scanCode()" class="secondary">Scan Code</button>
        <button onclick="loadResources()" class="secondary">Refresh</button>
    </div>

    <div id="loading" class="loading">
        Loading resources...
    </div>

    <div id="tableContainer" style="display: none;">
        <table id="resourceTable">
            <thead>
                <tr id="tableHeader">
                    <th>Key</th>
                </tr>
            </thead>
            <tbody id="tableBody">
            </tbody>
        </table>
    </div>

    <div class="status-bar">
        <span id="statusText">Ready</span>
        <span id="statusStats"></span>
    </div>

    <!-- Add Key Modal -->
    <div id="addKeyModal" class="modal">
        <div class="modal-content">
            <div class="modal-header">Add New Key</div>
            <div class="modal-body">
                <div class="form-group">
                    <label for="newKeyName">Key Name:</label>
                    <input type="text" id="newKeyName" placeholder="MyNewKey">
                </div>
                <div class="form-group">
                    <label for="newKeyValue">Default Value:</label>
                    <input type="text" id="newKeyValue" placeholder="Default text">
                </div>
            </div>
            <div class="modal-footer">
                <button class="secondary" onclick="closeAddKeyModal()">Cancel</button>
                <button onclick="submitNewKey()">Add</button>
            </div>
        </div>
    </div>

    <!-- Translate Modal -->
    <div id="translateModal" class="modal">
        <div class="modal-content" style="max-width: 600px;">
            <div class="modal-header">Translate Keys</div>
            <div class="modal-body">
                <div id="translateContext" style="display: none; margin-bottom: 15px; padding: 10px; background: var(--vscode-editor-background); border-radius: 4px;">
                    <div style="color: var(--vscode-foreground); margin-bottom: 5px;"><strong>Key:</strong> <span id="translateKeyName"></span></div>
                    <div style="color: var(--vscode-foreground); margin-bottom: 5px;"><strong>Source Text:</strong> <span id="translateSourceText"></span></div>
                    <div id="translateComment" style="display: none; color: var(--vscode-editorWarning-foreground);"><strong>Comment:</strong> <span id="translateCommentText"></span></div>
                </div>

                <div class="form-group">
                    <label for="translateProvider">Translation Provider:</label>
                    <select id="translateProvider" style="width: 100%; padding: 6px; background: var(--vscode-dropdown-background); color: var(--vscode-dropdown-foreground); border: 1px solid var(--vscode-dropdown-border);"></select>
                    <div id="providerWarning" style="display: none; margin-top: 5px; padding: 8px; background: var(--vscode-inputValidation-warningBackground); border-left: 3px solid var(--vscode-editorWarning-foreground); font-size: 12px;">
                        <div id="providerWarningText"></div>
                    </div>
                </div>

                <div class="form-group">
                    <label>Target Languages:</label>
                    <div id="languageCheckboxes" style="max-height: 150px; overflow-y: auto; padding: 5px;"></div>
                </div>

                <div class="form-group">
                    <label style="display: flex; align-items: center; cursor: pointer;">
                        <input type="checkbox" id="onlyMissingCheckbox" style="margin-right: 8px;">
                        <span>Only translate missing values</span>
                    </label>
                    <div style="font-size: 12px; opacity: 0.7; margin-top: 5px; margin-left: 24px;">
                        Skip keys that already have translations in target languages
                    </div>
                </div>

                <div id="translateProgress" style="display: none; margin-top: 20px;">
                    <div id="translateStatus" style="margin-bottom: 8px; font-size: 13px;">Translating...</div>
                    <div style="width: 100%; background: var(--vscode-progressBar-background); height: 20px; border-radius: 3px; overflow: hidden;">
                        <div id="translateProgressBar" style="width: 0%; height: 100%; background: var(--vscode-progressBar-background); transition: width 0.3s;"></div>
                    </div>
                    <div id="translateProgressText" style="margin-top: 5px; font-size: 12px; opacity: 0.8;">0 / 0</div>
                </div>
            </div>
            <div class="modal-footer">
                <button class="secondary" onclick="closeTranslateModal()" id="translateCancelBtn">Cancel</button>
                <button onclick="submitTranslateAll()" id="translateBtn">Translate</button>
            </div>
        </div>
    </div>

    <!-- Edit Key Modal -->
    <div id="editKeyModal" class="modal">
        <div class="modal-content" style="max-width: 700px;">
            <div class="modal-header">
                <span>Edit Key: <span id="editKeyName"></span></span>
            </div>
            <div class="modal-body">
                <div id="editKeyFields" style="max-height: 400px; overflow-y: auto;">
                    <!-- Language fields will be populated here -->
                </div>
            </div>
            <div class="modal-footer">
                <button class="secondary" onclick="closeEditKeyModal()">Cancel</button>
                <button onclick="autoTranslateKey()" class="secondary">Auto-Translate</button>
                <button onclick="saveEditedKey()" id="saveKeyBtn">Save</button>
            </div>
        </div>
    </div>

    <!-- Code Scan Modal -->
    <div id="scanModal" class="modal">
        <div class="modal-content" style="max-width: 900px;">
            <div class="modal-header">Code Scan Results</div>
            <div class="modal-body">
                <div id="scanLoading" style="display: none; text-align: center; padding: 20px;">
                    <div>Scanning codebase...</div>
                </div>
                <div id="scanResults" style="display: none;">
                    <div class="scan-stats" style="display: grid; grid-template-columns: repeat(auto-fit, minmax(150px, 1fr)); gap: 10px; margin-bottom: 20px;">
                        <div style="background: var(--vscode-editor-background); padding: 10px; border-radius: 4px;">
                            <div style="font-size: 24px; font-weight: bold;" id="scanFilesCount">0</div>
                            <div style="font-size: 12px; opacity: 0.7;">Files Scanned</div>
                        </div>
                        <div style="background: var(--vscode-editor-background); padding: 10px; border-radius: 4px;">
                            <div style="font-size: 24px; font-weight: bold;" id="scanRefsCount">0</div>
                            <div style="font-size: 12px; opacity: 0.7;">References Found</div>
                        </div>
                        <div style="background: var(--vscode-editor-background); padding: 10px; border-radius: 4px;">
                            <div style="font-size: 24px; font-weight: bold;" id="scanKeysCount">0</div>
                            <div style="font-size: 12px; opacity: 0.7;">Unique Keys</div>
                        </div>
                        <div style="background: var(--vscode-editor-background); padding: 10px; border-radius: 4px;">
                            <div style="font-size: 24px; font-weight: bold; color: var(--vscode-editorWarning-foreground);" id="scanMissingCount">0</div>
                            <div style="font-size: 12px; opacity: 0.7;">Missing Keys</div>
                        </div>
                        <div style="background: var(--vscode-editor-background); padding: 10px; border-radius: 4px;">
                            <div style="font-size: 24px; font-weight: bold; color: var(--vscode-editorInfo-foreground);" id="scanUnusedCount">0</div>
                            <div style="font-size: 12px; opacity: 0.7;">Unused Keys</div>
                        </div>
                    </div>

                    <div id="scanSections" style="max-height: 400px; overflow-y: auto;">
                        <!-- Missing Keys Section -->
                        <div id="missingSection" style="display: none; margin-bottom: 20px;">
                            <h3 style="margin: 10px 0; color: var(--vscode-editorWarning-foreground);">Missing Keys</h3>
                            <div id="missingKeysList" style="background: var(--vscode-editor-background); padding: 10px; border-radius: 4px; max-height: 200px; overflow-y: auto;"></div>
                        </div>

                        <!-- Unused Keys Section -->
                        <div id="unusedSection" style="display: none; margin-bottom: 20px;">
                            <h3 style="margin: 10px 0; color: var(--vscode-editorInfo-foreground);">Unused Keys</h3>
                            <div id="unusedKeysList" style="background: var(--vscode-editor-background); padding: 10px; border-radius: 4px; max-height: 200px; overflow-y: auto;"></div>
                        </div>

                        <!-- Key References Section -->
                        <div id="referencesSection" style="display: none;">
                            <h3 style="margin: 10px 0;">Key References</h3>
                            <input type="search" id="refSearchInput" placeholder="Filter references..." style="width: 100%; padding: 6px; margin-bottom: 10px; background: var(--vscode-input-background); color: var(--vscode-input-foreground); border: 1px solid var(--vscode-input-border);">
                            <div id="referencesList" style="background: var(--vscode-editor-background); padding: 10px; border-radius: 4px; max-height: 300px; overflow-y: auto;"></div>
                        </div>
                    </div>
                </div>
            </div>
            <div class="modal-footer">
                <button onclick="closeScanModal()">Close</button>
            </div>
        </div>
    </div>

    <!-- Key References Modal -->
    <div id="keyRefsModal" class="modal">
        <div class="modal-content" style="max-width: 700px;">
            <div class="modal-header">
                <span>References for: <span id="keyRefsTitle"></span></span>
            </div>
            <div class="modal-body">
                <div id="keyRefsLoading" style="display: none; text-align: center; padding: 20px;">
                    <div>Loading references...</div>
                </div>
                <div id="keyRefsContent" style="display: none;">
                    <div style="margin-bottom: 15px; padding: 10px; background: var(--vscode-editor-background); border-radius: 4px;">
                        <div style="font-weight: bold;"><span id="keyRefsCount">0</span> references found</div>
                    </div>
                    <div id="keyRefsList" style="max-height: 400px; overflow-y: auto;"></div>
                </div>
                <div id="keyRefsEmpty" style="display: none; text-align: center; padding: 40px; opacity: 0.7;">
                    No references found for this key.
                    <div style="margin-top: 10px; font-size: 12px;">
                        Run "Scan Code" to find references in your codebase.
                    </div>
                </div>
            </div>
            <div class="modal-footer">
                <button onclick="closeKeyRefsModal()">Close</button>
            </div>
        </div>
    </div>

    <script>
        const vscode = acquireVsCodeApi();
        let resources = [];
        let allResources = [];  // Store unfiltered list for client-side filtering
        let languages = [];
        let providers = [];
        let scanResultsCache = null;

        // Load initial data
        window.addEventListener('load', () => {
            loadResources();
            loadProviders();
            loadLanguages();
        });

        // Handle messages from extension
        window.addEventListener('message', event => {
            const message = event.data;

            switch (message.command) {
                case 'resourcesLoaded':
                    console.log('Received resourcesLoaded:', message.data?.length || 0, 'keys');
                    allResources = Array.isArray(message.data) ? message.data : [];
                    resources = allResources;
                    console.log('Resources array:', resources.length);
                    renderTable();
                    updateStats();
                    break;
                case 'languagesLoaded':
                    console.log('Received languagesLoaded:', message.data);
                    languages = Array.isArray(message.data) ? message.data : [];
                    console.log('Languages array:', languages.length, languages);
                    renderTable();
                    break;
                case 'providersLoaded':
                    console.log('Received providersLoaded:', message.data);
                    providers = Array.isArray(message.data) ? message.data : [];
                    renderProviderOptions();
                    break;
                case 'updateSuccess':
                case 'addSuccess':
                case 'deleteSuccess':
                case 'translateSuccess':
                case 'translateAllSuccess':
                    closeTranslateModal();
                    setStatus('Translation complete!', 3000);
                    break;
                case 'searchResults':
                    resources = Array.isArray(message.data) ? message.data : [];
                    renderTable();
                    updateStats();
                    break;
                case 'scanCodeComplete':
                    console.log('Received scan results:', message.data);
                    displayScanResults(message.data);
                    updateRefsCells(message.data);
                    setStatus('Code scan complete!', 2000);
                    break;
                case 'keyReferencesLoaded':
                    console.log('Received key references:', message.data);
                    displayKeyReferences(message.data);
                    break;
                case 'translationProgress':
                    updateTranslationProgress(message.data);
                    break;
                case 'keyDetailsLoaded':
                    showEditKeyModal(message.data);
                    setStatus('Ready');
                    break;
                case 'singleKeyTranslated':
                    // Populate translation results back to edit modal
                    populateTranslationsToEditModal(message.data);
                    closeTranslateModal();
                    setStatus('Translation complete! Review and save changes.');
                    break;
                case 'selectKeyAndTranslate':
                    // Select and scroll to key, open edit dialog
                    if (message.key) {
                        // Set search to find the key
                        const searchInput = document.getElementById('searchInput');
                        if (searchInput) {
                            searchInput.value = message.key;
                        }
                        // Trigger search to filter to this key
                        filterResources();
                        // Open the edit modal for the key
                        setTimeout(() => {
                            translateKey(message.key);
                        }, 200);
                    }
                    break;
                case 'error':
                    setStatus('Error: ' + message.message, 5000);
                    break;
            }
        });

        function loadResources() {
            setStatus('Loading...');
            vscode.postMessage({ command: 'loadResources' });
        }

        function loadLanguages() {
            vscode.postMessage({ command: 'getLanguages' });
        }

        function loadProviders() {
            vscode.postMessage({ command: 'getProviders' });
        }

        function renderTable() {
            console.log('renderTable called - resources:', resources?.length, 'languages:', languages?.length);
            console.log('Resources isArray:', Array.isArray(resources), 'Languages isArray:', Array.isArray(languages));

            if (!Array.isArray(resources) || !Array.isArray(languages) || resources.length === 0 || languages.length === 0) {
                console.log('Skipping render - missing data');
                return;
            }

            console.log('Rendering table...');
            document.getElementById('loading').style.display = 'none';
            document.getElementById('tableContainer').style.display = 'block';

            // Render header
            const header = document.getElementById('tableHeader');
            header.innerHTML = '<th>Key</th><th style="text-align: center; width: 60px;" title="Code References">Refs</th><th style="text-align: center; width: 80px;">Status</th>';
            languages.forEach(lang => {
                header.innerHTML += '<th>' + lang.code + '</th>';
            });
            header.innerHTML += '<th>Actions</th>';

            // Render body
            const tbody = document.getElementById('tableBody');
            tbody.innerHTML = '';

            resources.forEach(resource => {
                const row = document.createElement('tr');

                // Key column
                const keyCell = document.createElement('td');
                keyCell.className = 'key-column';
                keyCell.textContent = resource.key;
                row.appendChild(keyCell);

                // Refs column
                const refsCell = document.createElement('td');
                refsCell.style.textAlign = 'center';
                refsCell.style.cursor = 'pointer';
                refsCell.style.color = 'var(--vscode-textLink-foreground)';
                refsCell.dataset.key = resource.key;
                refsCell.textContent = '-';
                refsCell.title = 'Click to view references';
                refsCell.onclick = () => showKeyReferences(resource.key);
                row.appendChild(refsCell);

                // Status column
                const statusCell = document.createElement('td');
                statusCell.style.textAlign = 'center';
                statusCell.dataset.key = resource.key;

                const statuses = [];

                // Check for plural key
                if (resource.isPlural) {
                    statuses.push({ icon: '🔢', title: 'Plural key', color: 'var(--vscode-charts-purple)' });
                }

                // Check for missing translations
                const hasMissing = languages.some(lang => !resource.values[lang.code] || resource.values[lang.code].trim() === '');
                if (hasMissing) {
                    statuses.push({ icon: '⚠️', title: 'Missing translations', color: 'var(--vscode-editorWarning-foreground)' });
                }

                // Check for duplicates
                if (resource.hasDuplicates) {
                    statuses.push({ icon: '⚡', title: 'Duplicate key', color: 'var(--vscode-editorError-foreground)' });
                }

                if (statuses.length > 0) {
                    statusCell.innerHTML = statuses.map(s =>
                        \`<span style="color: \${s.color};" title="\${s.title}">\${s.icon}</span>\`
                    ).join(' ');
                } else {
                    statusCell.textContent = '✓';
                    statusCell.style.color = 'var(--vscode-testing-iconPassed)';
                    statusCell.title = 'OK';
                }

                row.appendChild(statusCell);

                // Language columns
                languages.forEach(lang => {
                    const cell = document.createElement('td');
                    const value = resource.values[lang.code] || '';

                    cell.className = 'editable-cell';
                    if (!value) {
                        cell.classList.add('empty-value');
                        cell.textContent = '(empty)';
                    } else {
                        cell.textContent = value;
                    }

                    cell.contentEditable = 'true';
                    cell.dataset.key = resource.key;
                    cell.dataset.language = lang.code;
                    cell.dataset.originalValue = value;

                    // Clear "(empty)" placeholder on focus
                    cell.addEventListener('focus', (e) => {
                        const target = e.target;
                        if (target.textContent === '(empty)') {
                            target.textContent = '';
                            target.classList.remove('empty-value');
                        }
                    });

                    cell.addEventListener('blur', handleCellEdit);
                    cell.addEventListener('keydown', (e) => {
                        if (e.key === 'Enter') {
                            e.preventDefault();
                            e.target.blur();
                        }
                        if (e.key === 'Escape') {
                            const originalValue = e.target.dataset.originalValue;
                            if (!originalValue) {
                                e.target.textContent = '(empty)';
                                e.target.classList.add('empty-value');
                            } else {
                                e.target.textContent = originalValue;
                                e.target.classList.remove('empty-value');
                            }
                            e.target.blur();
                        }
                    });

                    row.appendChild(cell);
                });

                // Actions column
                const actionsCell = document.createElement('td');
                actionsCell.className = 'actions';
                actionsCell.innerHTML = \`
                    <button class="icon-button" onclick="translateKey('\${resource.key}')" title="Translate">
                        🌐
                    </button>
                    <button class="icon-button" onclick="deleteKey('\${resource.key}')" title="Delete">
                        🗑️
                    </button>
                \`;
                row.appendChild(actionsCell);

                tbody.appendChild(row);
            });

            // Reapply scan results if available
            if (scanResultsCache) {
                updateRefsCells(scanResultsCache);
            }

            setStatus('Ready');
        }

        function handleCellEdit(e) {
            const cell = e.target;
            const key = cell.dataset.key;
            const language = cell.dataset.language;
            const originalValue = cell.dataset.originalValue || '';
            let newValue = cell.textContent.trim();

            if (newValue === '(empty)') {
                newValue = '';
            }

            // Restore "(empty)" display if the value is empty
            if (!newValue) {
                cell.textContent = '(empty)';
                cell.classList.add('empty-value');
            } else {
                cell.classList.remove('empty-value');
            }

            if (newValue !== originalValue) {
                setStatus('Saving...');
                console.log('Inline edit - saving key:', key, 'lang:', language, 'value:', newValue);
                vscode.postMessage({
                    command: 'updateKey',
                    key: key,
                    language: language,
                    value: newValue
                });
                cell.dataset.originalValue = newValue;
            }
        }

        function addNewKey() {
            document.getElementById('addKeyModal').style.display = 'block';
            document.getElementById('newKeyName').focus();
        }

        function closeAddKeyModal() {
            document.getElementById('addKeyModal').style.display = 'none';
            document.getElementById('newKeyName').value = '';
            document.getElementById('newKeyValue').value = '';
        }

        function submitNewKey() {
            const keyName = document.getElementById('newKeyName').value.trim();
            const keyValue = document.getElementById('newKeyValue').value.trim();

            if (!keyName) {
                setStatus('Key name is required', 3000);
                return;
            }

            vscode.postMessage({
                command: 'addKey',
                key: keyName,
                values: { default: keyValue }
            });

            closeAddKeyModal();
        }

        function deleteKey(key) {
            if (confirm('Delete key "' + key + '"?')) {
                vscode.postMessage({
                    command: 'deleteKey',
                    key: key
                });
            }
        }

        let currentEditingKey = null;

        function translateKey(key) {
            // Open edit modal for this key
            currentEditingKey = key;

            // Fetch full key details including comments
            vscode.postMessage({
                command: 'getKeyDetails',
                key: key
            });

            setStatus('Loading key details...');
        }

        let currentKeyIsPlural = false;

        function showEditKeyModal(keyData) {
            currentEditingKey = keyData.key;
            document.getElementById('editKeyName').textContent = keyData.key;

            // Check if any language has plural forms - if so, this is a plural key
            currentKeyIsPlural = Object.values(keyData.values).some((v) => v && v.isPlural);

            // Build fields for each language
            const fieldsContainer = document.getElementById('editKeyFields');
            fieldsContainer.innerHTML = '';

            // Add plural indicator if applicable
            if (currentKeyIsPlural) {
                const pluralBadge = document.createElement('div');
                pluralBadge.style.marginBottom = '15px';
                pluralBadge.style.padding = '8px 12px';
                pluralBadge.style.background = 'var(--vscode-inputValidation-infoBackground)';
                pluralBadge.style.borderLeft = '3px solid var(--vscode-charts-purple)';
                pluralBadge.style.borderRadius = '3px';
                pluralBadge.innerHTML = '🔢 <strong>Plural Key</strong> - This key has multiple plural forms (one, other, etc.)';
                fieldsContainer.appendChild(pluralBadge);
            }

            languages.forEach(lang => {
                const langData = keyData.values[lang.code] || { value: '', comment: null };

                const fieldGroup = document.createElement('div');
                fieldGroup.style.marginBottom = '20px';
                fieldGroup.style.padding = '15px';
                fieldGroup.style.background = 'var(--vscode-editor-background)';
                fieldGroup.style.borderRadius = '4px';

                // Check if this language has plural forms
                const hasPluralForms = langData.isPlural && langData.pluralForms && Object.keys(langData.pluralForms).length > 0;

                if (hasPluralForms) {
                    // Render all CLDR plural forms so users can add any form
                    const pluralFormsHtml = ['zero', 'one', 'two', 'few', 'many', 'other']
                        .map(form => \`
                            <div style="margin-bottom: 8px;">
                                <label style="display: inline-block; width: 60px; font-size: 11px; opacity: 0.8; text-transform: uppercase;">\${form}:</label>
                                <input
                                    type="text"
                                    id="edit-plural-\${lang.code}-\${form}"
                                    data-lang="\${lang.code}"
                                    data-form="\${form}"
                                    value="\${langData.pluralForms[form] || ''}"
                                    placeholder="\${form} form"
                                    style="flex: 1; padding: 6px; background: var(--vscode-input-background); color: var(--vscode-input-foreground); border: 1px solid var(--vscode-input-border); border-radius: 3px; width: calc(100% - 70px);"
                                />
                            </div>
                        \`).join('');

                    fieldGroup.innerHTML = \`
                        <div style="font-weight: bold; margin-bottom: 10px; color: var(--vscode-foreground);">
                            \${lang.code}\${lang.isDefault ? ' (Default)' : ''} <span style="font-weight: normal; font-size: 11px; color: var(--vscode-charts-purple);">🔢 Plural</span>
                        </div>
                        <div style="margin-bottom: 10px;">
                            <label style="display: block; margin-bottom: 8px; font-size: 12px; opacity: 0.8;">Plural Forms:</label>
                            \${pluralFormsHtml}
                        </div>
                        <div>
                            <label style="display: block; margin-bottom: 5px; font-size: 12px; opacity: 0.8;">Comment:</label>
                            <input
                                type="text"
                                id="edit-comment-\${lang.code}"
                                data-lang="\${lang.code}"
                                placeholder="Optional comment"
                                value="\${langData.comment || ''}"
                                style="width: 100%; padding: 8px; background: var(--vscode-input-background); color: var(--vscode-input-foreground); border: 1px solid var(--vscode-input-border); border-radius: 3px;"
                            />
                        </div>
                    \`;
                } else {
                    // Standard single value field
                    fieldGroup.innerHTML = \`
                        <div style="font-weight: bold; margin-bottom: 10px; color: var(--vscode-foreground);">
                            \${lang.code}\${lang.isDefault ? ' (Default)' : ''}
                        </div>
                        <div style="margin-bottom: 10px;">
                            <label style="display: block; margin-bottom: 5px; font-size: 12px; opacity: 0.8;">Value:</label>
                            <textarea
                                id="edit-value-\${lang.code}"
                                data-lang="\${lang.code}"
                                style="width: 100%; min-height: 60px; padding: 8px; background: var(--vscode-input-background); color: var(--vscode-input-foreground); border: 1px solid var(--vscode-input-border); border-radius: 3px; font-family: var(--vscode-font-family); resize: vertical;"
                            >\${langData.value || ''}</textarea>
                        </div>
                        <div>
                            <label style="display: block; margin-bottom: 5px; font-size: 12px; opacity: 0.8;">Comment:</label>
                            <input
                                type="text"
                                id="edit-comment-\${lang.code}"
                                data-lang="\${lang.code}"
                                placeholder="Optional comment"
                                value="\${langData.comment || ''}"
                                style="width: 100%; padding: 8px; background: var(--vscode-input-background); color: var(--vscode-input-foreground); border: 1px solid var(--vscode-input-border); border-radius: 3px;"
                            />
                        </div>
                    \`;
                }

                fieldsContainer.appendChild(fieldGroup);
            });

            document.getElementById('editKeyModal').style.display = 'block';
        }

        function closeEditKeyModal() {
            document.getElementById('editKeyModal').style.display = 'none';
            currentEditingKey = null;
        }

        function autoTranslateKey() {
            if (!currentEditingKey) {
                setStatus('No key selected', 3000);
                return;
            }

            // Show translate modal in single-key mode with higher z-index
            const translateModal = document.getElementById('translateModal');
            translateModal.style.zIndex = '10001'; // Higher than edit modal

            document.getElementById('translateContext').style.display = 'block';
            document.getElementById('translateKeyName').textContent = currentEditingKey;

            // Get source text from default language
            const defaultLang = languages.find(l => l.isDefault);
            const sourceValue = document.getElementById(\`edit-value-\${defaultLang?.code}\`)?.value || '';
            document.getElementById('translateSourceText').textContent = sourceValue || '(empty)';

            // Show comment if exists
            const comment = document.getElementById(\`edit-comment-\${defaultLang?.code}\`)?.value;
            if (comment) {
                document.getElementById('translateComment').style.display = 'block';
                document.getElementById('translateCommentText').textContent = comment;
            } else {
                document.getElementById('translateComment').style.display = 'none';
            }

            // Hide "Only translate missing" checkbox for single-key mode
            document.getElementById('onlyMissingCheckbox').parentElement.parentElement.style.display = 'none';

            // Render only missing languages for this key
            renderLanguageCheckboxesForKey();

            showTranslateModal();
        }

        function renderLanguageCheckboxesForKey() {
            const container = document.getElementById('languageCheckboxes');
            container.innerHTML = '';

            languages.filter(l => !l.isDefault).forEach(lang => {
                const value = document.getElementById(\`edit-value-\${lang.code}\`)?.value || '';
                const isMissing = !value.trim();

                const wrapper = document.createElement('div');
                wrapper.style.marginBottom = '8px';
                wrapper.style.padding = '5px';
                wrapper.style.cursor = 'pointer';

                wrapper.innerHTML = \`
                    <label style="display: flex; align-items: center; cursor: pointer;">
                        <input type="checkbox" value="\${lang.code}" \${isMissing ? 'checked' : ''} style="margin-right: 8px;">
                        <span>\${lang.code}</span>
                        \${isMissing ? '<span style="margin-left: 8px; color: var(--vscode-editorWarning-foreground); font-size: 11px;">(missing)</span>' : ''}
                    </label>
                \`;

                container.appendChild(wrapper);
            });
        }

        function populateTranslationsToEditModal(translations) {
            if (!translations || !currentEditingKey) {
                return;
            }

            // Populate translation results into edit modal fields
            Object.keys(translations).forEach(langCode => {
                const valueField = document.getElementById(\`edit-value-\${langCode}\`);
                if (valueField && translations[langCode]) {
                    valueField.value = translations[langCode];
                    // Highlight the updated field
                    valueField.style.border = '2px solid var(--vscode-focusBorder)';
                    setTimeout(() => {
                        valueField.style.border = '1px solid var(--vscode-input-border)';
                    }, 2000);
                }
            });
        }

        function saveEditedKey() {
            if (!currentEditingKey) {
                return;
            }

            // Collect all values and comments
            const values = {};
            languages.forEach(lang => {
                const commentField = document.getElementById(\`edit-comment-\${lang.code}\`);

                if (currentKeyIsPlural) {
                    // Collect plural forms
                    const pluralForms = {};
                    ['zero', 'one', 'two', 'few', 'many', 'other'].forEach(form => {
                        const formField = document.getElementById(\`edit-plural-\${lang.code}-\${form}\`);
                        if (formField && formField.value) {
                            pluralForms[form] = formField.value;
                        }
                    });

                    // Only include if there are any forms
                    if (Object.keys(pluralForms).length > 0) {
                        values[lang.code] = {
                            value: pluralForms['other'] || pluralForms['one'] || Object.values(pluralForms)[0] || '',
                            comment: commentField?.value || null,
                            isPlural: true,
                            pluralForms: pluralForms
                        };
                    } else {
                        values[lang.code] = {
                            value: '',
                            comment: commentField?.value || null,
                            isPlural: true,
                            pluralForms: {}
                        };
                    }
                } else {
                    // Standard single value
                    const valueField = document.getElementById(\`edit-value-\${lang.code}\`);
                    values[lang.code] = {
                        value: valueField?.value || '',
                        comment: commentField?.value || null
                    };
                }
            });

            vscode.postMessage({
                command: 'updateKey',
                key: currentEditingKey,
                values: values
            });

            closeEditKeyModal();
            setStatus('Saving...');
        }

        function updateRefsCells(scanData) {
            if (!scanData || !scanData.references) {
                return;
            }

            // Create a map of key -> reference count
            const refCounts = new Map();
            scanData.references.forEach(ref => {
                refCounts.set(ref.key, ref.referenceCount || ref.references.length);
            });

            // Create set of unused keys
            const unusedKeys = new Set(scanData.unused || []);

            // Update all rows
            const rows = document.querySelectorAll('#tableBody tr');
            rows.forEach(row => {
                const cells = row.querySelectorAll('td[data-key]');
                if (cells.length === 0) return;

                const key = cells[0].dataset.key;
                if (!key) return;

                // Update refs cell
                const refsCell = Array.from(cells).find(c => c.title === 'Click to view references');
                if (refsCell) {
                    const count = refCounts.get(key);
                    if (count !== undefined) {
                        refsCell.textContent = count.toString();
                        if (count === 0) {
                            refsCell.style.color = 'var(--vscode-editorWarning-foreground)';
                        }
                    }
                }

                // Update status cell
                const statusCell = cells[1]; // Status is the second cell with data-key
                if (statusCell && unusedKeys.has(key)) {
                    const currentContent = statusCell.innerHTML || statusCell.textContent;
                    if (!currentContent.includes('🔵')) {
                        const unusedIcon = '<span style="color: var(--vscode-editorInfo-foreground);" title="Unused in code">🔵</span>';
                        if (currentContent === '✓') {
                            statusCell.innerHTML = unusedIcon;
                        } else {
                            statusCell.innerHTML = currentContent + ' ' + unusedIcon;
                        }
                    }
                }
            });
        }

        function showTranslateModal() {
            const translateModal = document.getElementById('translateModal');
            translateModal.style.display = 'block';

            // If not in single-key mode, reset z-index and show "translate all" UI
            if (!currentEditingKey) {
                translateModal.style.zIndex = '10000';
                document.getElementById('translateContext').style.display = 'none';
                document.getElementById('onlyMissingCheckbox').parentElement.parentElement.style.display = 'block';
                renderLanguageCheckboxes();
            }

            renderProviderOptions();
        }

        function closeTranslateModal() {
            const translateModal = document.getElementById('translateModal');
            translateModal.style.display = 'none';
            translateModal.style.zIndex = '10000'; // Reset z-index

            // Reset UI
            document.getElementById('translateProgress').style.display = 'none';
            document.getElementById('translateBtn').disabled = false;
            document.getElementById('translateProgressBar').style.width = '0%';
            document.getElementById('translateProgressText').style.display = 'block'; // Reset to visible
            document.getElementById('providerWarning').style.display = 'none';
            document.getElementById('translateContext').style.display = 'none';
            document.getElementById('onlyMissingCheckbox').parentElement.parentElement.style.display = 'block';
        }

        function updateTranslationProgress(data) {
            const progressDiv = document.getElementById('translateProgress');
            const statusDiv = document.getElementById('translateStatus');
            const progressBar = document.getElementById('translateProgressBar');
            const progressText = document.getElementById('translateProgressText');

            if (data.completed !== undefined && data.total !== undefined) {
                const percentage = data.total > 0 ? (data.completed / data.total * 100) : 0;
                progressBar.style.width = percentage + '%';
                progressBar.style.background = 'var(--vscode-progressBar-background)';
                progressText.textContent = \`\${data.completed} / \${data.total}\`;

                if (data.currentKey) {
                    statusDiv.textContent = \`Translating: \${data.currentKey}\`;
                } else if (percentage >= 100) {
                    statusDiv.textContent = 'Translation complete!';
                    progressBar.style.background = 'var(--vscode-testing-iconPassed)';
                }
            }
        }

        // Code Scan Modal Functions
        function scanCode() {
            const modal = document.getElementById('scanModal');
            const loading = document.getElementById('scanLoading');
            const results = document.getElementById('scanResults');

            modal.style.display = 'block';
            loading.style.display = 'block';
            results.style.display = 'none';

            // Use cached results if available
            if (scanResultsCache) {
                displayScanResults(scanResultsCache);
                return;
            }

            vscode.postMessage({ command: 'scanCode' });
        }

        function closeScanModal() {
            document.getElementById('scanModal').style.display = 'none';
        }

        // Key References Modal Functions
        function showKeyReferences(keyName) {
            const modal = document.getElementById('keyRefsModal');
            const loading = document.getElementById('keyRefsLoading');
            const content = document.getElementById('keyRefsContent');
            const empty = document.getElementById('keyRefsEmpty');
            const title = document.getElementById('keyRefsTitle');

            title.textContent = keyName;
            modal.style.display = 'block';
            loading.style.display = 'block';
            content.style.display = 'none';
            empty.style.display = 'none';

            // Check if we have scan results cached
            if (scanResultsCache && scanResultsCache.references) {
                const keyRefs = scanResultsCache.references.find(r => r.key === keyName);
                if (keyRefs && keyRefs.references && keyRefs.references.length > 0) {
                    displayKeyReferences(keyRefs);
                } else {
                    loading.style.display = 'none';
                    empty.style.display = 'block';
                }
            } else {
                // Fetch references from API
                vscode.postMessage({
                    command: 'getKeyReferences',
                    key: keyName
                });
            }
        }

        function closeKeyRefsModal() {
            document.getElementById('keyRefsModal').style.display = 'none';
        }

        function displayKeyReferences(keyUsage) {
            const loading = document.getElementById('keyRefsLoading');
            const content = document.getElementById('keyRefsContent');
            const empty = document.getElementById('keyRefsEmpty');
            const countSpan = document.getElementById('keyRefsCount');
            const list = document.getElementById('keyRefsList');

            loading.style.display = 'none';

            if (!keyUsage || !keyUsage.references || keyUsage.references.length === 0) {
                empty.style.display = 'block';
                return;
            }

            content.style.display = 'block';
            countSpan.textContent = keyUsage.referenceCount || keyUsage.references.length;

            list.innerHTML = keyUsage.references.map(ref => {
                const confidenceColor = ref.confidence === 'High' ? 'var(--vscode-testing-iconPassed)' :
                                      ref.confidence === 'Medium' ? 'var(--vscode-editorWarning-foreground)' :
                                      'var(--vscode-editorInfo-foreground)';

                return \`
                    <div style="padding: 10px; margin-bottom: 10px; background: var(--vscode-editor-background); border-radius: 4px; border-left: 3px solid \${confidenceColor};">
                        <div>
                            <a href="#" onclick="openFile('\${escapeHtml(ref.file)}', \${ref.line}); return false;"
                               style="color: var(--vscode-textLink-foreground); text-decoration: none; font-weight: bold;">
                                \${escapeHtml(ref.file)}:\${ref.line}
                            </a>
                            <span style="color: \${confidenceColor}; margin-left: 8px; font-size: 11px;">[\${ref.confidence}]</span>
                        </div>
                        <div style="font-family: monospace; font-size: 12px; margin-top: 5px; padding: 5px; background: var(--vscode-textCodeBlock-background); border-radius: 3px; overflow-x: auto;">
                            \${escapeHtml(ref.pattern)}
                        </div>
                        \${ref.warning ? \`<div style="color: var(--vscode-editorWarning-foreground); font-size: 11px; margin-top: 5px;">\${escapeHtml(ref.warning)}</div>\` : ''}
                    </div>
                \`;
            }).join('');
        }

        function displayScanResults(scanData) {
            scanResultsCache = scanData;

            const loading = document.getElementById('scanLoading');
            const results = document.getElementById('scanResults');

            loading.style.display = 'none';
            results.style.display = 'block';

            // Update statistics
            document.getElementById('scanFilesCount').textContent = scanData.scannedFiles || 0;
            document.getElementById('scanRefsCount').textContent = scanData.totalReferences || 0;
            document.getElementById('scanKeysCount').textContent = scanData.uniqueKeysFound || 0;
            document.getElementById('scanMissingCount').textContent = scanData.missingKeysCount || 0;
            document.getElementById('scanUnusedCount').textContent = scanData.unusedKeysCount || 0;

            // Render missing keys
            const missingSection = document.getElementById('missingSection');
            const missingList = document.getElementById('missingKeysList');
            if (scanData.missing && scanData.missing.length > 0) {
                missingSection.style.display = 'block';
                missingList.innerHTML = scanData.missing.map(key =>
                    \`<div style="padding: 4px; border-bottom: 1px solid var(--vscode-widget-border);">\${escapeHtml(key)}</div>\`
                ).join('');
            } else {
                missingSection.style.display = 'none';
            }

            // Render unused keys
            const unusedSection = document.getElementById('unusedSection');
            const unusedList = document.getElementById('unusedKeysList');
            if (scanData.unused && scanData.unused.length > 0) {
                unusedSection.style.display = 'block';
                unusedList.innerHTML = scanData.unused.map(key =>
                    \`<div style="padding: 4px; border-bottom: 1px solid var(--vscode-widget-border);">\${escapeHtml(key)}</div>\`
                ).join('');
            } else {
                unusedSection.style.display = 'none';
            }

            // Render references
            const referencesSection = document.getElementById('referencesSection');
            const referencesList = document.getElementById('referencesList');
            if (scanData.references && scanData.references.length > 0) {
                referencesSection.style.display = 'block';
                renderReferences(scanData.references);

                // Add reference search handler
                const refSearch = document.getElementById('refSearchInput');
                refSearch.value = '';
                refSearch.oninput = () => {
                    const filter = refSearch.value.toLowerCase();
                    const filtered = scanData.references.filter(ref =>
                        ref.key.toLowerCase().includes(filter)
                    );
                    renderReferences(filtered);
                };
            } else {
                referencesSection.style.display = 'none';
            }
        }

        function renderReferences(references) {
            const referencesList = document.getElementById('referencesList');
            referencesList.innerHTML = references.map(ref => {
                const refsHtml = ref.references.map(r => {
                    const confidenceColor = r.confidence === 'High' ? 'var(--vscode-testing-iconPassed)' :
                                          r.confidence === 'Medium' ? 'var(--vscode-editorWarning-foreground)' :
                                          'var(--vscode-editorInfo-foreground)';
                    return \`
                        <div style="padding: 4px 0; margin-left: 20px; border-bottom: 1px solid var(--vscode-widget-border);">
                            <a href="#" onclick="openFile('\${escapeHtml(r.file)}', \${r.line}); return false;"
                               style="color: var(--vscode-textLink-foreground); text-decoration: none;">
                                \${escapeHtml(r.file)}:\${r.line}
                            </a>
                            <span style="color: \${confidenceColor}; margin-left: 8px; font-size: 11px;">[\${r.confidence}]</span>
                            <div style="font-size: 12px; opacity: 0.7; margin-top: 2px;">\${escapeHtml(r.pattern)}</div>
                            \${r.warning ? \`<div style="color: var(--vscode-editorWarning-foreground); font-size: 11px; margin-top: 2px;">\${escapeHtml(r.warning)}</div>\` : ''}
                        </div>
                    \`;
                }).join('');

                return \`
                    <div style="margin-bottom: 15px;">
                        <div style="font-weight: bold; padding: 6px; background: var(--vscode-badge-background); color: var(--vscode-badge-foreground); border-radius: 3px;">
                            \${escapeHtml(ref.key)} <span style="opacity: 0.7; font-weight: normal;">(\${ref.referenceCount} references)</span>
                        </div>
                        \${refsHtml}
                    </div>
                \`;
            }).join('');
        }

        function openFile(filePath, line) {
            vscode.postMessage({
                command: 'openFile',
                filePath: filePath,
                line: line
            });
        }

        function escapeHtml(text) {
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }

        function renderProviderOptions() {
            const select = document.getElementById('translateProvider');
            const warning = document.getElementById('providerWarning');
            const warningText = document.getElementById('providerWarningText');

            select.innerHTML = '';

            if (!Array.isArray(providers)) {
                console.error('Providers is not an array:', providers);
                return;
            }

            providers.forEach(provider => {
                const option = document.createElement('option');
                option.value = provider.name;
                option.textContent = provider.displayName;
                option.dataset.configured = provider.isConfigured;
                select.appendChild(option);
            });

            // Show warning if selected provider is not configured
            select.onchange = () => {
                const selectedOption = select.options[select.selectedIndex];
                const isConfigured = selectedOption.dataset.configured === 'true';
                if (!isConfigured) {
                    warning.style.display = 'block';
                    warningText.textContent = \`Provider "\${selectedOption.textContent}" is not configured. Translation may fail. Configure it in settings.\`;
                } else {
                    warning.style.display = 'none';
                }
            };

            // Trigger initial check
            if (select.options.length > 0) {
                select.onchange();
            }
        }

        function renderLanguageCheckboxes() {
            const container = document.getElementById('languageCheckboxes');
            container.innerHTML = '';

            languages.filter(l => !l.isDefault).forEach(lang => {
                const label = document.createElement('label');
                label.style.display = 'block';
                label.style.marginBottom = '8px';
                label.style.cursor = 'pointer';
                label.innerHTML = \`
                    <input type="checkbox" value="\${lang.code}" checked style="margin-right: 8px;">
                    <span>\${lang.code}</span>
                \`;
                container.appendChild(label);
            });
        }

        function submitTranslateAll() {
            const provider = document.getElementById('translateProvider').value;
            const checkboxes = document.querySelectorAll('#languageCheckboxes input:checked');
            const targetLanguages = Array.from(checkboxes).map(cb => cb.value);
            const onlyMissing = document.getElementById('onlyMissingCheckbox').checked;

            if (targetLanguages.length === 0) {
                setStatus('Please select at least one language', 3000);
                return;
            }

            // Show progress UI
            const progressDiv = document.getElementById('translateProgress');
            const statusDiv = document.getElementById('translateStatus');
            const progressBar = document.getElementById('translateProgressBar');
            const progressText = document.getElementById('translateProgressText');
            const translateBtn = document.getElementById('translateBtn');
            const cancelBtn = document.getElementById('translateCancelBtn');

            progressDiv.style.display = 'block';
            translateBtn.disabled = true;

            // Check if in single-key mode
            if (currentEditingKey) {
                // Single key - show simple loading message
                statusDiv.textContent = \`Translating to \${targetLanguages.length} language(s)...\`;
                progressBar.style.width = '50%'; // Show indeterminate progress
                progressText.style.display = 'none'; // Hide the count

                vscode.postMessage({
                    command: 'translateKey',
                    key: currentEditingKey,
                    provider: provider,
                    languages: targetLanguages,
                    onlyMissing: onlyMissing
                });
            } else {
                // Translate all - show detailed progress
                statusDiv.textContent = 'Starting translation...';
                progressBar.style.width = '0%';
                progressText.style.display = 'block';
                progressText.textContent = '0 / 0';

                vscode.postMessage({
                    command: 'translateAll',
                    provider: provider,
                    languages: targetLanguages,
                    onlyMissing: onlyMissing
                });
            }
        }

        function updateStats() {
            const totalKeys = resources.length;
            document.getElementById('stats').textContent = totalKeys + ' keys';
            document.getElementById('statusStats').textContent = totalKeys + ' keys loaded';
        }

        function setStatus(text, timeout) {
            document.getElementById('statusText').textContent = text;
            if (timeout) {
                setTimeout(() => {
                    document.getElementById('statusText').textContent = 'Ready';
                }, timeout);
            }
        }

        // Enhanced search functionality - client-side for substring, server-side for wildcard/regex
        let searchTimeout;
        function performSearch() {
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                const pattern = document.getElementById('searchInput').value.trim();
                const mode = document.getElementById('searchMode').value;
                const caseSensitive = document.getElementById('caseSensitive').checked;
                const scope = document.getElementById('searchScope').value;

                if (!pattern) {
                    // No filter - show all resources
                    resources = allResources;
                    renderTable();
                    updateStats();
                    return;
                }

                if (mode === 'substring') {
                    // Client-side filtering for substring (fast)
                    const searchPattern = caseSensitive ? pattern : pattern.toLowerCase();

                    resources = allResources.filter(r => {
                        const key = caseSensitive ? r.key : r.key.toLowerCase();

                        if (scope === 'keys' || scope === 'keysAndValues') {
                            if (key.includes(searchPattern)) return true;
                        }

                        if (scope === 'values' || scope === 'keysAndValues' || scope === 'all') {
                            for (const val of Object.values(r.values)) {
                                const value = caseSensitive ? (val || '') : (val || '').toLowerCase();
                                if (value.includes(searchPattern)) return true;
                            }
                        }

                        return false;
                    });

                    renderTable();
                    updateStats();
                } else {
                    // Server-side for wildcard/regex (needs backend processing)
                    vscode.postMessage({
                        command: 'searchEnhanced',
                        pattern: pattern,
                        filterMode: mode,
                        caseSensitive: caseSensitive,
                        searchScope: scope
                    });
                }
            }, 300);
        }

        // Simple filter for CodeLens quick-select (always substring, keys only)
        function filterResources() {
            const pattern = document.getElementById('searchInput').value.trim().toLowerCase();

            if (!pattern) {
                resources = allResources;
                renderTable();
                return;
            }

            resources = allResources.filter(r => r.key.toLowerCase().includes(pattern));
            renderTable();
        }

        // Bind search input event
        document.getElementById('searchInput').addEventListener('input', performSearch);
    </script>
</body>
</html>`;
    }
}
