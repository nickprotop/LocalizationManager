import * as vscode from 'vscode';
import * as path from 'path';
import { LrmService } from '../services/lrmService';
import { ResourceTreeProvider, ResourceFileItem, ResourceKeyItem, ResourceGroupItem } from '../providers/resourceTreeProvider';
import { ValidationTreeProvider, ValidationIssueItem } from '../providers/validationTreeProvider';
import { StatsTreeProvider } from '../providers/statsTreeProvider';
import { DiagnosticsProvider } from '../providers/diagnosticsProvider';

export function registerCommands(
    context: vscode.ExtensionContext,
    lrmService: LrmService,
    resourceTreeProvider: ResourceTreeProvider,
    validationTreeProvider: ValidationTreeProvider,
    statsTreeProvider: StatsTreeProvider,
    diagnosticsProvider: DiagnosticsProvider
): void {
    // Refresh command
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.refresh', () => {
            resourceTreeProvider.refresh();
            validationTreeProvider.refresh();
            statsTreeProvider.refresh();
        })
    );

    // Validate resources
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.validate', async () => {
            await vscode.window.withProgress(
                {
                    location: vscode.ProgressLocation.Notification,
                    title: 'Validating resources...',
                    cancellable: false
                },
                async () => {
                    const issues = await diagnosticsProvider.validateWorkspace();
                    validationTreeProvider.setIssues(issues);

                    if (issues.length === 0) {
                        vscode.window.showInformationMessage('No validation issues found!');
                    } else {
                        vscode.window.showWarningMessage(`Found ${issues.length} validation issue(s)`);
                    }
                }
            );
        })
    );

    // Validate single file
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.validateFile', async (item?: ResourceFileItem) => {
            let filePath: string | undefined;

            if (item instanceof ResourceFileItem) {
                filePath = item.resourceFile.path;
            } else {
                const editor = vscode.window.activeTextEditor;
                if (editor && editor.document.fileName.endsWith('.resx')) {
                    filePath = editor.document.fileName;
                }
            }

            if (!filePath) {
                vscode.window.showWarningMessage('Please select a .resx file to validate');
                return;
            }

            const issues = await diagnosticsProvider.validateFile(vscode.Uri.file(filePath));
            validationTreeProvider.setIssues(issues);

            if (issues.length === 0) {
                vscode.window.showInformationMessage(`No issues found in ${path.basename(filePath)}`);
            } else {
                vscode.window.showWarningMessage(`Found ${issues.length} issue(s) in ${path.basename(filePath)}`);
            }
        })
    );

    // Translate resources
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.translate', async (item?: ResourceFileItem) => {
            let filePath: string | undefined;

            if (item instanceof ResourceFileItem) {
                filePath = item.resourceFile.path;
            } else {
                const editor = vscode.window.activeTextEditor;
                if (editor && editor.document.fileName.endsWith('.resx')) {
                    filePath = editor.document.fileName;
                }
            }

            if (!filePath) {
                vscode.window.showWarningMessage('Please select a .resx file to translate');
                return;
            }

            // Get target language
            const targetLanguage = await vscode.window.showInputBox({
                prompt: 'Enter target language code (e.g., es, fr, de)',
                placeHolder: 'es'
            });

            if (!targetLanguage) {
                return;
            }

            // Get translation provider
            const config = vscode.workspace.getConfiguration('lrm');
            const defaultProvider = config.get<string>('translationProvider') || 'google';

            const provider = await vscode.window.showQuickPick(
                [
                    { label: 'Google Translate', value: 'google' },
                    { label: 'DeepL', value: 'deepl' },
                    { label: 'Azure Translator', value: 'azure' },
                    { label: 'OpenAI', value: 'openai' },
                    { label: 'Claude', value: 'claude' },
                    { label: 'Ollama (Local)', value: 'ollama' },
                    { label: 'LibreTranslate', value: 'libretranslate' },
                    { label: 'Lingva (Free)', value: 'lingva' },
                    { label: 'MyMemory (Free)', value: 'mymemory' }
                ],
                {
                    placeHolder: `Select translation provider (default: ${defaultProvider})`,
                    title: 'Translation Provider'
                }
            );

            const selectedProvider = provider?.value || defaultProvider;

            // Translate only missing?
            const onlyMissing = await vscode.window.showQuickPick(
                [
                    { label: 'Only missing translations', value: true },
                    { label: 'All keys', value: false }
                ],
                {
                    placeHolder: 'What to translate?'
                }
            );

            await vscode.window.withProgress(
                {
                    location: vscode.ProgressLocation.Notification,
                    title: `Translating to ${targetLanguage}...`,
                    cancellable: false
                },
                async () => {
                    const result = await lrmService.translate(filePath!, targetLanguage, {
                        provider: selectedProvider,
                        onlyMissing: onlyMissing?.value ?? true
                    });

                    if (result.success) {
                        vscode.window.showInformationMessage(
                            `Successfully translated ${result.translatedCount} key(s)`
                        );
                        resourceTreeProvider.refresh();
                    } else {
                        vscode.window.showErrorMessage(
                            `Translation failed: ${result.errors.join(', ')}`
                        );
                    }
                }
            );
        })
    );

    // Translate single key
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.translateKey', async (item?: ResourceKeyItem) => {
            if (!(item instanceof ResourceKeyItem)) {
                vscode.window.showWarningMessage('Please select a key to translate');
                return;
            }

            const targetLanguage = await vscode.window.showInputBox({
                prompt: 'Enter target language code',
                placeHolder: 'es'
            });

            if (!targetLanguage) {
                return;
            }

            const result = await lrmService.translate(item.filePath, targetLanguage, {
                keyPattern: item.resourceKey.name,
                onlyMissing: false
            });

            if (result.success) {
                vscode.window.showInformationMessage(`Key "${item.resourceKey.name}" translated`);
                resourceTreeProvider.refresh();
            } else {
                vscode.window.showErrorMessage(`Translation failed: ${result.errors.join(', ')}`);
            }
        })
    );

    // Scan for unused/missing keys
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.scan', async () => {
            const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
            if (!workspaceFolder) {
                vscode.window.showWarningMessage('No workspace folder open');
                return;
            }

            await vscode.window.withProgress(
                {
                    location: vscode.ProgressLocation.Notification,
                    title: 'Scanning for unused/missing keys...',
                    cancellable: false
                },
                async () => {
                    const result = await lrmService.scan(workspaceFolder.uri.fsPath);

                    const totalIssues = result.unusedKeys.length + result.missingKeys.length;

                    if (totalIssues === 0) {
                        vscode.window.showInformationMessage('No issues found!');
                    } else {
                        const message = [
                            result.unusedKeys.length > 0 ? `${result.unusedKeys.length} unused` : '',
                            result.missingKeys.length > 0 ? `${result.missingKeys.length} missing` : ''
                        ].filter(Boolean).join(', ');

                        vscode.window.showWarningMessage(`Found: ${message} key(s)`);
                    }
                }
            );
        })
    );

    // Add new key
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.addKey', async (item?: ResourceFileItem | ResourceGroupItem) => {
            let filePath: string | undefined;

            if (item instanceof ResourceFileItem) {
                filePath = item.resourceFile.path;
            } else if (item instanceof ResourceGroupItem) {
                filePath = item.resourceGroup.basePath;
            } else {
                const editor = vscode.window.activeTextEditor;
                if (editor && editor.document.fileName.endsWith('.resx')) {
                    filePath = editor.document.fileName;
                }
            }

            if (!filePath) {
                vscode.window.showWarningMessage('Please select a resource file');
                return;
            }

            const keyName = await vscode.window.showInputBox({
                prompt: 'Enter key name',
                placeHolder: 'MyApp.MyKey'
            });

            if (!keyName) {
                return;
            }

            const value = await vscode.window.showInputBox({
                prompt: 'Enter value',
                placeHolder: 'Enter the text value'
            });

            if (value === undefined) {
                return;
            }

            const comment = await vscode.window.showInputBox({
                prompt: 'Enter comment (optional)',
                placeHolder: 'Description of this key'
            });

            const success = await lrmService.addKey(filePath, keyName, value, comment);

            if (success) {
                vscode.window.showInformationMessage(`Key "${keyName}" added successfully`);
                resourceTreeProvider.refresh();
            } else {
                vscode.window.showErrorMessage(`Failed to add key "${keyName}"`);
            }
        })
    );

    // Delete key
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.deleteKey', async (item?: ResourceKeyItem) => {
            if (!(item instanceof ResourceKeyItem)) {
                vscode.window.showWarningMessage('Please select a key to delete');
                return;
            }

            const confirm = await vscode.window.showWarningMessage(
                `Delete key "${item.resourceKey.name}"?`,
                { modal: true },
                'Delete'
            );

            if (confirm !== 'Delete') {
                return;
            }

            const success = await lrmService.deleteKey(item.filePath, item.resourceKey.name);

            if (success) {
                vscode.window.showInformationMessage(`Key "${item.resourceKey.name}" deleted`);
                resourceTreeProvider.refresh();
            } else {
                vscode.window.showErrorMessage(`Failed to delete key "${item.resourceKey.name}"`);
            }
        })
    );

    // Edit key
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.editKey', async (item?: ResourceKeyItem) => {
            if (!(item instanceof ResourceKeyItem)) {
                vscode.window.showWarningMessage('Please select a key to edit');
                return;
            }

            const newValue = await vscode.window.showInputBox({
                prompt: `Edit value for "${item.resourceKey.name}"`,
                value: item.resourceKey.value
            });

            if (newValue === undefined) {
                return;
            }

            const success = await lrmService.updateKey(item.filePath, item.resourceKey.name, newValue);

            if (success) {
                vscode.window.showInformationMessage(`Key "${item.resourceKey.name}" updated`);
                resourceTreeProvider.refresh();
            } else {
                vscode.window.showErrorMessage(`Failed to update key "${item.resourceKey.name}"`);
            }
        })
    );

    // Copy key name
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.copyKey', async (item?: ResourceKeyItem) => {
            if (item instanceof ResourceKeyItem) {
                await vscode.env.clipboard.writeText(item.resourceKey.name);
                vscode.window.showInformationMessage(`Copied "${item.resourceKey.name}" to clipboard`);
            }
        })
    );

    // Copy value
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.copyValue', async (item?: ResourceKeyItem) => {
            if (item instanceof ResourceKeyItem) {
                await vscode.env.clipboard.writeText(item.resourceKey.value);
                vscode.window.showInformationMessage('Value copied to clipboard');
            }
        })
    );

    // Export to CSV
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.exportCsv', async (item?: ResourceFileItem) => {
            let filePath: string | undefined;

            if (item instanceof ResourceFileItem) {
                filePath = item.resourceFile.path;
            }

            if (!filePath) {
                vscode.window.showWarningMessage('Please select a resource file to export');
                return;
            }

            const outputUri = await vscode.window.showSaveDialog({
                defaultUri: vscode.Uri.file(filePath.replace('.resx', '.csv')),
                filters: { 'CSV': ['csv'] }
            });

            if (!outputUri) {
                return;
            }

            const success = await lrmService.exportToCsv(filePath, outputUri.fsPath);

            if (success) {
                const openFile = await vscode.window.showInformationMessage(
                    'Export successful!',
                    'Open File'
                );
                if (openFile) {
                    vscode.workspace.openTextDocument(outputUri).then(doc => {
                        vscode.window.showTextDocument(doc);
                    });
                }
            } else {
                vscode.window.showErrorMessage('Export failed');
            }
        })
    );

    // Export to JSON
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.exportJson', async (item?: ResourceFileItem) => {
            let filePath: string | undefined;

            if (item instanceof ResourceFileItem) {
                filePath = item.resourceFile.path;
            }

            if (!filePath) {
                vscode.window.showWarningMessage('Please select a resource file to export');
                return;
            }

            const outputUri = await vscode.window.showSaveDialog({
                defaultUri: vscode.Uri.file(filePath.replace('.resx', '.json')),
                filters: { 'JSON': ['json'] }
            });

            if (!outputUri) {
                return;
            }

            const success = await lrmService.exportToJson(filePath, outputUri.fsPath);

            if (success) {
                vscode.window.showInformationMessage('Export successful!');
            } else {
                vscode.window.showErrorMessage('Export failed');
            }
        })
    );

    // Import from CSV
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.importCsv', async (item?: ResourceFileItem) => {
            let filePath: string | undefined;

            if (item instanceof ResourceFileItem) {
                filePath = item.resourceFile.path;
            }

            if (!filePath) {
                vscode.window.showWarningMessage('Please select a resource file to import into');
                return;
            }

            const inputUri = await vscode.window.showOpenDialog({
                canSelectFiles: true,
                canSelectFolders: false,
                canSelectMany: false,
                filters: { 'CSV': ['csv'] }
            });

            if (!inputUri || inputUri.length === 0) {
                return;
            }

            const success = await lrmService.importFromCsv(filePath, inputUri[0].fsPath);

            if (success) {
                vscode.window.showInformationMessage('Import successful!');
                resourceTreeProvider.refresh();
            } else {
                vscode.window.showErrorMessage('Import failed');
            }
        })
    );

    // Find references
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.findReferences', async (item?: ResourceKeyItem) => {
            if (!(item instanceof ResourceKeyItem)) {
                vscode.window.showWarningMessage('Please select a key to find references');
                return;
            }

            const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
            if (!workspaceFolder) {
                return;
            }

            const locations = await lrmService.findReferences(
                item.resourceKey.name,
                workspaceFolder.uri.fsPath
            );

            if (locations.length === 0) {
                vscode.window.showInformationMessage(`No references found for "${item.resourceKey.name}"`);
            } else {
                // Show references in peek view
                vscode.commands.executeCommand(
                    'editor.action.showReferences',
                    vscode.Uri.file(item.filePath),
                    new vscode.Position(0, 0),
                    locations
                );
            }
        })
    );

    // Open settings
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.openSettings', () => {
            vscode.commands.executeCommand('workbench.action.openSettings', 'lrm');
        })
    );

    // Create config
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.createConfig', async () => {
            const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
            if (!workspaceFolder) {
                vscode.window.showWarningMessage('No workspace folder open');
                return;
            }

            const configPath = path.join(workspaceFolder.uri.fsPath, 'lrm.json');

            // Check if config already exists
            try {
                await vscode.workspace.fs.stat(vscode.Uri.file(configPath));
                const overwrite = await vscode.window.showWarningMessage(
                    'lrm.json already exists. Overwrite?',
                    'Overwrite',
                    'Cancel'
                );
                if (overwrite !== 'Overwrite') {
                    return;
                }
            } catch {
                // File doesn't exist, continue
            }

            const success = await lrmService.createConfig(workspaceFolder.uri.fsPath);

            if (success) {
                const doc = await vscode.workspace.openTextDocument(configPath);
                await vscode.window.showTextDocument(doc);
                vscode.window.showInformationMessage('Configuration file created!');
            } else {
                // Create a default config manually
                const defaultConfig = {
                    defaultLanguageCode: 'en',
                    translation: {
                        defaultProvider: 'google',
                        apiKeys: {}
                    },
                    scanning: {
                        resourceClassNames: ['Resources', 'Strings'],
                        localizationMethods: ['GetString', 'L', 'T']
                    },
                    validation: {
                        enablePlaceholderValidation: true,
                        placeholderTypes: ['dotnet']
                    }
                };

                await vscode.workspace.fs.writeFile(
                    vscode.Uri.file(configPath),
                    Buffer.from(JSON.stringify(defaultConfig, null, 2))
                );

                const doc = await vscode.workspace.openTextDocument(configPath);
                await vscode.window.showTextDocument(doc);
                vscode.window.showInformationMessage('Configuration file created!');
            }
        })
    );

    // Open in editor (custom resx editor)
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.openInEditor', async (item?: ResourceFileItem | vscode.Uri) => {
            let uri: vscode.Uri | undefined;

            if (item instanceof ResourceFileItem) {
                uri = vscode.Uri.file(item.resourceFile.path);
            } else if (item instanceof vscode.Uri) {
                uri = item;
            }

            if (!uri) {
                vscode.window.showWarningMessage('Please select a .resx file');
                return;
            }

            await vscode.commands.executeCommand('vscode.openWith', uri, 'lrm.resxEditor');
        })
    );

    // Show statistics
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.showStats', async (item?: ResourceFileItem) => {
            let filePath: string | undefined;

            if (item instanceof ResourceFileItem) {
                filePath = item.resourceFile.path;
            } else {
                const editor = vscode.window.activeTextEditor;
                if (editor && editor.document.fileName.endsWith('.resx')) {
                    filePath = editor.document.fileName;
                }
            }

            if (!filePath) {
                // Show stats for entire workspace
                const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
                if (workspaceFolder) {
                    filePath = workspaceFolder.uri.fsPath;
                }
            }

            if (!filePath) {
                return;
            }

            const stats = await lrmService.getStats(filePath);

            if (stats) {
                statsTreeProvider.setStats(stats, filePath);
                vscode.commands.executeCommand('lrmStats.focus');
            } else {
                vscode.window.showWarningMessage('Could not retrieve statistics');
            }
        })
    );

    // Add language
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.addLanguage', async (item?: ResourceGroupItem) => {
            if (!(item instanceof ResourceGroupItem)) {
                vscode.window.showWarningMessage('Please select a resource group');
                return;
            }

            const languageCode = await vscode.window.showInputBox({
                prompt: 'Enter language code (e.g., es, fr, de, zh-CN)',
                placeHolder: 'es'
            });

            if (!languageCode) {
                return;
            }

            const success = await lrmService.addLanguage(item.resourceGroup.basePath, languageCode);

            if (success) {
                vscode.window.showInformationMessage(`Language "${languageCode}" added`);
                resourceTreeProvider.refresh();
            } else {
                vscode.window.showErrorMessage(`Failed to add language "${languageCode}"`);
            }
        })
    );

    // Backup
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.backup', async (item?: ResourceFileItem) => {
            let filePath: string | undefined;

            if (item instanceof ResourceFileItem) {
                filePath = item.resourceFile.path;
            }

            if (!filePath) {
                const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
                if (workspaceFolder) {
                    filePath = workspaceFolder.uri.fsPath;
                }
            }

            if (!filePath) {
                return;
            }

            const success = await lrmService.createBackup(filePath);

            if (success) {
                vscode.window.showInformationMessage('Backup created successfully');
            } else {
                vscode.window.showErrorMessage('Failed to create backup');
            }
        })
    );

    // Restore backup
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.restoreBackup', async (item?: ResourceFileItem) => {
            let filePath: string | undefined;

            if (item instanceof ResourceFileItem) {
                filePath = item.resourceFile.path;
            }

            if (!filePath) {
                return;
            }

            const backups = await lrmService.listBackups(filePath);

            if (backups.length === 0) {
                vscode.window.showInformationMessage('No backups available');
                return;
            }

            const selected = await vscode.window.showQuickPick(
                backups.map(b => ({
                    label: b.date,
                    description: b.description,
                    backupId: b.id
                })),
                {
                    placeHolder: 'Select backup to restore'
                }
            );

            if (!selected) {
                return;
            }

            const confirm = await vscode.window.showWarningMessage(
                `Restore backup from ${selected.label}?`,
                { modal: true },
                'Restore'
            );

            if (confirm !== 'Restore') {
                return;
            }

            const success = await lrmService.restoreBackup(filePath, selected.backupId);

            if (success) {
                vscode.window.showInformationMessage('Backup restored successfully');
                resourceTreeProvider.refresh();
            } else {
                vscode.window.showErrorMessage('Failed to restore backup');
            }
        })
    );

    // Go to key in file
    context.subscriptions.push(
        vscode.commands.registerCommand('lrm.goToKey', async (filePath: string, keyName: string) => {
            const document = await vscode.workspace.openTextDocument(filePath);
            const editor = await vscode.window.showTextDocument(document);

            // Search for the key in the document
            const text = document.getText();
            const keyPattern = new RegExp(`name="${keyName}"`, 'i');
            const match = keyPattern.exec(text);

            if (match) {
                const position = document.positionAt(match.index);
                editor.selection = new vscode.Selection(position, position);
                editor.revealRange(
                    new vscode.Range(position, position),
                    vscode.TextEditorRevealType.InCenter
                );
            }
        })
    );
}
