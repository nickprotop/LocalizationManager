import * as vscode from 'vscode';
import { LrmService } from './backend/lrmService';
import { ApiClient } from './backend/apiClient';
import { CacheService } from './backend/cacheService';
import { CodeDiagnosticProvider } from './providers/codeDiagnostics';
import { ResxDiagnosticProvider } from './providers/resxDiagnostics';
import { ResourceTreeView } from './views/resourceTreeView';
import { QuickFixProvider } from './providers/quickFix';
import { LocalizationCompletionProvider } from './providers/completionProvider';
import { ResourceEditorPanel } from './views/resourceEditor';
import { StatusBarManager } from './views/statusBar';
import { DashboardPanel } from './views/dashboard';
import { SettingsPanel } from './views/settingsPanel';
import { LrmCodeLensProvider } from './providers/codeLens';
import { LrmDefinitionProvider } from './providers/definitionProvider';
import { LrmReferenceProvider } from './providers/referenceProvider';

let lrmService: LrmService;
let apiClient: ApiClient;
let cacheService: CacheService;
let statusBarManager: StatusBarManager;
let codeDiagnostics: CodeDiagnosticProvider;
let resxDiagnostics: ResxDiagnosticProvider;
let resourceTreeView: ResourceTreeView;
let completionProvider: LocalizationCompletionProvider;
let codeLensProvider: LrmCodeLensProvider;
let outputChannel: vscode.OutputChannel;

export async function activate(context: vscode.ExtensionContext) {
    // Create output channel for logging
    outputChannel = vscode.window.createOutputChannel('Localization Manager');
    outputChannel.appendLine('=== Localization Manager Extension Activating ===');
    console.log('Localization Manager extension is now active');

    // Initialize LRM service
    lrmService = new LrmService({
        extensionPath: context.extensionPath
    });
    context.subscriptions.push(lrmService);  // Register for automatic disposal

    try {
        // Start backend
        await lrmService.start();

        // Initialize API client
        apiClient = new ApiClient(lrmService.getBaseUrl());

        // Initialize shared cache service
        cacheService = new CacheService(apiClient);

        // Initialize diagnostic providers
        codeDiagnostics = new CodeDiagnosticProvider(apiClient, outputChannel);
        resxDiagnostics = new ResxDiagnosticProvider(apiClient);

        // Initialize tree view
        resourceTreeView = new ResourceTreeView(apiClient);
        const treeView = vscode.window.createTreeView('lrmResourceTree', {
            treeDataProvider: resourceTreeView,
            showCollapseAll: true
        });
        context.subscriptions.push(treeView);

        // Register QuickFix provider
        const quickFixProvider = new QuickFixProvider(apiClient);
        context.subscriptions.push(
            vscode.languages.registerCodeActionsProvider(
                [{ pattern: '**/*.{cs,razor,xaml,cshtml}' }],
                quickFixProvider,
                {
                    providedCodeActionKinds: QuickFixProvider.providedCodeActionKinds
                }
            )
        );

        // Register Completion provider for localization key autocomplete
        completionProvider = new LocalizationCompletionProvider(apiClient);
        context.subscriptions.push(
            vscode.languages.registerCompletionItemProvider(
                [
                    { language: 'csharp', scheme: 'file' },
                    { language: 'razor', scheme: 'file' },
                    { language: 'aspnetcorerazor', scheme: 'file' },
                    { pattern: '**/*.cshtml' },
                    { pattern: '**/*.xaml' }
                ],
                completionProvider,
                '.', '"', "'", '['  // Trigger characters
            )
        );
        outputChannel.appendLine('Completion provider registered');

        // Register CodeLens provider for localization keys
        codeLensProvider = new LrmCodeLensProvider(cacheService);
        context.subscriptions.push(
            // For .resx files (XML-based)
            vscode.languages.registerCodeLensProvider(
                [
                    { language: 'xml', pattern: '**/*.resx' },
                    { pattern: '**/*.resx' }
                ],
                codeLensProvider
            ),
            // For JSON resource files
            vscode.languages.registerCodeLensProvider(
                [
                    { language: 'json', pattern: '**/strings*.json' },
                    { language: 'json', pattern: '**/locales/**/*.json' },
                    { language: 'json', pattern: '**/translations/**/*.json' },
                    { language: 'json', pattern: '**/i18n/**/*.json' },
                    { pattern: '**/strings*.json' },
                    { pattern: '**/locales/**/*.json' },
                    { pattern: '**/translations/**/*.json' },
                    { pattern: '**/i18n/**/*.json' }
                ],
                codeLensProvider
            ),
            // For code files
            vscode.languages.registerCodeLensProvider(
                [
                    { language: 'csharp', scheme: 'file' },
                    { language: 'razor', scheme: 'file' },
                    { language: 'aspnetcorerazor', scheme: 'file' },
                    { pattern: '**/*.cs' },
                    { pattern: '**/*.razor' },
                    { pattern: '**/*.cshtml' },
                    { pattern: '**/*.xaml' }
                ],
                codeLensProvider
            )
        );
        outputChannel.appendLine('CodeLens provider registered');

        // Register Definition Provider (F12 - Go to Definition)
        const definitionProvider = new LrmDefinitionProvider(cacheService);
        context.subscriptions.push(
            vscode.languages.registerDefinitionProvider(
                [
                    { language: 'csharp', scheme: 'file' },
                    { language: 'razor', scheme: 'file' },
                    { language: 'aspnetcorerazor', scheme: 'file' },
                    { pattern: '**/*.cs' },
                    { pattern: '**/*.razor' },
                    { pattern: '**/*.cshtml' },
                    { pattern: '**/*.xaml' },
                    { pattern: '**/*.ts' },
                    { pattern: '**/*.tsx' },
                    { pattern: '**/*.js' },
                    { pattern: '**/*.jsx' }
                ],
                definitionProvider
            )
        );
        outputChannel.appendLine('Definition provider registered');

        // Register Reference Provider (Shift+F12 - Find All References)
        const referenceProvider = new LrmReferenceProvider();
        context.subscriptions.push(
            vscode.languages.registerReferenceProvider(
                [
                    // Code files
                    { language: 'csharp', scheme: 'file' },
                    { language: 'razor', scheme: 'file' },
                    { language: 'aspnetcorerazor', scheme: 'file' },
                    { pattern: '**/*.cs' },
                    { pattern: '**/*.razor' },
                    { pattern: '**/*.cshtml' },
                    { pattern: '**/*.xaml' },
                    { pattern: '**/*.ts' },
                    { pattern: '**/*.tsx' },
                    { pattern: '**/*.js' },
                    { pattern: '**/*.jsx' },
                    // Resource files
                    { language: 'xml', pattern: '**/*.resx' },
                    { pattern: '**/*.resx' },
                    { language: 'json', pattern: '**/strings*.json' },
                    { language: 'json', pattern: '**/locales/**/*.json' },
                    { language: 'json', pattern: '**/translations/**/*.json' },
                    { language: 'json', pattern: '**/i18n/**/*.json' }
                ],
                referenceProvider
            )
        );
        outputChannel.appendLine('Reference provider registered');

        // Enable diagnostic providers based on settings
        const config = vscode.workspace.getConfiguration('lrm');
        const enableRealtimeScan = config.get<boolean>('enableRealtimeScan', true); // Now default true
        const scanOnSave = config.get<boolean>('scanOnSave', true);

        outputChannel.appendLine(`Settings: enableRealtimeScan=${enableRealtimeScan}, scanOnSave=${scanOnSave}`);

        // Enable code diagnostics if either real-time or on-save is enabled
        if (enableRealtimeScan || scanOnSave) {
            codeDiagnostics.enable();
            outputChannel.appendLine('Code diagnostics enabled');
        } else {
            outputChannel.appendLine('Code diagnostics DISABLED (check settings)');
        }

        // Always enable .resx diagnostics
        resxDiagnostics.enable();

        // Load resources into tree view
        await resourceTreeView.loadResources();

        // Validate all .resx files
        await resxDiagnostics.validateAllResources();

        // Initialize status bar manager
        statusBarManager = new StatusBarManager(apiClient, lrmService);
        context.subscriptions.push(statusBarManager);

        // Set up document event listeners
        setupEventListeners(context, enableRealtimeScan, scanOnSave);

        // Listen for configuration changes (e.g., resource path changed in settings)
        context.subscriptions.push(
            vscode.workspace.onDidChangeConfiguration(async (e) => {
                if (e.affectsConfiguration('lrm.resourcePath')) {
                    const newConfig = vscode.workspace.getConfiguration('lrm');
                    const newPath = newConfig.get<string>('resourcePath');
                    if (newPath && lrmService.isRunning()) {
                        try {
                            outputChannel.appendLine(`Resource path changed in settings: ${newPath}`);
                            await lrmService.setResourcePath(newPath);

                            // Recreate API client and cache service with new port
                            apiClient = new ApiClient(lrmService.getBaseUrl());
                            cacheService = new CacheService(apiClient);

                            // Refresh views
                            await resourceTreeView.loadResources();
                            await resxDiagnostics.validateAllResources();
                            if (statusBarManager) {
                                await statusBarManager.update();
                            }

                            vscode.window.showInformationMessage(`Resource path updated to: ${newPath}`);
                        } catch (error: any) {
                            vscode.window.showErrorMessage(`Failed to update resource path: ${error.message}`);
                        }
                    }
                }
            })
        );

        // Set context for views
        vscode.commands.executeCommand('setContext', 'lrm.hasResources', true);

    } catch (error: any) {
        const errorMessage = `Failed to start LRM backend: ${error.message}`;

        vscode.window.showErrorMessage(
            errorMessage,
            'Show Logs',
            'Retry'
        ).then(choice => {
            if (choice === 'Show Logs' && lrmService) {
                lrmService.outputChannel.show();
            } else if (choice === 'Retry') {
                vscode.commands.executeCommand('lrm.restartBackend');
            }
        });

        // Don't block extension activation completely
        console.error('LRM activation error:', error);
        console.error('Error stack:', error.stack);
    }

    // Register commands
    registerCommands(context);
}

function setupEventListeners(context: vscode.ExtensionContext, enableRealtimeScan: boolean, scanOnSave: boolean) {
    // Listen for document changes (real-time scanning)
    if (enableRealtimeScan) {
        outputChannel.appendLine('Setting up realtime scan listener');
        context.subscriptions.push(
            vscode.workspace.onDidChangeTextDocument(event => {
                // Only process actual file documents (not output, git, etc.)
                if (event.document.uri.scheme !== 'file') {
                    return;
                }

                // Only process if there were actual content changes
                if (event.contentChanges.length === 0) {
                    return;
                }

                const fileName = event.document.fileName;
                const ext = fileName.substring(fileName.lastIndexOf('.'));
                outputChannel.appendLine(`Document changed: ${fileName} (extension: ${ext})`);

                // Check if supported before debouncing
                const supportedExtensions = ['.cs', '.razor', '.xaml', '.cshtml'];
                if (!supportedExtensions.includes(ext)) {
                    outputChannel.appendLine(`  → Skipped: Unsupported file type`);
                    return;
                }

                outputChannel.appendLine(`  → Debouncing scan (500ms)...`);
                codeDiagnostics.debounce(event.document, 500);
            })
        );
    } else {
        outputChannel.appendLine('Realtime scan listener NOT set up (disabled in settings)');
    }

    // Listen for document save (scan on save)
    if (scanOnSave) {
        context.subscriptions.push(
            vscode.workspace.onDidSaveTextDocument(document => {
                codeDiagnostics.scanDocument(document);
            })
        );
    }

    // Listen for active editor changes
    context.subscriptions.push(
        vscode.window.onDidChangeActiveTextEditor(editor => {
            if (editor && (enableRealtimeScan || scanOnSave)) {
                outputChannel.appendLine(`Active editor changed: ${editor.document.fileName}`);
                codeDiagnostics.scanDocument(editor.document);
            }
        })
    );

    outputChannel.appendLine('=== Extension activation complete ===');

    // Listen for resource file changes (.resx and JSON)
    const resxWatcher = vscode.workspace.createFileSystemWatcher('**/*.resx');
    const jsonWatcher = vscode.workspace.createFileSystemWatcher('**/*.json');
    const lrmConfigWatcher = vscode.workspace.createFileSystemWatcher('**/lrm.json');

    // Helper to handle resource file changes
    const handleResourceChange = async () => {
        // Invalidate shared cache first
        if (cacheService) {
            cacheService.invalidate();
        }
        await resxDiagnostics.validateAllResources();
        await resourceTreeView.loadResources();
        if (completionProvider) {
            completionProvider.invalidateCache();
        }
        if (codeLensProvider) {
            codeLensProvider.refresh();
        }
    };

    // Helper to filter JSON events to only resource files (exclude config files)
    const isResourceJson = (uri: vscode.Uri): boolean => {
        const fileName = uri.fsPath.toLowerCase();
        // Exclude common config files
        if (fileName.endsWith('lrm.json') ||
            fileName.endsWith('package.json') ||
            fileName.endsWith('package-lock.json') ||
            fileName.endsWith('tsconfig.json') ||
            fileName.includes('.vscode') ||
            fileName.includes('node_modules')) {
            return false;
        }
        return true;
    };

    // RESX file watchers
    resxWatcher.onDidChange(handleResourceChange);
    resxWatcher.onDidCreate(handleResourceChange);
    resxWatcher.onDidDelete(handleResourceChange);

    // JSON file watchers (filtered to resource files only)
    jsonWatcher.onDidChange(async (uri) => {
        if (isResourceJson(uri)) {
            await handleResourceChange();
        }
    });
    jsonWatcher.onDidCreate(async (uri) => {
        if (isResourceJson(uri)) {
            await handleResourceChange();
        }
    });
    jsonWatcher.onDidDelete(async (uri) => {
        if (isResourceJson(uri)) {
            await handleResourceChange();
        }
    });

    // lrm.json config file watcher - triggers full refresh when format config changes
    lrmConfigWatcher.onDidChange(async () => {
        outputChannel.appendLine('lrm.json config changed, refreshing...');
        // Invalidate everything when config changes
        if (cacheService) {
            cacheService.invalidate();
        }
        await resxDiagnostics.validateAllResources();
        await resourceTreeView.loadResources();
        if (completionProvider) {
            completionProvider.invalidateCache();
        }
        if (codeLensProvider) {
            codeLensProvider.refresh();
        }
    });
    lrmConfigWatcher.onDidCreate(async () => {
        outputChannel.appendLine('lrm.json config created, refreshing...');
        await handleResourceChange();
    });
    lrmConfigWatcher.onDidDelete(async () => {
        outputChannel.appendLine('lrm.json config deleted, refreshing...');
        await handleResourceChange();
    });

    context.subscriptions.push(resxWatcher, jsonWatcher, lrmConfigWatcher);

    // Scan visible editors on activation
    vscode.window.visibleTextEditors.forEach(editor => {
        if (enableRealtimeScan || scanOnSave) {
            codeDiagnostics.scanDocument(editor.document);
        }
    });
}

function registerCommands(context: vscode.ExtensionContext) {
    // Scan Code
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.scanCode', async () => {
            try {
                const result = await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: 'Scanning code for localization keys...',
                    cancellable: false
                }, async () => {
                    return await apiClient.scanCode();
                });

                const message = `Scanned ${result.scannedFiles} files. Found ${result.missingKeysCount} missing keys and ${result.unusedKeysCount} unused keys.`;

                if (result.missingKeysCount > 0 || result.unusedKeysCount > 0) {
                    const choice = await vscode.window.showWarningMessage(
                        message,
                        'Show Details'
                    );

                    if (choice === 'Show Details') {
                        // TODO: Open scan results in a webview or output channel
                        const channel = vscode.window.createOutputChannel('LRM Scan Results');
                        channel.clear();
                        channel.appendLine('=== Scan Results ===\n');
                        channel.appendLine(`Scanned: ${result.scannedFiles} files`);
                        channel.appendLine(`References: ${result.totalReferences} (${result.uniqueKeysFound} unique keys)`);
                        channel.appendLine('');

                        if (result.missingKeysCount > 0) {
                            channel.appendLine(`Missing Keys (${result.missingKeysCount}):`);
                            result.missing.forEach(key => {
                                channel.appendLine(`  - ${key}`);
                            });
                            channel.appendLine('');
                        }

                        if (result.unusedKeysCount > 0) {
                            channel.appendLine(`Unused Keys (${result.unusedKeysCount}):`);
                            result.unused.slice(0, 50).forEach(key => {
                                channel.appendLine(`  - ${key}`);
                            });
                            if (result.unused.length > 50) {
                                channel.appendLine(`  ... and ${result.unused.length - 50} more`);
                            }
                        }

                        channel.show();
                    }
                } else {
                    vscode.window.showInformationMessage(message);
                }
            } catch (error: any) {
                vscode.window.showErrorMessage(`Scan failed: ${error.message}`);
            }
        })
    );

    // Validate Resources
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.validateResources', async () => {
            try {
                const result = await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: 'Validating resources...',
                    cancellable: false
                }, async () => {
                    return await apiClient.validate();
                });

                if (result.isValid) {
                    vscode.window.showInformationMessage('All resources are valid!');
                } else {
                    const missingCount = Object.values(result.missingKeys).reduce((sum, keys) => sum + keys.length, 0);
                    const extraCount = Object.values(result.extraKeys).reduce((sum, keys) => sum + keys.length, 0);
                    const duplicateCount = result.duplicateKeys.length;

                    vscode.window.showWarningMessage(
                        `Validation found issues: ${missingCount} missing, ${extraCount} extra, ${duplicateCount} duplicates`
                    );
                }
            } catch (error: any) {
                vscode.window.showErrorMessage(`Validation failed: ${error.message}`);
            }
        })
    );

    // Open Resource Editor
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.openResourceEditor', async () => {
            ResourceEditorPanel.createOrShow(context.extensionUri, apiClient, cacheService);
        })
    );

    // Add Key
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.addKey', async () => {
            const keyName = await vscode.window.showInputBox({
                prompt: 'Enter the key name',
                placeHolder: 'MyNewKey'
            });

            if (!keyName) {
                return;
            }

            const defaultValue = await vscode.window.showInputBox({
                prompt: 'Enter the default value',
                placeHolder: 'Default text'
            });

            if (!defaultValue) {
                return;
            }

            try {
                await apiClient.addKey({
                    key: keyName,
                    values: { default: defaultValue }
                });

                vscode.window.showInformationMessage(`Key '${keyName}' added successfully`);
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to add key: ${error.message}`);
            }
        })
    );

    // Translate Missing
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.translateMissing', async () => {
            try {
                // Get available providers
                const providers = await apiClient.getTranslationProviders();
                const configuredProviders = providers.filter(p => p.isConfigured);

                if (configuredProviders.length === 0) {
                    vscode.window.showWarningMessage('No translation providers configured');
                    return;
                }

                // Select provider
                const provider = await vscode.window.showQuickPick(
                    configuredProviders.map(p => ({ label: p.displayName, value: p.name })),
                    { placeHolder: 'Select translation provider' }
                );

                if (!provider) {
                    return;
                }

                // Get languages
                const languages = await apiClient.getLanguages();
                const targetLanguages = languages.filter(l => !l.isDefault).map(l => l.code);

                if (targetLanguages.length === 0) {
                    vscode.window.showWarningMessage('No target languages found');
                    return;
                }

                // Translate
                await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: 'Translating missing values...',
                    cancellable: false
                }, async () => {
                    await apiClient.translateAll({
                        provider: provider.value,
                        targetLanguages,
                        onlyMissing: true,
                        dryRun: false
                    });
                });

                vscode.window.showInformationMessage('Translation completed!');
            } catch (error: any) {
                vscode.window.showErrorMessage(`Translation failed: ${error.message}`);
            }
        })
    );

    // Refresh Resource Tree
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.refreshResourceTree', async () => {
            try {
                await resourceTreeView.loadResources();
                await resxDiagnostics.validateAllResources();
                vscode.window.showInformationMessage('Resource tree refreshed');
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to refresh: ${error.message}`);
            }
        })
    );

    // View Key Details
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.viewKeyDetails', async (keyName: string) => {
            try {
                const details = await apiClient.getKeyDetails(keyName);

                const message = Object.entries(details.values)
                    .map(([lang, data]) => `${lang}: ${data.value}`)
                    .join('\n');

                vscode.window.showInformationMessage(
                    `Key: ${keyName}\n\n${message}`,
                    { modal: false }
                );
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to get key details: ${error.message}`);
            }
        })
    );

    // Quick Fix: Add Key
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.addKeyQuickFix', async (keyName: string, defaultValue: string) => {
            try {
                await apiClient.addKey({
                    key: keyName,
                    values: { default: defaultValue }
                });

                vscode.window.showInformationMessage(`Key '${keyName}' added`);

                // Refresh diagnostics and tree view
                await resourceTreeView.loadResources();
                await resxDiagnostics.validateAllResources();

                // Re-scan current document
                const editor = vscode.window.activeTextEditor;
                if (editor) {
                    await codeDiagnostics.scanDocument(editor.document);
                }
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to add key: ${error.message}`);
            }
        })
    );

    // Quick Fix: Add Key with Custom Value
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.addKeyWithValueQuickFix', async (keyName: string) => {
            const value = await vscode.window.showInputBox({
                prompt: `Enter value for key '${keyName}'`,
                placeHolder: 'Enter the default value'
            });

            if (!value) {
                return;
            }

            try {
                await apiClient.addKey({
                    key: keyName,
                    values: { default: value }
                });

                vscode.window.showInformationMessage(`Key '${keyName}' added with value '${value}'`);

                // Refresh diagnostics and tree view
                await resourceTreeView.loadResources();
                await resxDiagnostics.validateAllResources();

                // Re-scan current document
                const editor = vscode.window.activeTextEditor;
                if (editor) {
                    await codeDiagnostics.scanDocument(editor.document);
                }
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to add key: ${error.message}`);
            }
        })
    );

    // Quick Fix: Merge Duplicate Key
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.mergeDuplicateKey', async (keyName: string) => {
            const choice = await vscode.window.showQuickPick(
                ['Keep first occurrence', 'Keep last occurrence'],
                { placeHolder: `Which occurrence of '${keyName}' should be kept?` }
            );

            if (!choice) {
                return;
            }

            try {
                await apiClient.mergeDuplicates(keyName);

                vscode.window.showInformationMessage(`Merged duplicates of '${keyName}'`);

                // Refresh diagnostics
                await resxDiagnostics.validateAllResources();
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to merge duplicates: ${error.message}`);
            }
        })
    );

    // Quick Fix: Translate Key
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.translateKeyQuickFix', async (keyName: string) => {
            try {
                const config = vscode.workspace.getConfiguration('lrm');
                const provider = config.get<string>('translationProvider', 'mymemory');

                const languages = await apiClient.getLanguages();
                const targetLanguages = languages.filter(l => !l.isDefault).map(l => l.code);

                await apiClient.translate({
                    keys: [keyName],
                    provider,
                    targetLanguages,
                    onlyMissing: true
                });

                vscode.window.showInformationMessage(`Translated '${keyName}'`);

                // Refresh diagnostics
                await resxDiagnostics.validateAllResources();
            } catch (error: any) {
                vscode.window.showErrorMessage(`Translation failed: ${error.message}`);
            }
        })
    );

    // Find Unused Keys
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.findUnusedKeys', async () => {
            try {
                const result = await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: 'Finding unused keys...',
                    cancellable: false
                }, async () => {
                    return await apiClient.getUnusedKeys();
                });

                if (result.length === 0) {
                    vscode.window.showInformationMessage('No unused keys found!');
                } else {
                    const choice = await vscode.window.showWarningMessage(
                        `Found ${result.length} unused keys`,
                        'Show in Editor',
                        'Delete All',
                        'Cancel'
                    );

                    if (choice === 'Show in Editor') {
                        // Open in output channel
                        const channel = vscode.window.createOutputChannel('LRM Unused Keys');
                        channel.clear();
                        channel.appendLine('=== Unused Keys ===\n');
                        channel.appendLine(`Total: ${result.length}\n`);
                        result.forEach(key => {
                            channel.appendLine(`  - ${key}`);
                        });
                        channel.show();
                    } else if (choice === 'Delete All') {
                        const confirm = await vscode.window.showWarningMessage(
                            `This will delete ${result.length} keys. Are you sure?`,
                            { modal: true },
                            'Delete'
                        );

                        if (confirm === 'Delete') {
                            let deleted = 0;
                            for (const key of result) {
                                try {
                                    await apiClient.deleteKey(key);
                                    deleted++;
                                } catch (error) {
                                    console.error(`Failed to delete ${key}:`, error);
                                }
                            }

                            vscode.window.showInformationMessage(`Deleted ${deleted} of ${result.length} unused keys`);

                            // Refresh views
                            await resourceTreeView.loadResources();
                            await resxDiagnostics.validateAllResources();
                        }
                    }
                }
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to find unused keys: ${error.message}`);
            }
        })
    );

    // Export Resources
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.exportResources', async () => {
            const format = await vscode.window.showQuickPick(
                ['CSV', 'JSON'],
                { placeHolder: 'Select export format' }
            );

            if (!format) {
                return;
            }

            try {
                const data = format === 'CSV'
                    ? await apiClient.exportCsv()
                    : JSON.stringify(await apiClient.exportJson(), null, 2);

                const uri = await vscode.window.showSaveDialog({
                    defaultUri: vscode.Uri.file(`resources.${format.toLowerCase()}`),
                    filters: {
                        [format]: [format.toLowerCase()]
                    }
                });

                if (uri) {
                    await vscode.workspace.fs.writeFile(uri, Buffer.from(data, 'utf-8'));
                    vscode.window.showInformationMessage(`Exported to ${uri.fsPath}`);
                }
            } catch (error: any) {
                vscode.window.showErrorMessage(`Export failed: ${error.message}`);
            }
        })
    );

    // Import Resources
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.importResources', async () => {
            const uri = await vscode.window.showOpenDialog({
                canSelectFiles: true,
                canSelectFolders: false,
                canSelectMany: false,
                filters: {
                    'CSV': ['csv']
                },
                openLabel: 'Import'
            });

            if (!uri || uri.length === 0) {
                return;
            }

            try {
                const fileData = await vscode.workspace.fs.readFile(uri[0]);
                const csvData = new TextDecoder().decode(fileData);

                await apiClient.importCsv(csvData);

                vscode.window.showInformationMessage('Import completed successfully');

                // Refresh views
                await resourceTreeView.loadResources();
                await resxDiagnostics.validateAllResources();
            } catch (error: any) {
                vscode.window.showErrorMessage(`Import failed: ${error.message}`);
            }
        })
    );

    // Set Resource Path
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.setResourcePath', async () => {
            const uri = await vscode.window.showOpenDialog({
                canSelectFiles: false,
                canSelectFolders: true,
                canSelectMany: false,
                openLabel: 'Select Resource Folder'
            });

            if (uri && uri.length > 0) {
                const resourcePath = uri[0].fsPath;

                try {
                    await lrmService.setResourcePath(resourcePath);
                    vscode.window.showInformationMessage(`Resource path set to: ${resourcePath}`);
                } catch (error: any) {
                    vscode.window.showErrorMessage(`Failed to set resource path: ${error.message}`);
                }
            }
        })
    );

    // Restart Backend
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.restartBackend', async () => {
            try {
                await lrmService.restart();

                // Recreate API client and cache service
                apiClient = new ApiClient(lrmService.getBaseUrl());
                cacheService = new CacheService(apiClient);

                // Update status bar
                if (statusBarManager) {
                    await statusBarManager.update();
                }

                vscode.window.showInformationMessage('LRM backend restarted successfully');
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to restart backend: ${error.message}`);
            }
        })
    );

    // Show Backend Logs
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.showLogs', () => {
            lrmService.outputChannel.show();
        })
    );

    // Show Resource Tree (for debugging)
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.showResourceTree', async () => {
            await vscode.commands.executeCommand('workbench.view.extension.lrmResourceTree');
        })
    );

    // Open Dashboard
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.openDashboard', () => {
            DashboardPanel.createOrShow(apiClient);
        })
    );

    // Open Settings
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.openSettings', () => {
            SettingsPanel.createOrShow(apiClient);
        })
    );

    // Show Quick Actions (status bar click)
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.showQuickActions', async () => {
            interface QuickActionItem extends vscode.QuickPickItem {
                action: string;
            }

            const resourcePath = lrmService.getResourcePath() || 'Not configured';
            const isRunning = lrmService.isRunning();

            const items: QuickActionItem[] = [
                {
                    label: '$(folder) Resource Folder',
                    description: resourcePath,
                    detail: 'Change the resource folder location',
                    action: 'setPath'
                },
                {
                    label: isRunning ? '$(refresh) Restart Backend' : '$(play) Start Backend',
                    detail: isRunning ? 'Restart the LRM backend service' : 'Start the LRM backend service',
                    action: 'restart'
                },
                {
                    label: '$(gear) Open Settings',
                    detail: 'Configure translation providers and API keys',
                    action: 'settings'
                },
                {
                    label: '$(dashboard) Open Dashboard',
                    detail: 'View translation coverage and statistics',
                    action: 'dashboard'
                },
                {
                    label: '$(edit) Open Resource Editor',
                    detail: 'Edit localization resources',
                    action: 'editor'
                },
                {
                    label: '$(output) Show Logs',
                    detail: 'View backend output and logs',
                    action: 'logs'
                },
                {
                    label: '$(settings-gear) Open Workspace Settings',
                    detail: 'Open VS Code settings for LRM extension',
                    action: 'workspaceSettings'
                }
            ];

            const selected = await vscode.window.showQuickPick(items, {
                placeHolder: 'LRM Quick Actions',
                matchOnDescription: true,
                matchOnDetail: true
            });

            if (selected) {
                switch (selected.action) {
                    case 'setPath':
                        vscode.commands.executeCommand('lrm.setResourcePath');
                        break;
                    case 'restart':
                        vscode.commands.executeCommand('lrm.restartBackend');
                        break;
                    case 'settings':
                        vscode.commands.executeCommand('lrm.openSettings');
                        break;
                    case 'dashboard':
                        vscode.commands.executeCommand('lrm.openDashboard');
                        break;
                    case 'editor':
                        vscode.commands.executeCommand('lrm.openResourceEditor');
                        break;
                    case 'logs':
                        vscode.commands.executeCommand('lrm.showLogs');
                        break;
                    case 'workspaceSettings':
                        vscode.commands.executeCommand('workbench.action.openSettings', '@ext:nickprotop.localization-manager');
                        break;
                }
            }
        })
    );

    // CodeLens Commands

    // Show Key References
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.showKeyReferences', async (keyName: string) => {
            try {
                const usage = await cacheService.getKeyReferences(keyName);

                if (usage.referenceCount === 0) {
                    vscode.window.showInformationMessage(`Key '${keyName}' has no references in code.`);
                    return;
                }

                // Create output channel to show references
                const channel = vscode.window.createOutputChannel('LRM Key References');
                channel.clear();
                channel.appendLine(`=== References for '${keyName}' ===\n`);
                channel.appendLine(`Total: ${usage.referenceCount} references\n`);

                for (const ref of usage.references) {
                    channel.appendLine(`${ref.file}:${ref.line}`);
                    channel.appendLine(`  Pattern: ${ref.pattern}`);
                    channel.appendLine(`  Confidence: ${ref.confidence}`);
                    if (ref.warning) {
                        channel.appendLine(`  Warning: ${ref.warning}`);
                    }
                    channel.appendLine('');
                }

                channel.show();
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to get references: ${error.message}`);
            }
        })
    );

    // Show Missing Languages
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.showMissingLanguages', async (keyName: string) => {
            try {
                const details = await cacheService.getKeyDetails(keyName);

                const filled: string[] = [];
                const missing: string[] = [];

                for (const [lang, data] of Object.entries(details.values)) {
                    if (data.value && data.value.trim() !== '') {
                        filled.push(lang || 'default');
                    } else {
                        missing.push(lang || 'default');
                    }
                }

                let message = `Key: ${keyName}\n\nFilled: ${filled.join(', ')}`;
                if (missing.length > 0) {
                    message += `\nMissing: ${missing.join(', ')}`;
                }

                const choice = await vscode.window.showInformationMessage(
                    message,
                    { modal: false },
                    'Translate Missing'
                );

                if (choice === 'Translate Missing') {
                    vscode.commands.executeCommand('lrm.translateKeyFromLens', keyName);
                }
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to get key details: ${error.message}`);
            }
        })
    );

    // Translate Key from CodeLens
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.translateKeyFromLens', async (keyName: string) => {
            try {
                const config = vscode.workspace.getConfiguration('lrm');
                const provider = config.get<string>('translationProvider', 'mymemory');

                const languages = await apiClient.getLanguages();
                const targetLanguages = languages.filter(l => !l.isDefault).map(l => l.code);

                if (targetLanguages.length === 0) {
                    vscode.window.showWarningMessage('No target languages found');
                    return;
                }

                await vscode.window.withProgress({
                    location: vscode.ProgressLocation.Notification,
                    title: `Translating '${keyName}'...`,
                    cancellable: false
                }, async () => {
                    await apiClient.translate({
                        keys: [keyName],
                        provider,
                        targetLanguages,
                        onlyMissing: true
                    });
                });

                vscode.window.showInformationMessage(`Translated '${keyName}'`);

                // Invalidate cache and refresh
                cacheService.invalidateKey(keyName);
                if (codeLensProvider) {
                    codeLensProvider.refresh();
                }
                await resxDiagnostics.validateAllResources();
            } catch (error: any) {
                vscode.window.showErrorMessage(`Translation failed: ${error.message}`);
            }
        })
    );

    // Edit Key from CodeLens - opens Resource Editor with key selected and translate dialog
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.editKeyFromLens', async (keyName: string) => {
            // Open Resource Editor with this key selected and translate dialog open
            ResourceEditorPanel.createOrShow(context.extensionUri, apiClient, cacheService, {
                selectKey: keyName,
                openTranslate: true
            });
        })
    );

    // Delete Unused Key from CodeLens
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.deleteUnusedKey', async (keyName: string) => {
            const confirm = await vscode.window.showWarningMessage(
                `Delete unused key '${keyName}'?`,
                { modal: true },
                'Delete'
            );

            if (confirm !== 'Delete') {
                return;
            }

            try {
                await apiClient.deleteKey(keyName);
                vscode.window.showInformationMessage(`Deleted '${keyName}'`);

                // Invalidate cache and refresh
                cacheService.invalidate();
                if (codeLensProvider) {
                    codeLensProvider.refresh();
                }
                await resourceTreeView.loadResources();
                await resxDiagnostics.validateAllResources();
            } catch (error: any) {
                vscode.window.showErrorMessage(`Failed to delete key: ${error.message}`);
            }
        })
    );
}

/**
 * Get the shared cache service instance (for use by providers)
 */
export function getCacheService(): CacheService | undefined {
    return cacheService;
}

/**
 * Get the API client instance (for use by providers)
 */
export function getApiClient(): ApiClient | undefined {
    return apiClient;
}

export function deactivate() {
    // Clean up diagnostic providers
    if (codeDiagnostics) {
        codeDiagnostics.dispose();
    }

    if (resxDiagnostics) {
        resxDiagnostics.dispose();
    }

    // Synchronous kill - VS Code doesn't wait for async deactivate
    if (lrmService) {
        lrmService.dispose();
    }
}
