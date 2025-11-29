import * as vscode from 'vscode';
import * as path from 'path';
import { LrmService, ResourceGroup, ResourceFile, ResourceKey } from '../services/lrmService';

export type ResourceTreeItem = ResourceGroupItem | ResourceFileItem | ResourceKeyItem;

export class ResourceTreeProvider implements vscode.TreeDataProvider<ResourceTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<ResourceTreeItem | undefined | null | void> = new vscode.EventEmitter<ResourceTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<ResourceTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    private resourceGroups: ResourceGroup[] = [];

    constructor(private lrmService: LrmService) {}

    refresh(): void {
        this.resourceGroups = [];
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: ResourceTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: ResourceTreeItem): Promise<ResourceTreeItem[]> {
        if (!vscode.workspace.workspaceFolders) {
            return [];
        }

        if (!element) {
            // Root level - show resource groups
            return this.getResourceGroups();
        }

        if (element instanceof ResourceGroupItem) {
            // Show files in the group
            return this.getResourceFiles(element.resourceGroup);
        }

        if (element instanceof ResourceFileItem) {
            // Show keys in the file
            return this.getResourceKeys(element.resourceFile);
        }

        return [];
    }

    getParent(element: ResourceTreeItem): vscode.ProviderResult<ResourceTreeItem> {
        if (element instanceof ResourceKeyItem) {
            // Find the parent file
            for (const group of this.resourceGroups) {
                const file = group.files.find(f => f.path === element.filePath);
                if (file) {
                    return new ResourceFileItem(file, group);
                }
            }
        }

        if (element instanceof ResourceFileItem) {
            return new ResourceGroupItem(element.resourceGroup);
        }

        return null;
    }

    private async getResourceGroups(): Promise<ResourceGroupItem[]> {
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
        if (!workspaceFolder) {
            return [];
        }

        try {
            this.resourceGroups = await this.lrmService.discoverResources(workspaceFolder.uri.fsPath);

            return this.resourceGroups.map(group => new ResourceGroupItem(group));
        } catch (error) {
            vscode.window.showErrorMessage(`Failed to discover resources: ${error}`);
            return [];
        }
    }

    private getResourceFiles(group: ResourceGroup): ResourceFileItem[] {
        return group.files.map(file => new ResourceFileItem(file, group));
    }

    private async getResourceKeys(file: ResourceFile): Promise<ResourceKeyItem[]> {
        try {
            const keys = await this.lrmService.getKeys(file.path);
            return keys.map(key => new ResourceKeyItem(key, file.path));
        } catch (error) {
            vscode.window.showErrorMessage(`Failed to get keys: ${error}`);
            return [];
        }
    }

    public getResourceGroup(basePath: string): ResourceGroup | undefined {
        return this.resourceGroups.find(g => g.basePath === basePath);
    }
}

export class ResourceGroupItem extends vscode.TreeItem {
    constructor(public readonly resourceGroup: ResourceGroup) {
        super(resourceGroup.baseName, vscode.TreeItemCollapsibleState.Collapsed);

        this.contextValue = 'resourceGroup';
        this.tooltip = `${resourceGroup.baseName}\n${resourceGroup.files.length} language(s)`;
        this.description = `${resourceGroup.files.length} file(s)`;
        this.iconPath = new vscode.ThemeIcon('folder-library');

        // Get workspace relative path
        const workspaceFolder = vscode.workspace.workspaceFolders?.[0];
        if (workspaceFolder) {
            const relativePath = path.relative(workspaceFolder.uri.fsPath, path.dirname(resourceGroup.basePath));
            if (relativePath) {
                this.description = `${relativePath} - ${resourceGroup.files.length} file(s)`;
            }
        }
    }
}

export class ResourceFileItem extends vscode.TreeItem {
    constructor(
        public readonly resourceFile: ResourceFile,
        public readonly resourceGroup: ResourceGroup
    ) {
        super(resourceFile.name, vscode.TreeItemCollapsibleState.Collapsed);

        this.contextValue = 'resourceFile';
        this.tooltip = new vscode.MarkdownString();
        this.tooltip.appendMarkdown(`**${resourceFile.name}**\n\n`);
        this.tooltip.appendMarkdown(`- Language: ${resourceFile.language || 'Default'}\n`);
        this.tooltip.appendMarkdown(`- Path: ${resourceFile.path}\n`);
        if (resourceFile.keyCount > 0) {
            this.tooltip.appendMarkdown(`- Keys: ${resourceFile.keyCount}\n`);
        }

        this.description = resourceFile.isBase ? '(base)' : resourceFile.language;

        // Set icon based on language
        if (resourceFile.isBase) {
            this.iconPath = new vscode.ThemeIcon('file-code', new vscode.ThemeColor('charts.blue'));
        } else {
            this.iconPath = new vscode.ThemeIcon('globe');
        }

        // Set command to open file
        this.command = {
            command: 'vscode.open',
            title: 'Open File',
            arguments: [vscode.Uri.file(resourceFile.path)]
        };

        this.resourceUri = vscode.Uri.file(resourceFile.path);
    }
}

export class ResourceKeyItem extends vscode.TreeItem {
    constructor(
        public readonly resourceKey: ResourceKey,
        public readonly filePath: string
    ) {
        super(resourceKey.name, vscode.TreeItemCollapsibleState.None);

        this.contextValue = 'resourceKey';

        // Build tooltip with markdown
        this.tooltip = new vscode.MarkdownString();
        this.tooltip.appendMarkdown(`**${resourceKey.name}**\n\n`);
        this.tooltip.appendCodeblock(resourceKey.value || '(empty)', 'text');
        if (resourceKey.comment) {
            this.tooltip.appendMarkdown(`\n\n*${resourceKey.comment}*`);
        }

        // Show value preview as description
        const maxLength = 50;
        const value = resourceKey.value || '';
        this.description = value.length > maxLength
            ? value.substring(0, maxLength) + '...'
            : value;

        // Set icon based on value status
        if (!resourceKey.value) {
            this.iconPath = new vscode.ThemeIcon('warning', new vscode.ThemeColor('charts.yellow'));
        } else {
            this.iconPath = new vscode.ThemeIcon('symbol-string');
        }

        // Set command to go to key in file
        this.command = {
            command: 'lrm.goToKey',
            title: 'Go to Key',
            arguments: [filePath, resourceKey.name]
        };
    }
}
