import { ApiClient, ScanResult, ResourceKeyDetails, ValidationResult, KeyUsage, ResourceKey } from './apiClient';

/**
 * Resource format type
 */
export type ResourceFormat = 'resx' | 'json' | null;

/**
 * Shared cache service for the LRM extension.
 * Provides centralized caching for scan results, key details, and validation data.
 * Used by CodeLens provider, Resource Editor, and diagnostics.
 *
 * Format-aware: Automatically invalidates cache when resource format changes.
 */
export class CacheService {
    private scanResultsCache: ScanResult | null = null;
    private keyDetailsCache: Map<string, ResourceKeyDetails> = new Map();
    private keysCache: ResourceKey[] | null = null;
    private validationCache: ValidationResult | null = null;
    private keyReferencesCache: Map<string, KeyUsage> = new Map();

    private scanResultsTimestamp: number = 0;
    private validationTimestamp: number = 0;
    private keysTimestamp: number = 0;

    private readonly TTL = 30000; // 30 seconds cache TTL

    /** Current resource format (resx or json) */
    private currentFormat: ResourceFormat = null;

    /** Resource path associated with this cache */
    private resourcePath: string | null = null;

    constructor(private apiClient: ApiClient) {}

    /**
     * Check if a timestamp has expired based on TTL
     */
    private isExpired(timestamp: number): boolean {
        return Date.now() - timestamp > this.TTL;
    }

    /**
     * Get scan results (cached or fresh)
     */
    async getScanResults(forceRefresh = false): Promise<ScanResult> {
        if (!forceRefresh && this.scanResultsCache && !this.isExpired(this.scanResultsTimestamp)) {
            return this.scanResultsCache;
        }

        this.scanResultsCache = await this.apiClient.scanCode();
        this.scanResultsTimestamp = Date.now();

        // Also populate key references cache from scan results
        this.keyReferencesCache.clear();
        for (const ref of this.scanResultsCache.references) {
            this.keyReferencesCache.set(ref.key, ref);
        }

        return this.scanResultsCache;
    }

    /**
     * Get all resource keys (cached or fresh)
     */
    async getKeys(forceRefresh = false): Promise<ResourceKey[]> {
        if (!forceRefresh && this.keysCache && !this.isExpired(this.keysTimestamp)) {
            return this.keysCache;
        }

        this.keysCache = await this.apiClient.getKeys();
        this.keysTimestamp = Date.now();
        return this.keysCache;
    }

    /**
     * Get details for a specific key (cached or fresh)
     */
    async getKeyDetails(key: string, forceRefresh = false): Promise<ResourceKeyDetails> {
        if (!forceRefresh && this.keyDetailsCache.has(key)) {
            return this.keyDetailsCache.get(key)!;
        }

        const details = await this.apiClient.getKeyDetails(key);
        this.keyDetailsCache.set(key, details);
        return details;
    }

    /**
     * Get validation results (cached or fresh)
     */
    async getValidation(forceRefresh = false): Promise<ValidationResult> {
        if (!forceRefresh && this.validationCache && !this.isExpired(this.validationTimestamp)) {
            return this.validationCache;
        }

        this.validationCache = await this.apiClient.validate();
        this.validationTimestamp = Date.now();
        return this.validationCache;
    }

    /**
     * Get references for a specific key (from cached scan results or API)
     */
    async getKeyReferences(key: string, forceRefresh = false): Promise<KeyUsage> {
        // Try to get from cache first
        if (!forceRefresh && this.keyReferencesCache.has(key)) {
            return this.keyReferencesCache.get(key)!;
        }

        // If we have scan results, the key might just not have any references
        if (!forceRefresh && this.scanResultsCache && !this.isExpired(this.scanResultsTimestamp)) {
            const cached = this.keyReferencesCache.get(key);
            if (cached) {
                return cached;
            }
            // Key not in references means 0 references
            return { key, referenceCount: 0, references: [] };
        }

        // Fetch fresh from API
        const usage = await this.apiClient.getKeyReferences(key);
        this.keyReferencesCache.set(key, usage);
        return usage;
    }

    /**
     * Get reference count for a key (quick lookup from cache)
     */
    getReferenceCountFromCache(key: string): number | null {
        const cached = this.keyReferencesCache.get(key);
        return cached ? cached.referenceCount : null;
    }

    /**
     * Check if a key is in the unused list (from cached scan results)
     */
    isKeyUnused(key: string): boolean | null {
        if (!this.scanResultsCache || !Array.isArray(this.scanResultsCache.unused)) {
            return null;
        }
        return this.scanResultsCache.unused.includes(key);
    }

    /**
     * Check if a key has duplicates (from cached validation)
     */
    isKeyDuplicate(key: string): boolean | null {
        if (!this.validationCache || !Array.isArray(this.validationCache.duplicateKeys)) {
            return null;
        }
        return this.validationCache.duplicateKeys.includes(key);
    }

    /**
     * Get missing languages for a key (from cached key details)
     */
    getMissingLanguages(key: string): string[] | null {
        const details = this.keyDetailsCache.get(key);
        if (!details) {
            return null;
        }

        // Find languages with empty values
        const missing: string[] = [];
        for (const [lang, data] of Object.entries(details.values)) {
            if (!data.value || data.value.trim() === '') {
                missing.push(lang);
            }
        }
        return missing;
    }

    /**
     * Invalidate all caches (call when resource files change)
     */
    invalidate(): void {
        this.scanResultsCache = null;
        this.keyDetailsCache.clear();
        this.keysCache = null;
        this.validationCache = null;
        this.keyReferencesCache.clear();
        this.scanResultsTimestamp = 0;
        this.validationTimestamp = 0;
        this.keysTimestamp = 0;
    }

    /**
     * Set the current resource format and path
     * Automatically invalidates cache if format or path changes
     */
    setResourceContext(format: ResourceFormat, resourcePath: string | null): void {
        const formatChanged = this.currentFormat !== format;
        const pathChanged = this.resourcePath !== resourcePath;

        if (formatChanged || pathChanged) {
            console.log(`CacheService: Context changed - format: ${this.currentFormat} -> ${format}, path changed: ${pathChanged}`);
            this.currentFormat = format;
            this.resourcePath = resourcePath;
            this.invalidate();
        }
    }

    /**
     * Get the current resource format
     */
    getResourceFormat(): ResourceFormat {
        return this.currentFormat;
    }

    /**
     * Get the current resource path
     */
    getResourcePath(): string | null {
        return this.resourcePath;
    }

    /**
     * Check if the cache is valid for a given format and path
     */
    isValidFor(format: ResourceFormat, resourcePath: string | null): boolean {
        return this.currentFormat === format && this.resourcePath === resourcePath;
    }

    /**
     * Invalidate only key-specific caches (for when a single key is modified)
     */
    invalidateKey(key: string): void {
        this.keyDetailsCache.delete(key);
        this.keyReferencesCache.delete(key);
        // Also invalidate keys list and validation since they might be affected
        this.keysCache = null;
        this.validationCache = null;
        this.keysTimestamp = 0;
        this.validationTimestamp = 0;
    }

    /**
     * Check if cache has any data (for UI status)
     */
    hasData(): boolean {
        return this.scanResultsCache !== null || this.keysCache !== null;
    }

    /**
     * Get cached scan results if available (does NOT fetch from API)
     * Returns null if no cached results or if cache is expired
     */
    getCachedScanResults(): ScanResult | null {
        if (this.scanResultsCache && !this.isExpired(this.scanResultsTimestamp)) {
            return this.scanResultsCache;
        }
        return null;
    }
}
