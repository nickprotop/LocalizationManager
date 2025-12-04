import * as vscode from 'vscode';

/**
 * Represents a parsed resource key with location information
 */
export interface ResourceKey {
    /** The key name (may include namespace for nested keys) */
    key: string;
    /** The value text */
    value: string;
    /** Optional comment/description */
    comment?: string;
    /** Line number in the document (0-based) */
    lineNumber: number;
    /** Column start position */
    columnStart: number;
    /** Column end position */
    columnEnd: number;
    /** Whether this is a plural key */
    isPlural?: boolean;
    /** Plural forms if isPlural is true */
    pluralForms?: Record<string, string>;
}

/**
 * Format-agnostic interface for parsing resource documents
 */
export interface IResourceDocumentParser {
    /**
     * Parse all keys from a document
     * @param document The VS Code text document
     * @returns Array of parsed resource keys with location info
     */
    parseDocument(document: vscode.TextDocument): ResourceKey[];

    /**
     * Get the key at a specific position in the document
     * @param document The VS Code text document
     * @param position The cursor position
     * @returns The resource key at that position, or null
     */
    getKeyAtPosition(document: vscode.TextDocument, position: vscode.Position): ResourceKey | null;

    /**
     * Get the range of a specific key in the document
     * @param document The VS Code text document
     * @param key The key name to find
     * @returns The range of the key, or null if not found
     */
    getKeyRange(document: vscode.TextDocument, key: string): vscode.Range | null;
}

/**
 * Configuration for JSON resource format
 */
export interface JsonFormatConfig {
    /** Whether to use i18next-compatible format */
    i18nextCompatible?: boolean;
    /** Interpolation format: dotnet ({0}), i18next ({{name}}), or icu */
    interpolationFormat?: 'dotnet' | 'i18next' | 'icu';
    /** Plural format: CLDR or simple */
    pluralFormat?: 'cldr' | 'simple';
    /** Base filename for resource files */
    baseName?: string;
    /** Whether to use nested keys (dot notation becomes nested objects) */
    useNestedKeys?: boolean;
}

/**
 * Detected format type for JSON resources
 */
export type JsonFormatType = 'standard' | 'i18next';

/**
 * Resource format type
 */
export type ResourceFormat = 'resx' | 'json';
