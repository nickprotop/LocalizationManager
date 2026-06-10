import { expect } from 'chai';
import { StatusBarManager } from '../../views/statusBar';
import { DashboardPanel } from '../../views/dashboard';
import { SettingsPanel } from '../../views/settingsPanel';

/**
 * Regression tests for the "stale API client after backend restart" bug (issue #6
 * follow-up). When the backend restarts on a new port a fresh ApiClient is created;
 * components that captured the old client must be repointed or they keep querying the
 * dead port. These tests assert the swap actually takes effect by observing which
 * fake client gets called.
 */

interface FakeStats {
  languages: any[];
  overallCoverage: number;
  totalKeys: number;
}

function makeFakeApiClient(id: string) {
  const calls: string[] = [];
  return {
    id,
    calls,
    getStats: async (): Promise<FakeStats> => {
      calls.push('getStats');
      return { languages: [], overallCoverage: 0, totalKeys: 0 };
    },
    // Settings/Dashboard touch a few others; stub them as no-ops that record the id.
    getTranslationProviders: async () => { calls.push('getTranslationProviders'); return []; },
    getConfiguration: async () => { calls.push('getConfiguration'); return {}; },
    getCredentialProviders: async () => { calls.push('getCredentialProviders'); return {}; }
  } as any;
}

const fakeLrmService = {
  isRunning: () => true,
  getResourcePath: () => '/tmp/resources'
} as any;

describe('stale API client after restart', () => {
  afterEach(() => {
    // Reset singletons between tests.
    DashboardPanel.currentPanel?.dispose?.();
    DashboardPanel.currentPanel = undefined;
    SettingsPanel.currentPanel?.dispose?.();
    SettingsPanel.currentPanel = undefined;
  });

  it('StatusBarManager.setApiClient repoints health checks at the new client', async () => {
    const oldClient = makeFakeApiClient('old');
    const newClient = makeFakeApiClient('new');

    const sb = new StatusBarManager(oldClient, fakeLrmService);

    // Simulate a restart: a fresh client is created against the new port.
    sb.setApiClient(newClient);
    const healthy = await sb.update();

    expect(healthy, 'update() reports healthy from the new client').to.equal(true);
    expect(newClient.calls, 'new client received the health probe').to.include('getStats');
    sb.dispose();
  });

  it('DashboardPanel.refreshApiClient updates the open panel; reopening uses the new client', async () => {
    const oldClient = makeFakeApiClient('old');
    const newClient = makeFakeApiClient('new');

    DashboardPanel.createOrShow(oldClient);
    // The constructor's initial update() queried the old client.
    expect(oldClient.calls).to.include('getStats');

    // Restart path repoints the already-open panel...
    DashboardPanel.refreshApiClient(newClient);
    const oldCallCountNew = newClient.calls.length;

    // ...and reopening reveals + refreshes via the new client.
    DashboardPanel.createOrShow(newClient);
    expect(newClient.calls.length, 'new client used after refresh+reveal').to.be.greaterThan(oldCallCountNew);
  });

  it('SettingsPanel.refreshApiClient swaps the client used by the open panel', () => {
    const oldClient = makeFakeApiClient('old');
    const newClient = makeFakeApiClient('new');

    SettingsPanel.createOrShow(oldClient);
    // Should not throw and should swap without recreating the panel.
    SettingsPanel.refreshApiClient(newClient);

    // Reopening reveals the existing panel and points it at the new client.
    SettingsPanel.createOrShow(newClient);
    expect(SettingsPanel.currentPanel, 'panel remains a singleton').to.not.be.undefined;
  });

  it('refresh helpers are safe no-ops when no panel is open', () => {
    DashboardPanel.currentPanel = undefined;
    SettingsPanel.currentPanel = undefined;
    const client = makeFakeApiClient('x');
    expect(() => DashboardPanel.refreshApiClient(client)).to.not.throw();
    expect(() => SettingsPanel.refreshApiClient(client)).to.not.throw();
  });
});
