import { expect } from 'chai';
import { escapeJs, escapeAttr } from '../../views/webviewEscaping';

/**
 * Minimal HTML attribute-value entity decoder, mimicking what the browser does when it
 * parses an on* attribute before evaluating its JavaScript. & must be decoded last.
 */
function htmlAttrDecode(s: string): string {
  return s
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&amp;/g, '&');
}

/**
 * Reproduces the full webview round-trip for a delete button: embed the escaped key
 * into the onclick attribute exactly as renderTable() does, let the "browser" decode
 * the attribute, then evaluate the handler and capture what deleteKey() receives.
 */
function roundTripThroughOnclick(key: string, group: string): { key: string; group: string } {
  const attrValue = `deleteKey('${escapeJs(key)}', '${escapeJs(group)}')`;
  // The attribute lives inside double quotes in the HTML; ensure escaping never emits a
  // raw double quote that would terminate the attribute early.
  expect(attrValue, 'no raw double-quote may leak into the attribute').to.not.match(/(?<!&)(?<!#)"/);
  // Also ensure no raw < or > that could be parsed as markup inside the attribute.
  expect(attrValue, 'no raw angle brackets').to.not.match(/[<>]/);

  const js = htmlAttrDecode(attrValue);
  let captured: { key: string; group: string } | null = null;
  const deleteKey = (k: string, g: string) => {
    captured = { key: k, group: g };
  };
  void deleteKey; // referenced via eval(js) below
  // eslint-disable-next-line no-eval
  eval(js);
  if (!captured) {
    throw new Error('handler did not run');
  }
  return captured;
}

describe('escapeJs (webview onclick escaping)', () => {
  const cases: Array<[string, string]> = [
    ['Plain', 'plain key'],
    ['Apostrophe', "It's"],
    ['Ampersand', 'a&b'],
    ['AngleBrackets', 'x<y>z'],
    ['DoubleQuote', 'q"r'],
    ['Backslash', 'back\\slash'],
    ['BackslashN', 'literal\\nbackslash-n'],
    ['Newline', 'line\nbreak'],
    ['Markup injection', '<script>alert(1)</script>'],
    ['Onclick breakout', "'),deleteKey('evil"],
    ['Combined', `a&'<>"\\b`]
  ];

  for (const [name, key] of cases) {
    it(`round-trips "${name}" through the onclick handler intact`, () => {
      const out = roundTripThroughOnclick(key, 'MyGroup');
      expect(out.key).to.equal(key);
      expect(out.group).to.equal('MyGroup');
    });
  }

  it('handles a group with special characters too', () => {
    const out = roundTripThroughOnclick('SomeKey', "Group&'<>");
    expect(out.key).to.equal('SomeKey');
    expect(out.group).to.equal("Group&'<>");
  });

  it('coerces null/undefined to empty string', () => {
    expect(escapeJs(null)).to.equal('');
    expect(escapeJs(undefined)).to.equal('');
  });

  it('does not let a crafted key inject a second statement', () => {
    // The classic breakout attempt: close the string and call something else.
    const malicious = "');window.__pwned=true;//";
    const out = roundTripThroughOnclick(malicious, '');
    // The whole thing must arrive as a single literal argument, not execute.
    expect(out.key).to.equal(malicious);
    expect((globalThis as any).__pwned).to.be.undefined;
  });
});

describe('escapeAttr', () => {
  it('encodes the five attribute-breaking characters', () => {
    expect(escapeAttr('a&b"c<d>e')).to.equal('a&amp;b&quot;c&lt;d&gt;e');
  });

  it('coerces null/undefined to empty string', () => {
    expect(escapeAttr(null)).to.equal('');
    expect(escapeAttr(undefined)).to.equal('');
  });
});
