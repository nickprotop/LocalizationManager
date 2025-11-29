import * as vscode from 'vscode';
import { ResourceTreeProvider } from './providers/resourceTreeProvider';
import { ValidationTreeProvider } from './providers/validationTreeProvider';
import { StatsTreeProvider } from './providers/statsTreeProvider';
import { LrmService } from './services/lrmService';
import { DiagnosticsProvider } from './providers/diagnosticsProvider';
import { ResxEditorProvider } from './editors/resxEditorProvider';
import { registerCommands } from './commands';

let lrmService: LrmService;
let resourceTreeProvider: ResourceTreeProvider;
let validationTreeProvider: ValidationTreeProvider;
let statsTreeProvider: StatsTreeProvider;
let diagnosticsProvider: DiagnosticsProvider;

export async function activate(context: vscode.ExtensionContext): Promise<void> {
    console.log('Localization Resource Manager extension is now active');

    // Initialize the LRM service
    lrmService = new LrmService();

    // Check if LRM is available
    const isAvailable = await lrmService.checkAvailability();
    if (!isAvailable) {
        const action = await vscode.window.showWarningMessage(
            'LRM (Localization Resource Manager) CLI is not found. Some features may not work correctly.',
            'Install LRM',
            'Configure Path',
            'Dismiss'
        );

        if (action === 'Install LRM') {
            vscode.env.openExternal(vscode.Uri.parse('https://github.com/nickprotop/LocalizationManager#installation'));
        } else if (action === 'Configure Path') {
            vscode.commands.executeCommand('workbench.action.openSettings', 'lrm.lrmPath');
        }
    }

    // Initialize tree providers
    resourceTreeProvider = new ResourceTreeProvider(lrmService);
    validationTreeProvider = new ValidationTreeProvider(lrmService);
    statsTreeProvider = new StatsTreeProvider(lrmService);

    // Register tree views
    const resourceTreeView = vscode.window.createTreeView('lrmResources', {
        treeDataProvider: resourceTreeProvider,
        showCollapseAll: true
    });

    const validationTreeView = vscode.window.createTreeView('lrmValidation', {
        treeDataProvider: validationTreeProvider,
        showCollapseAll: true
    });

    const statsTreeView = vscode.window.createTreeView('lrmStats', {
        treeDataProvider: statsTreeProvider
    });

    // Initialize diagnostics provider
    diagnosticsProvider = new DiagnosticsProvider(lrmService);
    context.subscriptions.push(diagnosticsProvider);

    // Register custom editor for .resx files
    context.subscriptions.push(
        ResxEditorProvider.register(context, lrmService)
    );

    // Register all commands
    registerCommands(
        context,
        lrmService,
        resourceTreeProvider,
        validationTreeProvider,
        statsTreeProvider,
        diagnosticsProvider
    );

    // Add tree views to subscriptions
    context.subscriptions.push(resourceTreeView);
    context.subscriptions.push(validationTreeView);
    context.subscriptions.push(statsTreeView);

    // Watch for .resx file changes
    const resxWatcher = vscode.workspace.createFileSystemWatcher('**/*.resx');

    resxWatcher.onDidCreate(() => {
        resourceTreeProvider.refresh();
    });

    resxWatcher.onDidDelete(() => {
        resourceTreeProvider.refresh();
    });

    resxWatcher.onDidChange((uri) => {
        const config = vscode.workspace.getConfiguration('lrm');
        if (config.get<boolean>('autoValidateOnSave')) {
            diagnosticsProvider.validateFile(uri);
        }
    });

    context.subscriptions.push(resxWatcher);

    // Auto-scan on startup if configured
    const config = vscode.workspace.getConfiguration('lrm');
    if (config.get<boolean>('scanOnStartup')) {
        resourceTreeProvider.refresh();
    }

    // Watch for configuration changes
    context.subscriptions.push(
        vscode.workspace.onDidChangeConfiguration((e) => {
            if (e.affectsConfiguration('lrm')) {
                lrmService.updateConfiguration();
            }
        })
    );

    // Show welcome message for first-time users
    const hasShownWelcome = context.globalState.get<boolean>('lrm.hasShownWelcome');
    if (!hasShownWelcome) {
        const action = await vscode.window.showInformationMessage(
            'Welcome to Localization Resource Manager! Would you like to create a configuration file for this project?',
            'Create lrm.json',
            'Later'
        );

        if (action === 'Create lrm.json') {
            vscode.commands.executeCommand('lrm.createConfig');
        }

        context.globalState.update('lrm.hasShownWelcome', true);
    }
}

export function deactivate(): void {
    console.log('Localization Resource Manager extension is now deactivated');
}
