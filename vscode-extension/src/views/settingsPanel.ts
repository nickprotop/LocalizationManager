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
            // Build complete lrm.json configuration
            const lrmConfig: any = {
                DefaultLanguageCode: settings.defaultLanguageCode || undefined,
                Translation: {
                    DefaultProvider: settings.translationProvider,
                    MaxRetries: settings.maxRetries,
                    TimeoutSeconds: settings.timeoutSeconds,
                    BatchSize: settings.batchSize,
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

            // Add API keys if provided
            if (settings.apiKeys && Object.keys(settings.apiKeys).length > 0) {
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

            const vscodeConfig = vscode.workspace.getConfiguration('lrm');

            const settings = {
                // General
                defaultLanguageCode: lrmConfig?.DefaultLanguageCode || lrmConfig?.defaultLanguageCode || '',
                // Translation
                translationProvider: lrmConfig?.Translation?.DefaultProvider || vscodeConfig.get('translationProvider', 'lingva'),
                maxRetries: lrmConfig?.Translation?.MaxRetries ?? 3,
                timeoutSeconds: lrmConfig?.Translation?.TimeoutSeconds ?? 30,
                batchSize: lrmConfig?.Translation?.BatchSize ?? 10,
                // Scanning
                resourceClasses: lrmConfig?.Scanning?.ResourceClassNames || vscodeConfig.get('resourceClasses', ['Resources', 'Strings', 'AppResources']),
                localizationMethods: lrmConfig?.Scanning?.LocalizationMethods || vscodeConfig.get('localizationMethods', ['GetString', 'GetLocalizedString', 'Translate', 'L', 'T']),
                // Validation
                enablePlaceholderValidation: lrmConfig?.Validation?.EnablePlaceholderValidation ?? vscodeConfig.get('enablePlaceholderValidation', true),
                placeholderTypes: lrmConfig?.Validation?.PlaceholderTypes || ['dotnet'],
                // VS Code specific
                scanCSharp: vscodeConfig.get('scanCSharp', true),
                scanRazor: vscodeConfig.get('scanRazor', true),
                scanXaml: vscodeConfig.get('scanXaml', true),
                // API Keys
                apiKeys: lrmConfig?.Translation?.ApiKeys || {},
                // AI Provider Settings
                aiProviders: {
                    ollama: lrmConfig?.Translation?.AIProviders?.Ollama || {},
                    openai: lrmConfig?.Translation?.AIProviders?.OpenAI || {},
                    claude: lrmConfig?.Translation?.AIProviders?.Claude || {},
                    azureOpenAI: lrmConfig?.Translation?.AIProviders?.AzureOpenAI || {},
                    azureTranslator: lrmConfig?.Translation?.AIProviders?.AzureTranslator || {},
                    lingva: lrmConfig?.Translation?.AIProviders?.Lingva || {},
                    myMemory: lrmConfig?.Translation?.AIProviders?.MyMemory || {}
                }
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
                            ${isConfigured ? 'Configured' : 'Requires API key'}
                        </div>
                        <div class="form-group" style="margin-top: 10px; margin-bottom: 0;">
                            <input type="password"
                                   id="apiKey_${providerKey}"
                                   placeholder="Enter API key..."
                                   value="${currentKey}"
                                   style="max-width: 400px;">
                        </div>
                    </div>
                    <button class="secondary" onclick="testProvider('${p.name}')">Test</button>
                </li>
            `;
            }).join('')}
        </ul>
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

    <!-- AI Provider Settings -->
    <div class="section">
        <h2>AI Provider Settings</h2>
        <div class="hint" style="margin-bottom: 15px;">Configure AI-specific settings for each provider. Click to expand.</div>

        <!-- Ollama -->
        <div class="collapsible" id="ollama-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('ollama-settings')">
                <span>Ollama (Local AI)</span>
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
            </div>
        </div>

        <!-- OpenAI -->
        <div class="collapsible" id="openai-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('openai-settings')">
                <span>OpenAI</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-row">
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
            </div>
        </div>

        <!-- Claude -->
        <div class="collapsible" id="claude-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('claude-settings')">
                <span>Claude (Anthropic)</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-row">
                    <div class="form-group">
                        <label for="claudeModel">Model</label>
                        <input type="text" id="claudeModel" list="claudeModelList" value="${settings.aiProviders.claude.Model || settings.aiProviders.claude.model || ''}" placeholder="claude-3-5-sonnet-20241022">
                        <datalist id="claudeModelList">
                            <option value="claude-3-5-sonnet-20241022">
                            <option value="claude-3-5-haiku-20241022">
                            <option value="claude-3-opus-20240229">
                            <option value="claude-3-sonnet-20240229">
                            <option value="claude-3-haiku-20240307">
                        </datalist>
                    </div>
                    <div class="form-group">
                        <label for="claudeRateLimit">Rate Limit/min</label>
                        <input type="number" id="claudeRateLimit" min="1" max="500" value="${settings.aiProviders.claude.RateLimitPerMinute || settings.aiProviders.claude.rateLimitPerMinute || ''}" placeholder="50">
                    </div>
                </div>
            </div>
        </div>

        <!-- Azure OpenAI -->
        <div class="collapsible" id="azureopenai-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('azureopenai-settings')">
                <span>Azure OpenAI</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-row">
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
            </div>
        </div>

        <!-- Azure Translator -->
        <div class="collapsible" id="azuretranslator-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('azuretranslator-settings')">
                <span>Azure Translator</span>
                <span class="arrow">&#9654;</span>
            </div>
            <div class="collapsible-content">
                <div class="form-row">
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
            </div>
        </div>

        <!-- Lingva -->
        <div class="collapsible" id="lingva-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('lingva-settings')">
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
            </div>
        </div>

        <!-- MyMemory -->
        <div class="collapsible" id="mymemory-settings">
            <div class="collapsible-header" onclick="toggleCollapsible('mymemory-settings')">
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
