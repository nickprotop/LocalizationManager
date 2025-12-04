import * as vscode from 'vscode';
import * as path from 'path';
import { IResourceDocumentParser, JsonFormatConfig, JsonFormatType, ResourceFormat } from './resourceDocumentParser';
import { ResxDocumentParser } from './resxDocumentParser';
import { JsonDocumentParser } from './jsonDocumentParser';
import { JsonFormatDetector, getJsonFormatConfig } from './jsonFormatDetector';

/**
 * Factory for creating format-appropriate resource document parsers
 * Handles detection and caching of format configuration
 */
export class ResourceParserFactory {
    private jsonConfig: JsonFormatConfig | null = null;
    private detectedFormat: JsonFormatType = 'standard';
    private resourcePath: string = '';
    private resourceFormat: ResourceFormat = 'resx';
    private initialized: boolean = false;

    // Singleton parsers (stateless, so can be reused)
    private resxParser: ResxDocumentParser | null = null;
    private jsonParser: JsonDocumentParser | null = null;

    /**
     * Initialize the factory with a resource path and format
     * Must be called before using getParser()
     */
    async initialize(resourcePath: string, format: ResourceFormat): Promise<void> {
        this.resourcePath = resourcePath;
        this.resourceFormat = format;

        if (format === 'json') {
            // Try to read config from lrm.json
            this.jsonConfig = await getJsonFormatConfig(resourcePath);

            if (this.jsonConfig?.i18nextCompatible) {
                // Config explicitly sets i18next mode
                this.detectedFormat = 'i18next';
                console.log('ResourceParserFactory: Using i18next format (from config)');
            } else {
                // Auto-detect format by analyzing files
                const jsonFiles = await vscode.workspace.findFiles(
                    new vscode.RelativePattern(resourcePath, '*.json'),
                    '**/lrm*.json',
                    20
                );

                if (jsonFiles.length > 0) {
                    this.detectedFormat = await JsonFormatDetector.detectFormat(jsonFiles, resourcePath);
                    console.log(`ResourceParserFactory: Detected ${this.detectedFormat} format`);
                } else {
                    this.detectedFormat = 'standard';
                    console.log('ResourceParserFactory: No JSON files found, defaulting to standard');
                }
            }

            // Create JSON parser with detected settings
            this.jsonParser = new JsonDocumentParser(this.jsonConfig || undefined, this.detectedFormat);
        }

        // Create RESX parser (always available)
        this.resxParser = new ResxDocumentParser();

        this.initialized = true;
    }

    /**
     * Re-initialize if the config file changed
     */
    async refresh(): Promise<void> {
        if (this.resourcePath) {
            await this.initialize(this.resourcePath, this.resourceFormat);
        }
    }

    /**
     * Get the appropriate parser for a document
     * @param document The VS Code text document
     * @returns The format-appropriate parser
     */
    getParser(document: vscode.TextDocument): IResourceDocumentParser {
        if (!this.initialized) {
            // Return a default parser if not initialized
            console.warn('ResourceParserFactory: Not initialized, returning default parser');
            if (this.isResxDocument(document)) {
                return this.resxParser || new ResxDocumentParser();
            }
            return new JsonDocumentParser();
        }

        if (this.isResxDocument(document)) {
            return this.resxParser!;
        }

        return this.jsonParser!;
    }

    /**
     * Get the detected JSON format type
     */
    getJsonFormat(): JsonFormatType {
        return this.detectedFormat;
    }

    /**
     * Get the resource format
     */
    getResourceFormat(): ResourceFormat {
        return this.resourceFormat;
    }

    /**
     * Get the JSON configuration
     */
    getJsonConfig(): JsonFormatConfig | null {
        return this.jsonConfig;
    }

    /**
     * Check if the factory is initialized
     */
    isInitialized(): boolean {
        return this.initialized;
    }

    /**
     * Check if a document is a RESX file
     */
    private isResxDocument(document: vscode.TextDocument): boolean {
        return document.fileName.endsWith('.resx') || document.languageId === 'xml';
    }

    /**
     * Check if a document is a JSON resource file (not config)
     */
    isResourceDocument(document: vscode.TextDocument): boolean {
        const fileName = path.basename(document.fileName).toLowerCase();

        // RESX files are always resource files
        if (document.fileName.endsWith('.resx')) {
            return true;
        }

        // For JSON, check if it's in the resource path and not a config file
        if (document.fileName.endsWith('.json')) {
            // Exclude lrm.json config
            if (fileName === 'lrm.json') {
                return false;
            }

            // Check if file is in resource path
            const documentDir = path.dirname(document.fileName);
            if (documentDir === this.resourcePath || documentDir.startsWith(this.resourcePath + path.sep)) {
                return true;
            }

            // Check for common resource file patterns
            return this.matchesResourcePattern(document.fileName);
        }

        return false;
    }

    /**
     * Check if a file path matches common resource file patterns
     */
    private matchesResourcePattern(filePath: string): boolean {
        const normalizedPath = filePath.toLowerCase().replace(/\\/g, '/');

        const resourcePatterns = [
            '/locales/',
            '/translations/',
            '/i18n/',
            '/lang/',
            '/languages/',
            'strings.json',
            'strings.',
            'messages.json',
            'messages.',
            'translation.json',
            'translation.'
        ];

        for (const pattern of resourcePatterns) {
            if (normalizedPath.includes(pattern)) {
                return true;
            }
        }

        // Check for culture code pattern (en.json, fr-FR.json)
        const fileName = path.basename(filePath, '.json');
        if (JsonFormatDetector.isValidCultureCode(fileName)) {
            return true;
        }

        return false;
    }
}

// Export singleton instance for use across the extension
let _parserFactory: ResourceParserFactory | null = null;

/**
 * Get the shared parser factory instance
 */
export function getParserFactory(): ResourceParserFactory {
    if (!_parserFactory) {
        _parserFactory = new ResourceParserFactory();
    }
    return _parserFactory;
}

/**
 * Reset the parser factory (for testing or when resource path changes)
 */
export function resetParserFactory(): void {
    _parserFactory = null;
}
