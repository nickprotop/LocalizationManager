import * as vscode from 'vscode';
import { ApiClient } from '../backend/apiClient';
import { LrmService } from '../backend/lrmService';

export class StatusBarManager {
    private statusBarItem: vscode.StatusBarItem;
    private apiClient: ApiClient;
    private lrmService: LrmService;
    private updateInterval: NodeJS.Timeout | undefined;

    constructor(apiClient: ApiClient, lrmService: LrmService) {
        this.apiClient = apiClient;
        this.lrmService = lrmService;

        // Create status bar item (aligned to right, priority 100)
        this.statusBarItem = vscode.window.createStatusBarItem(
            vscode.StatusBarAlignment.Right,
            100
        );

        this.statusBarItem.command = 'lrm.showQuickActions';
        this.statusBarItem.tooltip = 'Click for LRM quick actions';
        this.statusBarItem.show();

        // Update every 30 seconds
        this.startAutoUpdate();
    }

    /**
     * Points the status bar at a new API client. Required after the backend is
     * restarted or the resource path changes, because the client bakes the
     * (now stale) port into its base URL — without this, update() would keep
     * probing the dead port and report "Failed" even on a healthy backend.
     */
    public setApiClient(apiClient: ApiClient): void {
        this.apiClient = apiClient;
    }

    private startAutoUpdate(): void {
        // Initial update
        this.update();

        // Auto-update every 30 seconds
        this.updateInterval = setInterval(() => {
            this.update();
        }, 30000);
    }

    /**
     * Refreshes the status bar from the backend. Returns true when the backend was
     * reachable and stats were rendered, false when it appears down (status shows
     * "Failed"). Callers (e.g. the restart command) rely on this return value to
     * avoid reporting success while the backend is actually down (issue #6).
     */
    public async update(): Promise<boolean> {
        try {
            // Get stats from API
            const stats = await this.apiClient.getStats();

            // Calculate translation coverage from languages
            const languages = stats.languages || [];

            // Use overall coverage from API
            const avgCoverage = Math.round(stats.overallCoverage || 0);

            // Count total missing translations across all languages (excluding default)
            const totalMissing = languages
                .filter(lang => !lang.isDefault)
                .reduce((sum, lang) => sum + (lang.totalCount - lang.translatedCount), 0);

            // Determine validation status
            let validationIcon = '$(check)';
            let validationText = 'Valid';

            if (totalMissing > 10) {
                validationIcon = '$(warning)';
                validationText = `${totalMissing} missing`;
            }

            // Service status
            const serviceIcon = '$(circle-filled)';
            const serviceText = 'Running';

            // Build status bar text
            this.statusBarItem.text = `$(globe) LRM: ${avgCoverage}% | ${validationIcon} ${validationText} | ${serviceIcon} ${serviceText}`;

            // Update tooltip with detailed info
            this.statusBarItem.tooltip = this.buildTooltip(stats, avgCoverage, totalMissing);

            return true;
        } catch (error) {
            // Service is down
            this.statusBarItem.text = '$(error) LRM: Failed';
            this.statusBarItem.tooltip = 'Localization Manager service is not running. Click to restart.';
            this.statusBarItem.command = 'lrm.restartBackend';
            return false;
        }
    }

    /**
     * Resolves a culture code (e.g. "it") to a display name (e.g. "Italian"),
     * falling back to the upper-cased code when the code is unknown.
     */
    private formatLanguageName(code: string): string {
        try {
            const cultureName = new Intl.DisplayNames(['en'], { type: 'language' });
            return cultureName.of(code) || code.toUpperCase();
        } catch {
            return code.toUpperCase();
        }
    }

    private buildTooltip(stats: any, avgCoverage: number, totalMissing: number): string {
        const languages = stats.languages || [];
        const resourcePath = this.lrmService.getResourcePath() || 'Not configured';

        const lines = [
            'Localization Manager',
            '',
            `📁 Resource Folder:`,
            `  ${resourcePath}`,
            '',
            `Translation Coverage: ${avgCoverage}%`,
            `  Total Keys: ${stats.totalKeys}`,
            `  Languages: ${languages.length}`,
            `  Missing Translations: ${totalMissing}`,
            '',
            'Languages:',
            ...languages.map((lang: any) => {
                // The default file carries the configured defaultLanguageCode; only
                // fall back to a generic label when no concrete code is reported.
                const code = lang.languageCode;
                const hasConcreteCode = code && code !== '' && code !== 'default';
                let langName: string;
                if (lang.isDefault) {
                    langName = hasConcreteCode ? `${this.formatLanguageName(code)} (Default)` : 'Default';
                } else {
                    langName = this.formatLanguageName(code);
                }
                return `  ${langName}: ${Math.round(lang.coverage)}%`;
            }),
            '',
            'Click for quick actions'
        ];

        return lines.join('\n');
    }

    public dispose(): void {
        if (this.updateInterval) {
            clearInterval(this.updateInterval);
        }
        this.statusBarItem.dispose();
    }
}
