import * as vscode from 'vscode';
import { ApiClient, ValidationResult } from '../backend/apiClient';

export class ResxDiagnosticProvider {
    private diagnosticCollection: vscode.DiagnosticCollection;
    private apiClient: ApiClient;
    private enabled: boolean = false;
    private validationCache: ValidationResult | null = null;

    constructor(apiClient: ApiClient) {
        this.apiClient = apiClient;
        this.diagnosticCollection = vscode.languages.createDiagnosticCollection('lrm-resx');
    }

    public enable(): void {
        this.enabled = true;
    }

    public disable(): void {
        this.enabled = false;
        this.diagnosticCollection.clear();
    }

    public isEnabled(): boolean {
        return this.enabled;
    }

    public async validateAllResources(): Promise<void> {
        if (!this.enabled) {
            return;
        }

        try {
            // Fetch validation result from API
            const result = await this.apiClient.validate();
            this.validationCache = result;

            // Clear all existing diagnostics
            this.diagnosticCollection.clear();

            // Get all .resx files in workspace
            const resxFiles = await vscode.workspace.findFiles('**/*.resx', '**/node_modules/**');

            // Process each .resx file
            for (const fileUri of resxFiles) {
                const diagnostics = await this.createDiagnosticsForFile(fileUri, result);
                if (diagnostics.length > 0) {
                    this.diagnosticCollection.set(fileUri, diagnostics);
                }
            }
        } catch (error) {
            console.error('Resource validation error:', error);
        }
    }

    private async createDiagnosticsForFile(
        fileUri: vscode.Uri,
        validation: ValidationResult
    ): Promise<vscode.Diagnostic[]> {
        const diagnostics: vscode.Diagnostic[] = [];

        try {
            const document = await vscode.workspace.openTextDocument(fileUri);
            const fileName = fileUri.fsPath.substring(fileUri.fsPath.lastIndexOf('/') + 1);

            // Determine language code from filename
            // e.g., Resources.el.resx -> el, Resources.resx -> default
            const languageCode = this.extractLanguageCode(fileName);

            if (!languageCode) {
                return diagnostics;
            }

            // Check for missing keys in this language
            const missingKeys = validation.missingKeys?.[languageCode] || [];
            for (const key of missingKeys) {
                const diagnostic = this.createMissingKeyDiagnostic(document, key);
                if (diagnostic) {
                    diagnostics.push(diagnostic);
                }
            }

            // Check for extra keys in non-default languages
            if (languageCode !== 'default') {
                const extraKeys = validation.extraKeys?.[languageCode] || [];
                for (const key of extraKeys) {
                    const diagnostic = this.createExtraKeyDiagnostic(document, key);
                    if (diagnostic) {
                        diagnostics.push(diagnostic);
                    }
                }
            }

            // Check for empty values
            const emptyKeys = validation.emptyValues?.[languageCode] || [];
            for (const key of emptyKeys) {
                const diagnostic = this.createEmptyValueDiagnostic(document, key);
                if (diagnostic) {
                    diagnostics.push(diagnostic);
                }
            }

            // Check for duplicate keys (applies to all languages)
            for (const duplicateKey of validation.duplicateKeys || []) {
                const diagnostic = this.createDuplicateKeyDiagnostic(document, duplicateKey);
                if (diagnostic) {
                    diagnostics.push(diagnostic);
                }
            }
        } catch (error) {
            console.error('Error creating diagnostics for file:', error);
        }

        return diagnostics;
    }

    private extractLanguageCode(fileName: string): string | null {
        // Extract language code from filename
        // Resources.resx -> "default"
        // Resources.el.resx -> "el"
        // Resources.en-US.resx -> "en-US"

        const match = fileName.match(/\.([a-z]{2}(-[A-Z]{2})?)\.resx$/i);
        if (match) {
            return match[1];
        }

        // Check if it's the default file (no language code)
        if (fileName.endsWith('.resx')) {
            return 'default';
        }

        return null;
    }

    private createMissingKeyDiagnostic(_document: vscode.TextDocument, key: string): vscode.Diagnostic | null {
        // For missing keys, we show a diagnostic at the top of the file
        // since the key doesn't exist in this file
        const range = new vscode.Range(0, 0, 0, 0);

        const diagnostic = new vscode.Diagnostic(
            range,
            `Missing translation for key: ${key}`,
            vscode.DiagnosticSeverity.Warning
        );

        diagnostic.source = 'LRM';
        diagnostic.code = 'missing-translation';

        return diagnostic;
    }

    private createExtraKeyDiagnostic(document: vscode.TextDocument, key: string): vscode.Diagnostic | null {
        // Find the line containing this key
        const lineNumber = this.findKeyInDocument(document, key);

        if (lineNumber === -1) {
            return null;
        }

        const line = document.lineAt(lineNumber);
        const range = line.range;

        const diagnostic = new vscode.Diagnostic(
            range,
            `Extra key '${key}' exists in this language but not in default resources`,
            vscode.DiagnosticSeverity.Information
        );

        diagnostic.source = 'LRM';
        diagnostic.code = 'extra-key';

        return diagnostic;
    }

    private createEmptyValueDiagnostic(document: vscode.TextDocument, key: string): vscode.Diagnostic | null {
        // Find the line containing this key
        const lineNumber = this.findKeyInDocument(document, key);

        if (lineNumber === -1) {
            return null;
        }

        const line = document.lineAt(lineNumber);
        const range = line.range;

        const diagnostic = new vscode.Diagnostic(
            range,
            `Key '${key}' has an empty value`,
            vscode.DiagnosticSeverity.Warning
        );

        diagnostic.source = 'LRM';
        diagnostic.code = 'empty-value';

        return diagnostic;
    }

    private createDuplicateKeyDiagnostic(document: vscode.TextDocument, key: string): vscode.Diagnostic | null {
        // Find all occurrences of this key
        const lineNumbers = this.findAllKeyOccurrences(document, key);

        if (lineNumbers.length < 2) {
            return null;
        }

        // Create diagnostic for the first occurrence
        const line = document.lineAt(lineNumbers[0]);
        const range = line.range;

        const diagnostic = new vscode.Diagnostic(
            range,
            `Duplicate key '${key}' (appears ${lineNumbers.length} times)`,
            vscode.DiagnosticSeverity.Error
        );

        diagnostic.source = 'LRM';
        diagnostic.code = 'duplicate-key';

        return diagnostic;
    }

    private findKeyInDocument(document: vscode.TextDocument, key: string): number {
        // Search for <data name="KeyName" pattern in .resx XML
        const pattern = `<data name="${key}"`;

        for (let i = 0; i < document.lineCount; i++) {
            const line = document.lineAt(i);
            if (line.text.includes(pattern)) {
                return i;
            }
        }

        return -1;
    }

    private findAllKeyOccurrences(document: vscode.TextDocument, key: string): number[] {
        const pattern = `<data name="${key}"`;
        const lineNumbers: number[] = [];

        for (let i = 0; i < document.lineCount; i++) {
            const line = document.lineAt(i);
            if (line.text.includes(pattern)) {
                lineNumbers.push(i);
            }
        }

        return lineNumbers;
    }

    public clear(): void {
        this.diagnosticCollection.clear();
        this.validationCache = null;
    }

    public dispose(): void {
        this.diagnosticCollection.dispose();
    }

    public getValidationCache(): ValidationResult | null {
        return this.validationCache;
    }
}
