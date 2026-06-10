/**
 * Escaping helpers for the Resource Editor webview. The webview's HTML is built as a
 * string, so the inline functions there cannot import this module — these are the
 * canonical, unit-tested implementations that the inline copies MUST mirror.
 */

/**
 * Escapes a value for an HTML attribute context (e.g. value="..."). Encodes the five
 * characters that can break attribute parsing or inject markup.
 */
export function escapeAttr(s: string | null | undefined): string {
  return String(s === null || s === undefined ? '' : s)
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

/**
 * Escapes a value for embedding inside a single-quoted JS string literal that itself
 * lives inside a double-quoted HTML on* attribute (e.g.
 * onclick="deleteKey('VALUE')"). Two contexts apply at once:
 *
 *  1. JS string literal — backslash and single-quote must be backslash-escaped, and
 *     newlines must not terminate the literal.
 *  2. HTML attribute — &, <, >, ", ' must be entity-encoded so the attribute and the
 *     surrounding markup cannot be broken (or used to inject) by the value.
 *
 * Order matters: JS-escape first (so the literal is valid), then HTML-encode (so the
 * browser decodes entities back to the intended characters before evaluating the JS).
 */
export function escapeJs(s: string | null | undefined): string {
  const jsEscaped = String(s === null || s === undefined ? '' : s)
    .replace(/\\/g, '\\\\') // backslash -> doubled
    .replace(/'/g, "\\'") // single quote -> escaped
    .replace(/\r/g, '') // drop CR
    .replace(/\n/g, '\\n'); // newline -> \n

  return jsEscaped
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}
