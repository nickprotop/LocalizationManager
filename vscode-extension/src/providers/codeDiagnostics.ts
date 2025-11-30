import * as vscode from 'vscode';
import { ApiClient, ScanFileResponse } from '../backend/apiClient';

export class CodeDiagnosticProvider {
    private diagnosticCollection: vscode.DiagnosticCollection;
    private debounceTimers: Map<string, NodeJS.Timeout> = new Map();
    private apiClient: ApiClient;
    private enabled: boolean = false;
    private outputChannel: vscode.OutputChannel | undefined;

    // Supported file extensions
    private readonly supportedExtensions = ['.cs', '.razor', '.xaml', '.cshtml'];

    constructor(apiClient: ApiClient, outputChannel?: vscode.OutputChannel) {
        this.apiClient = apiClient;
        this.outputChannel = outputChannel;
        this.diagnosticCollection = vscode.languages.createDiagnosticCollection('lrm-code');
    }

    private log(message: string): void {
        if (this.outputChannel) {
            this.outputChannel.appendLine(message);
        }
        console.log(message);
    }

    public enable(): void {
        this.enabled = true;
        this.log('[CodeDiagnostics] Live code scan enabled');
    }

    public disable(): void {
        this.enabled = false;
        this.diagnosticCollection.clear();
    }

    public isEnabled(): boolean {
        return this.enabled;
    }

    public async scanDocument(document: vscode.TextDocument): Promise<void> {
        if (!this.enabled) {
            this.log('[CodeDiagnostics] Skipping scan - not enabled');
            return;
        }

        // Only scan supported file types
        if (!this.isSupportedFile(document)) {
            this.log(`[CodeDiagnostics] Skipping ${document.fileName} - unsupported file type`);
            return;
        }

        // Don't scan files that are too large (> 1MB)
        if (document.getText().length > 1000000) {
            this.log(`[CodeDiagnostics] Skipping ${document.fileName} - file too large`);
            return;
        }

        try {
            this.log(`  → Scanning file: ${document.uri.fsPath}`);
            const result = await this.apiClient.scanFile({
                filePath: document.uri.fsPath,
                content: document.getText()  // Send the in-memory content, not the saved file!
            });
            this.log(`  → Scan complete: ${result.totalReferences || 0} refs, ${result.missingKeysCount || 0} missing`);

            this.updateDiagnostics(document, result);
        } catch (error: any) {
            this.log(`  → Scan error: ${error.message || error}`);
        }
    }

    public debounce(document: vscode.TextDocument, delay: number = 500): void {
        if (!this.enabled) {
            return;
        }

        if (!this.isSupportedFile(document)) {
            return;
        }

        const key = document.uri.toString();
        const existingTimer = this.debounceTimers.get(key);

        if (existingTimer) {
            clearTimeout(existingTimer);
        }

        this.log(`  → Will scan after ${delay}ms of inactivity`);

        const timer = setTimeout(() => {
            this.scanDocument(document);
            this.debounceTimers.delete(key);
        }, delay);

        this.debounceTimers.set(key, timer);
    }

    private updateDiagnostics(document: vscode.TextDocument, result: ScanFileResponse): void {
        const diagnostics: vscode.Diagnostic[] = [];

        this.log(`  → Processing ${result.references?.length || 0} key references`);
        if (result.missing && result.missing.length > 0) {
            this.log(`  → Missing keys: ${result.missing.join(', ')}`);
        }

        // Process missing keys
        for (const keyUsage of result.references || []) {
            const key = keyUsage.key;
            const isMissing = result.missing?.includes(key) || false;

            if (!isMissing) {
                continue;
            }

            this.log(`  → Found missing key '${key}' with ${keyUsage.references?.length || 0} references`);

            // Find all references to this key in the document
            for (const reference of keyUsage.references || []) {
                const diagnostic = this.createDiagnostic(document, reference.pattern, key, reference.line);
                if (diagnostic) {
                    diagnostics.push(diagnostic);
                }
            }
        }

        this.log(`  → Created ${diagnostics.length} diagnostics`);
        this.diagnosticCollection.set(document.uri, diagnostics);
    }

    private createDiagnostic(
        document: vscode.TextDocument,
        _pattern: string,
        key: string,
        lineNumber: number
    ): vscode.Diagnostic | null {
        try {
            // Convert 1-based line number to 0-based
            const lineIndex = Math.max(0, lineNumber - 1);

            if (lineIndex >= document.lineCount) {
                return null;
            }

            const line = document.lineAt(lineIndex);
            const lineText = line.text;

            // Find the key in the line
            const keyIndex = lineText.indexOf(key);

            if (keyIndex === -1) {
                // Key not found on this exact line, search nearby
                // This handles cases where the API reports a slightly different line
                return this.searchNearbyLines(document, key, lineIndex);
            }

            // Create range for the key
            const startPos = new vscode.Position(lineIndex, keyIndex);
            const endPos = new vscode.Position(lineIndex, keyIndex + key.length);
            const range = new vscode.Range(startPos, endPos);

            // Create diagnostic
            const diagnostic = new vscode.Diagnostic(
                range,
                `Localization key '${key}' not found in resources`,
                vscode.DiagnosticSeverity.Warning
            );

            diagnostic.source = 'LRM';
            diagnostic.code = 'missing-key';

            // Add tags
            diagnostic.tags = [vscode.DiagnosticTag.Unnecessary];

            return diagnostic;
        } catch (error) {
            console.error('Error creating diagnostic:', error);
            return null;
        }
    }

    private searchNearbyLines(
        document: vscode.TextDocument,
        key: string,
        centerLine: number
    ): vscode.Diagnostic | null {
        // Search within ±2 lines
        for (let offset = 0; offset <= 2; offset++) {
            // Try below first
            const lineBelow = centerLine + offset;
            if (lineBelow < document.lineCount) {
                const diagnostic = this.tryCreateDiagnosticForLine(document, key, lineBelow);
                if (diagnostic) {
                    return diagnostic;
                }
            }

            // Try above (skip 0 offset to avoid duplicate)
            if (offset > 0) {
                const lineAbove = centerLine - offset;
                if (lineAbove >= 0) {
                    const diagnostic = this.tryCreateDiagnosticForLine(document, key, lineAbove);
                    if (diagnostic) {
                        return diagnostic;
                    }
                }
            }
        }

        return null;
    }

    private tryCreateDiagnosticForLine(
        document: vscode.TextDocument,
        key: string,
        lineIndex: number
    ): vscode.Diagnostic | null {
        const line = document.lineAt(lineIndex);
        const lineText = line.text;
        const keyIndex = lineText.indexOf(key);

        if (keyIndex === -1) {
            return null;
        }

        const startPos = new vscode.Position(lineIndex, keyIndex);
        const endPos = new vscode.Position(lineIndex, keyIndex + key.length);
        const range = new vscode.Range(startPos, endPos);

        const diagnostic = new vscode.Diagnostic(
            range,
            `Localization key '${key}' not found in resources`,
            vscode.DiagnosticSeverity.Warning
        );

        diagnostic.source = 'LRM';
        diagnostic.code = 'missing-key';
        diagnostic.tags = [vscode.DiagnosticTag.Unnecessary];

        return diagnostic;
    }

    private isSupportedFile(document: vscode.TextDocument): boolean {
        // Check if file has supported extension
        const ext = document.fileName.substring(document.fileName.lastIndexOf('.'));
        return this.supportedExtensions.includes(ext);
    }

    public clear(): void {
        this.diagnosticCollection.clear();
    }

    public dispose(): void {
        this.diagnosticCollection.dispose();

        // Clear all debounce timers
        for (const timer of this.debounceTimers.values()) {
            clearTimeout(timer);
        }
        this.debounceTimers.clear();
    }
}
