import * as vscode from 'vscode';
import { CacheService } from '../backend/cacheService';
import { getParserFactory, ResourceParserFactory } from '../parsers';

/**
 * CodeLens provider for LRM extension.
 * Shows inline information and actions for localization keys in:
 * - .resx files: reference count, language coverage, translate action, unused/duplicate warnings
 * - .json files: reference count, language coverage, translate action, unused/duplicate warnings
 * - Code files: key value, missing languages
 */
export class LrmCodeLensProvider implements vscode.CodeLensProvider {
    private _onDidChangeCodeLenses = new vscode.EventEmitter<void>();
    readonly onDidChangeCodeLenses = this._onDidChangeCodeLenses.event;

    private cacheService: CacheService;
    private parserFactory: ResourceParserFactory;

    constructor(cacheService: CacheService) {
        this.cacheService = cacheService;
        this.parserFactory = getParserFactory();
    }

    /**
     * Refresh all CodeLenses (call when data changes)
     */
    refresh(): void {
        this._onDidChangeCodeLenses.fire();
    }

    async provideCodeLenses(document: vscode.TextDocument, token: vscode.CancellationToken): Promise<vscode.CodeLens[]> {
        // Check if CodeLens is enabled
        const config = vscode.workspace.getConfiguration('lrm');
        if (!config.get<boolean>('enableCodeLens', true)) {
            console.log('LRM CodeLens: disabled in settings');
            return [];
        }

        console.log(`LRM CodeLens: provideCodeLenses called for ${document.fileName}`);

        try {
            // Check if this is a resource file (RESX or JSON)
            if (this.isResourceFile(document)) {
                const lenses = await this.provideResourceFileCodeLenses(document, token);
                console.log(`LRM CodeLens: returning ${lenses.length} lenses for resource file`);
                return lenses;
            } else {
                const lenses = await this.provideCodeFileCodeLenses(document, token);
                console.log(`LRM CodeLens: returning ${lenses.length} lenses for code file`);
                return lenses;
            }
        } catch (error) {
            console.error('LRM CodeLens error:', error);
            return [];
        }
    }

    /**
     * Check if a document is a resource file (RESX or JSON)
     */
    private isResourceFile(document: vscode.TextDocument): boolean {
        const fileName = document.fileName.toLowerCase();

        // Always consider .resx files as resource files
        if (fileName.endsWith('.resx')) {
            return true;
        }

        // For JSON, check if it's a likely resource file (not config)
        if (fileName.endsWith('.json')) {
            // Use the parser factory if initialized
            if (this.parserFactory.isInitialized()) {
                return this.parserFactory.isResourceDocument(document);
            }

            // Fallback: check common patterns
            const baseName = fileName.replace(/\.json$/, '');
            return this.matchesResourcePattern(baseName);
        }

        return false;
    }

    /**
     * Check if a filename matches common resource file patterns
     */
    private matchesResourcePattern(baseName: string): boolean {
        // Common resource file patterns
        const resourcePatterns = [
            /^strings(\.[a-z]{2}(-[a-z]{2,4})?)?$/i,
            /^messages(\.[a-z]{2}(-[a-z]{2,4})?)?$/i,
            /^translations?(\.[a-z]{2}(-[a-z]{2,4})?)?$/i,
            /^[a-z]{2}(-[a-z]{2,4})?$/i,  // Culture code only (e.g., en.json, fr-FR.json)
        ];

        return resourcePatterns.some(p => p.test(baseName));
    }

    /**
     * Provide CodeLenses for resource files (.resx or .json)
     * Shows above each key entry:
     * - Reference count (e.g., "12 references")
     * - Language coverage (e.g., "3/5 languages")
     * - Translate action
     * - Unused warning (if 0 references)
     * - Duplicate warning (if duplicated)
     */
    private async provideResourceFileCodeLenses(document: vscode.TextDocument, token: vscode.CancellationToken): Promise<vscode.CodeLens[]> {
        const lenses: vscode.CodeLens[] = [];
        const config = vscode.workspace.getConfiguration('lrm');

        console.log(`LRM CodeLens: provideResourceFileCodeLenses for ${document.fileName}`);

        // Pre-fetch data for all keys
        try {
            // Get scan results for reference counts (don't force refresh, use cache)
            await this.cacheService.getScanResults(false);
            console.log('LRM CodeLens: scan results fetched');

            // Get validation for duplicates
            await this.cacheService.getValidation(false);
            console.log('LRM CodeLens: validation fetched');
        } catch (error) {
            console.log('LRM CodeLens: cache fetch error:', error);
            // Continue without cached data
        }

        // Get the appropriate parser for this document type
        const parser = this.parserFactory.getParser(document);
        const keys = parser.parseDocument(document);

        console.log(`LRM CodeLens: parser found ${keys.length} keys`);

        for (const key of keys) {
            if (token.isCancellationRequested) {
                break;
            }

            const startPos = new vscode.Position(key.lineNumber, key.columnStart);
            const range = new vscode.Range(startPos, startPos);

            // Get reference count from cache
            const refCount = this.cacheService.getReferenceCountFromCache(key.key);
            const isUnused = this.cacheService.isKeyUnused(key.key);
            const isDuplicate = this.cacheService.isKeyDuplicate(key.key);

            // Reference count lens
            if (config.get<boolean>('codeLens.showReferences', true)) {
                if (refCount !== null) {
                    const refLabel = refCount === 1 ? '1 reference' : `${refCount} references`;
                    lenses.push(new vscode.CodeLens(range, {
                        title: refLabel,
                        command: 'lrm.showKeyReferences',
                        arguments: [key.key]
                    }));
                }
            }

            // Language coverage lens
            if (config.get<boolean>('codeLens.showCoverage', true)) {
                try {
                    const details = await this.cacheService.getKeyDetails(key.key);
                    const languages = Object.keys(details.values);
                    const filledCount = Object.values(details.values).filter(v => v.value && v.value.trim() !== '').length;

                    if (languages.length > 0) {
                        const coverageLabel = `${filledCount}/${languages.length} languages`;
                        lenses.push(new vscode.CodeLens(range, {
                            title: coverageLabel,
                            command: 'lrm.showMissingLanguages',
                            arguments: [key.key]
                        }));
                    }
                } catch (error) {
                    // Skip coverage lens if we can't get details
                }
            }

            // Translate action lens
            if (config.get<boolean>('codeLens.showTranslate', true)) {
                lenses.push(new vscode.CodeLens(range, {
                    title: 'Translate',
                    command: 'lrm.translateKeyFromLens',
                    arguments: [key.key]
                }));
            }

            // Unused warning lens
            if (isUnused === true) {
                lenses.push(new vscode.CodeLens(range, {
                    title: '$(warning) Unused',
                    command: 'lrm.deleteUnusedKey',
                    arguments: [key.key]
                }));
            }

            // Duplicate warning lens
            if (isDuplicate === true) {
                lenses.push(new vscode.CodeLens(range, {
                    title: '$(warning) Duplicate',
                    command: 'lrm.mergeDuplicateKey',
                    arguments: [key.key]
                }));
            }
        }

        console.log(`LRM CodeLens: found ${keys.length} keys, created ${lenses.length} lenses`);
        return lenses;
    }

    /**
     * Provide CodeLenses for code files (.cs, .razor, .xaml, .cshtml)
     * Shows above resource key references:
     * - Key value (e.g., "Welcome to our app")
     * - Missing languages warning (e.g., "Missing: el, de")
     * - Key not found warning
     *
     * Key discovery is delegated to the backend scanner (`scanFile`) rather than
     * client-side regex. This is deliberate: the backend ignores `namespace` /
     * `using` declarations (so a nested namespace like `Vitrum.Resources.Components`
     * no longer produces a phantom `Components` key — issue #6 Bug 2), and it honors
     * configured `localizationMethods` plus auto-detected `@inject IStringLocalizer<T>`
     * variables (so value hints appear for `Q["..."]`, `Loc["..."]`, etc., not only
     * `L` — issue #6 Feature). The CodeLens and the diagnostics now share one source
     * of truth.
     */
    private async provideCodeFileCodeLenses(document: vscode.TextDocument, token: vscode.CancellationToken): Promise<vscode.CodeLens[]> {
        const lenses: vscode.CodeLens[] = [];
        const config = vscode.workspace.getConfiguration('lrm');

        console.log(`LRM CodeLens: provideCodeFileCodeLenses for ${document.fileName}`);

        // Ask the backend what keys this file references (and which are missing).
        let scan;
        try {
            scan = await this.cacheService.scanFile(document.uri.fsPath, document.getText());
        } catch (error) {
            console.log('LRM CodeLens: scanFile error:', error);
            return [];
        }

        if (token.isCancellationRequested) {
            return [];
        }

        const missingSet = new Set(scan.missing || []);

        // Track processed (line, key) pairs to avoid duplicate lenses on the same line.
        const processedLines = new Map<number, Set<string>>();

        for (const usage of scan.references || []) {
            if (token.isCancellationRequested) {
                break;
            }

            const keyName = usage.key;

            for (const reference of usage.references || []) {
                // Backend reports 1-based line numbers; CodeLens positions are 0-based.
                const lineNumber = Math.max(0, (reference.line ?? 1) - 1);

                if (!processedLines.has(lineNumber)) {
                    processedLines.set(lineNumber, new Set());
                }
                if (processedLines.get(lineNumber)!.has(keyName)) {
                    continue;
                }
                processedLines.get(lineNumber)!.add(keyName);

                const startPos = new vscode.Position(lineNumber, 0);
                const range = new vscode.Range(startPos, startPos);

                if (missingSet.has(keyName)) {
                    // Key referenced but not defined in resources.
                    lenses.push(new vscode.CodeLens(range, {
                        title: `$(error) Key not found: ${keyName}`,
                        command: 'lrm.addKeyWithValueQuickFix',
                        arguments: [keyName]
                    }));
                    continue;
                }

                try {
                    const details = await this.cacheService.getKeyDetails(keyName);

                    // Show value lens
                    if (config.get<boolean>('codeLens.showValue', true)) {
                        // Get the default language value
                        const defaultValue = details.values['default']?.value ||
                                            details.values['']?.value ||
                                            Object.values(details.values)[0]?.value;

                        if (defaultValue) {
                            // Truncate long values
                            const displayValue = defaultValue.length > 40
                                ? defaultValue.substring(0, 37) + '...'
                                : defaultValue;

                            lenses.push(new vscode.CodeLens(range, {
                                title: `"${displayValue}"`,
                                command: 'lrm.editKeyFromLens',
                                arguments: [keyName]
                            }));
                        }
                    }

                    // Show missing languages lens
                    const missingLanguages = this.cacheService.getMissingLanguages(keyName);
                    if (missingLanguages && missingLanguages.length > 0) {
                        // Filter out 'default' from missing list
                        const displayMissing = missingLanguages.filter(l => l !== 'default' && l !== '');

                        if (displayMissing.length > 0) {
                            const missingLabel = `Missing: ${displayMissing.slice(0, 3).join(', ')}${displayMissing.length > 3 ? '...' : ''}`;
                            lenses.push(new vscode.CodeLens(range, {
                                title: `$(warning) ${missingLabel}`,
                                command: 'lrm.translateKeyFromLens',
                                arguments: [keyName]
                            }));
                        }
                    }
                } catch (error) {
                    // Details lookup failed even though the key wasn't reported missing;
                    // surface it as not-found so the user still gets a quick fix.
                    lenses.push(new vscode.CodeLens(range, {
                        title: `$(error) Key not found: ${keyName}`,
                        command: 'lrm.addKeyWithValueQuickFix',
                        arguments: [keyName]
                    }));
                }
            }
        }

        return lenses;
    }
}
