import * as vscode from 'vscode';
import { LrmService, TranslationStats } from '../services/lrmService';

export type StatsTreeItem = StatsOverviewItem | StatsLanguageItem;

export class StatsTreeProvider implements vscode.TreeDataProvider<StatsTreeItem> {
    private _onDidChangeTreeData: vscode.EventEmitter<StatsTreeItem | undefined | null | void> = new vscode.EventEmitter<StatsTreeItem | undefined | null | void>();
    readonly onDidChangeTreeData: vscode.Event<StatsTreeItem | undefined | null | void> = this._onDidChangeTreeData.event;

    private stats: TranslationStats | null = null;
    private resourcePath: string | null = null;

    constructor(private lrmService: LrmService) {}

    refresh(): void {
        this.stats = null;
        this._onDidChangeTreeData.fire();
    }

    setStats(stats: TranslationStats, resourcePath: string): void {
        this.stats = stats;
        this.resourcePath = resourcePath;
        this._onDidChangeTreeData.fire();
    }

    getTreeItem(element: StatsTreeItem): vscode.TreeItem {
        return element;
    }

    async getChildren(element?: StatsTreeItem): Promise<StatsTreeItem[]> {
        if (!this.stats) {
            return [new StatsPlaceholderItem()];
        }

        if (!element) {
            return this.getOverviewItems();
        }

        if (element instanceof StatsOverviewItem && element.label === 'Languages') {
            return this.getLanguageItems();
        }

        return [];
    }

    private getOverviewItems(): StatsTreeItem[] {
        if (!this.stats) {
            return [];
        }

        const items: StatsTreeItem[] = [
            new StatsOverviewItem('Total Keys', String(this.stats.totalKeys), 'symbol-key'),
            new StatsOverviewItem('Translated', String(this.stats.translatedKeys), 'check'),
            new StatsOverviewItem('Missing', String(this.stats.missingKeys), 'warning'),
            new StatsOverviewItem('Coverage', `${this.stats.percentage.toFixed(1)}%`, 'pie-chart'),
            new StatsOverviewItem(
                'Languages',
                `${this.stats.byLanguage.size}`,
                'globe',
                vscode.TreeItemCollapsibleState.Collapsed
            )
        ];

        return items;
    }

    private getLanguageItems(): StatsLanguageItem[] {
        if (!this.stats) {
            return [];
        }

        const items: StatsLanguageItem[] = [];

        for (const [language, langStats] of this.stats.byLanguage) {
            items.push(new StatsLanguageItem(language, langStats));
        }

        return items.sort((a, b) => b.stats.percentage - a.stats.percentage);
    }
}

class StatsPlaceholderItem extends vscode.TreeItem {
    constructor() {
        super('No statistics available', vscode.TreeItemCollapsibleState.None);
        this.description = 'Run validation to see stats';
        this.iconPath = new vscode.ThemeIcon('info');
    }
}

export class StatsOverviewItem extends vscode.TreeItem {
    constructor(
        label: string,
        value: string,
        iconName: string,
        collapsibleState: vscode.TreeItemCollapsibleState = vscode.TreeItemCollapsibleState.None
    ) {
        super(label, collapsibleState);

        this.description = value;
        this.iconPath = new vscode.ThemeIcon(iconName);
        this.contextValue = 'statsOverview';
    }
}

export class StatsLanguageItem extends vscode.TreeItem {
    constructor(
        public readonly language: string,
        public readonly stats: { translated: number; missing: number; percentage: number }
    ) {
        super(language, vscode.TreeItemCollapsibleState.None);

        this.description = `${stats.percentage.toFixed(1)}% (${stats.translated}/${stats.translated + stats.missing})`;
        this.contextValue = 'statsLanguage';

        // Set icon color based on percentage
        let iconColor: vscode.ThemeColor;
        if (stats.percentage >= 90) {
            iconColor = new vscode.ThemeColor('charts.green');
        } else if (stats.percentage >= 70) {
            iconColor = new vscode.ThemeColor('charts.yellow');
        } else if (stats.percentage >= 50) {
            iconColor = new vscode.ThemeColor('charts.orange');
        } else {
            iconColor = new vscode.ThemeColor('charts.red');
        }

        this.iconPath = new vscode.ThemeIcon('globe', iconColor);

        // Tooltip with details
        this.tooltip = new vscode.MarkdownString();
        this.tooltip.appendMarkdown(`**${language}**\n\n`);
        this.tooltip.appendMarkdown(`- Translated: ${stats.translated}\n`);
        this.tooltip.appendMarkdown(`- Missing: ${stats.missing}\n`);
        this.tooltip.appendMarkdown(`- Coverage: ${stats.percentage.toFixed(1)}%`);
    }
}
