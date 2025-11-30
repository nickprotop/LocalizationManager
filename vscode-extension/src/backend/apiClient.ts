import axios, { AxiosInstance } from 'axios';

// API Response Types
export interface ResourceFile {
    fileName: string;
    filePath: string;
    code: string;
    isDefault: boolean;
}

export interface ResourceKey {
    key: string;
    values: { [language: string]: string };
    occurrenceCount: number;
    hasDuplicates: boolean;
}

export interface ResourceKeyDetails {
    key: string;
    values: { [language: string]: { value: string; comment: string | null } };
    occurrenceCount: number;
    hasDuplicates: boolean;
}

export interface KeyReference {
    file: string;
    line: number;
    pattern: string;
    confidence: string;
    warning?: string;
}

export interface KeyUsage {
    key: string;
    referenceCount: number;
    references: KeyReference[];
}

export interface ScanResult {
    scannedFiles: number;
    totalReferences: number;
    uniqueKeysFound: number;
    unusedKeysCount: number;
    missingKeysCount: number;
    unused: string[];
    missing: string[];
    references: KeyUsage[];
}

export interface ScanFileRequest {
    filePath: string;
    content?: string;  // Optional: if provided, scan this content instead of reading from disk
}

export interface ScanFileResponse {
    scannedFiles: number;
    totalReferences: number;
    uniqueKeysFound: number;
    unusedKeysCount: number;
    missingKeysCount: number;
    unused: string[];
    missing: string[];
    references: KeyUsage[];
}

export interface ValidationResult {
    isValid: boolean;
    missingKeys: { [language: string]: string[] };
    extraKeys: { [language: string]: string[] };
    emptyValues: { [language: string]: string[] };
    duplicateKeys: string[];
}

export interface TranslationProvider {
    name: string;
    displayName: string;
    isConfigured: boolean;
    requiresApiKey: boolean;
}

export interface TranslateRequest {
    keys: string[];
    provider: string;
    targetLanguages: string[];
    onlyMissing: boolean;
}

export interface TranslateResponse {
    success: boolean;
    translatedCount: number;
    errorCount: number;
    results: TranslationResult[];
    errors: TranslationError[];
    dryRun: boolean;
}

export interface TranslationResult {
    key: string;
    language: string;
    translatedValue: string | null;
    success: boolean;
}

export interface TranslationError {
    key: string;
    language: string;
    error: string;
}

export interface TranslateAllRequest {
    provider: string;
    targetLanguages: string[];
    onlyMissing: boolean;
    dryRun: boolean;
}

export interface LanguageStats {
    languageCode: string;
    filePath: string;
    isDefault: boolean;
    translatedCount: number;
    totalCount: number;
    coverage: number;
}

export interface Statistics {
    totalKeys: number;
    languages: LanguageStats[];
    overallCoverage: number;
}

export interface AddKeyRequest {
    key: string;
    values: { [language: string]: string };
    comment?: string;
}

export interface UpdateKeyRequest {
    values: { [language: string]: { value: string; comment?: string } };
}

export interface AddLanguageRequest {
    culture: string;
    copyFrom?: string;
}

export interface SearchRequest {
    pattern: string;
    filterMode?: 'substring' | 'wildcard' | 'regex';
    caseSensitive?: boolean;
    searchScope?: 'keys' | 'values' | 'keysAndValues' | 'comments' | 'all';
    statusFilters?: ('missing' | 'extra' | 'duplicates')[];
    limit?: number;
    offset?: number;
}

export interface SearchResponse {
    results: ResourceKey[];
    totalCount: number;
    filteredCount: number;
    appliedFilterMode: string;
}

// Credentials API types
export interface CredentialProviderInfo {
    provider: string;
    displayName: string;
    requiresApiKey: boolean;
    source: 'environment' | 'secure_store' | 'config_file' | null;
    isConfigured: boolean;
}

export interface CredentialProvidersResponse {
    providers: CredentialProviderInfo[];
    useSecureCredentialStore: boolean;
}

export interface ProviderTestResponse {
    success: boolean;
    provider: string;
    message: string;
}

export class ApiClient {
    private client: AxiosInstance;

    constructor(baseUrl: string) {
        this.client = axios.create({
            baseURL: baseUrl,
            timeout: 30000,
            headers: {
                'Content-Type': 'application/json'
            }
        });
    }

    // Resources
    async getResources(): Promise<ResourceFile[]> {
        const response = await this.client.get('/api/resources');
        return response.data;
    }

    async getKeys(): Promise<ResourceKey[]> {
        const response = await this.client.get('/api/resources/keys');
        return response.data;
    }

    async getKeyDetails(keyName: string, occurrence?: number): Promise<ResourceKeyDetails> {
        const params = occurrence !== undefined ? { occurrence } : {};
        const response = await this.client.get(`/api/resources/keys/${encodeURIComponent(keyName)}`, { params });
        return response.data;
    }

    async addKey(request: AddKeyRequest): Promise<void> {
        await this.client.post('/api/resources/keys', request);
    }

    async updateKey(keyName: string, request: UpdateKeyRequest, occurrence?: number): Promise<void> {
        const params = occurrence !== undefined ? { occurrence } : {};
        await this.client.put(`/api/resources/keys/${encodeURIComponent(keyName)}`, request, { params });
    }

    async deleteKey(keyName: string, occurrence?: number, allDuplicates?: boolean): Promise<void> {
        const params: any = {};
        if (occurrence !== undefined) {
            params.occurrence = occurrence;
        }
        if (allDuplicates !== undefined) {
            params.allDuplicates = allDuplicates;
        }
        await this.client.delete(`/api/resources/keys/${encodeURIComponent(keyName)}`, { params });
    }

    // Search
    async search(request: SearchRequest): Promise<SearchResponse> {
        const response = await this.client.post('/api/search', request);
        return response.data;
    }

    // Validation
    async validate(): Promise<ValidationResult> {
        const response = await this.client.post('/api/validation/validate', {});
        return response.data;
    }

    // Translation
    async getTranslationProviders(): Promise<TranslationProvider[]> {
        const response = await this.client.get('/api/translation/providers');
        return response.data.providers || response.data;
    }

    // Configuration (lrm.json)
    async getConfiguration(): Promise<any> {
        const response = await this.client.get('/api/configuration');
        return response.data.configuration || response.data;
    }

    async updateConfiguration(config: any): Promise<void> {
        await this.client.put('/api/configuration', config);
    }

    async translate(request: TranslateRequest): Promise<TranslateResponse> {
        const response = await this.client.post('/api/translation/translate', request);
        return response.data;
    }

    async translateAll(request: TranslateAllRequest): Promise<any> {
        const response = await this.client.post('/api/translation/translate-all', request);
        return response.data;
    }

    // Code Scanning
    async scanCode(): Promise<ScanResult> {
        const response = await this.client.post('/api/scan/scan', {});
        return response.data;
    }

    async scanFile(request: ScanFileRequest): Promise<ScanFileResponse> {
        const response = await this.client.post('/api/scan/file', request);
        return response.data;
    }

    async getUnusedKeys(): Promise<string[]> {
        const response = await this.client.get('/api/scan/unused');
        return response.data.unusedKeys || response.data;
    }

    async getMissingKeys(): Promise<string[]> {
        const response = await this.client.get('/api/scan/missing');
        return response.data.missingKeys || response.data;
    }

    async getKeyReferences(keyName: string): Promise<KeyUsage> {
        const response = await this.client.get(`/api/scan/references/${encodeURIComponent(keyName)}`);
        // KeyReferencesResponse structure matches KeyUsage interface
        return {
            key: response.data.key,
            referenceCount: response.data.referenceCount,
            references: response.data.references
        };
    }

    // Statistics
    async getStats(): Promise<Statistics> {
        const response = await this.client.get('/api/stats');
        return response.data;
    }

    // Languages
    async getLanguages(): Promise<ResourceFile[]> {
        const response = await this.client.get('/api/language');
        return response.data.languages || response.data;
    }

    async addLanguage(request: AddLanguageRequest): Promise<void> {
        await this.client.post('/api/language', request);
    }

    async removeLanguage(culture: string): Promise<void> {
        await this.client.delete(`/api/language/${encodeURIComponent(culture)}`);
    }

    // Export/Import
    async exportCsv(): Promise<string> {
        const response = await this.client.get('/api/export/csv', {
            responseType: 'text'
        });
        return response.data;
    }

    async exportJson(): Promise<any> {
        const response = await this.client.get('/api/export/json');
        return response.data;
    }

    async importCsv(csvData: string): Promise<any> {
        const response = await this.client.post('/api/import/csv', { csvData });
        return response.data;
    }

    // Merge Duplicates
    async getDuplicates(): Promise<string[]> {
        const response = await this.client.get('/api/merge-duplicates/list');
        // Backend returns DuplicateKeysResponse with DuplicateKeys array
        return response.data.duplicateKeys?.map((d: any) => d.key) || [];
    }

    async mergeDuplicates(keyName: string): Promise<void> {
        await this.client.post('/api/merge-duplicates/merge', {
            key: keyName,
            mergeAll: false
        });
    }

    // Credentials API
    async getCredentialProviders(): Promise<CredentialProvidersResponse> {
        const response = await this.client.get('/api/credentials/providers');
        return response.data;
    }

    async setApiKey(provider: string, apiKey: string): Promise<{ success: boolean; message: string }> {
        const response = await this.client.put(`/api/credentials/${encodeURIComponent(provider)}`, { apiKey });
        return response.data;
    }

    async deleteApiKey(provider: string): Promise<{ success: boolean; message: string }> {
        const response = await this.client.delete(`/api/credentials/${encodeURIComponent(provider)}`);
        return response.data;
    }

    async getApiKeySource(provider: string): Promise<{ provider: string; source: string | null; isConfigured: boolean }> {
        const response = await this.client.get(`/api/credentials/${encodeURIComponent(provider)}/source`);
        return response.data;
    }

    async testProvider(provider: string): Promise<ProviderTestResponse> {
        const response = await this.client.post(`/api/credentials/${encodeURIComponent(provider)}/test`);
        return response.data;
    }

    async setSecureStoreEnabled(enabled: boolean): Promise<{ success: boolean; message: string }> {
        const response = await this.client.put('/api/credentials/secure-store', { enabled });
        return response.data;
    }
}
