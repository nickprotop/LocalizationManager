import { expect } from 'chai';
// deleteKeyMessage has no 'vscode' dependency, so a normal import is safe here.
import { buildDeleteKeyMessage } from '../../views/deleteKeyMessage';

describe('buildDeleteKeyMessage', () => {
  it('includes resourceGroup when provided (multi-group)', () => {
    const msg = buildDeleteKeyMessage('MyKey', 'CustomerResources');
    expect(msg).to.deep.equal({ command: 'deleteKey', key: 'MyKey', resourceGroup: 'CustomerResources' });
  });

  it('omits resourceGroup when not provided (single-group)', () => {
    const msg = buildDeleteKeyMessage('K', undefined);
    expect(msg).to.deep.equal({ command: 'deleteKey', key: 'K' });
  });

  it('omits resourceGroup when blank', () => {
    const msg = buildDeleteKeyMessage('K', '');
    expect(msg).to.deep.equal({ command: 'deleteKey', key: 'K' });
  });

  it('preserves the key verbatim (no trimming — keys may be selected exactly)', () => {
    const msg = buildDeleteKeyMessage('Users_New_Title', 'G');
    expect(msg.key).to.equal('Users_New_Title');
  });

  it('throws when key is empty', () => {
    expect(() => buildDeleteKeyMessage('', 'G')).to.throw('Key is required');
  });

  it('throws when key is whitespace only', () => {
    expect(() => buildDeleteKeyMessage('   ', undefined)).to.throw('Key is required');
  });
});
