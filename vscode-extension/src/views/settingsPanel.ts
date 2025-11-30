import * as vscode from 'vscode';
import { ApiClient } from '../backend/apiClient';

export class SettingsPanel {
    public static currentPanel: SettingsPanel | undefined;
    private readonly panel: vscode.WebviewPanel;
    private readonly apiClient: ApiClient;
    private disposables: vscode.Disposable[] = [];

    private constructor(panel: vscode.WebviewPanel, apiClient: ApiClient) {
        this.panel = panel;
        this.apiClient = apiClient;

        // Set the webview's initial html content
        this.update();

        // Listen for when the panel is disposed
        this.panel.onDidDispose(() => this.dispose(), null, this.disposables);

        // Handle messages from the webview
        this.panel.webview.onDidReceiveMessage(
            async message => {
                switch (message.command) {
                    case 'testProvider':
                        await this.testProvider(message.provider);
                        break;
                    case 'saveSettings':
                        await this.saveSettings(message.settings);
                        break;
                    case 'resetDefaults':
                        await this.resetDefaults();
                        break;
                }
            },
            null,
            this.disposables
        );
    }

    public static createOrShow(apiClient: ApiClient): void {
        const column = vscode.window.activeTextEditor
            ? vscode.window.activeTextEditor.viewColumn
            : undefined;

        // If we already have a panel, show it
        if (SettingsPanel.currentPanel) {
            SettingsPanel.currentPanel.panel.reveal(column);
            SettingsPanel.currentPanel.update();
            return;
        }

        // Otherwise, create a new panel
        const panel = vscode.window.createWebviewPanel(
            'lrmSettings',
            'LRM Settings',
            column || vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: true
            }
        );

        SettingsPanel.currentPanel = new SettingsPanel(panel, apiClient);
    }

    private async testProvider(provider: string): Promise<void> {
        vscode.window.showInformationMessage(`Testing ${provider} provider...`);
        // TODO: Implement provider testing
        // This would call the API to test the translation provider with the configured API key
    }

    private async saveSettings(settings: any): Promise<void> {
        const config = vscode.workspace.getConfiguration('lrm');

        try {
            // Build lrm.json configuration
            const lrmConfig: any = {
                Translation: {
                    DefaultProvider: settings.translationProvider
                },
                Scanning: {
                    ResourceClassNames: settings.scanSettings?.resourceClasses || [],
                    LocalizationMethods: settings.scanSettings?.localizationMethods || []
                },
                Validation: {
                    EnablePlaceholderValidation: settings.enablePlaceholderValidation
                }
            };

            // Add API keys if provided
            if (settings.apiKeys && Object.keys(settings.apiKeys).length > 0) {
                lrmConfig.Translation.ApiKeys = settings.apiKeys;
            }

            // Save to lrm.json via API
            await this.apiClient.updateConfiguration(lrmConfig);

            // Save VS Code extension-specific settings (these do NOT go to lrm.json)
            if (settings.scanSettings) {
                await config.update('scanCSharp', settings.scanSettings.scanCSharp, vscode.ConfigurationTarget.Workspace);
                await config.update('scanRazor', settings.scanSettings.scanRazor, vscode.ConfigurationTarget.Workspace);
                await config.update('scanXaml', settings.scanSettings.scanXaml, vscode.ConfigurationTarget.Workspace);
            }

            // Also save to VS Code settings as backup (in case lrm.json is deleted)
            await config.update('translationProvider', settings.translationProvider, vscode.ConfigurationTarget.Workspace);
            await config.update('resourceClasses', settings.scanSettings?.resourceClasses, vscode.ConfigurationTarget.Workspace);
            await config.update('localizationMethods', settings.scanSettings?.localizationMethods, vscode.ConfigurationTarget.Workspace);
            await config.update('enablePlaceholderValidation', settings.enablePlaceholderValidation, vscode.ConfigurationTarget.Workspace);

            vscode.window.showInformationMessage('Settings saved to lrm.json successfully!');
            this.update();
        } catch (error: any) {
            vscode.window.showErrorMessage(`Failed to save settings: ${error.message}`);
        }
    }

    private async resetDefaults(): Promise<void> {
        const result = await vscode.window.showWarningMessage(
            'Reset all settings to defaults?',
            { modal: true },
            'Reset'
        );

        if (result === 'Reset') {
            const config = vscode.workspace.getConfiguration('lrm');
            await config.update('translationProvider', undefined, vscode.ConfigurationTarget.Workspace);
            await config.update('resourceClasses', undefined, vscode.ConfigurationTarget.Workspace);
            await config.update('localizationMethods', undefined, vscode.ConfigurationTarget.Workspace);

            vscode.window.showInformationMessage('Settings reset to defaults');
            this.update();
        }
    }

    public async update(): Promise<void> {
        try {
            const providers = await this.apiClient.getTranslationProviders();

            // Try to load from lrm.json first, fall back to VS Code settings
            let lrmConfig: any = null;
            try {
                lrmConfig = await this.apiClient.getConfiguration();
            } catch (error) {
                // lrm.json doesn't exist yet, will use defaults
            }

            const vscodeConfig = vscode.workspace.getConfiguration('lrm');

            const settings = {
                translationProvider: lrmConfig?.Translation?.DefaultProvider || vscodeConfig.get('translationProvider', 'lingva'),
                resourceClasses: lrmConfig?.Scanning?.ResourceClassNames || vscodeConfig.get('resourceClasses', ['Resources', 'Strings', 'AppResources']),
                localizationMethods: lrmConfig?.Scanning?.LocalizationMethods || vscodeConfig.get('localizationMethods', ['GetString', 'GetLocalizedString', 'Translate', 'L', 'T']),
                enablePlaceholderValidation: lrmConfig?.Validation?.EnablePlaceholderValidation ?? vscodeConfig.get('enablePlaceholderValidation', true),
                scanCSharp: vscodeConfig.get('scanCSharp', true),
                scanRazor: vscodeConfig.get('scanRazor', true),
                scanXaml: vscodeConfig.get('scanXaml', true),
                apiKeys: lrmConfig?.Translation?.ApiKeys || {}
            };

            this.panel.webview.html = this.getHtmlContent(providers, settings);
        } catch (error) {
            this.panel.webview.html = this.getErrorHtml();
        }
    }

    private getHtmlContent(providers: any[], settings: any): string {
        const freeProviders = providers.filter(p => !p.requiresApiKey);
        const paidProviders = providers.filter(p => p.requiresApiKey);

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>LRM Settings</title>
    <style>
        body {
            font-family: var(--vscode-font-family);
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
            padding: 20px;
            margin: 0;
        }
        h1 {
            font-size: 24px;
            margin-bottom: 20px;
            border-bottom: 1px solid var(--vscode-panel-border);
            padding-bottom: 10px;
        }
        h2 {
            font-size: 18px;
            margin-top: 30px;
            margin-bottom: 15px;
        }
        h3 {
            font-size: 16px;
            margin-top: 20px;
            margin-bottom: 10px;
            color: var(--vscode-descriptionForeground);
        }
        .section {
            background-color: var(--vscode-editor-inactiveSelectionBackground);
            border: 1px solid var(--vscode-panel-border);
            border-radius: 6px;
            padding: 20px;
            margin-bottom: 20px;
        }
        .form-group {
            margin-bottom: 20px;
        }
        label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
        }
        select, input[type="text"], input[type="number"] {
            width: 100%;
            padding: 8px;
            background-color: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            border: 1px solid var(--vscode-input-border);
            border-radius: 4px;
            font-family: var(--vscode-font-family);
            font-size: 14px;
        }
        .provider-list {
            list-style: none;
            padding: 0;
        }
        .provider-item {
            display: flex;
            align-items: center;
            justify-content: space-between;
            padding: 12px;
            margin-bottom: 8px;
            background-color: var(--vscode-input-background);
            border-radius: 4px;
            border-left: 3px solid var(--vscode-activityBarBadge-background);
        }
        .provider-name {
            font-weight: 500;
        }
        .provider-status {
            font-size: 12px;
            opacity: 0.8;
        }
        .status-ready {
            color: var(--vscode-charts-green);
        }
        .status-needs-key {
            color: var(--vscode-editorWarning-foreground);
        }
        button {
            padding: 10px 20px;
            background-color: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
            font-family: var(--vscode-font-family);
            margin-right: 10px;
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
        .checkbox-group {
            display: flex;
            align-items: center;
            gap: 8px;
            margin-bottom: 10px;
        }
        input[type="checkbox"] {
            width: auto;
        }
        .button-group {
            margin-top: 30px;
            padding-top: 20px;
            border-top: 1px solid var(--vscode-panel-border);
        }
        .hint {
            font-size: 12px;
            color: var(--vscode-descriptionForeground);
            margin-top: 5px;
        }
    </style>
</head>
<body>
    <h1>⚙️ LRM Settings</h1>

    <div class="section">
        <h2>Translation Providers</h2>

        <div class="form-group">
            <label for="defaultProvider">Default Provider</label>
            <select id="defaultProvider">
                ${providers.map(p => `
                    <option value="${p.name}" ${settings.translationProvider === p.name ? 'selected' : ''}>
                        ${p.displayName || p.name}${p.requiresApiKey ? ' (requires API key)' : ' (free)'}
                    </option>
                `).join('')}
            </select>
            <div class="hint">Choose the default translation provider for missing values</div>
        </div>

        <h3>Free Providers (No API Key Required)</h3>
        <ul class="provider-list">
            ${freeProviders.map(p => `
                <li class="provider-item">
                    <div>
                        <div class="provider-name">${p.displayName || p.name}</div>
                        <div class="provider-status status-ready">✅ Ready</div>
                    </div>
                </li>
            `).join('')}
        </ul>

        <h3>Paid Providers (API Key Required)</h3>
        <ul class="provider-list">
            ${paidProviders.map(p => {
                const providerKey = p.name.charAt(0).toUpperCase() + p.name.slice(1);
                const currentKey = settings.apiKeys[providerKey] || '';
                const isConfigured = p.isConfigured || currentKey.length > 0;
                return `
                <li class="provider-item">
                    <div style="flex: 1;">
                        <div class="provider-name">${p.displayName || p.name}</div>
                        <div class="provider-status ${isConfigured ? 'status-ready' : 'status-needs-key'}">
                            ${isConfigured ? '✅ Configured' : '⚠️ Requires API key'}
                        </div>
                        <div class="form-group" style="margin-top: 10px;">
                            <input type="password"
                                   id="apiKey_${providerKey}"
                                   placeholder="Enter API key..."
                                   value="${currentKey}"
                                   style="width: 100%; max-width: 400px;">
                        </div>
                    </div>
                    <button class="secondary" onclick="testProvider('${p.name}')">Test</button>
                </li>
            `;
            }).join('')}
        </ul>

        <div class="hint">
            💡 API keys are saved to lrm.json in your resource folder.
            See the <a href="https://github.com/nickprotop/LocalizationManager">documentation</a> for details.
        </div>
    </div>

    <div class="section">
        <h2>Validation</h2>

        <div class="checkbox-group">
            <input type="checkbox" id="enablePlaceholderValidation" ${settings.enablePlaceholderValidation ? 'checked' : ''}>
            <label for="enablePlaceholderValidation">Enable placeholder validation ({0}, {1}, etc.)</label>
        </div>

        <div class="hint">Validates that placeholders match across all language translations</div>
    </div>

    <div class="section">
        <h2>Code Scanning</h2>
        <div class="hint" style="margin-bottom: 15px;">
            ℹ️ Resource classes and methods are saved to <strong>lrm.json</strong> (shared across CLI, Web UI, and VS Code).
            File type scanning is VS Code extension-specific.
        </div>

        <div class="form-group">
            <label for="resourceClasses">Resource Classes (comma-separated)</label>
            <input type="text" id="resourceClasses" value="${settings.resourceClasses.join(', ')}">
            <div class="hint">Class names to detect in code (e.g., Resources, Strings, AppResources)</div>
        </div>

        <div class="form-group">
            <label for="localizationMethods">Localization Methods (comma-separated)</label>
            <input type="text" id="localizationMethods" value="${settings.localizationMethods.join(', ')}">
            <div class="hint">Method names to detect in code (e.g., GetString, Translate, L, T)</div>
        </div>

        <h3>File Types to Scan (VS Code Extension Only)</h3>
        <div class="checkbox-group">
            <input type="checkbox" id="scanCSharp" ${settings.scanCSharp ? 'checked' : ''}>
            <label for="scanCSharp">C# files (.cs)</label>
        </div>
        <div class="checkbox-group">
            <input type="checkbox" id="scanRazor" ${settings.scanRazor ? 'checked' : ''}>
            <label for="scanRazor">Razor files (.razor, .cshtml)</label>
        </div>
        <div class="checkbox-group">
            <input type="checkbox" id="scanXaml" ${settings.scanXaml ? 'checked' : ''}>
            <label for="scanXaml">XAML files (.xaml)</label>
        </div>
    </div>

    <div class="button-group">
        <button onclick="saveSettings()">Save Settings</button>
        <button class="secondary" onclick="resetDefaults()">Reset to Defaults</button>
    </div>

    <script>
        const vscode = acquireVsCodeApi();

        function testProvider(providerName) {
            vscode.postMessage({
                command: 'testProvider',
                provider: providerName
            });
        }

        function saveSettings() {
            const resourceClasses = document.getElementById('resourceClasses').value
                .split(',')
                .map(s => s.trim())
                .filter(s => s.length > 0);

            const localizationMethods = document.getElementById('localizationMethods').value
                .split(',')
                .map(s => s.trim())
                .filter(s => s.length > 0);

            // Collect API keys from input fields
            const apiKeys = {};
            document.querySelectorAll('input[id^="apiKey_"]').forEach(input => {
                const providerKey = input.id.replace('apiKey_', '');
                const value = input.value.trim();
                if (value.length > 0) {
                    apiKeys[providerKey] = value;
                }
            });

            vscode.postMessage({
                command: 'saveSettings',
                settings: {
                    translationProvider: document.getElementById('defaultProvider').value,
                    enablePlaceholderValidation: document.getElementById('enablePlaceholderValidation').checked,
                    apiKeys: apiKeys,
                    scanSettings: {
                        resourceClasses: resourceClasses,
                        localizationMethods: localizationMethods,
                        scanCSharp: document.getElementById('scanCSharp').checked,
                        scanRazor: document.getElementById('scanRazor').checked,
                        scanXaml: document.getElementById('scanXaml').checked
                    }
                }
            });
        }

        function resetDefaults() {
            vscode.postMessage({ command: 'resetDefaults' });
        }
    </script>
</body>
</html>`;
    }

    private getErrorHtml(): string {
        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Settings Error</title>
    <style>
        body {
            font-family: var(--vscode-font-family);
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
            padding: 40px;
            text-align: center;
        }
        .error-icon {
            font-size: 48px;
            margin-bottom: 20px;
        }
        h1 {
            color: var(--vscode-errorForeground);
        }
    </style>
</head>
<body>
    <div class="error-icon">❌</div>
    <h1>Unable to Load Settings</h1>
    <p>The Localization Manager service is not running.</p>
    <p>Please check the LRM Backend output channel for details.</p>
</body>
</html>`;
    }

    public dispose(): void {
        SettingsPanel.currentPanel = undefined;

        this.panel.dispose();

        while (this.disposables.length) {
            const disposable = this.disposables.pop();
            if (disposable) {
                disposable.dispose();
            }
        }
    }
}
