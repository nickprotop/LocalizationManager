import * as vscode from 'vscode';
import { ApiClient } from '../backend/apiClient';

export class DashboardPanel {
    public static currentPanel: DashboardPanel | undefined;
    private readonly panel: vscode.WebviewPanel;
    private readonly apiClient: ApiClient;
    private disposables: vscode.Disposable[] = [];
    private autoRefreshInterval: NodeJS.Timeout | undefined;

    private constructor(panel: vscode.WebviewPanel, apiClient: ApiClient) {
        this.panel = panel;
        this.apiClient = apiClient;

        // Set the webview's initial html content
        this.update();

        // Listen for when the panel is disposed
        this.panel.onDidDispose(() => this.dispose(), null, this.disposables);

        // Handle messages from the webview
        this.panel.webview.onDidReceiveMessage(
            message => {
                switch (message.command) {
                    case 'openResourceEditor':
                        vscode.commands.executeCommand('lrm.openResourceEditor');
                        break;
                    case 'openSettings':
                        vscode.commands.executeCommand('lrm.openSettings');
                        break;
                    case 'refresh':
                        this.update();
                        break;
                }
            },
            null,
            this.disposables
        );

        // Auto-refresh every 30 seconds
        this.startAutoRefresh();
    }

    public static createOrShow(apiClient: ApiClient): void {
        const column = vscode.window.activeTextEditor
            ? vscode.window.activeTextEditor.viewColumn
            : undefined;

        // If we already have a panel, show it
        if (DashboardPanel.currentPanel) {
            DashboardPanel.currentPanel.panel.reveal(column);
            DashboardPanel.currentPanel.update();
            return;
        }

        // Otherwise, create a new panel
        const panel = vscode.window.createWebviewPanel(
            'lrmDashboard',
            'Localization Dashboard',
            column || vscode.ViewColumn.One,
            {
                enableScripts: true,
                retainContextWhenHidden: true
            }
        );

        DashboardPanel.currentPanel = new DashboardPanel(panel, apiClient);
    }

    private startAutoRefresh(): void {
        this.autoRefreshInterval = setInterval(() => {
            this.update();
        }, 30000); // 30 seconds
    }

    public async update(): Promise<void> {
        try {
            const stats = await this.apiClient.getStats();
            this.panel.webview.html = this.getHtmlContent(stats);
        } catch (error: any) {
            console.error('Dashboard update failed:', error);
            console.error('Error details:', error.response?.data, error.message);
            this.panel.webview.html = this.getErrorHtml(error.message);
        }
    }

    private getHtmlContent(stats: any): string {
        // Calculate translation coverage for each language
        const languageStats = stats.languages || [];
        const totalKeys = stats.totalKeys || 0;

        // Calculate average coverage across all languages
        const avgCoverage = stats.overallCoverage || 0;

        // Count total missing across all languages (excluding default)
        const totalMissingAcrossAllLangs = languageStats
            .filter((lang: any) => !lang.isDefault)
            .reduce((sum: number, lang: any) => {
                const missing = lang.totalCount - lang.translatedCount;
                return sum + missing;
            }, 0);

        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Localization Dashboard</title>
    <style>
        body {
            font-family: var(--vscode-font-family);
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
            padding: 20px;
            margin: 0;
        }
        h1 {
            font-size: 24px;
            margin-bottom: 20px;
            border-bottom: 1px solid var(--vscode-panel-border);
            padding-bottom: 10px;
        }
        h2 {
            font-size: 18px;
            margin-top: 30px;
            margin-bottom: 15px;
        }
        .section {
            background-color: var(--vscode-editor-inactiveSelectionBackground);
            border: 1px solid var(--vscode-panel-border);
            border-radius: 6px;
            padding: 20px;
            margin-bottom: 20px;
        }
        .language-bar {
            display: flex;
            align-items: center;
            margin-bottom: 12px;
        }
        .language-name {
            min-width: 150px;
            font-weight: 500;
        }
        .progress-bar {
            flex: 1;
            height: 24px;
            background-color: var(--vscode-input-background);
            border-radius: 4px;
            overflow: hidden;
            position: relative;
            margin: 0 15px;
        }
        .progress-fill {
            height: 100%;
            background: linear-gradient(90deg,
                var(--vscode-progressBar-background) 0%,
                var(--vscode-charts-green) 100%);
            transition: width 0.3s ease;
        }
        .progress-text {
            position: absolute;
            width: 100%;
            text-align: center;
            line-height: 24px;
            font-size: 12px;
            font-weight: bold;
            mix-blend-mode: difference;
        }
        .stats-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 15px;
            margin-bottom: 20px;
        }
        .stat-card {
            padding: 15px;
            background-color: var(--vscode-input-background);
            border-radius: 6px;
            border-left: 4px solid var(--vscode-activityBarBadge-background);
        }
        .stat-value {
            font-size: 28px;
            font-weight: bold;
            margin-bottom: 5px;
        }
        .stat-label {
            font-size: 13px;
            opacity: 0.8;
        }
        .error-badge {
            color: var(--vscode-errorForeground);
        }
        .warning-badge {
            color: var(--vscode-editorWarning-foreground);
        }
        .success-badge {
            color: var(--vscode-charts-green);
        }
        .button-group {
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
        }
        button {
            padding: 10px 20px;
            background-color: var(--vscode-button-background);
            color: var(--vscode-button-foreground);
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
            font-family: var(--vscode-font-family);
        }
        button:hover {
            background-color: var(--vscode-button-hoverBackground);
        }
        .refresh-btn {
            background-color: var(--vscode-button-secondaryBackground);
            color: var(--vscode-button-secondaryForeground);
        }
        .refresh-btn:hover {
            background-color: var(--vscode-button-secondaryHoverBackground);
        }
        .issue-list {
            list-style: none;
            padding: 0;
        }
        .issue-item {
            padding: 10px;
            margin-bottom: 8px;
            background-color: var(--vscode-input-background);
            border-radius: 4px;
            border-left: 3px solid var(--vscode-editorWarning-foreground);
        }
    </style>
</head>
<body>
    <h1>🌍 Localization Dashboard</h1>

    <div class="section">
        <h2>Translation Coverage</h2>
        <div class="stats-grid">
            <div class="stat-card">
                <div class="stat-value">${totalKeys}</div>
                <div class="stat-label">Total Keys</div>
            </div>
            <div class="stat-card">
                <div class="stat-value success-badge">${avgCoverage}%</div>
                <div class="stat-label">Avg Coverage</div>
            </div>
            <div class="stat-card">
                <div class="stat-value warning-badge">${totalMissingAcrossAllLangs}</div>
                <div class="stat-label">Missing Translations</div>
            </div>
            <div class="stat-card">
                <div class="stat-value">${languageStats.length}</div>
                <div class="stat-label">Languages</div>
            </div>
        </div>

        ${languageStats.map((lang: any) => {
            const percentage = Math.round(lang.coverage);
            const translated = lang.translatedCount;

            // Format language name from languageCode
            let langName: string;
            if (lang.isDefault || lang.languageCode === '' || lang.languageCode === 'default') {
                langName = 'English (Default)';
            } else {
                // Try to get proper language name from CultureInfo
                try {
                    const cultureName = new Intl.DisplayNames(['en'], { type: 'language' });
                    langName = cultureName.of(lang.languageCode) || lang.languageCode.toUpperCase();
                } catch {
                    langName = lang.languageCode.toUpperCase();
                }
            }

            return `
                <div class="language-bar">
                    <div class="language-name">${langName}</div>
                    <div class="progress-bar">
                        <div class="progress-fill" style="width: ${percentage}%"></div>
                        <div class="progress-text">${translated}/${lang.totalCount} (${percentage}%)</div>
                    </div>
                </div>
            `;
        }).join('')}
    </div>

    <div class="section">
        <h2>Validation Issues</h2>
        <div class="stats-grid">
            <div class="stat-card">
                <div class="stat-value error-badge">0</div>
                <div class="stat-label">Errors</div>
            </div>
            <div class="stat-card">
                <div class="stat-value warning-badge">${totalMissingAcrossAllLangs}</div>
                <div class="stat-label">Missing Translations</div>
            </div>
        </div>
        ${totalMissingAcrossAllLangs > 0 ? `
            <p>💡 Open the Resource Editor to add missing translations</p>
        ` : `
            <p class="success-badge">✓ All translations complete!</p>
        `}
    </div>

    <div class="section">
        <h2>Quick Actions</h2>
        <div class="button-group">
            <button onclick="openResourceEditor()">📝 Open Resource Editor</button>
            <button onclick="openSettings()">⚙️ Settings</button>
            <button class="refresh-btn" onclick="refresh()">↻ Refresh</button>
        </div>
    </div>

    <script>
        const vscode = acquireVsCodeApi();

        function openResourceEditor() {
            vscode.postMessage({ command: 'openResourceEditor' });
        }

        function openSettings() {
            vscode.postMessage({ command: 'openSettings' });
        }

        function refresh() {
            vscode.postMessage({ command: 'refresh' });
        }
    </script>
</body>
</html>`;
    }

    private getErrorHtml(errorMessage?: string): string {
        return `<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>Dashboard Error</title>
    <style>
        body {
            font-family: var(--vscode-font-family);
            color: var(--vscode-foreground);
            background-color: var(--vscode-editor-background);
            padding: 40px;
            text-align: center;
        }
        .error-icon {
            font-size: 48px;
            margin-bottom: 20px;
        }
        h1 {
            color: var(--vscode-errorForeground);
        }
        .error-details {
            background-color: var(--vscode-input-background);
            border: 1px solid var(--vscode-panel-border);
            border-radius: 4px;
            padding: 15px;
            margin: 20px auto;
            max-width: 600px;
            text-align: left;
            font-family: var(--vscode-editor-font-family);
            font-size: 12px;
        }
    </style>
</head>
<body>
    <div class="error-icon">❌</div>
    <h1>Unable to Load Dashboard</h1>
    <p>The Localization Manager service is not running.</p>
    <p>Please check the LRM Backend output channel for details.</p>
    ${errorMessage ? `<div class="error-details"><strong>Error:</strong> ${errorMessage}</div>` : ''}
</body>
</html>`;
    }

    public dispose(): void {
        DashboardPanel.currentPanel = undefined;

        if (this.autoRefreshInterval) {
            clearInterval(this.autoRefreshInterval);
        }

        this.panel.dispose();

        while (this.disposables.length) {
            const disposable = this.disposables.pop();
            if (disposable) {
                disposable.dispose();
            }
        }
    }
}
