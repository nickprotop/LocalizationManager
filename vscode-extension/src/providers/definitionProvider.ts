import * as vscode from 'vscode';
import * as path from 'path';
import { CacheService } from '../backend/cacheService';
import { getParserFactory, ResourceParserFactory } from '../parsers';

/**
 * Definition Provider for LRM extension.
 * Allows F12 "Go to Definition" on localization keys in code files
 * to jump to their definition in .resx or .json resource files.
 */
export class LrmDefinitionProvider implements vscode.DefinitionProvider {
    private cacheService: CacheService;
    private parserFactory: ResourceParserFactory;

    // Patterns to find resource key usage in code
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

    constructor(cacheService: CacheService) {
        this.cacheService = cacheService;
        this.parserFactory = getParserFactory();
    }

    async provideDefinition(
        document: vscode.TextDocument,
        position: vscode.Position,
        _token: vscode.CancellationToken
    ): Promise<vscode.Definition | null> {
        // Get the key at the current position
        const keyName = this.getKeyAtPosition(document, position);

        if (!keyName) {
            return null;
        }

        console.log(`LRM Definition: Looking for key "${keyName}"`);

        // Find the resource file containing this key
        const location = await this.findKeyDefinition(keyName);

        if (location) {
            console.log(`LRM Definition: Found at ${location.uri.fsPath}:${location.range.start.line + 1}`);
            return location;
        }

        console.log(`LRM Definition: Key "${keyName}" not found in resource files`);
        return null;
    }

    /**
     * Extract the localization key at the given position
     */
    private getKeyAtPosition(document: vscode.TextDocument, position: vscode.Position): string | null {
        const line = document.lineAt(position.line).text;

        for (const pattern of this.keyPatterns) {
            pattern.lastIndex = 0;
            let match;

            while ((match = pattern.exec(line)) !== null) {
                const matchStart = match.index;
                const matchEnd = matchStart + match[0].length;

                // Check if position is within this match
                if (position.character >= matchStart && position.character <= matchEnd) {
                    return match[1];
                }
            }
        }

        return null;
    }

    /**
     * Find the location of a key definition in resource files
     */
    private async findKeyDefinition(keyName: string): Promise<vscode.Location | null> {
        const resourcePath = this.cacheService.getResourcePath();

        if (!resourcePath) {
            // Try to find resource files in workspace
            return this.searchWorkspaceForKey(keyName);
        }

        // Determine format and search in resource path
        const format = this.parserFactory.getResourceFormat();

        if (format === 'json') {
            return this.findKeyInJsonFiles(keyName, resourcePath);
        } else {
            return this.findKeyInResxFiles(keyName, resourcePath);
        }
    }

    /**
     * Search workspace for key when no resource path is configured
     */
    private async searchWorkspaceForKey(keyName: string): Promise<vscode.Location | null> {
        // Try RESX files first
        const resxFiles = await vscode.workspace.findFiles('**/*.resx', '{**/node_modules/**,**/bin/**,**/obj/**}', 10);

        for (const file of resxFiles) {
            const location = await this.findKeyInFile(file, keyName);
            if (location) {
                return location;
            }
        }

        // Try JSON files
        const jsonFiles = await vscode.workspace.findFiles(
            '{**/locales/**/*.json,**/translations/**/*.json,**/i18n/**/*.json,**/strings*.json}',
            '{**/node_modules/**,**/bin/**,**/obj/**}',
            20
        );

        for (const file of jsonFiles) {
            const location = await this.findKeyInFile(file, keyName);
            if (location) {
                return location;
            }
        }

        return null;
    }

    /**
     * Find key in RESX files within resource path
     */
    private async findKeyInResxFiles(keyName: string, resourcePath: string): Promise<vscode.Location | null> {
        const pattern = new vscode.RelativePattern(resourcePath, '*.resx');
        const files = await vscode.workspace.findFiles(pattern, null, 20);

        // Sort to prefer default (non-localized) file
        files.sort((a, b) => {
            const aIsDefault = !a.fsPath.match(/\.[a-z]{2}(-[a-z]{2,4})?\.resx$/i);
            const bIsDefault = !b.fsPath.match(/\.[a-z]{2}(-[a-z]{2,4})?\.resx$/i);
            return aIsDefault === bIsDefault ? 0 : aIsDefault ? -1 : 1;
        });

        for (const file of files) {
            const location = await this.findKeyInFile(file, keyName);
            if (location) {
                return location;
            }
        }

        return null;
    }

    /**
     * Find key in JSON files within resource path
     */
    private async findKeyInJsonFiles(keyName: string, resourcePath: string): Promise<vscode.Location | null> {
        const pattern = new vscode.RelativePattern(resourcePath, '*.json');
        const files = await vscode.workspace.findFiles(pattern, '**/lrm*.json', 20);

        // Sort to prefer default file (en.json or strings.json without culture)
        files.sort((a, b) => {
            const aName = path.basename(a.fsPath).toLowerCase();
            const bName = path.basename(b.fsPath).toLowerCase();

            // Prefer en.json or files without culture codes
            const aIsDefault = aName === 'en.json' || !aName.match(/\.[a-z]{2}(-[a-z]{2,4})?\.json$/i);
            const bIsDefault = bName === 'en.json' || !bName.match(/\.[a-z]{2}(-[a-z]{2,4})?\.json$/i);

            return aIsDefault === bIsDefault ? 0 : aIsDefault ? -1 : 1;
        });

        for (const file of files) {
            const location = await this.findKeyInFile(file, keyName);
            if (location) {
                return location;
            }
        }

        return null;
    }

    /**
     * Find key location within a specific file
     */
    private async findKeyInFile(fileUri: vscode.Uri, keyName: string): Promise<vscode.Location | null> {
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
                // RESX pattern: <data name="KeyName"
                const pattern = new RegExp(`<data\\s+name="${this.escapeRegex(keyName)}"`, 'i');
                const match = pattern.exec(text);

                if (match) {
                    const pos = document.positionAt(match.index);
                    return new vscode.Location(fileUri, pos);
                }
            } else if (fileName.endsWith('.json')) {
                // JSON pattern: "KeyName":
                // Handle both flat and nested keys
                const keyParts = keyName.split('.');
                const searchKey = keyParts[keyParts.length - 1];
                const pattern = new RegExp(`"${this.escapeRegex(searchKey)}"\\s*:`, 'i');
                const match = pattern.exec(text);

                if (match) {
                    const pos = document.positionAt(match.index);
                    return new vscode.Location(fileUri, pos);
                }
            }
        } catch (error) {
            console.error(`LRM Definition: Error searching file ${fileUri.fsPath}:`, error);
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
