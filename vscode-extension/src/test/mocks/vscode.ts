/**
 * Minimal VS Code API mocks for unit testing without VS Code runtime
 */

/**
 * Mock for vscode.Uri
 */
export class Uri {
    fsPath: string;

    constructor(fsPath: string) {
        this.fsPath = fsPath;
    }

    static file(filePath: string): Uri {
        return new Uri(filePath);
    }

    static parse(value: string): Uri {
        return new Uri(value);
    }

    get path(): string {
        return this.fsPath;
    }

    toString(): string {
        return `file://${this.fsPath}`;
    }
}

/**
 * Mock for vscode.Position
 */
export class Position {
    line: number;
    character: number;

    constructor(line: number, character: number) {
        this.line = line;
        this.character = character;
    }

    isEqual(other: Position): boolean {
        return this.line === other.line && this.character === other.character;
    }

    isBefore(other: Position): boolean {
        if (this.line < other.line) return true;
        if (this.line > other.line) return false;
        return this.character < other.character;
    }

    isAfter(other: Position): boolean {
        return !this.isEqual(other) && !this.isBefore(other);
    }
}

/**
 * Mock for vscode.Range
 */
export class Range {
    start: Position;
    end: Position;

    constructor(start: Position, end: Position) {
        this.start = start;
        this.end = end;
    }

    contains(position: Position): boolean {
        if (position.isBefore(this.start)) return false;
        if (position.isAfter(this.end)) return false;
        return true;
    }

    get isEmpty(): boolean {
        return this.start.isEqual(this.end);
    }
}

/**
 * Mock for vscode.TextDocument
 */
export interface TextDocument {
    getText(): string;
    positionAt(offset: number): Position;
    offsetAt(position: Position): number;
    lineAt(line: number): { text: string };
    languageId: string;
    fileName: string;
    uri: Uri;
}

/**
 * Factory to create mock TextDocument from content
 */
export function createMockDocument(content: string, fileName = 'test.json'): TextDocument {
    const lines = content.split('\n');

    return {
        getText: () => content,
        positionAt: (offset: number): Position => {
            const textUpToOffset = content.slice(0, offset);
            const linesUpToOffset = textUpToOffset.split('\n');
            const line = linesUpToOffset.length - 1;
            const character = linesUpToOffset[line].length;
            return new Position(line, character);
        },
        offsetAt: (position: Position): number => {
            let offset = 0;
            for (let i = 0; i < position.line && i < lines.length; i++) {
                offset += lines[i].length + 1; // +1 for newline
            }
            offset += Math.min(position.character, lines[position.line]?.length || 0);
            return offset;
        },
        lineAt: (lineNumber: number) => ({
            text: lines[lineNumber] || ''
        }),
        languageId: fileName.endsWith('.json') ? 'json' :
                    fileName.endsWith('.resx') ? 'xml' : 'plaintext',
        fileName,
        uri: Uri.file(fileName)
    };
}

/**
 * Mock for vscode.workspace to support file reading in format detection
 */
export const workspace = {
    fs: {
        readFile: async (uri: Uri): Promise<Uint8Array> => {
            // This will be stubbed in tests that need file system access
            throw new Error(`Mock fs.readFile called for ${uri.fsPath} - stub this in your test`);
        }
    }
};
