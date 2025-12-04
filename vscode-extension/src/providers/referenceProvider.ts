import * as vscode from 'vscode';
import { getParserFactory, ResourceParserFactory } from '../parsers';

/**
 * Reference Provider for LRM extension.
 * Allows Shift+F12 "Find All References" on localization keys
 * to find all usages in code files and resource files.
 */
export class LrmReferenceProvider implements vscode.ReferenceProvider {
    private parserFactory: ResourceParserFactory;

    // Patterns to find resource key usage in code (same as Definition Provider)
    private readonly keyPatterns = [
        // Resources.KeyName
        /\bResources\.([A-Za-z_][A-Za-z0-9_]*)\b/g,
        // Resources["KeyName"]
        /\bResources\["([^"]+)"\]/g,
        // Resources['KeyName']
        /\bResources\['([^']+)'\]/g,
        // GetString("KeyName")
        /\bGetString\("([^"]+)"\)/g,
        // GetLocalizedString("KeyName")
        /\bGetLocalizedString\("([^"]+)"\)/g,
        // XAML: {x:Static res:Resources.KeyName}
        /\{x:Static\s+[^:]+:Resources\.([A-Za-z_][A-Za-z0-9_]*)\}/g,
        // @Resources.KeyName (Razor)
        /@Resources\.([A-Za-z_][A-Za-z0-9_]*)\b/g,
        // SharedResource.KeyName
        /\bSharedResource\.([A-Za-z_][A-Za-z0-9_]*)\b/g,
        // Localizer["KeyName"] (IStringLocalizer)
        /\bLocalizer\["([^"]+)"\]/g,
        // L["KeyName"] (short localizer)
        /\bL\["([^"]+)"\]/g,
        // T["KeyName"] (another common pattern)
        /\bT\["([^"]+)"\]/g,
        // t("KeyName") (i18next style)
        /\bt\("([^"]+)"\)/g,
        /\bt\('([^']+)'\)/g,
    ];

    constructor() {
        this.parserFactory = getParserFactory();
    }

    async provideReferences(
        document: vscode.TextDocument,
        position: vscode.Position,
        context: vscode.ReferenceContext,
        token: vscode.CancellationToken
    ): Promise<vscode.Location[] | null> {
        // Get the key at the current position
        const keyName = this.getKeyAtPosition(document, position);

        if (!keyName) {
            return null;
        }

        console.log(`LRM References: Finding references for "${keyName}", includeDeclaration: ${context.includeDeclaration}`);

        const references: vscode.Location[] = [];

        // Find references in code files
        const codeReferences = await this.findCodeReferences(keyName, token);
        references.push(...codeReferences);

        // Include declaration if requested
        if (context.includeDeclaration) {
            const declarations = await this.findDeclarations(keyName, token);
            references.push(...declarations);
        }

        console.log(`LRM References: Found ${references.length} references for "${keyName}"`);
        return references;
    }

    /**
     * Extract the localization key at the given position
     */
    private getKeyAtPosition(document: vscode.TextDocument, position: vscode.Position): string | null {
        const line = document.lineAt(position.line).text;
        const fileName = document.fileName.toLowerCase();

        // Check if we're in a resource file
        if (fileName.endsWith('.resx')) {
            return this.getKeyFromResxLine(line, position.character);
        } else if (fileName.endsWith('.json')) {
            return this.getKeyFromJsonLine(line, position.character);
        }

        // Code file - look for key patterns
        for (const pattern of this.keyPatterns) {
            pattern.lastIndex = 0;
            let match;

            while ((match = pattern.exec(line)) !== null) {
                const matchStart = match.index;
                const matchEnd = matchStart + match[0].length;

                if (position.character >= matchStart && position.character <= matchEnd) {
                    return match[1];
                }
            }
        }

        return null;
    }

    /**
     * Extract key name from a RESX file line
     */
    private getKeyFromResxLine(line: string, character: number): string | null {
        // Pattern: <data name="KeyName"
        const match = line.match(/<data\s+name="([^"]+)"/);
        if (match) {
            const keyStart = line.indexOf(`"${match[1]}"`) + 1;
            const keyEnd = keyStart + match[1].length;

            if (character >= keyStart && character <= keyEnd) {
                return match[1];
            }
        }
        return null;
    }

    /**
     * Extract key name from a JSON file line
     */
    private getKeyFromJsonLine(line: string, character: number): string | null {
        // Pattern: "KeyName": (at start of value)
        const match = line.match(/^\s*"([^"]+)"\s*:/);
        if (match) {
            const keyStart = line.indexOf(`"${match[1]}"`) + 1;
            const keyEnd = keyStart + match[1].length;

            if (character >= keyStart && character <= keyEnd) {
                return match[1];
            }
        }
        return null;
    }

    /**
     * Find all code references to a key
     */
    private async findCodeReferences(keyName: string, token: vscode.CancellationToken): Promise<vscode.Location[]> {
        const references: vscode.Location[] = [];

        // File patterns for code files
        const codePatterns = [
            '**/*.cs',
            '**/*.razor',
            '**/*.cshtml',
            '**/*.xaml',
            '**/*.ts',
            '**/*.tsx',
            '**/*.js',
            '**/*.jsx',
        ];

        const excludePattern = '{**/node_modules/**,**/bin/**,**/obj/**,**/.git/**,**/dist/**}';

        for (const pattern of codePatterns) {
            if (token.isCancellationRequested) {
                break;
            }

            const files = await vscode.workspace.findFiles(pattern, excludePattern, 100);

            for (const file of files) {
                if (token.isCancellationRequested) {
                    break;
                }

                const fileRefs = await this.findReferencesInFile(file, keyName);
                references.push(...fileRefs);
            }
        }

        return references;
    }

    /**
     * Find references to a key within a specific file
     */
    private async findReferencesInFile(fileUri: vscode.Uri, keyName: string): Promise<vscode.Location[]> {
        const references: vscode.Location[] = [];

        try {
            const document = await vscode.workspace.openTextDocument(fileUri);
            const text = document.getText();

            // Build search patterns for this key
            const searchPatterns = this.buildSearchPatterns(keyName);

            for (const pattern of searchPatterns) {
                pattern.lastIndex = 0;
                let match;

                while ((match = pattern.exec(text)) !== null) {
                    // Verify the captured group matches our key
                    if (match[1] === keyName) {
                        const pos = document.positionAt(match.index);
                        const endPos = document.positionAt(match.index + match[0].length);
                        references.push(new vscode.Location(fileUri, new vscode.Range(pos, endPos)));
                    }
                }
            }
        } catch (error) {
            console.error(`LRM References: Error searching file ${fileUri.fsPath}:`, error);
        }

        return references;
    }

    /**
     * Build regex patterns for searching a specific key
     */
    private buildSearchPatterns(keyName: string): RegExp[] {
        const escaped = this.escapeRegex(keyName);

        return [
            new RegExp(`\\bResources\\.(${escaped})\\b`, 'g'),
            new RegExp(`\\bResources\\["(${escaped})"\\]`, 'g'),
            new RegExp(`\\bResources\\['(${escaped})'\\]`, 'g'),
            new RegExp(`\\bGetString\\("(${escaped})"\\)`, 'g'),
            new RegExp(`\\bGetLocalizedString\\("(${escaped})"\\)`, 'g'),
            new RegExp(`\\{x:Static\\s+[^:]+:Resources\\.(${escaped})\\}`, 'g'),
            new RegExp(`@Resources\\.(${escaped})\\b`, 'g'),
            new RegExp(`\\bSharedResource\\.(${escaped})\\b`, 'g'),
            new RegExp(`\\bLocalizer\\["(${escaped})"\\]`, 'g'),
            new RegExp(`\\bL\\["(${escaped})"\\]`, 'g'),
            new RegExp(`\\bT\\["(${escaped})"\\]`, 'g'),
            new RegExp(`\\bt\\("(${escaped})"\\)`, 'g'),
            new RegExp(`\\bt\\('(${escaped})'\\)`, 'g'),
        ];
    }

    /**
     * Find declarations of a key in resource files
     */
    private async findDeclarations(keyName: string, token: vscode.CancellationToken): Promise<vscode.Location[]> {
        const declarations: vscode.Location[] = [];
        const excludePattern = '{**/node_modules/**,**/bin/**,**/obj/**,**/.git/**}';

        // Search RESX files
        const resxFiles = await vscode.workspace.findFiles('**/*.resx', excludePattern, 50);
        for (const file of resxFiles) {
            if (token.isCancellationRequested) break;

            const location = await this.findDeclarationInFile(file, keyName);
            if (location) {
                declarations.push(location);
            }
        }

        // Search JSON resource files
        const jsonPatterns = [
            '**/locales/**/*.json',
            '**/translations/**/*.json',
            '**/i18n/**/*.json',
            '**/lang/**/*.json',
            '**/strings*.json',
        ];

        for (const pattern of jsonPatterns) {
            if (token.isCancellationRequested) break;

            const jsonFiles = await vscode.workspace.findFiles(pattern, excludePattern, 50);
            for (const file of jsonFiles) {
                if (token.isCancellationRequested) break;

                // Skip config files
                if (file.fsPath.toLowerCase().includes('lrm.json')) continue;

                const location = await this.findDeclarationInFile(file, keyName);
                if (location) {
                    declarations.push(location);
                }
            }
        }

        return declarations;
    }

    /**
     * Find declaration of a key in a specific resource file
     */
    private async findDeclarationInFile(fileUri: vscode.Uri, keyName: string): Promise<vscode.Location | null> {
        try {
            const document = await vscode.workspace.openTextDocument(fileUri);

            // Use parser if available
            if (this.parserFactory.isInitialized()) {
                const parser = this.parserFactory.getParser(document);
                const range = parser.getKeyRange(document, keyName);

                if (range) {
                    return new vscode.Location(fileUri, range);
                }
            }

            // Fallback: search by pattern
            const text = document.getText();
            const fileName = fileUri.fsPath.toLowerCase();

            if (fileName.endsWith('.resx')) {
                const pattern = new RegExp(`<data\\s+name="${this.escapeRegex(keyName)}"`, 'i');
                const match = pattern.exec(text);

                if (match) {
                    const pos = document.positionAt(match.index);
                    const endPos = document.positionAt(match.index + match[0].length);
                    return new vscode.Location(fileUri, new vscode.Range(pos, endPos));
                }
            } else if (fileName.endsWith('.json')) {
                // Handle nested keys
                const keyParts = keyName.split('.');
                const searchKey = keyParts[keyParts.length - 1];
                const pattern = new RegExp(`"${this.escapeRegex(searchKey)}"\\s*:`, 'i');
                const match = pattern.exec(text);

                if (match) {
                    const pos = document.positionAt(match.index);
                    const endPos = document.positionAt(match.index + match[0].length);
                    return new vscode.Location(fileUri, new vscode.Range(pos, endPos));
                }
            }
        } catch (error) {
            console.error(`LRM References: Error in file ${fileUri.fsPath}:`, error);
        }

        return null;
    }

    /**
     * Escape special regex characters
     */
    private escapeRegex(str: string): string {
        return str.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
    }
}
