import * as vscode from 'vscode';
import { LrmService, ValidationIssue } from '../services/lrmService';

export class DiagnosticsProvider implements vscode.Disposable {
    private diagnosticCollection: vscode.DiagnosticCollection;
    private disposables: vscode.Disposable[] = [];

    constructor(private lrmService: LrmService) {
        this.diagnosticCollection = vscode.languages.createDiagnosticCollection('lrm');
        this.disposables.push(this.diagnosticCollection);

        // Validate on document save
        this.disposables.push(
            vscode.workspace.onDidSaveTextDocument((document) => {
                if (document.fileName.endsWith('.resx')) {
                    const config = vscode.workspace.getConfiguration('lrm');
                    if (config.get<boolean>('autoValidateOnSave')) {
                        this.validateFile(document.uri);
                    }
                }
            })
        );

        // Clear diagnostics when document is closed
        this.disposables.push(
            vscode.workspace.onDidCloseTextDocument((document) => {
                if (document.fileName.endsWith('.resx')) {
                    this.diagnosticCollection.delete(document.uri);
                }
            })
        );
    }

    public async validateFile(uri: vscode.Uri): Promise<ValidationIssue[]> {
        const issues = await this.lrmService.validate(uri.fsPath);
        this.updateDiagnostics(uri, issues);
        return issues;
    }

    public async validateWorkspace(): Promise<ValidationIssue[]> {
        const allIssues: ValidationIssue[] = [];
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];

        if (!workspaceFolder) {
            return allIssues;
        }

        // Find all .resx files
        const config = vscode.workspace.getConfiguration('lrm');
        const excludePatterns = config.get<string[]>('excludePatterns') || [];
        const pattern = new vscode.RelativePattern(workspaceFolder, '**/*.resx');
        const files = await vscode.workspace.findFiles(pattern, `{${excludePatterns.join(',')}}`);

        // Validate each file
        for (const file of files) {
            const issues = await this.lrmService.validate(file.fsPath);
            allIssues.push(...issues);
            this.updateDiagnostics(file, issues);
        }

        return allIssues;
    }

    private updateDiagnostics(uri: vscode.Uri, issues: ValidationIssue[]): void {
        const diagnostics: vscode.Diagnostic[] = [];

        for (const issue of issues) {
            // Try to find the line number for the key
            const diagnostic = this.createDiagnostic(issue);
            diagnostics.push(diagnostic);
        }

        this.diagnosticCollection.set(uri, diagnostics);
    }

    private createDiagnostic(issue: ValidationIssue): vscode.Diagnostic {
        // Default to first line if we can't find the key
        const range = new vscode.Range(0, 0, 0, 100);

        const severity = this.mapSeverity(issue.severity);

        const diagnostic = new vscode.Diagnostic(
            range,
            `${issue.type}: ${issue.message}`,
            severity
        );

        diagnostic.source = 'LRM';
        diagnostic.code = issue.type;

        // Add related information if available
        if (issue.language) {
            diagnostic.relatedInformation = [
                new vscode.DiagnosticRelatedInformation(
                    new vscode.Location(vscode.Uri.file(issue.file), range),
                    `Language: ${issue.language}`
                )
            ];
        }

        return diagnostic;
    }

    private mapSeverity(severity: 'error' | 'warning' | 'info'): vscode.DiagnosticSeverity {
        switch (severity) {
            case 'error':
                return vscode.DiagnosticSeverity.Error;
            case 'warning':
                return vscode.DiagnosticSeverity.Warning;
            default:
                return vscode.DiagnosticSeverity.Information;
        }
    }

    public clearDiagnostics(): void {
        this.diagnosticCollection.clear();
    }

    public dispose(): void {
        for (const disposable of this.disposables) {
            disposable.dispose();
        }
        this.diagnosticCollection.dispose();
    }
}
