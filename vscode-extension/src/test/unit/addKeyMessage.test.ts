import { expect } from 'chai';
// addKeyMessage has no 'vscode' dependency, so a normal import is safe here.
import { buildAddKeyMessage, isDefaultValueEmpty } from '../../views/addKeyMessage';

describe('buildAddKeyMessage', () => {
  it('includes resourceGroup when provided (multi-group)', () => {
    const msg = buildAddKeyMessage({
      keyName: 'MyKey',
      resourceGroup: 'CustomerResources',
      values: { default: 'Hello' }
    });
    expect(msg).to.deep.equal({ command: 'addKey', key: 'MyKey', resourceGroup: 'CustomerResources', values: { default: 'Hello' } });
  });

  it('omits resourceGroup when not provided (single-group)', () => {
    const msg = buildAddKeyMessage({ keyName: 'K', resourceGroup: undefined, values: { default: 'V' } });
    expect(msg).to.deep.equal({ command: 'addKey', key: 'K', values: { default: 'V' } });
  });

  it('carries per-language values for every requested column', () => {
    const msg = buildAddKeyMessage({
      keyName: 'Greeting',
      resourceGroup: undefined,
      values: { default: 'Hello', el: 'Γεια', it: 'Ciao' }
    });
    expect(msg.values).to.deep.equal({ default: 'Hello', el: 'Γεια', it: 'Ciao' });
  });

  it('ensures a default entry exists even when only culture values are given', () => {
    const msg = buildAddKeyMessage({ keyName: 'K', resourceGroup: undefined, values: { el: 'Γεια' } });
    expect(msg.values).to.deep.equal({ el: 'Γεια', default: '' });
  });

  it('coerces undefined/null values to empty strings', () => {
    const msg = buildAddKeyMessage({
      keyName: 'K',
      resourceGroup: undefined,
      values: { default: undefined as any, el: null as any }
    });
    expect(msg.values).to.deep.equal({ default: '', el: '' });
  });

  it('trims the key name', () => {
    const msg = buildAddKeyMessage({ keyName: '  Spaced  ', resourceGroup: undefined, values: { default: 'V' } });
    expect(msg.key).to.equal('Spaced');
  });

  it('throws when key name is empty', () => {
    expect(() => buildAddKeyMessage({ keyName: '   ', resourceGroup: 'G', values: { default: 'V' } })).to.throw('Key name is required');
  });
});

describe('isDefaultValueEmpty', () => {
  it('is true when default value is missing', () => {
    expect(isDefaultValueEmpty({ el: 'Γεια' })).to.be.true;
  });
  it('is true when default value is blank/whitespace', () => {
    expect(isDefaultValueEmpty({ default: '   ' })).to.be.true;
  });
  it('is false when default value has content', () => {
    expect(isDefaultValueEmpty({ default: 'Hello' })).to.be.false;
  });
});
