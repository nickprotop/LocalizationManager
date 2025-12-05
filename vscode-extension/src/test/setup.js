/**
 * Test setup file that mocks the vscode module before any tests run
 * This allows us to test VS Code extension code without the VS Code runtime
 */

'use strict';

const Module = require('module');

// Position class
class Position {
    constructor(line, character) {
        this.line = line;
        this.character = character;
    }

    isEqual(other) {
        return this.line === other.line && this.character === other.character;
    }

    isBefore(other) {
        if (this.line < other.line) return true;
        if (this.line > other.line) return false;
        return this.character < other.character;
    }

    isAfter(other) {
        return !this.isEqual(other) && !this.isBefore(other);
    }
}

// Range class
class Range {
    constructor(start, end) {
        this.start = start;
        this.end = end;
    }

    contains(position) {
        if (position.isBefore(this.start)) return false;
        if (position.isAfter(this.end)) return false;
        return true;
    }

    get isEmpty() {
        return this.start.isEqual(this.end);
    }
}

// Disposable class
class Disposable {
    constructor(callOnDispose) {
        this.callOnDispose = callOnDispose;
    }

    dispose() {
        if (this.callOnDispose) {
            this.callOnDispose();
        }
    }
}

// EventEmitter class
class EventEmitter {
    constructor() {
        this.listeners = [];
        this.event = (listener) => {
            this.listeners.push(listener);
            return { dispose: () => {} };
        };
    }

    fire(data) {
        for (const l of this.listeners) {
            l(data);
        }
    }
}

// Create mock vscode module
const mockVscode = {
    Uri: {
        file: (path) => ({
            fsPath: path,
            path: path,
            toString: () => `file://${path}`
        }),
        parse: (value) => ({
            fsPath: value,
            path: value,
            toString: () => value
        })
    },
    Position,
    Range,
    TextDocument: {},
    workspace: {
        fs: {
            readFile: async () => { throw new Error('Mock readFile - stub in test'); }
        },
        getConfiguration: () => ({
            get: () => undefined,
            has: () => false,
            inspect: () => undefined,
            update: async () => {}
        })
    },
    window: {
        showInformationMessage: async () => undefined,
        showWarningMessage: async () => undefined,
        showErrorMessage: async () => undefined,
        createOutputChannel: () => ({
            appendLine: () => {},
            append: () => {},
            show: () => {},
            hide: () => {},
            dispose: () => {}
        })
    },
    commands: {
        registerCommand: () => ({ dispose: () => {} }),
        executeCommand: async () => undefined
    },
    languages: {
        registerCodeLensProvider: () => ({ dispose: () => {} }),
        registerCompletionItemProvider: () => ({ dispose: () => {} })
    },
    Disposable,
    EventEmitter,
    DiagnosticSeverity: {
        Error: 0,
        Warning: 1,
        Information: 2,
        Hint: 3
    },
    CompletionItemKind: {
        Text: 0,
        Method: 1,
        Function: 2,
        Constructor: 3,
        Field: 4,
        Variable: 5,
        Class: 6,
        Interface: 7,
        Module: 8,
        Property: 9,
        Unit: 10,
        Value: 11,
        Enum: 12,
        Keyword: 13,
        Snippet: 14,
        Color: 15,
        Reference: 17,
        File: 16,
        Folder: 18,
        EnumMember: 19,
        Constant: 20,
        Struct: 21,
        Event: 22,
        Operator: 23,
        TypeParameter: 24
    }
};

// Intercept require for 'vscode' module using Module._load
const originalLoad = Module._load;
Module._load = function(request, parent, isMain) {
    if (request === 'vscode') {
        return mockVscode;
    }
    return originalLoad(request, parent, isMain);
};

module.exports = { mockVscode };
