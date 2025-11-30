import * as vscode from 'vscode';
import { ApiClient, ResourceKey } from '../backend/apiClient';

export class ResourceTreeView implements vscode.TreeDataProvider<ResourceTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<ResourceTreeItem | undefined | null | void> = new vscode.EventEmitter<ResourceTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<ResourceTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    private apiClient: ApiClient;
    private resourceKeys: ResourceKey[] = [];

    constructor(apiClient: ApiClient) {
        this.apiClient = apiClient;
    }

    public refresh(): void {
        this._onDidChangeTreeData.fire();
    }

    public async loadResources(): Promise<void> {
        try {
            this.resourceKeys = await this.apiClient.getKeys();
            this.refresh();
        } catch (error) {
            console.error('Failed to load resources:', error);
            this.resourceKeys = [];
        }
    }

    getTreeItem(element: ResourceTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: ResourceTreeItem): Promise<ResourceTreeItem[]> {
        if (!element) {
            // Root level - show all keys
            if (this.resourceKeys.length === 0) {
                // Try to load if empty
                await this.loadResources();
            }

            return this.resourceKeys.map(key => {
                const hasTranslations = Object.keys(key.values).length > 0;
                return new ResourceTreeItem(
                    key.key,
                    key,
                    hasTranslations ? vscode.TreeItemCollapsibleState.Collapsed : vscode.TreeItemCollapsibleState.None
                );
            });
        } else {
            // Child level - show translations for this key
            const children = this.getTranslationsForKey(element.resourceKey);
            return children;
        }
    }

    private getTranslationsForKey(resourceKey: ResourceKey): ResourceTreeItem[] {
        const items: ResourceTreeItem[] = [];

        for (const [language, value] of Object.entries(resourceKey.values)) {
            const item = new ResourceTreeItem(
                `${language}: ${value}`,
                resourceKey,
                vscode.TreeItemCollapsibleState.None,
                language
            );

            // Set icon based on whether value is empty
            if (!value || value.trim() === '') {
                item.iconPath = new vscode.ThemeIcon('warning', new vscode.ThemeColor('problemsWarningIcon.foreground'));
                item.tooltip = `${language}: (empty value)`;
            } else {
                item.iconPath = new vscode.ThemeIcon('symbol-string');
                item.tooltip = `${language}: ${value}`;
            }

            items.push(item);
        }

        return items;
    }

    public getResourceKeys(): ResourceKey[] {
        return this.resourceKeys;
    }
}

export class ResourceTreeItem extends vscode.TreeItem {
    constructor(
        public readonly label: string,
        public readonly resourceKey: ResourceKey,
        public readonly collapsibleState: vscode.TreeItemCollapsibleState,
        public readonly language?: string
    ) {
        super(label, collapsibleState);

        if (!language) {
            // Root level - key item (parent)
            this.tooltip = `${resourceKey.key} (${Object.keys(resourceKey.values).length} translations)`;
            this.iconPath = new vscode.ThemeIcon('key');

            // Add badges for issues
            if (resourceKey.hasDuplicates) {
                this.description = '$(error) Duplicate';
            }

            // Context value for commands
            this.contextValue = 'resourceKey';

            // Don't add command - let it expand naturally
        } else {
            // Child level - translation item (leaf)
            this.contextValue = 'resourceTranslation';
        }
    }
}
