import * as vscode from 'vscode';
import * as path from 'path';
import { ApiClient, ValidationResult } from '../backend/apiClient';
import { getParserFactory, ResourceParserFactory } from '../parsers';

/**
 * Diagnostic provider for resource files (RESX and JSON)
 * Provides inline warnings and errors for:
 * - Missing translations
 * - Extra keys (in non-default languages)
 * - Empty values
 * - Duplicate keys
 */
export class ResxDiagnosticProvider {
    private diagnosticCollection: vscode.DiagnosticCollection;
    private apiClient: ApiClient;
    private enabled: boolean = false;
    private validationCache: ValidationResult | null = null;
    private parserFactory: ResourceParserFactory;

    constructor(apiClient: ApiClient) {
        this.apiClient = apiClient;
        this.diagnosticCollection = vscode.languages.createDiagnosticCollection('lrm-resources');
        this.parserFactory = getParserFactory();
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

            // Get resource files based on format
            const format = this.parserFactory.getResourceFormat();
            const excludePattern = '{**/node_modules/**,**/bin/**,**/obj/**,**/.git/**}';

            let resourceFiles: vscode.Uri[] = [];

            if (format === 'json') {
                // Get JSON resource files
                const jsonFiles = await vscode.workspace.findFiles('**/*.json', excludePattern);
                // Filter to only resource files
                resourceFiles = jsonFiles.filter(f => this.isResourceFile(f.fsPath));
            } else {
                // Get RESX files
                resourceFiles = await vscode.workspace.findFiles('**/*.resx', excludePattern);
            }

            // Process each resource file
            for (const fileUri of resourceFiles) {
                const diagnostics = await this.createDiagnosticsForFile(fileUri, result);
                if (diagnostics.length > 0) {
                    this.diagnosticCollection.set(fileUri, diagnostics);
                }
            }
        } catch (error) {
            console.error('Resource validation error:', error);
        }
    }

    /**
     * Check if a file path is likely a resource file
     */
    private isResourceFile(filePath: string): boolean {
        const fileName = path.basename(filePath).toLowerCase();

        // Exclude known config files
        const excludePatterns = [
            /^lrm.*\.json$/,
            /^package(-lock)?\.json$/,
            /^tsconfig.*\.json$/,
            /^appsettings.*\.json$/,
            /.*config\.json$/,
            /.*settings\.json$/,
        ];

        if (excludePatterns.some(p => p.test(fileName))) {
            return false;
        }

        // Use parser factory if initialized
        if (this.parserFactory.isInitialized()) {
            try {
                // Check if file matches resource patterns
                const normalizedPath = filePath.toLowerCase().replace(/\\/g, '/');
                const resourcePatterns = ['/locales/', '/translations/', '/i18n/', '/lang/'];
                if (resourcePatterns.some(p => normalizedPath.includes(p))) {
                    return true;
                }
            } catch {
                // Fall through to pattern matching
            }
        }

        // Check for culture code patterns
        const baseName = fileName.replace(/\.json$/, '');
        return /^[a-z]{2}(-[a-z]{2,4})?$/i.test(baseName) ||
               /^(strings|messages|translations?)(\.[a-z]{2}(-[a-z]{2,4})?)?$/i.test(baseName);
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
        const lowerFileName = fileName.toLowerCase();

        // RESX pattern: Resources.resx, Resources.el.resx, Resources.en-US.resx
        if (lowerFileName.endsWith('.resx')) {
            const match = fileName.match(/\.([a-z]{2}(-[A-Z]{2})?)\.resx$/i);
            if (match) {
                return match[1];
            }
            return 'default';
        }

        // JSON patterns
        if (lowerFileName.endsWith('.json')) {
            const baseName = fileName.replace(/\.json$/i, '');

            // i18next pattern: en.json, fr-FR.json (entire filename is culture code)
            if (/^[a-z]{2}(-[a-z]{2,4})?$/i.test(baseName)) {
                // Check if this is the default language (usually 'en' or configured)
                // For now, treat 'en' as default, all others as localized
                if (baseName.toLowerCase() === 'en') {
                    return 'default';
                }
                return baseName;
            }

            // Standard pattern: strings.json, strings.el.json, strings.en-US.json
            const match = baseName.match(/\.([a-z]{2}(-[a-z]{2,4})?)$/i);
            if (match) {
                return match[1];
            }

            // No culture code - this is the default file
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
        const fileName = document.fileName.toLowerCase();

        // Use parser to find key location
        if (this.parserFactory.isInitialized()) {
            const parser = this.parserFactory.getParser(document);
            const range = parser.getKeyRange(document, key);
            if (range) {
                return range.start.line;
            }
        }

        // Fallback: RESX pattern search
        if (fileName.endsWith('.resx')) {
            const pattern = `<data name="${key}"`;
            for (let i = 0; i < document.lineCount; i++) {
                const line = document.lineAt(i);
                if (line.text.includes(pattern)) {
                    return i;
                }
            }
        }

        // Fallback: JSON pattern search
        if (fileName.endsWith('.json')) {
            const pattern = `"${key}"`;
            for (let i = 0; i < document.lineCount; i++) {
                const line = document.lineAt(i);
                if (line.text.includes(pattern)) {
                    return i;
                }
            }
        }

        return -1;
    }

    private findAllKeyOccurrences(document: vscode.TextDocument, key: string): number[] {
        const lineNumbers: number[] = [];
        const fileName = document.fileName.toLowerCase();

        // Use parser to find all occurrences
        if (this.parserFactory.isInitialized()) {
            const parser = this.parserFactory.getParser(document);
            const keys = parser.parseDocument(document);
            for (const k of keys) {
                if (k.key === key) {
                    lineNumbers.push(k.lineNumber);
                }
            }
            if (lineNumbers.length > 0) {
                return lineNumbers;
            }
        }

        // Fallback: RESX pattern search
        if (fileName.endsWith('.resx')) {
            const pattern = `<data name="${key}"`;
            for (let i = 0; i < document.lineCount; i++) {
                const line = document.lineAt(i);
                if (line.text.includes(pattern)) {
                    lineNumbers.push(i);
                }
            }
        }

        // Fallback: JSON pattern search
        if (fileName.endsWith('.json')) {
            const pattern = `"${key}"`;
            for (let i = 0; i < document.lineCount; i++) {
                const line = document.lineAt(i);
                if (line.text.includes(pattern)) {
                    lineNumbers.push(i);
                }
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
