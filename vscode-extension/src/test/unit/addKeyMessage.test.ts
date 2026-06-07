import { expect } from 'chai';
// addKeyMessage has no 'vscode' dependency, so a normal import is safe here.
import { buildAddKeyMessage } from '../../views/addKeyMessage';

describe('buildAddKeyMessage', () => {
  it('includes resourceGroup when provided (multi-group)', () => {
    const msg = buildAddKeyMessage('MyKey', 'Hello', 'CustomerResources');
    expect(msg).to.deep.equal({ command: 'addKey', key: 'MyKey', resourceGroup: 'CustomerResources', values: { default: 'Hello' } });
  });
  it('omits resourceGroup when not provided (single-group)', () => {
    const msg = buildAddKeyMessage('K', 'V', undefined);
    expect(msg).to.deep.equal({ command: 'addKey', key: 'K', values: { default: 'V' } });
  });
  it('trims the key name', () => {
    const msg = buildAddKeyMessage('  Spaced  ', 'V', undefined);
    expect(msg.key).to.equal('Spaced');
  });
  it('throws when key name is empty', () => {
    expect(() => buildAddKeyMessage('   ', 'V', 'G')).to.throw('Key name is required');
  });
});
