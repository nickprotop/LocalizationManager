import * as vscode from 'vscode';
import * as fs from 'fs';
import * as path from 'path';
import { JsonFormatType, JsonFormatConfig } from './resourceDocumentParser';

/**
 * Score accumulator for format detection
 */
interface FormatScore {
    i18next: number;
    standard: number;
}

/**
 * Scoring-based detector for JSON sub-formats (Standard LRM vs i18next)
 * Mirrors the logic in Core/Backends/Json/JsonFormatDetector.cs
 */
export class JsonFormatDetector {
    /** Minimum score threshold for confident detection */
    private static readonly MIN_THRESHOLD = 3;

    /** Common culture codes for validation */
    private static readonly COMMON_CULTURE_CODES = new Set([
        'en', 'en-us', 'en-gb', 'fr', 'fr-fr', 'fr-ca',
        'de', 'de-de', 'es', 'es-es', 'es-mx', 'it', 'it-it',
        'pt', 'pt-br', 'pt-pt', 'ru', 'ja', 'ko', 'zh',
        'zh-hans', 'zh-hant', 'zh-cn', 'zh-tw', 'ar', 'he',
        'nl', 'nl-nl', 'pl', 'tr', 'el', 'cs', 'sv', 'da', 'fi', 'no',
        'nb', 'nn', 'hu', 'ro', 'sk', 'uk', 'vi', 'th', 'id', 'ms'
    ]);

    /** i18next plural suffixes */
    private static readonly PLURAL_SUFFIXES = ['_zero', '_one', '_two', '_few', '_many', '_other'];

    /**
     * Detect the JSON format by analyzing files in the resource path
     * @param files Array of JSON file URIs to analyze
     * @param _resourcePath The resource directory path (reserved for future use)
     * @returns Detected format type
     */
    static async detectFormat(files: vscode.Uri[], _resourcePath: string): Promise<JsonFormatType> {
        const score: FormatScore = { i18next: 0, standard: 0 };

        // 1. Analyze file naming patterns
        this.analyzeFileNames(files, score);

        // 2. Analyze file content (sample up to 3 files)
        const filesToSample = files.slice(0, 3);
        for (const file of filesToSample) {
            try {
                await this.analyzeFileContent(file.fsPath, score);
            } catch (error) {
                // Skip files that can't be read
                console.log(`JsonFormatDetector: Could not analyze ${file.fsPath}:`, error);
            }
        }

        console.log(`JsonFormatDetector: Scores - i18next: ${score.i18next}, standard: ${score.standard}`);

        // 3. Determine winner (require minimum threshold)
        if (score.i18next > score.standard && score.i18next >= this.MIN_THRESHOLD) {
            return 'i18next';
        }
        if (score.standard > score.i18next && score.standard >= this.MIN_THRESHOLD) {
            return 'standard';
        }

        // Default to standard if no clear winner
        return 'standard';
    }

    /**
     * Analyze file naming patterns for format signals
     */
    private static analyzeFileNames(files: vscode.Uri[], score: FormatScore): void {
        for (const file of files) {
            const fileName = path.basename(file.fsPath, '.json').toLowerCase();

            // Pure culture code (en.json, fr-FR.json) → strong i18next signal
            if (this.isValidCultureCode(fileName)) {
                score.i18next += 2;
                continue;
            }

            // basename.culture.json pattern (strings.fr.json) → standard signal
            if (fileName.includes('.')) {
                const lastPart = fileName.split('.').pop()!;
                if (this.isValidCultureCode(lastPart)) {
                    score.standard += 2;
                }
            }
        }
    }

    /**
     * Analyze file content for format signals
     */
    private static async analyzeFileContent(filePath: string, score: FormatScore): Promise<void> {
        const content = fs.readFileSync(filePath, 'utf-8');

        // Interpolation patterns
        // i18next: {{name}}, {{count}}
        if (/\{\{[^}]+\}\}/.test(content)) {
            score.i18next += 2;
        }

        // Standard: {0}, {1}, {name} (without double braces)
        if (/\{(\d+|[a-zA-Z_][a-zA-Z0-9_]*)\}/.test(content) && !/\{\{/.test(content)) {
            score.standard += 2;
        }

        // i18next nesting: $t(key)
        if (/\$t\([^)]+\)/.test(content)) {
            score.i18next += 2;
        }

        // Parse JSON and analyze structure
        try {
            const json = JSON.parse(content);
            this.analyzeJsonKeys(json, score);
        } catch (error) {
            // Invalid JSON, skip structure analysis
        }
    }

    /**
     * Recursively analyze JSON keys for format signals
     */
    private static analyzeJsonKeys(obj: any, score: FormatScore, prefix = ''): void {
        if (typeof obj !== 'object' || obj === null) {
            return;
        }

        for (const [key, value] of Object.entries(obj)) {
            // Skip metadata properties
            if (key.startsWith('_')) {
                continue;
            }

            // i18next plural suffix keys (_one, _other, etc.)
            if (this.PLURAL_SUFFIXES.some(suffix => key.endsWith(suffix))) {
                score.i18next += 3;
            }

            // i18next namespace separator (:)
            if (key.includes(':')) {
                score.i18next += 1;
            }

            // Dot notation without : → slight standard preference
            if (key.includes('.') && !key.includes(':')) {
                score.standard += 1;
            }

            // LRM-style plural objects with _plural marker
            if (typeof value === 'object' && value !== null && '_plural' in value) {
                score.standard += 3;
            }

            // LRM-style value objects with _value
            if (typeof value === 'object' && value !== null && '_value' in value) {
                score.standard += 2;
            }

            // Recurse into nested objects
            if (typeof value === 'object' && value !== null) {
                this.analyzeJsonKeys(value, score, `${prefix}${key}.`);
            }
        }
    }

    /**
     * Check if a string is a valid culture code
     */
    static isValidCultureCode(code: string): boolean {
        const lowerCode = code.toLowerCase();

        // Check against known codes first
        if (this.COMMON_CULTURE_CODES.has(lowerCode)) {
            return true;
        }

        // Pattern: xx or xx-XX or xx-Xxxx (BCP 47)
        return /^[a-z]{2}(-[a-z]{2,4})?$/i.test(code);
    }
}

/**
 * Read JSON format configuration from lrm.json
 */
export async function getJsonFormatConfig(resourcePath: string): Promise<JsonFormatConfig | null> {
    // Check in resource path
    let lrmConfigPath = path.join(resourcePath, 'lrm.json');

    if (!fs.existsSync(lrmConfigPath)) {
        // Check parent directory
        const parentConfig = path.join(path.dirname(resourcePath), 'lrm.json');
        if (!fs.existsSync(parentConfig)) {
            return null;
        }
        lrmConfigPath = parentConfig;
    }

    try {
        const content = fs.readFileSync(lrmConfigPath, 'utf-8');
        const config = JSON.parse(content);
        return config.json || null;
    } catch (error) {
        console.log(`JsonFormatDetector: Could not read lrm.json:`, error);
        return null;
    }
}
