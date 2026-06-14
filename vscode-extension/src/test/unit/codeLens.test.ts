import { expect } from 'chai';
import * as sinon from 'sinon';
import * as vscode from 'vscode';
import { LrmCodeLensProvider } from '../../providers/codeLens';
import { CacheService } from '../../backend/cacheService';
import { createMockDocument } from '../mocks/vscode';
import { ScanFileResponse } from '../../backend/apiClient';

/**
 * CodeLens for code files (.cs / .razor) is driven by the backend scanner via
 * cacheService.scanFile, NOT by client-side regex. These tests pin the issue #6
 * round-4 behavior:
 *   - Bug 2: namespace/using segments are never lensed (the backend doesn't
 *     report them, so no "Key not found: Components" CodeLens appears).
 *   - Feature: value hints appear for ANY injected localizer variable the backend
 *     reports (Q, Loc, ...), not only `L`.
 */
describe('LrmCodeLensProvider (code files)', () => {
    let cache: sinon.SinonStubbedInstance<CacheService>;
    let provider: LrmCodeLensProvider;
    const noToken = { isCancellationRequested: false } as vscode.CancellationToken;

    function scanResponse(partial: Partial<ScanFileResponse>): ScanFileResponse {
        return {
            scannedFiles: 1,
            totalReferences: 0,
            uniqueKeysFound: 0,
            unusedKeysCount: 0,
            missingKeysCount: 0,
            unused: [],
            missing: [],
            references: [],
            ...partial
        };
    }

    beforeEach(() => {
        cache = sinon.createStubInstance(CacheService);
        provider = new LrmCodeLensProvider(cache as unknown as CacheService);
    });

    afterEach(() => sinon.restore());

    it('shows a value hint for an injected localizer variable that is not `L` (Feature)', async () => {
        // Backend resolved `@Q["Customer_BusinessName_Label"]` on line 5 (1-based).
        cache.scanFile.resolves(scanResponse({
            references: [{
                key: 'Customer_BusinessName_Label',
                referenceCount: 1,
                references: [{ file: 'a.razor', line: 5, pattern: 'Q[...]', confidence: 'high' }]
            }]
        }));
        cache.getKeyDetails.resolves({
            key: 'Customer_BusinessName_Label',
            resourceGroup: 'CustomerResources',
            values: { default: { value: 'Business name' } },
            occurrenceCount: 1,
            hasDuplicates: false
        } as any);
        cache.getMissingLanguages.returns([]);

        const doc = createMockDocument('@inject IStringLocalizer<CustomerResources> Q\n\n\n\n<div>@Q["Customer_BusinessName_Label"]</div>', 'Page.razor');
        const lenses = await provider.provideCodeLenses(doc as any, noToken);

        const valueLens = lenses.find(l => l.command?.title === '"Business name"');
        expect(valueLens, 'expected a value hint lens for the injected variable').to.exist;
        // It is placed on the backend-reported line (0-based 4).
        expect(valueLens!.range.start.line).to.equal(4);
    });

    it('does NOT lens namespace segments (Bug 2)', async () => {
        // A nested namespace `Vitrum.Resources.Components.Account.Pages` must not
        // produce a `Components` CodeLens. The backend ignores namespace lines, so
        // it returns no references at all for such a file.
        cache.scanFile.resolves(scanResponse({ references: [], missing: [] }));

        const doc = createMockDocument('namespace Vitrum.Resources.Components.Account.Pages;\n\npublic class Login {}', 'Login.cs');
        const lenses = await provider.provideCodeLenses(doc as any, noToken);

        expect(lenses.find(l => /Components/.test(l.command?.title ?? '')), 'no namespace segment should be lensed').to.be.undefined;
        expect(lenses).to.have.length(0);
    });

    it('shows "Key not found" for a referenced-but-missing key', async () => {
        cache.scanFile.resolves(scanResponse({
            missing: ['Ghost_Key'],
            references: [{
                key: 'Ghost_Key',
                referenceCount: 1,
                references: [{ file: 'a.razor', line: 2, pattern: 'L[...]', confidence: 'high' }]
            }]
        }));

        const doc = createMockDocument('<div>\n@L["Ghost_Key"]\n</div>', 'Page.razor');
        const lenses = await provider.provideCodeLenses(doc as any, noToken);

        const notFound = lenses.find(l => l.command?.title === '$(error) Key not found: Ghost_Key');
        expect(notFound).to.exist;
        expect(notFound!.command?.command).to.equal('lrm.addKeyWithValueQuickFix');
        // Missing keys never trigger a getKeyDetails lookup.
        expect(cache.getKeyDetails.called).to.be.false;
    });

    it('resolves a data-annotation key against the group named by its ResourceType (Bug hunt #3)', async () => {
        // [Display(Name = "Product_Name_Label", ResourceType = typeof(GlassResources))]
        // The backend tags the reference with resourceTypeClassName. CodeLens must
        // resolve details/coverage against THAT group, not the first-cached group.
        cache.scanFile.resolves(scanResponse({
            references: [{
                key: 'Product_Name_Label',
                referenceCount: 1,
                references: [{
                    file: 'Model.cs', line: 2, pattern: '[Display(...)]', confidence: 'high',
                    resourceTypeClassName: 'GlassResources'
                }]
            }]
        }));
        cache.getKeyDetails.resolves({
            key: 'Product_Name_Label', resourceGroup: 'GlassResources',
            values: { default: { value: 'Glass product name' } },
            occurrenceCount: 1, hasDuplicates: false
        } as any);
        cache.getMissingLanguages.returns([]);

        const doc = createMockDocument('\n[Display(Name = "Product_Name_Label", ResourceType = typeof(GlassResources))]\npublic string Name { get; set; }', 'Model.cs');
        await provider.provideCodeLenses(doc as any, noToken);

        // Details and coverage must be looked up scoped to the bound group.
        expect(cache.getKeyDetails.calledWith('Product_Name_Label', false, 'GlassResources'),
            'getKeyDetails called with the bound group').to.be.true;
        expect(cache.getMissingLanguages.calledWith('Product_Name_Label', 'GlassResources'),
            'getMissingLanguages called with the bound group').to.be.true;
    });

    it('passes no group for an untyped reference (group-agnostic key)', async () => {
        cache.scanFile.resolves(scanResponse({
            references: [{
                key: 'Plain_Key',
                referenceCount: 1,
                references: [{ file: 'a.razor', line: 2, pattern: 'L[...]', confidence: 'high' }]
            }]
        }));
        cache.getKeyDetails.resolves({
            key: 'Plain_Key', resourceGroup: 'R', values: { default: { value: 'X' } },
            occurrenceCount: 1, hasDuplicates: false
        } as any);
        cache.getMissingLanguages.returns([]);

        const doc = createMockDocument('<div>\n@L["Plain_Key"]\n</div>', 'Page.razor');
        await provider.provideCodeLenses(doc as any, noToken);

        // boundGroup resolves to undefined -> details fetched without a group.
        expect(cache.getKeyDetails.calledWith('Plain_Key', false, undefined),
            'getKeyDetails called without a group for untyped refs').to.be.true;
    });

    it('does not emit duplicate lenses for the same key on the same line', async () => {
        cache.scanFile.resolves(scanResponse({
            references: [{
                key: 'Dup',
                referenceCount: 2,
                references: [
                    { file: 'a.razor', line: 3, pattern: 'L[...]', confidence: 'high' },
                    { file: 'a.razor', line: 3, pattern: 'L[...]', confidence: 'high' }
                ]
            }]
        }));
        cache.getKeyDetails.resolves({
            key: 'Dup', resourceGroup: 'R', values: { default: { value: 'X' } },
            occurrenceCount: 1, hasDuplicates: false
        } as any);
        cache.getMissingLanguages.returns([]);

        const doc = createMockDocument('\n\n@L["Dup"] @L["Dup"]', 'Page.razor');
        const lenses = await provider.provideCodeLenses(doc as any, noToken);

        expect(lenses.filter(l => l.command?.title === '"X"')).to.have.length(1);
    });
});
