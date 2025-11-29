import * as vscode from 'vscode';
import * as path from 'path';
import { LrmService, ValidationIssue } from '../services/lrmService';

export type ValidationTreeItem = ValidationFileItem | ValidationIssueItem;

export class ValidationTreeProvider implements vscode.TreeDataProvider<ValidationTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<ValidationTreeItem | undefined | null | void> = new vscode.EventEmitter<ValidationTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<ValidationTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    private issues: ValidationIssue[] = [];
    private issuesByFile: Map<string, ValidationIssue[]> = new Map();

    constructor(private lrmService: LrmService) {}

    refresh(): void {
        this.issues = [];
        this.issuesByFile.clear();
        this._onDidChangeTreeData.fire();
    }

    setIssues(issues: ValidationIssue[]): void {
        this.issues = issues;
        this.issuesByFile.clear();

        // Group issues by file
        for (const issue of issues) {
            const existing = this.issuesByFile.get(issue.file) || [];
            existing.push(issue);
            this.issuesByFile.set(issue.file, existing);
        }

        this._onDidChangeTreeData.fire();
    }

    getIssueCount(): number {
        return this.issues.length;
    }

    getTreeItem(element: ValidationTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: ValidationTreeItem): Promise<ValidationTreeItem[]> {
        if (!element) {
            // Root level - show files with issues
            return this.getFileItems();
        }

        if (element instanceof ValidationFileItem) {
            // Show issues for this file
            return this.getIssueItems(element.filePath);
        }

        return [];
    }

    getParent(element: ValidationTreeItem): vscode.ProviderResult<ValidationTreeItem> {
        if (element instanceof ValidationIssueItem) {
            return new ValidationFileItem(element.issue.file, this.issuesByFile.get(element.issue.file)?.length || 0);
        }
        return null;
    }

    private getFileItems(): ValidationFileItem[] {
        const items: ValidationFileItem[] = [];

        for (const [filePath, fileIssues] of this.issuesByFile) {
            items.push(new ValidationFileItem(filePath, fileIssues.length));
        }

        return items.sort((a, b) => b.issueCount - a.issueCount);
    }

    private getIssueItems(filePath: string): ValidationIssueItem[] {
        const fileIssues = this.issuesByFile.get(filePath) || [];
        return fileIssues.map(issue => new ValidationIssueItem(issue));
    }
}

export class ValidationFileItem extends vscode.TreeItem {
    constructor(
        public readonly filePath: string,
        public readonly issueCount: number
    ) {
        super(path.basename(filePath), vscode.TreeItemCollapsibleState.Expanded);

        this.contextValue = 'validationFile';
        this.description = `${issueCount} issue(s)`;
        this.tooltip = filePath;
        this.iconPath = new vscode.ThemeIcon('file-code');
        this.resourceUri = vscode.Uri.file(filePath);
    }
}

export class ValidationIssueItem extends vscode.TreeItem {
    constructor(public readonly issue: ValidationIssue) {
        super(issue.key, vscode.TreeItemCollapsibleState.None);

        this.contextValue = 'validationIssue';
        this.description = this.getIssueDescription();
        this.tooltip = new vscode.MarkdownString();
        this.tooltip.appendMarkdown(`**${issue.type.toUpperCase()}**: ${issue.key}\n\n`);
        this.tooltip.appendMarkdown(issue.message);
        if (issue.language) {
            this.tooltip.appendMarkdown(`\n\nLanguage: ${issue.language}`);
        }

        // Set icon and color based on severity
        switch (issue.severity) {
            case 'error':
                this.iconPath = new vscode.ThemeIcon('error', new vscode.ThemeColor('charts.red'));
                break;
            case 'warning':
                this.iconPath = new vscode.ThemeIcon('warning', new vscode.ThemeColor('charts.yellow'));
                break;
            default:
                this.iconPath = new vscode.ThemeIcon('info', new vscode.ThemeColor('charts.blue'));
                break;
        }

        // Set command to go to the issue
        this.command = {
            command: 'lrm.goToKey',
            title: 'Go to Key',
            arguments: [issue.file, issue.key]
        };
    }

    private getIssueDescription(): string {
        switch (this.issue.type) {
            case 'missing':
                return `Missing translation for ${this.issue.language || 'language'}`;
            case 'duplicate':
                return 'Duplicate key';
            case 'empty':
                return 'Empty value';
            case 'placeholder':
                return 'Placeholder mismatch';
            case 'unused':
                return 'Unused key';
            default:
                return this.issue.message;
        }
    }
}
