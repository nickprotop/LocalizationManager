import * as vscode from 'vscode';
import { LrmService } from './backend/lrmService';
import { ApiClient } from './backend/apiClient';
import { CodeDiagnosticProvider } from './providers/codeDiagnostics';
import { ResxDiagnosticProvider } from './providers/resxDiagnostics';
import { ResourceTreeView } from './views/resourceTreeView';
import { QuickFixProvider } from './providers/quickFix';
import { ResourceEditorPanel } from './views/resourceEditor';
import { StatusBarManager } from './views/statusBar';
import { DashboardPanel } from './views/dashboard';
import { SettingsPanel } from './views/settingsPanel';

let lrmService: LrmService;
let apiClient: ApiClient;
let statusBarManager: StatusBarManager;
let codeDiagnostics: CodeDiagnosticProvider;
let resxDiagnostics: ResxDiagnosticProvider;
let resourceTreeView: ResourceTreeView;
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
        statusBarManager = new StatusBarManager(apiClient);
        context.subscriptions.push(statusBarManager);

        // Set up document event listeners
        setupEventListeners(context, enableRealtimeScan, scanOnSave);

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

    // Listen for .resx file changes
    const resxWatcher = vscode.workspace.createFileSystemWatcher('**/*.resx');

    resxWatcher.onDidChange(async () => {
        await resxDiagnostics.validateAllResources();
        await resourceTreeView.loadResources();
    });

    resxWatcher.onDidCreate(async () => {
        await resxDiagnostics.validateAllResources();
        await resourceTreeView.loadResources();
    });

    resxWatcher.onDidDelete(async () => {
        await resxDiagnostics.validateAllResources();
        await resourceTreeView.loadResources();
    });

    context.subscriptions.push(resxWatcher);

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
            ResourceEditorPanel.createOrShow(context.extensionUri, apiClient);
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
                const provider = config.get<string>('translationProvider', 'lingva');

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

                // Recreate API client
                apiClient = new ApiClient(lrmService.getBaseUrl());

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
