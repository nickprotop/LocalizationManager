import * as vscode from 'vscode';
import { ApiClient } from '../backend/apiClient';

export class QuickFixProvider implements vscode.CodeActionProvider {
    constructor(_apiClient: ApiClient) {
        // API client not needed here - commands handle API calls
    }

    public static readonly providedCodeActionKinds = [
        vscode.CodeActionKind.QuickFix
    ];

    async provideCodeActions(
        _document: vscode.TextDocument,
        _range: vscode.Range | vscode.Selection,
        context: vscode.CodeActionContext,
        _token: vscode.CancellationToken
    ): Promise<vscode.CodeAction[]> {
        const actions: vscode.CodeAction[] = [];

        // Process each diagnostic in the context
        for (const diagnostic of context.diagnostics) {
            if (diagnostic.source !== 'LRM') {
                continue;
            }

            if (diagnostic.code === 'missing-key') {
                // Extract key name from diagnostic message
                const match = diagnostic.message.match(/Localization key '(.+?)' not found/);
                if (match) {
                    const keyName = match[1];

                    // Create quick fix to add the key
                    const addKeyAction = this.createAddKeyAction(keyName, diagnostic);
                    actions.push(addKeyAction);

                    // Create quick fix to add key with custom value
                    const addKeyWithValueAction = this.createAddKeyWithValueAction(keyName, diagnostic);
                    actions.push(addKeyWithValueAction);
                }
            }

            if (diagnostic.code === 'duplicate-key') {
                // Extract key name from diagnostic message
                const match = diagnostic.message.match(/Duplicate key '(.+?)'/);
                if (match) {
                    const keyName = match[1];

                    // Create quick fix to merge duplicates
                    const mergeDuplicatesAction = this.createMergeDuplicatesAction(keyName, diagnostic);
                    actions.push(mergeDuplicatesAction);
                }
            }

            if (diagnostic.code === 'empty-value') {
                // Extract key name from diagnostic message
                const match = diagnostic.message.match(/Key '(.+?)' has an empty value/);
                if (match) {
                    const keyName = match[1];

                    // Create quick fix to translate the key
                    const translateAction = this.createTranslateKeyAction(keyName, diagnostic);
                    actions.push(translateAction);
                }
            }
        }

        return actions;
    }

    private createAddKeyAction(keyName: string, diagnostic: vscode.Diagnostic): vscode.CodeAction {
        const action = new vscode.CodeAction(
            `Add key '${keyName}' to resources`,
            vscode.CodeActionKind.QuickFix
        );

        action.diagnostics = [diagnostic];
        action.isPreferred = true;

        action.command = {
            command: 'lrm.addKeyQuickFix',
            title: 'Add Key',
            arguments: [keyName, keyName] // Use key name as default value
        };

        return action;
    }

    private createAddKeyWithValueAction(keyName: string, diagnostic: vscode.Diagnostic): vscode.CodeAction {
        const action = new vscode.CodeAction(
            `Add key '${keyName}' with custom value...`,
            vscode.CodeActionKind.QuickFix
        );

        action.diagnostics = [diagnostic];

        action.command = {
            command: 'lrm.addKeyWithValueQuickFix',
            title: 'Add Key with Value',
            arguments: [keyName]
        };

        return action;
    }

    private createMergeDuplicatesAction(keyName: string, diagnostic: vscode.Diagnostic): vscode.CodeAction {
        const action = new vscode.CodeAction(
            `Merge duplicate occurrences of '${keyName}'`,
            vscode.CodeActionKind.QuickFix
        );

        action.diagnostics = [diagnostic];

        action.command = {
            command: 'lrm.mergeDuplicateKey',
            title: 'Merge Duplicates',
            arguments: [keyName]
        };

        return action;
    }

    private createTranslateKeyAction(keyName: string, diagnostic: vscode.Diagnostic): vscode.CodeAction {
        const action = new vscode.CodeAction(
            `Translate '${keyName}' automatically`,
            vscode.CodeActionKind.QuickFix
        );

        action.diagnostics = [diagnostic];

        action.command = {
            command: 'lrm.translateKeyQuickFix',
            title: 'Translate Key',
            arguments: [keyName]
        };

        return action;
    }
}
