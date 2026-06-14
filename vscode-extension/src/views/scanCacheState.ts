/**
 * Scan-results cache lifecycle for the Resource Editor webview.
 *
 * The webview keeps the last "Scan Code" result in a JS variable so re-opening the
 * modal is instant. The bug (issue #6 follow-up) was that this cache was never
 * cleared while the editor stayed open: adding a previously-missing key in the
 * editor left the stale snapshot in place, so "Scan Code" (and even the Refresh
 * button) kept reporting the key as missing until the tab was closed and reopened.
 *
 * The webview's HTML/JS is built as a string and cannot import this module, so these
 * are the canonical, unit-tested rules the inline logic MUST mirror:
 *
 *  - Any resource mutation (add/update/delete) and the explicit Refresh both funnel
 *    through loadResources(), which clears the cache. The backend cache is already
 *    invalidated on mutations, so the next scan re-queries fresh.
 *  - "Scan Code" only short-circuits to the in-memory cache when it is still present;
 *    once cleared it posts a real `scanCode` message to the extension.
 */

export type ScanResultsCache = unknown | null;

/**
 * Events that occur while the editor is open and that affect whether the cached scan
 * snapshot is still trustworthy.
 */
export type ResourceEditorEvent =
  | 'refresh' // user clicked the Refresh button
  | 'addSuccess' // a key was added
  | 'updateSuccess' // a key value was updated
  | 'deleteSuccess'; // a key was deleted

/**
 * Whether the cached scan snapshot must be invalidated in response to an event.
 * Every event that reloads the resource grid also invalidates the scan cache, because
 * the set of keys (and therefore which keys are missing/unused) may have changed.
 */
export function shouldInvalidateScanCache(event: ResourceEditorEvent): boolean {
  switch (event) {
    case 'refresh':
    case 'addSuccess':
    case 'updateSuccess':
    case 'deleteSuccess':
      return true;
    default:
      return false;
  }
}

/**
 * Decide what "Scan Code" should do given the current cache. When the cache has been
 * cleared (null), it must re-query the backend rather than re-displaying stale data.
 */
export function resolveScanAction(cache: ScanResultsCache): 'use-cache' | 'requery' {
  return cache ? 'use-cache' : 'requery';
}
