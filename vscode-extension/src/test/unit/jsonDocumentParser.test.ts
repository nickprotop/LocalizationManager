import { expect } from 'chai';
import { createMockDocument } from '../mocks/vscode';

// The JsonDocumentParser imports from 'vscode', so we need to mock it
// For now, we'll use require and rely on our mock TextDocument matching the interface
const { JsonDocumentParser } = require('../../parsers/jsonDocumentParser');

describe('JsonDocumentParser', () => {
    describe('Standard Format', () => {
        it('parses simple string values', () => {
            const json = JSON.stringify({
                greeting: 'Hello',
                farewell: 'Goodbye'
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(2);
            expect(keys[0].key).to.equal('greeting');
            expect(keys[0].value).to.equal('Hello');
            expect(keys[1].key).to.equal('farewell');
            expect(keys[1].value).to.equal('Goodbye');
        });

        it('parses nested object keys with dot notation', () => {
            const json = JSON.stringify({
                buttons: {
                    save: 'Save',
                    cancel: 'Cancel',
                    delete: 'Delete'
                }
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(3);
            expect(keys[0].key).to.equal('buttons.save');
            expect(keys[0].value).to.equal('Save');
            expect(keys[1].key).to.equal('buttons.cancel');
            expect(keys[1].value).to.equal('Cancel');
            expect(keys[2].key).to.equal('buttons.delete');
            expect(keys[2].value).to.equal('Delete');
        });

        it('parses deeply nested keys', () => {
            const json = JSON.stringify({
                ui: {
                    dialogs: {
                        confirm: {
                            title: 'Confirm Action',
                            message: 'Are you sure?'
                        }
                    }
                }
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(2);
            expect(keys[0].key).to.equal('ui.dialogs.confirm.title');
            expect(keys[0].value).to.equal('Confirm Action');
            expect(keys[1].key).to.equal('ui.dialogs.confirm.message');
            expect(keys[1].value).to.equal('Are you sure?');
        });

        it('parses CLDR plural objects with _plural marker', () => {
            const json = JSON.stringify({
                itemCount: {
                    _plural: true,
                    one: '{0} item',
                    other: '{0} items'
                }
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(1);
            expect(keys[0].key).to.equal('itemCount');
            expect(keys[0].isPlural).to.be.true;
            expect(keys[0].pluralForms).to.deep.equal({
                one: '{0} item',
                other: '{0} items'
            });
            // Value should be the 'other' form (or 'one' if 'other' missing)
            expect(keys[0].value).to.equal('{0} items');
        });

        it('parses plural objects with all CLDR forms', () => {
            const json = JSON.stringify({
                documentCount: {
                    _plural: true,
                    zero: 'No documents',
                    one: '{0} document',
                    two: '{0} documents',
                    few: '{0} documents',
                    many: '{0} documents',
                    other: '{0} documents'
                }
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(1);
            expect(keys[0].key).to.equal('documentCount');
            expect(keys[0].isPlural).to.be.true;
            expect(keys[0].pluralForms).to.have.keys(['zero', 'one', 'two', 'few', 'many', 'other']);
        });

        it('parses _value/_comment metadata objects', () => {
            const json = JSON.stringify({
                withComment: {
                    _value: 'Value with comment',
                    _comment: 'This explains the purpose'
                }
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(1);
            expect(keys[0].key).to.equal('withComment');
            expect(keys[0].value).to.equal('Value with comment');
            expect(keys[0].comment).to.equal('This explains the purpose');
        });

        it('skips _meta object at root level', () => {
            const json = JSON.stringify({
                _meta: {
                    version: '1.0',
                    generator: 'LocalizationManager'
                },
                greeting: 'Hello',
                farewell: 'Goodbye'
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            // Should not include _meta keys
            expect(keys).to.have.length(2);
            expect(keys.map((k: any) => k.key)).to.deep.equal(['greeting', 'farewell']);
        });

        it('detects implicit plural objects (without _plural marker) with 2+ CLDR forms', () => {
            // Some systems use plural objects without explicit _plural marker
            const json = JSON.stringify({
                messages: {
                    one: 'One message',
                    other: 'Multiple messages'
                }
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(1);
            expect(keys[0].key).to.equal('messages');
            expect(keys[0].isPlural).to.be.true;
        });
    });

    describe('i18next Format', () => {
        it('consolidates plural suffix keys (_one, _other)', () => {
            const json = JSON.stringify({
                items_one: '{{count}} item',
                items_other: '{{count}} items'
            }, null, 2);

            const doc = createMockDocument(json, 'en.json');
            const parser = new JsonDocumentParser({}, 'i18next');
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(1);
            expect(keys[0].key).to.equal('items');
            expect(keys[0].isPlural).to.be.true;
            expect(keys[0].pluralForms).to.deep.equal({
                one: '{{count}} item',
                other: '{{count}} items'
            });
        });

        it('consolidates all i18next plural suffixes (_zero through _other)', () => {
            const json = JSON.stringify({
                messages_zero: 'No messages',
                messages_one: '{{count}} message',
                messages_two: '{{count}} messages',
                messages_few: '{{count}} messages',
                messages_many: '{{count}} messages',
                messages_other: '{{count}} messages'
            }, null, 2);

            const doc = createMockDocument(json, 'en.json');
            const parser = new JsonDocumentParser({}, 'i18next');
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(1);
            expect(keys[0].key).to.equal('messages');
            expect(keys[0].isPlural).to.be.true;
            expect(keys[0].pluralForms).to.have.keys(['zero', 'one', 'two', 'few', 'many', 'other']);
        });

        it('handles mixed regular and plural keys', () => {
            const json = JSON.stringify({
                greeting: 'Hello',
                items_one: '{{count}} item',
                items_other: '{{count}} items',
                farewell: 'Goodbye'
            }, null, 2);

            const doc = createMockDocument(json, 'en.json');
            const parser = new JsonDocumentParser({}, 'i18next');
            const keys = parser.parseDocument(doc);

            // Should have 3 keys: greeting, items (consolidated), farewell
            expect(keys).to.have.length(3);

            const keyNames = keys.map((k: any) => k.key);
            expect(keyNames).to.include('greeting');
            expect(keyNames).to.include('items');
            expect(keyNames).to.include('farewell');

            const itemsKey = keys.find((k: any) => k.key === 'items');
            expect(itemsKey.isPlural).to.be.true;
        });

        it('preserves nested structure with plural keys', () => {
            const json = JSON.stringify({
                nested: {
                    key: 'Nested value'
                },
                count_one: '{{count}} thing',
                count_other: '{{count}} things'
            }, null, 2);

            const doc = createMockDocument(json, 'en.json');
            const parser = new JsonDocumentParser({}, 'i18next');
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(2);
            expect(keys.find((k: any) => k.key === 'nested.key')).to.exist;
            expect(keys.find((k: any) => k.key === 'count')).to.exist;
        });

        it('does NOT consolidate plural keys when format is standard', () => {
            // When not in i18next mode, _one/_other suffixes are treated as regular keys
            const json = JSON.stringify({
                items_one: '{{count}} item',
                items_other: '{{count}} items'
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            const parser = new JsonDocumentParser({}, 'standard');
            const keys = parser.parseDocument(doc);

            // Should have 2 separate keys
            expect(keys).to.have.length(2);
            expect(keys.map((k: any) => k.key)).to.include('items_one');
            expect(keys.map((k: any) => k.key)).to.include('items_other');
        });

        it('config i18nextCompatible flag forces i18next mode', () => {
            const json = JSON.stringify({
                items_one: '{{count}} item',
                items_other: '{{count}} items'
            }, null, 2);

            const doc = createMockDocument(json, 'strings.json');
            // Pass config with i18nextCompatible: true
            const parser = new JsonDocumentParser({ i18nextCompatible: true });
            const keys = parser.parseDocument(doc);

            // Should consolidate due to config flag
            expect(keys).to.have.length(1);
            expect(keys[0].key).to.equal('items');
            expect(keys[0].isPlural).to.be.true;
        });
    });

    describe('Error Handling', () => {
        it('returns empty array for invalid JSON', () => {
            const invalidJson = '{ "key": "value", }'; // trailing comma

            const doc = createMockDocument(invalidJson, 'test.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.be.an('array');
            expect(keys).to.have.length(0);
        });

        it('returns empty array for empty document', () => {
            const doc = createMockDocument('', 'test.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.be.an('array');
            expect(keys).to.have.length(0);
        });

        it('returns empty array for non-object JSON', () => {
            const doc = createMockDocument('"just a string"', 'test.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.be.an('array');
            expect(keys).to.have.length(0);
        });

        it('handles null values gracefully', () => {
            const json = JSON.stringify({
                validKey: 'Valid',
                nullKey: null,
                anotherValid: 'Also valid'
            }, null, 2);

            const doc = createMockDocument(json, 'test.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            // Should skip null values
            expect(keys).to.have.length(2);
            expect(keys.map((k: any) => k.key)).to.deep.equal(['validKey', 'anotherValid']);
        });
    });

    describe('Line Number Tracking', () => {
        it('tracks line numbers for keys', () => {
            const json = `{
  "first": "First value",
  "second": "Second value"
}`;

            const doc = createMockDocument(json, 'test.json');
            const parser = new JsonDocumentParser();
            const keys = parser.parseDocument(doc);

            expect(keys).to.have.length(2);
            // Each key should have lineNumber defined
            expect(keys[0].lineNumber).to.be.a('number');
            expect(keys[1].lineNumber).to.be.a('number');
            // First key should be on line 1, second on line 2
            expect(keys[0].lineNumber).to.equal(1);
            expect(keys[1].lineNumber).to.equal(2);
        });
    });
});
