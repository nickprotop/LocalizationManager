import * as vscode from 'vscode';
import { IResourceDocumentParser, ResourceKey, JsonFormatConfig, JsonFormatType } from './resourceDocumentParser';

/**
 * Parser for JSON resource files
 * Supports both Standard (LRM) and i18next formats
 */
export class JsonDocumentParser implements IResourceDocumentParser {
    private _config: JsonFormatConfig;
    private detectedFormat: JsonFormatType;

    /** CLDR plural forms */
    private static readonly PLURAL_FORMS = ['zero', 'one', 'two', 'few', 'many', 'other'];

    /** i18next plural suffixes */
    private static readonly I18NEXT_PLURAL_SUFFIXES = ['_zero', '_one', '_two', '_few', '_many', '_other'];

    constructor(config?: JsonFormatConfig, detectedFormat?: JsonFormatType) {
        this._config = config || {};
        this.detectedFormat = this._config.i18nextCompatible ? 'i18next' : (detectedFormat || 'standard');
    }

    parseDocument(document: vscode.TextDocument): ResourceKey[] {
        const keys: ResourceKey[] = [];
        const text = document.getText();

        try {
            const json = JSON.parse(text);
            this.extractKeys(json, '', keys, text, document);
        } catch (error) {
            // Invalid JSON - return empty
            console.log('JsonDocumentParser: Could not parse JSON:', error);
        }

        return keys;
    }

    getKeyAtPosition(document: vscode.TextDocument, position: vscode.Position): ResourceKey | null {
        const keys = this.parseDocument(document);
        const offset = document.offsetAt(position);
        const text = document.getText();

        // Find the key whose range contains the position
        for (const key of keys) {
            if (key.lineNumber < 0) {
                continue;
            }
            const keyOffset = document.offsetAt(new vscode.Position(key.lineNumber, key.columnStart));
            const endOffset = document.offsetAt(new vscode.Position(key.endLine ?? key.lineNumber, key.columnEnd));

            // Expand range to include the full key-value pair
            // Look backwards for the key start
            let keyStart = this.findKeyStartOffset(text, keyOffset, key.key);
            if (keyStart === -1) keyStart = keyOffset;

            // Check if position is within the key's scope
            if (offset >= keyStart && offset <= endOffset) {
                return key;
            }
        }

        return null;
    }

    getKeyRange(document: vscode.TextDocument, keyName: string): vscode.Range | null {
        const keys = this.parseDocument(document);

        for (const key of keys) {
            if (key.key === keyName) {
                // lineNumber -1 means the key was parsed but its text location could
                // not be resolved; return null rather than a bogus range at 0,0.
                if (key.lineNumber < 0) {
                    return null;
                }
                const startPos = new vscode.Position(key.lineNumber, key.columnStart);
                const endPos = new vscode.Position(key.endLine ?? key.lineNumber, key.columnEnd);
                return new vscode.Range(startPos, endPos);
            }
        }

        return null;
    }

    /**
     * Recursively extract keys from JSON object
     */
    private extractKeys(
        obj: any,
        prefix: string,
        keys: ResourceKey[],
        text: string,
        document: vscode.TextDocument,
        searchFrom = 0
    ): void {
        if (typeof obj !== 'object' || obj === null) {
            return;
        }

        // Local cursor advanced past each key as it is consumed, so sibling keys —
        // and identical simple names nested under different parents — resolve to
        // distinct, in-order locations rather than all matching the first occurrence.
        let cursor = searchFrom;

        for (const [key, value] of Object.entries(obj)) {
            // Skip metadata properties (except when they contain the value)
            if (key.startsWith('_') && key !== '_value') {
                continue;
            }

            const fullKey = prefix ? `${prefix}.${key}` : key;

            if (typeof value === 'string') {
                // Simple string value
                const { endOffset, ...location } = this.findKeyLocation(text, document, key, cursor);
                if (location.lineNumber !== -1) cursor = endOffset;
                keys.push({
                    key: fullKey,
                    value,
                    ...location
                });
            } else if (typeof value === 'object' && value !== null) {
                if (this.isPluralObject(value)) {
                    // Plural object
                    const pluralForms = this.extractPluralForms(value);
                    const { endOffset, ...location } = this.findKeyLocation(text, document, key, cursor);
                    if (location.lineNumber !== -1) cursor = endOffset;
                    keys.push({
                        key: fullKey,
                        value: pluralForms['other'] || pluralForms['one'] || Object.values(pluralForms)[0] || '',
                        isPlural: true,
                        pluralForms,
                        ...location
                    });
                } else if ('_value' in value) {
                    // LRM-style value with metadata
                    const valueObj = value as Record<string, unknown>;
                    const val = valueObj._value as string;
                    const comment = valueObj._comment as string | undefined;
                    const { endOffset, ...location } = this.findKeyLocation(text, document, key, cursor);
                    if (location.lineNumber !== -1) cursor = endOffset;
                    keys.push({
                        key: fullKey,
                        value: val,
                        comment,
                        ...location
                    });
                } else {
                    // Recurse into nested object. Locate the parent key first so the
                    // child scope is searched from inside the parent's braces — this
                    // is what disambiguates duplicate simple names across scopes.
                    const parent = this.findKeyLocation(text, document, key, cursor);
                    if (parent.lineNumber !== -1) cursor = parent.endOffset;
                    const childStart = parent.lineNumber !== -1
                        ? document.offsetAt(new vscode.Position(parent.lineNumber, parent.columnStart))
                        : cursor;
                    this.extractKeys(value, fullKey, keys, text, document, childStart);
                }
            }
        }

        // Handle i18next-style plural keys (key_one, key_other)
        if (this.detectedFormat === 'i18next') {
            this.handleI18nextPluralKeys(obj, prefix, keys, text, document);
        }
    }

    /**
     * Handle i18next-style plural keys that are siblings (key_one, key_other)
     */
    private handleI18nextPluralKeys(
        obj: any,
        prefix: string,
        keys: ResourceKey[],
        text: string,
        document: vscode.TextDocument
    ): void {
        const processedBases = new Set<string>();
        const objKeys = Object.keys(obj);

        for (const key of objKeys) {
            // Check if this is a plural suffix key
            for (const suffix of JsonDocumentParser.I18NEXT_PLURAL_SUFFIXES) {
                if (key.endsWith(suffix)) {
                    const baseKey = key.slice(0, -suffix.length);

                    if (processedBases.has(baseKey)) {
                        continue;
                    }
                    processedBases.add(baseKey);

                    // Collect all plural forms for this base key
                    const pluralForms: Record<string, string> = {};
                    for (const checkSuffix of JsonDocumentParser.I18NEXT_PLURAL_SUFFIXES) {
                        const pluralKey = `${baseKey}${checkSuffix}`;
                        if (pluralKey in obj && typeof obj[pluralKey] === 'string') {
                            // Remove the underscore prefix from the form name
                            const formName = checkSuffix.slice(1); // _one -> one
                            pluralForms[formName] = obj[pluralKey];
                        }
                    }

                    if (Object.keys(pluralForms).length > 0) {
                        const fullKey = prefix ? `${prefix}.${baseKey}` : baseKey;

                        // Check if we already added this key as a regular key
                        const existingIndex = keys.findIndex(k => k.key === fullKey);
                        if (existingIndex !== -1) {
                            // Update existing key to be plural
                            keys[existingIndex].isPlural = true;
                            keys[existingIndex].pluralForms = pluralForms;
                        } else {
                            // Add as new plural key
                            const { endOffset, ...location } = this.findKeyLocation(text, document, `${baseKey}_one`);
                            void endOffset;
                            keys.push({
                                key: fullKey,
                                value: pluralForms['other'] || pluralForms['one'] || Object.values(pluralForms)[0],
                                isPlural: true,
                                pluralForms,
                                ...location
                            });
                        }

                        // Remove individual plural keys from results
                        for (const checkSuffix of JsonDocumentParser.I18NEXT_PLURAL_SUFFIXES) {
                            const pluralFullKey = prefix ? `${prefix}.${baseKey}${checkSuffix}` : `${baseKey}${checkSuffix}`;
                            const idx = keys.findIndex(k => k.key === pluralFullKey);
                            if (idx !== -1) {
                                keys.splice(idx, 1);
                            }
                        }
                    }
                    break;
                }
            }
        }
    }

    /**
     * Check if an object represents plural forms
     */
    private isPluralObject(obj: any): boolean {
        // LRM-style: has _plural marker
        if ('_plural' in obj && obj._plural === true) {
            return true;
        }

        // Standard CLDR plural object: has at least 2 of the CLDR forms
        const objKeys = Object.keys(obj);
        const pluralKeys = objKeys.filter(k =>
            JsonDocumentParser.PLURAL_FORMS.includes(k.toLowerCase())
        );

        return pluralKeys.length >= 2;
    }

    /**
     * Extract plural forms from a plural object
     */
    private extractPluralForms(obj: any): Record<string, string> {
        const forms: Record<string, string> = {};

        for (const [key, value] of Object.entries(obj)) {
            if (key.startsWith('_')) {
                continue;
            }

            const lowerKey = key.toLowerCase();
            if (JsonDocumentParser.PLURAL_FORMS.includes(lowerKey) && typeof value === 'string') {
                forms[lowerKey] = value;
            }
        }

        return forms;
    }

    /**
     * Find the location of a key in the JSON text
     */
    private findKeyLocation(
        text: string,
        document: vscode.TextDocument,
        key: string,
        searchFrom = 0
    ): { lineNumber: number; columnStart: number; endLine: number; columnEnd: number; comment?: string; endOffset: number } {
        // Search for the key in the text starting at searchFrom. The caller advances
        // searchFrom past each key it has already consumed so that the same simple
        // name appearing in different nesting scopes (e.g. users.id vs posts.id)
        // resolves to its OWN occurrence rather than always the first global match.
        // Pattern: "key": "value" or "key": { ... }
        const escapedKey = this.escapeRegexChars(key);
        const keyPattern = new RegExp(`"${escapedKey}"\\s*:`, 'g');
        keyPattern.lastIndex = Math.max(0, searchFrom);
        const match = keyPattern.exec(text);

        if (match) {
            const startOffset = match.index;
            const startPos = document.positionAt(startOffset);

            // Find the end of the value
            let endOffset = startOffset + match[0].length;

            // Skip whitespace after colon
            while (endOffset < text.length && /\s/.test(text[endOffset])) {
                endOffset++;
            }

            // Find the end of the value (string, object, or other)
            if (text[endOffset] === '"') {
                // String value - find closing quote
                let inEscape = false;
                endOffset++; // Skip opening quote
                while (endOffset < text.length) {
                    if (inEscape) {
                        inEscape = false;
                    } else if (text[endOffset] === '\\') {
                        inEscape = true;
                    } else if (text[endOffset] === '"') {
                        endOffset++; // Include closing quote
                        break;
                    }
                    endOffset++;
                }
            } else if (text[endOffset] === '{') {
                // Object value - find matching closing brace
                let depth = 1;
                endOffset++;
                while (endOffset < text.length && depth > 0) {
                    if (text[endOffset] === '{') depth++;
                    else if (text[endOffset] === '}') depth--;
                    endOffset++;
                }
            }

            const endPos = document.positionAt(endOffset);

            return {
                lineNumber: startPos.line,
                columnStart: startPos.character,
                // Carry the real end line so multi-line values (multiline strings,
                // object values) produce a correct range instead of collapsing the
                // end onto the start line.
                endLine: endPos.line,
                columnEnd: endPos.character,
                endOffset
            };
        }

        // Key not found. Use lineNumber -1 as a sentinel so callers (getKeyRange)
        // can return null instead of pointing at a bogus 0,0 location.
        return {
            lineNumber: -1,
            columnStart: 0,
            endLine: -1,
            columnEnd: 0,
            endOffset: Math.max(0, searchFrom)
        };
    }

    /**
     * Find the start offset of a key in the text
     */
    private findKeyStartOffset(text: string, approximateOffset: number, key: string): number {
        // Look backwards from the approximate offset to find the key start
        const searchStart = Math.max(0, approximateOffset - 200);
        const searchText = text.slice(searchStart, approximateOffset + 100);
        const escapedKey = this.escapeRegexChars(key);
        const keyPattern = new RegExp(`"${escapedKey}"\\s*:`);
        const match = keyPattern.exec(searchText);

        if (match) {
            return searchStart + match.index;
        }

        return -1;
    }

    /**
     * Escape special regex characters in a string
     */
    private escapeRegexChars(str: string): string {
        return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }
}
