import * as vscode from 'vscode';
import { IResourceDocumentParser, ResourceKey } from './resourceDocumentParser';

/**
 * Parser for .resx (XML) resource files
 * Extracts <data name="..."><value>...</value></data> entries
 */
export class ResxDocumentParser implements IResourceDocumentParser {
    // Matches <value>content</value> - captures content
    private readonly valueRegex = /<value>([^<]*)<\/value>/;

    // Matches <comment>content</comment> - captures content
    private readonly commentRegex = /<comment>([^<]*)<\/comment>/;

    // Matches complete <data>...</data> block
    private readonly dataBlockRegex = /<data\s+name="([^"]+)"[^>]*>([\s\S]*?)<\/data>/g;

    parseDocument(document: vscode.TextDocument): ResourceKey[] {
        const keys: ResourceKey[] = [];
        const text = document.getText();

        // Reset regex state
        this.dataBlockRegex.lastIndex = 0;

        let match;
        while ((match = this.dataBlockRegex.exec(text)) !== null) {
            const keyName = match[1];
            const blockContent = match[2];
            const startIndex = match.index;

            // Find line number
            const startPos = document.positionAt(startIndex);
            const lineNumber = startPos.line;
            const columnStart = startPos.character;

            // Find the end position (after closing </data>)
            const endIndex = startIndex + match[0].length;
            const endPos = document.positionAt(endIndex);
            const columnEnd = endPos.character;

            // Extract value
            const valueMatch = this.valueRegex.exec(blockContent);
            const value = valueMatch ? this.decodeXmlEntities(valueMatch[1]) : '';

            // Extract comment
            const commentMatch = this.commentRegex.exec(blockContent);
            const comment = commentMatch ? this.decodeXmlEntities(commentMatch[1]) : undefined;

            keys.push({
                key: keyName,
                value,
                comment,
                lineNumber,
                columnStart,
                columnEnd
            });
        }

        return keys;
    }

    getKeyAtPosition(document: vscode.TextDocument, position: vscode.Position): ResourceKey | null {
        const keys = this.parseDocument(document);

        for (const key of keys) {
            // Check if position is on the same line as the key start
            // or within the data block
            if (position.line >= key.lineNumber) {
                // Get the range of this key
                const range = this.getKeyRange(document, key.key);
                if (range && range.contains(position)) {
                    return key;
                }
            }
        }

        return null;
    }

    getKeyRange(document: vscode.TextDocument, keyName: string): vscode.Range | null {
        const text = document.getText();

        // Reset regex state
        this.dataBlockRegex.lastIndex = 0;

        let match;
        while ((match = this.dataBlockRegex.exec(text)) !== null) {
            if (match[1] === keyName) {
                const startPos = document.positionAt(match.index);
                const endPos = document.positionAt(match.index + match[0].length);
                return new vscode.Range(startPos, endPos);
            }
        }

        return null;
    }

    /**
     * Decode XML entities to their character equivalents
     */
    private decodeXmlEntities(text: string): string {
        return text
            .replace(/&lt;/g, '<')
            .replace(/&gt;/g, '>')
            .replace(/&amp;/g, '&')
            .replace(/&quot;/g, '"')
            .replace(/&apos;/g, "'")
            .replace(/&#(\d+);/g, (_, code) => String.fromCharCode(parseInt(code, 10)))
            .replace(/&#x([0-9a-fA-F]+);/g, (_, code) => String.fromCharCode(parseInt(code, 16)));
    }
}
