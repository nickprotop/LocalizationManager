import { expect } from 'chai';
import {
  shouldInvalidateScanCache,
  resolveScanAction,
  ResourceEditorEvent
} from '../../views/scanCacheState';

/**
 * Regression tests for the "Scan Code results become stale after editing keys"
 * bug (issue #6 follow-up).
 *
 * Repro from the report: scan reports a missing key -> add it via the editor ->
 * scan again still reports it missing; Refresh doesn't help; only closing and
 * reopening the editor does. Root cause: the webview's in-memory scanResultsCache
 * was never cleared while the editor stayed open, so "Scan Code" kept short-
 * circuiting to the snapshot taken when the editor first opened.
 */
describe('scan results cache staleness (issue #6 follow-up)', () => {
  it('invalidates the scan cache after adding a key', () => {
    expect(shouldInvalidateScanCache('addSuccess')).to.equal(true);
  });

  it('invalidates the scan cache after updating a key', () => {
    expect(shouldInvalidateScanCache('updateSuccess')).to.equal(true);
  });

  it('invalidates the scan cache after deleting a key', () => {
    expect(shouldInvalidateScanCache('deleteSuccess')).to.equal(true);
  });

  it('invalidates the scan cache on an explicit Refresh', () => {
    // The reporter noted Refresh alone did not resolve the stale scan.
    expect(shouldInvalidateScanCache('refresh')).to.equal(true);
  });

  it('re-queries the backend once the cache is cleared', () => {
    expect(resolveScanAction(null)).to.equal('requery');
  });

  it('still serves the cache while it is populated (fast re-open)', () => {
    expect(resolveScanAction({ missing: [], unused: [], references: [] })).to.equal('use-cache');
  });

  it('end-to-end: add-then-rescan re-queries instead of reusing the stale snapshot', () => {
    // Simulate the webview state machine.
    let cache: unknown | null = { missing: ['Processing_Description_Label'], references: [] };

    // First scan uses the snapshot.
    expect(resolveScanAction(cache)).to.equal('use-cache');

    // User adds the missing key -> loadResources() runs -> cache cleared.
    const event: ResourceEditorEvent = 'addSuccess';
    if (shouldInvalidateScanCache(event)) {
      cache = null;
    }

    // Next "Scan Code" must hit the backend, which no longer reports it missing.
    expect(resolveScanAction(cache)).to.equal('requery');
  });
});
