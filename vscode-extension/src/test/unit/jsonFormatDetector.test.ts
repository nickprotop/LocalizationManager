import { expect } from 'chai';

// Use our mock Uri class since we can't import from vscode
import { Uri } from '../mocks/vscode';

// Import the detector - need to mock vscode before importing
// Use require to avoid TypeScript import issues with vscode module
const { JsonFormatDetector } = require('../../parsers/jsonFormatDetector');

describe('JsonFormatDetector', () => {
    describe('isValidCultureCode', () => {
        it('recognizes common two-letter culture codes', () => {
            expect(JsonFormatDetector.isValidCultureCode('en')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('fr')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('de')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('es')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('it')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('ru')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('ja')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('ko')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('zh')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('el')).to.be.true;
        });

        it('recognizes culture codes with region (en-US, fr-FR)', () => {
            expect(JsonFormatDetector.isValidCultureCode('en-US')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('en-GB')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('fr-FR')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('fr-CA')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('de-DE')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('es-ES')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('es-MX')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('pt-BR')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('pt-PT')).to.be.true;
        });

        it('recognizes culture codes with script (zh-Hans, zh-Hant)', () => {
            expect(JsonFormatDetector.isValidCultureCode('zh-Hans')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('zh-Hant')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('zh-CN')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('zh-TW')).to.be.true;
        });

        it('is case insensitive for known codes', () => {
            expect(JsonFormatDetector.isValidCultureCode('EN')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('EN-us')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('en-us')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('ZH-hans')).to.be.true;
        });

        it('rejects invalid codes that are common filenames', () => {
            expect(JsonFormatDetector.isValidCultureCode('config')).to.be.false;
            expect(JsonFormatDetector.isValidCultureCode('strings')).to.be.false;
            expect(JsonFormatDetector.isValidCultureCode('translation')).to.be.false;
            expect(JsonFormatDetector.isValidCultureCode('resources')).to.be.false;
            expect(JsonFormatDetector.isValidCultureCode('messages')).to.be.false;
            expect(JsonFormatDetector.isValidCultureCode('common')).to.be.false;
        });

        it('rejects single-character codes', () => {
            expect(JsonFormatDetector.isValidCultureCode('e')).to.be.false;
            expect(JsonFormatDetector.isValidCultureCode('a')).to.be.false;
        });

        it('accepts BCP 47 pattern codes even if not in common list', () => {
            // Two letter codes matching xx pattern
            expect(JsonFormatDetector.isValidCultureCode('xy')).to.be.true;
            // With region: xx-XX pattern
            expect(JsonFormatDetector.isValidCultureCode('xy-ZZ')).to.be.true;
        });

        it('rejects codes that are too long', () => {
            // Codes like 'translation' don't match the BCP 47 pattern
            expect(JsonFormatDetector.isValidCultureCode('toolong')).to.be.false;
            expect(JsonFormatDetector.isValidCultureCode('invalid-code')).to.be.false;
        });

        it('accepts Nordic and Eastern European codes', () => {
            expect(JsonFormatDetector.isValidCultureCode('sv')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('da')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('fi')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('no')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('nb')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('pl')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('cs')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('hu')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('sk')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('uk')).to.be.true;
        });

        it('accepts Southeast Asian and Middle Eastern codes', () => {
            expect(JsonFormatDetector.isValidCultureCode('vi')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('th')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('id')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('ms')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('ar')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('he')).to.be.true;
            expect(JsonFormatDetector.isValidCultureCode('tr')).to.be.true;
        });
    });

    describe('detectFormat - file naming patterns (unit tests)', () => {
        // These tests use actual fixture files to test format detection
        // without needing to stub fs.readFileSync

        it('detects i18next from pure culture code filenames', async () => {
            // Test with our fixture files
            const fixtureDir = __dirname + '/../fixtures/i18next';
            const files = [
                Uri.file(fixtureDir + '/en.json'),
                Uri.file(fixtureDir + '/fr.json')
            ];

            const format = await JsonFormatDetector.detectFormat(files, fixtureDir);
            expect(format).to.equal('i18next');
        });

        it('detects standard from basename.culture.json pattern', async () => {
            const fixtureDir = __dirname + '/../fixtures/standard';
            const files = [
                Uri.file(fixtureDir + '/strings.json'),
                Uri.file(fixtureDir + '/strings.fr.json')
            ];

            const format = await JsonFormatDetector.detectFormat(files, fixtureDir);
            expect(format).to.equal('standard');
        });
    });
});
