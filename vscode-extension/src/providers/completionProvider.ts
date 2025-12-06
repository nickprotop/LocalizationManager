import * as vscode from 'vscode';
import { ApiClient, ResourceKey } from '../backend/apiClient';

/**
 * Default resource class names that trigger completion.
 * These can be overridden by configuration in lrm.json.
 */
const DEFAULT_RESOURCE_CLASSES = ['Resources', 'Strings', 'AppResources'];

/**
 * Default localization method names that trigger completion.
 * These can be overridden by configuration in lrm.json.
 */
const DEFAULT_LOCALIZATION_METHODS = ['GetString', 'GetLocalizedString', 'Translate', 'L', 'T'];

/**
 * Cache entry with TTL support.
 */
interface CacheEntry<T> {
    data: T;
    timestamp: number;
}

/**
 * Provides IntelliSense autocomplete for localization keys.
 * Triggers on patterns like Resources., GetString(", _localizer[", etc.
 */
export class LocalizationCompletionProvider implements vscode.CompletionItemProvider {
    private apiClient: ApiClient;
    private keysCache: CacheEntry<ResourceKey[]> | null = null;
    private configCache: CacheEntry<any> | null = null;
    private readonly cacheTtlMs = 5000; // 5 second cache TTL

    constructor(apiClient: ApiClient) {
        this.apiClient = apiClient;
    }

    async provideCompletionItems(
        document: vscode.TextDocument,
        position: vscode.Position,
        _token: vscode.CancellationToken,
        _context: vscode.CompletionContext
    ): Promise<vscode.CompletionItem[] | undefined> {
        // Get text from start of line to cursor position
        const lineText = document.lineAt(position).text;
        const textBeforeCursor = lineText.substring(0, position.character);

        // Get configuration (resource class names, method names)
        const config = await this.getConfigurationCached();
        const resourceClasses = config?.scanning?.resourceClassNames || DEFAULT_RESOURCE_CLASSES;
        const localizationMethods = config?.scanning?.localizationMethods || DEFAULT_LOCALIZATION_METHODS;

        // Check if we're in a completion trigger context
        const triggerMatch = this.matchTriggerPattern(textBeforeCursor, resourceClasses, localizationMethods, document.languageId);

        if (!triggerMatch) {
            return undefined;
        }

        // Get keys from cache or API
        const keys = await this.getKeysCached();
        if (!keys || keys.length === 0) {
            return undefined;
        }

        // Find default language (using config for defaultLanguageCode if set)
        const defaultLang = this.findDefaultLanguage(config);

        // Filter keys by prefix if user has typed partial key
        const prefix = triggerMatch.prefix.toLowerCase();
        const filteredKeys = prefix
            ? keys.filter(k => k.key.toLowerCase().startsWith(prefix))
            : keys;

        // Create completion items
        return filteredKeys.map(key => this.createCompletionItem(key, defaultLang, triggerMatch.insertType));
    }

    /**
     * Match text against trigger patterns.
     * Returns match info or null if no match.
     */
    private matchTriggerPattern(
        text: string,
        resourceClasses: string[],
        localizationMethods: string[],
        languageId: string
    ): { prefix: string; insertType: 'property' | 'string' } | null {
        // Build dynamic patterns from configuration
        const classesPattern = resourceClasses.join('|');
        const methodsPattern = localizationMethods.join('|');

        // Pattern 1: Property access - Resources.Key, @Resources.Key (Razor)
        // Matches: Resources., Strings., AppResources., @Resources., etc.
        const propertyPattern = new RegExp(`(?:^|[^.\\w@])@?(${classesPattern})\\.(\\w*)$`, 'i');
        const propertyMatch = text.match(propertyPattern);
        if (propertyMatch) {
            return { prefix: propertyMatch[2], insertType: 'property' };
        }

        // Pattern 2: Indexer access - _localizer["Key"], Resources["Key"]
        // Matches: _localizer[", localizer[", Resources[", etc.
        const indexerPattern = new RegExp(`(?:_?[lL]ocalizer|${classesPattern})\\s*\\[\\s*["']([\\w]*)$`, 'i');
        const indexerMatch = text.match(indexerPattern);
        if (indexerMatch) {
            return { prefix: indexerMatch[1], insertType: 'string' };
        }

        // Pattern 3: Method call - GetString("Key"), T("Key"), Translate("Key")
        // Matches: GetString(", T(", Translate(", etc.
        const methodPattern = new RegExp(`(?:${methodsPattern})\\s*\\(\\s*["']([\\w]*)$`, 'i');
        const methodMatch = text.match(methodPattern);
        if (methodMatch) {
            return { prefix: methodMatch[1], insertType: 'string' };
        }

        // Pattern 4: IStringLocalizer/IHtmlLocalizer (Razor) - @IStringLocalizer["Key"]
        const razorLocalizerPattern = /I(?:Html|String)Localizer\s*\[\s*["']([\w]*)$/i;
        const razorLocalizerMatch = text.match(razorLocalizerPattern);
        if (razorLocalizerMatch) {
            return { prefix: razorLocalizerMatch[1], insertType: 'string' };
        }

        // Pattern 5: XAML x:Static - {x:Static res:Resources.Key}
        if (languageId === 'xml' || languageId === 'xaml') {
            const xamlPattern = new RegExp(`\\{x:Static\\s+(?:res:)?(${classesPattern})\\.(\\w*)$`, 'i');
            const xamlMatch = text.match(xamlPattern);
            if (xamlMatch) {
                return { prefix: xamlMatch[2], insertType: 'property' };
            }
        }

        return null;
    }

    /**
     * Create a completion item for a resource key.
     */
    private createCompletionItem(
        key: ResourceKey,
        defaultLang: string,
        _insertType: 'property' | 'string'
    ): vscode.CompletionItem {
        const item = new vscode.CompletionItem(key.key, vscode.CompletionItemKind.Constant);

        // Show default language value as detail, with plural indicator
        const defaultValue = key.values[defaultLang];
        const pluralPrefix = key.isPlural ? '[plural] ' : '';
        if (defaultValue) {
            // Truncate long values for display
            const displayValue = defaultValue.length > 60
                ? defaultValue.substring(0, 57) + '...'
                : defaultValue;
            item.detail = pluralPrefix + displayValue;
        } else {
            item.detail = pluralPrefix + '(no default value)';
        }

        // Show all translations in documentation popup
        const translations = Object.entries(key.values)
            .filter(([, value]) => value && value.trim())
            .map(([lang, value]) => `**${lang}**: ${value}`)
            .join('\n\n');

        if (translations) {
            item.documentation = new vscode.MarkdownString(translations);
        }

        // For string contexts, just insert the key name (quotes are already there)
        // For property contexts, insert the key name directly
        item.insertText = key.key;

        // Sort by key name
        item.sortText = key.key.toLowerCase();

        // Filter text for fuzzy matching
        item.filterText = key.key;

        return item;
    }

    /**
     * Find the default language code from configuration.
     * Uses defaultLanguageCode from lrm.json if set, otherwise empty string.
     */
    private findDefaultLanguage(config: any): string {
        return config?.defaultLanguageCode || '';
    }

    /**
     * Get keys from cache or fetch from API.
     */
    private async getKeysCached(): Promise<ResourceKey[]> {
        const now = Date.now();

        // Return cached data if still valid
        if (this.keysCache && (now - this.keysCache.timestamp) < this.cacheTtlMs) {
            return this.keysCache.data;
        }

        try {
            const keys = await this.apiClient.getKeys();
            this.keysCache = { data: keys, timestamp: now };
            return keys;
        } catch (error) {
            // Return stale cache if API fails
            if (this.keysCache) {
                return this.keysCache.data;
            }
            return [];
        }
    }

    /**
     * Get configuration from cache or fetch from API.
     */
    private async getConfigurationCached(): Promise<any> {
        const now = Date.now();

        // Use longer TTL for config (30 seconds) since it changes rarely
        const configTtlMs = 30000;

        if (this.configCache && (now - this.configCache.timestamp) < configTtlMs) {
            return this.configCache.data;
        }

        try {
            const config = await this.apiClient.getConfiguration();
            this.configCache = { data: config, timestamp: now };
            return config;
        } catch (error) {
            // Return stale cache or empty config if API fails
            return this.configCache?.data || {};
        }
    }

    /**
     * Invalidate cache (called when resources change).
     */
    public invalidateCache(): void {
        this.keysCache = null;
    }
}
