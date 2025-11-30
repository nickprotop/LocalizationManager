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
        try {
            vscode.window.withProgress({
                location: vscode.ProgressLocation.Notification,
                title: `Testing ${provider} provider...`,
                cancellable: false
            }, async () => {
                const result = await this.apiClient.testProvider(provider);
                if (result.success) {
                    vscode.window.showInformationMessage(result.message);
                } else {
                    vscode.window.showWarningMessage(result.message);
                }
            });
        } catch (error: any) {
            vscode.window.showErrorMessage(`Failed to test provider: ${error.message}`);
        }
    }

    private async saveSettings(settings: any): Promise<void> {
        const config = vscode.workspace.getConfiguration('lrm');

        try {
            // Handle secure credential store toggle change
            if (settings.useSecureCredentialStore !== undefined) {
                try {
                    await this.apiClient.setSecureStoreEnabled(settings.useSecureCredentialStore);
                } catch (error) {
                    // Continue even if this fails
                }
            }

            // Save API keys to secure store if enabled and keys were provided
            if (settings.useSecureCredentialStore && settings.apiKeys) {
                const providerMap: { [key: string]: string } = {
                    'Google': 'google',
                    'Deepl': 'deepl',
                    'Openai': 'openai',
                    'Claude': 'claude',
                    'Azureopenai': 'azureopenai',
                    'Azure': 'azuretranslator'
                };

                for (const [inputKey, providerName] of Object.entries(providerMap)) {
                    const apiKey = settings.apiKeys[inputKey];
                    if (apiKey && apiKey.trim().length > 0) {
                        try {
                            await this.apiClient.setApiKey(providerName, apiKey);
                        } catch (error: any) {
                            vscode.window.showWarningMessage(`Failed to save ${providerName} API key: ${error.message}`);
                        }
                    }
                }
                // Clear apiKeys from settings so they don't get saved to lrm.json
                settings.apiKeys = {};
            }

            // Build complete lrm.json configuration
            const lrmConfig: any = {
                DefaultLanguageCode: settings.defaultLanguageCode || undefined,
                Translation: {
                    DefaultProvider: settings.translationProvider,
                    MaxRetries: settings.maxRetries,
                    TimeoutSeconds: settings.timeoutSeconds,
                    BatchSize: settings.batchSize,
                    UseSecureCredentialStore: settings.useSecureCredentialStore,
                    AIProviders: {}
                },
                Scanning: {
                    ResourceClassNames: settings.scanSettings?.resourceClasses || [],
                    LocalizationMethods: settings.scanSettings?.localizationMethods || []
                },
                Validation: {
                    EnablePlaceholderValidation: settings.enablePlaceholderValidation,
                    PlaceholderTypes: settings.placeholderTypes || ['dotnet']
                }
            };

            // Add API keys to config only if NOT using secure store
            if (!settings.useSecureCredentialStore && settings.apiKeys && Object.keys(settings.apiKeys).length > 0) {
                lrmConfig.Translation.ApiKeys = settings.apiKeys;
            }

            // Add AI provider settings
            if (settings.aiProviders) {
                if (settings.aiProviders.ollama) {
                    lrmConfig.Translation.AIProviders.Ollama = {
                        ApiUrl: settings.aiProviders.ollama.apiUrl || undefined,
                        Model: settings.aiProviders.ollama.model || undefined,
                        RateLimitPerMinute: settings.aiProviders.ollama.rateLimitPerMinute || undefined
                    };
                }
                if (settings.aiProviders.openai) {
                    lrmConfig.Translation.AIProviders.OpenAI = {
                        Model: settings.aiProviders.openai.model || undefined,
                        RateLimitPerMinute: settings.aiProviders.openai.rateLimitPerMinute || undefined
                    };
                }
                if (settings.aiProviders.claude) {
                    lrmConfig.Translation.AIProviders.Claude = {
                        Model: settings.aiProviders.claude.model || undefined,
                        RateLimitPerMinute: settings.aiProviders.claude.rateLimitPerMinute || undefined
                    };
                }
                if (settings.aiProviders.azureOpenAI) {
                    lrmConfig.Translation.AIProviders.AzureOpenAI = {
                        Endpoint: settings.aiProviders.azureOpenAI.endpoint || undefined,
                        DeploymentName: settings.aiProviders.azureOpenAI.deploymentName || undefined,
                        RateLimitPerMinute: settings.aiProviders.azureOpenAI.rateLimitPerMinute || undefined
                    };
                }
                if (settings.aiProviders.azureTranslator) {
                    lrmConfig.Translation.AIProviders.AzureTranslator = {
                        Region: settings.aiProviders.azureTranslator.region || undefined,
                        Endpoint: settings.aiProviders.azureTranslator.endpoint || undefined,
                        RateLimitPerMinute: settings.aiProviders.azureTranslator.rateLimitPerMinute || undefined
                    };
                }
                if (settings.aiProviders.lingva) {
                    lrmConfig.Translation.AIProviders.Lingva = {
                        InstanceUrl: settings.aiProviders.lingva.instanceUrl || undefined,
                        RateLimitPerMinute: settings.aiProviders.lingva.rateLimitPerMinute || undefined
                    };
                }
                if (settings.aiProviders.myMemory) {
                    lrmConfig.Translation.AIProviders.MyMemory = {
                        RateLimitPerMinute: settings.aiProviders.myMemory.rateLimitPerMinute || undefined
                    };
                }
            }

            // Clean up empty objects
            if (Object.keys(lrmConfig.Translation.AIProviders).length === 0) {
                delete lrmConfig.Translation.AIProviders;
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

            // Get credential provider status (sources for API keys)
            let credentialInfo: any = { providers: [], useSecureCredentialStore: false };
            try {
                credentialInfo = await this.apiClient.getCredentialProviders();
            } catch (error) {
                // Credentials API not available, continue with defaults
            }

            // Build a map of provider -> source
            const credentialSources: { [key: string]: string | null } = {};
            for (const p of credentialInfo.providers || []) {
                credentialSources[p.provider.toLowerCase()] = p.source;
            }

            const vscodeConfig = vscode.workspace.getConfiguration('lrm');

            // Support both camelCase (new) and PascalCase (legacy) property names
            const translation = lrmConfig?.translation || lrmConfig?.Translation;
            const scanning = lrmConfig?.scanning || lrmConfig?.Scanning;
            const validation = lrmConfig?.validation || lrmConfig?.Validation;
            const aiProviders = translation?.aiProviders || translation?.AIProviders;

            const settings = {
                // General
                defaultLanguageCode: lrmConfig?.defaultLanguageCode || lrmConfig?.DefaultLanguageCode || '',
                // Translation
                translationProvider: translation?.defaultProvider || translation?.DefaultProvider || vscodeConfig.get('translationProvider', 'mymemory'),
                maxRetries: translation?.maxRetries ?? translation?.MaxRetries ?? 3,
                timeoutSeconds: translation?.timeoutSeconds ?? translation?.TimeoutSeconds ?? 30,
                batchSize: translation?.batchSize ?? translation?.BatchSize ?? 10,
                // Secure credential store
                useSecureCredentialStore: credentialInfo.useSecureCredentialStore || translation?.useSecureCredentialStore || translation?.UseSecureCredentialStore || false,
                credentialSources: credentialSources,
                // Scanning
                resourceClasses: scanning?.resourceClassNames || scanning?.ResourceClassNames || vscodeConfig.get('resourceClasses', ['Resources', 'Strings', 'AppResources']),
                localizationMethods: scanning?.localizationMethods || scanning?.LocalizationMethods || vscodeConfig.get('localizationMethods', ['GetString', 'GetLocalizedString', 'Translate', 'L', 'T']),
                // Validation
                enablePlaceholderValidation: validation?.enablePlaceholderValidation ?? validation?.EnablePlaceholderValidation ?? vscodeConfig.get('enablePlaceholderValidation', true),
                placeholderTypes: validation?.placeholderTypes || validation?.PlaceholderTypes || ['dotnet'],
                // VS Code specific
                scanCSharp: vscodeConfig.get('scanCSharp', true),
                scanRazor: vscodeConfig.get('scanRazor', true),
                scanXaml: vscodeConfig.get('scanXaml', true),
                // API Keys (from config file - may be overridden by secure store)
                apiKeys: translation?.apiKeys || translation?.ApiKeys || {},
                // AI Provider Settings
                aiProviders: {
                    ollama: aiProviders?.ollama || aiProviders?.Ollama || {},
                    openai: aiProviders?.openai || aiProviders?.OpenAI || {},
                    claude: aiProviders?.claude || aiProviders?.Claude || {},
                    azureOpenAI: aiProviders?.azureOpenAI || aiProviders?.AzureOpenAI || {},
                    azureTranslator: aiProviders?.azureTranslator || aiProviders?.AzureTranslator || {},
                    lingva: aiProviders?.lingva || aiProviders?.Lingva || {},
                    myMemory: aiProviders?.myMemory || aiProviders?.MyMemory || {}
                }
            };

            this.panel.webview.html = this.getHtmlContent(providers, settings);
        } catch (error) {
            this.panel.webview.html = this.getErrorHtml();
        }
    }

    private getHtmlContent(providers: any[], settings: any): string {
        const freeProviders = providers.filter(p => !p.requiresApiKey);

        // Helper to generate source indicator HTML
        const getSourceIndicator = (providerKey: string): string => {
            const source = settings.credentialSources?.[providerKey.toLowerCase()];
            if (source === 'environment') {
                return '<div class="key-source source-env">🌐 Environment Variable</div>';
            } else if (source === 'secure_store') {
                return '<div class="key-source source-secure">🔐 Secure Store</div>';
            } else if (source === 'config_file') {
                return '<div class="key-source source-config">📄 Config File (lrm.json)</div>';
            } else {
                return '<div class="key-source source-none">⚠️ Not configured</div>';
            }
        };

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
        .form-row {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }
        .form-row .form-group {
            margin-bottom: 0;
        }
        label {
            display: block;
            margin-bottom: 5px;
            font-weight: 500;
        }
        select, input[type="text"], input[type="number"], input[type="password"] {
            width: 100%;
            padding: 8px;
            background-color: var(--vscode-input-background);
            color: var(--vscode-input-foreground);
            border: 1px solid var(--vscode-input-border);
            border-radius: 4px;
            font-family: var(--vscode-font-family);
            font-size: 14px;
            box-sizing: border-box;
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
        .collapsible {
            background-color: var(--vscode-input-background);
            border: 1px solid var(--vscode-panel-border);
            border-radius: 4px;
            margin-bottom: 10px;
        }
        .collapsible-header {
            padding: 12px 15px;
            cursor: pointer;
            display: flex;
            justify-content: space-between;
            align-items: center;
            user-select: none;
        }
        .collapsible-header:hover {
            background-color: var(--vscode-list-hoverBackground);
        }
        .collapsible-header .arrow {
            transition: transform 0.2s;
        }
        .collapsible.open .collapsible-header .arrow {
            transform: rotate(90deg);
        }
        .collapsible-content {
            display: none;
            padding: 15px;
            border-top: 1px solid var(--vscode-panel-border);
        }
        .collapsible.open .collapsible-content {
            display: block;
        }
        .provider-settings {
            margin-top: 15px;
        }
        .key-source {
            font-size: 11px;
            color: var(--vscode-descriptionForeground);
            margin-top: 4px;
            display: flex;
            align-items: center;
            gap: 4px;
        }
        .key-source.source-secure { color: var(--vscode-charts-green); }
        .key-source.source-env { color: var(--vscode-charts-blue); }
        .key-source.source-config { color: var(--vscode-charts-yellow); }
        .key-source.source-none { color: var(--vscode-editorWarning-foreground); }
        .secure-store-toggle {
            background-color: var(--vscode-editor-background);
            border: 1px solid var(--vscode-panel-border);
            border-radius: 4px;
            padding: 12px 15px;
            margin-bottom: 20px;
        }
    </style>
</head>
<body>
    <h1>LRM Settings</h1>

    <!-- General Settings -->
    <div class="section">
        <h2>General</h2>
        <div class="form-group">
            <label for="defaultLanguageCode">Default Language Code</label>
            <input type="text" id="defaultLanguageCode" value="${settings.defaultLanguageCode || ''}" placeholder="e.g., en, en-US">
            <div class="hint">Language code for the default .resx file. Used as source language for translations.</div>
        </div>
    </div>

    <!-- Translation Providers -->
    <div class="section">
        <h2>Translation Providers</h2>

        <div class="secure-store-toggle">
            <div class="checkbox-group">
                <input type="checkbox" id="useSecureCredentialStore" ${settings.useSecureCredentialStore ? 'checked' : ''}>
                <label for="useSecureCredentialStore">Use Secure Credential Store (AES-256 encrypted)</label>
            </div>
            <div class="hint">When enabled, new API keys are stored encrypted locally instead of in lrm.json</div>
        </div>

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
                        <div class="provider-status status-ready">Ready</div>
                    </div>
                </li>
            `).join('')}
        </ul>

        <h3>Paid Providers</h3>
        <div class="hint" style="margin-bottom: 15px;">Click to expand and configure API key and settings for each provider.</div>

        <!-- Google -->
        <div class="collapsible" id="google-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('google-settings')">
                <span>Google Cloud Translation ${settings.credentialSources?.['google'] ? '✓' : ''}</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-group">
                    <label for="apiKey_Google">API Key</label>
                    <input type="password" id="apiKey_Google" value="" placeholder="Enter new API key to update...">
                    ${getSourceIndicator('google')}
                </div>
                <button class="secondary" onclick="testProvider('google')" style="margin-top: 10px;">Test Connection</button>
            </div>
        </div>

        <!-- DeepL -->
        <div class="collapsible" id="deepl-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('deepl-settings')">
                <span>DeepL ${settings.credentialSources?.['deepl'] ? '✓' : ''}</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-group">
                    <label for="apiKey_Deepl">API Key</label>
                    <input type="password" id="apiKey_Deepl" value="" placeholder="Enter new API key to update...">
                    ${getSourceIndicator('deepl')}
                </div>
                <button class="secondary" onclick="testProvider('deepl')" style="margin-top: 10px;">Test Connection</button>
            </div>
        </div>

        <!-- OpenAI -->
        <div class="collapsible" id="openai-provider-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('openai-provider-settings')">
                <span>OpenAI ${settings.credentialSources?.['openai'] ? '✓' : ''}</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-group">
                    <label for="apiKey_Openai">API Key</label>
                    <input type="password" id="apiKey_Openai" value="" placeholder="Enter new API key to update...">
                    ${getSourceIndicator('openai')}
                </div>
                <div class="form-row" style="margin-top: 15px;">
                    <div class="form-group">
                        <label for="openaiModel">Model</label>
                        <input type="text" id="openaiModel" list="openaiModelList" value="${settings.aiProviders.openai.Model || settings.aiProviders.openai.model || ''}" placeholder="gpt-4o-mini">
                        <datalist id="openaiModelList">
                            <option value="gpt-4o-mini">
                            <option value="gpt-4o">
                            <option value="gpt-4-turbo">
                            <option value="gpt-4">
                            <option value="gpt-3.5-turbo">
                            <option value="o1-preview">
                            <option value="o1-mini">
                        </datalist>
                    </div>
                    <div class="form-group">
                        <label for="openaiRateLimit">Rate Limit/min</label>
                        <input type="number" id="openaiRateLimit" min="1" max="500" value="${settings.aiProviders.openai.RateLimitPerMinute || settings.aiProviders.openai.rateLimitPerMinute || ''}" placeholder="60">
                    </div>
                </div>
                <button class="secondary" onclick="testProvider('openai')" style="margin-top: 10px;">Test Connection</button>
            </div>
        </div>

        <!-- Claude -->
        <div class="collapsible" id="claude-provider-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('claude-provider-settings')">
                <span>Claude (Anthropic) ${settings.credentialSources?.['claude'] ? '✓' : ''}</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-group">
                    <label for="apiKey_Claude">API Key</label>
                    <input type="password" id="apiKey_Claude" value="" placeholder="Enter new API key to update...">
                    ${getSourceIndicator('claude')}
                </div>
                <div class="form-row" style="margin-top: 15px;">
                    <div class="form-group">
                        <label for="claudeModel">Model</label>
                        <input type="text" id="claudeModel" list="claudeModelList" value="${settings.aiProviders.claude.Model || settings.aiProviders.claude.model || ''}" placeholder="claude-sonnet-4-20250514">
                        <datalist id="claudeModelList">
                            <option value="claude-sonnet-4-20250514">
                            <option value="claude-3-5-sonnet-20241022">
                            <option value="claude-3-5-haiku-20241022">
                            <option value="claude-3-opus-20240229">
                        </datalist>
                    </div>
                    <div class="form-group">
                        <label for="claudeRateLimit">Rate Limit/min</label>
                        <input type="number" id="claudeRateLimit" min="1" max="500" value="${settings.aiProviders.claude.RateLimitPerMinute || settings.aiProviders.claude.rateLimitPerMinute || ''}" placeholder="50">
                    </div>
                </div>
                <button class="secondary" onclick="testProvider('claude')" style="margin-top: 10px;">Test Connection</button>
            </div>
        </div>

        <!-- Azure OpenAI -->
        <div class="collapsible" id="azureopenai-provider-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('azureopenai-provider-settings')">
                <span>Azure OpenAI ${settings.credentialSources?.['azureopenai'] ? '✓' : ''}</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-group">
                    <label for="apiKey_Azureopenai">API Key</label>
                    <input type="password" id="apiKey_Azureopenai" value="" placeholder="Enter new API key to update...">
                    ${getSourceIndicator('azureopenai')}
                </div>
                <div class="form-row" style="margin-top: 15px;">
                    <div class="form-group">
                        <label for="azureOpenAIEndpoint">Endpoint</label>
                        <input type="text" id="azureOpenAIEndpoint" value="${settings.aiProviders.azureOpenAI.Endpoint || settings.aiProviders.azureOpenAI.endpoint || ''}" placeholder="https://your-resource.openai.azure.com">
                    </div>
                    <div class="form-group">
                        <label for="azureOpenAIDeployment">Deployment Name</label>
                        <input type="text" id="azureOpenAIDeployment" value="${settings.aiProviders.azureOpenAI.DeploymentName || settings.aiProviders.azureOpenAI.deploymentName || ''}" placeholder="gpt-4">
                    </div>
                    <div class="form-group">
                        <label for="azureOpenAIRateLimit">Rate Limit/min</label>
                        <input type="number" id="azureOpenAIRateLimit" min="1" max="500" value="${settings.aiProviders.azureOpenAI.RateLimitPerMinute || settings.aiProviders.azureOpenAI.rateLimitPerMinute || ''}" placeholder="60">
                    </div>
                </div>
                <button class="secondary" onclick="testProvider('azureopenai')" style="margin-top: 10px;">Test Connection</button>
            </div>
        </div>

        <!-- Azure Translator -->
        <div class="collapsible" id="azuretranslator-provider-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('azuretranslator-provider-settings')">
                <span>Azure Translator ${settings.credentialSources?.['azuretranslator'] ? '✓' : ''}</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-group">
                    <label for="apiKey_Azure">API Key</label>
                    <input type="password" id="apiKey_Azure" value="" placeholder="Enter new API key to update...">
                    ${getSourceIndicator('azuretranslator')}
                </div>
                <div class="form-row" style="margin-top: 15px;">
                    <div class="form-group">
                        <label for="azureTranslatorRegion">Region</label>
                        <input type="text" id="azureTranslatorRegion" value="${settings.aiProviders.azureTranslator.Region || settings.aiProviders.azureTranslator.region || ''}" placeholder="westus">
                    </div>
                    <div class="form-group">
                        <label for="azureTranslatorEndpoint">Endpoint (optional)</label>
                        <input type="text" id="azureTranslatorEndpoint" value="${settings.aiProviders.azureTranslator.Endpoint || settings.aiProviders.azureTranslator.endpoint || ''}" placeholder="Default: api.cognitive.microsofttranslator.com">
                    </div>
                    <div class="form-group">
                        <label for="azureTranslatorRateLimit">Rate Limit/min</label>
                        <input type="number" id="azureTranslatorRateLimit" min="1" max="1000" value="${settings.aiProviders.azureTranslator.RateLimitPerMinute || settings.aiProviders.azureTranslator.rateLimitPerMinute || ''}" placeholder="100">
                    </div>
                </div>
                <button class="secondary" onclick="testProvider('azuretranslator')" style="margin-top: 10px;">Test Connection</button>
            </div>
        </div>

        <h3 style="margin-top: 25px;">Free Providers</h3>
        <div class="hint" style="margin-bottom: 15px;">No API key required. Click to configure optional settings.</div>

        <!-- Ollama -->
        <div class="collapsible" id="ollama-provider-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('ollama-provider-settings')">
                <span>Ollama (Local AI - Free)</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-row">
                    <div class="form-group">
                        <label for="ollamaApiUrl">API URL</label>
                        <input type="text" id="ollamaApiUrl" value="${settings.aiProviders.ollama.ApiUrl || settings.aiProviders.ollama.apiUrl || ''}" placeholder="http://localhost:11434">
                    </div>
                    <div class="form-group">
                        <label for="ollamaModel">Model</label>
                        <input type="text" id="ollamaModel" list="ollamaModelList" value="${settings.aiProviders.ollama.Model || settings.aiProviders.ollama.model || ''}" placeholder="llama3.2">
                        <datalist id="ollamaModelList">
                            <option value="llama3.2">
                            <option value="llama3.1">
                            <option value="mistral">
                            <option value="phi3">
                            <option value="gemma2">
                            <option value="qwen2.5">
                            <option value="codellama">
                        </datalist>
                    </div>
                    <div class="form-group">
                        <label for="ollamaRateLimit">Rate Limit/min</label>
                        <input type="number" id="ollamaRateLimit" min="1" max="100" value="${settings.aiProviders.ollama.RateLimitPerMinute || settings.aiProviders.ollama.rateLimitPerMinute || ''}" placeholder="10">
                    </div>
                </div>
                <button class="secondary" onclick="testProvider('ollama')" style="margin-top: 10px;">Test Connection</button>
            </div>
        </div>

        <!-- Lingva -->
        <div class="collapsible" id="lingva-provider-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('lingva-provider-settings')">
                <span>Lingva (Free)</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-row">
                    <div class="form-group">
                        <label for="lingvaInstanceUrl">Instance URL</label>
                        <input type="text" id="lingvaInstanceUrl" value="${settings.aiProviders.lingva.InstanceUrl || settings.aiProviders.lingva.instanceUrl || ''}" placeholder="https://lingva.ml">
                    </div>
                    <div class="form-group">
                        <label for="lingvaRateLimit">Rate Limit/min</label>
                        <input type="number" id="lingvaRateLimit" min="1" max="100" value="${settings.aiProviders.lingva.RateLimitPerMinute || settings.aiProviders.lingva.rateLimitPerMinute || ''}" placeholder="30">
                    </div>
                </div>
                <button class="secondary" onclick="testProvider('lingva')" style="margin-top: 10px;">Test Connection</button>
            </div>
        </div>

        <!-- MyMemory -->
        <div class="collapsible" id="mymemory-provider-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('mymemory-provider-settings')">
                <span>MyMemory (Free)</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-row">
                    <div class="form-group">
                        <label for="mymemoryRateLimit">Rate Limit/min</label>
                        <input type="number" id="mymemoryRateLimit" min="1" max="100" value="${settings.aiProviders.myMemory.RateLimitPerMinute || settings.aiProviders.myMemory.rateLimitPerMinute || ''}" placeholder="20">
                        <div class="hint">Free tier: 5,000 chars/day</div>
                    </div>
                </div>
                <button class="secondary" onclick="testProvider('mymemory')" style="margin-top: 10px;">Test Connection</button>
            </div>
        </div>
    </div>

    <!-- Translation Settings -->
    <div class="section">
        <h2>Translation Settings</h2>
        <div class="form-row">
            <div class="form-group">
                <label for="maxRetries">Max Retries</label>
                <input type="number" id="maxRetries" min="0" max="10" value="${settings.maxRetries}">
                <div class="hint">Retry attempts for failed requests</div>
            </div>
            <div class="form-group">
                <label for="timeoutSeconds">Timeout (seconds)</label>
                <input type="number" id="timeoutSeconds" min="5" max="300" value="${settings.timeoutSeconds}">
                <div class="hint">Request timeout</div>
            </div>
            <div class="form-group">
                <label for="batchSize">Batch Size</label>
                <input type="number" id="batchSize" min="1" max="100" value="${settings.batchSize}">
                <div class="hint">Keys per batch request</div>
            </div>
        </div>
    </div>

    <!-- Validation -->
    <div class="section">
        <h2>Validation</h2>

        <div class="checkbox-group">
            <input type="checkbox" id="enablePlaceholderValidation" ${settings.enablePlaceholderValidation ? 'checked' : ''}>
            <label for="enablePlaceholderValidation">Enable placeholder validation</label>
        </div>
        <div class="hint" style="margin-bottom: 15px;">Validates that placeholders match across all language translations</div>

        <div class="form-group">
            <label>Placeholder Types to Validate</label>
            <div class="checkbox-group">
                <input type="checkbox" id="placeholderDotnet" ${settings.placeholderTypes.includes('dotnet') ? 'checked' : ''}>
                <label for="placeholderDotnet">.NET format strings ({0}, {1})</label>
            </div>
            <div class="checkbox-group">
                <input type="checkbox" id="placeholderPrintf" ${settings.placeholderTypes.includes('printf') ? 'checked' : ''}>
                <label for="placeholderPrintf">printf-style (%s, %d)</label>
            </div>
            <div class="checkbox-group">
                <input type="checkbox" id="placeholderIcu" ${settings.placeholderTypes.includes('icu') ? 'checked' : ''}>
                <label for="placeholderIcu">ICU MessageFormat</label>
            </div>
            <div class="checkbox-group">
                <input type="checkbox" id="placeholderTemplate" ${settings.placeholderTypes.includes('template') ? 'checked' : ''}>
                <label for="placeholderTemplate">Template literals (\${name})</label>
            </div>
        </div>
    </div>

    <!-- Code Scanning -->
    <div class="section">
        <h2>Code Scanning</h2>
        <div class="hint" style="margin-bottom: 15px;">
            Resource classes and methods are saved to <strong>lrm.json</strong> (shared across CLI, Web UI, and VS Code).
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

        function toggleCollapsible(id) {
            document.getElementById(id).classList.toggle('open');
        }

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

            // Collect placeholder types
            const placeholderTypes = [];
            if (document.getElementById('placeholderDotnet').checked) placeholderTypes.push('dotnet');
            if (document.getElementById('placeholderPrintf').checked) placeholderTypes.push('printf');
            if (document.getElementById('placeholderIcu').checked) placeholderTypes.push('icu');
            if (document.getElementById('placeholderTemplate').checked) placeholderTypes.push('template');

            vscode.postMessage({
                command: 'saveSettings',
                settings: {
                    // General
                    defaultLanguageCode: document.getElementById('defaultLanguageCode').value.trim(),
                    // Translation
                    translationProvider: document.getElementById('defaultProvider').value,
                    maxRetries: parseInt(document.getElementById('maxRetries').value) || 3,
                    timeoutSeconds: parseInt(document.getElementById('timeoutSeconds').value) || 30,
                    batchSize: parseInt(document.getElementById('batchSize').value) || 10,
                    // Secure Credential Store
                    useSecureCredentialStore: document.getElementById('useSecureCredentialStore').checked,
                    // Validation
                    enablePlaceholderValidation: document.getElementById('enablePlaceholderValidation').checked,
                    placeholderTypes: placeholderTypes.length > 0 ? placeholderTypes : ['dotnet'],
                    // API Keys
                    apiKeys: apiKeys,
                    // AI Provider Settings
                    aiProviders: {
                        ollama: {
                            apiUrl: document.getElementById('ollamaApiUrl').value.trim() || undefined,
                            model: document.getElementById('ollamaModel').value || undefined,
                            rateLimitPerMinute: parseInt(document.getElementById('ollamaRateLimit').value) || undefined
                        },
                        openai: {
                            model: document.getElementById('openaiModel').value || undefined,
                            rateLimitPerMinute: parseInt(document.getElementById('openaiRateLimit').value) || undefined
                        },
                        claude: {
                            model: document.getElementById('claudeModel').value || undefined,
                            rateLimitPerMinute: parseInt(document.getElementById('claudeRateLimit').value) || undefined
                        },
                        azureOpenAI: {
                            endpoint: document.getElementById('azureOpenAIEndpoint').value.trim() || undefined,
                            deploymentName: document.getElementById('azureOpenAIDeployment').value.trim() || undefined,
                            rateLimitPerMinute: parseInt(document.getElementById('azureOpenAIRateLimit').value) || undefined
                        },
                        azureTranslator: {
                            region: document.getElementById('azureTranslatorRegion').value.trim() || undefined,
                            endpoint: document.getElementById('azureTranslatorEndpoint').value.trim() || undefined,
                            rateLimitPerMinute: parseInt(document.getElementById('azureTranslatorRateLimit').value) || undefined
                        },
                        lingva: {
                            instanceUrl: document.getElementById('lingvaInstanceUrl').value.trim() || undefined,
                            rateLimitPerMinute: parseInt(document.getElementById('lingvaRateLimit').value) || undefined
                        },
                        myMemory: {
                            rateLimitPerMinute: parseInt(document.getElementById('mymemoryRateLimit').value) || undefined
                        }
                    },
                    // Scanning
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
    <div class="error-icon">Unable to load</div>
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
