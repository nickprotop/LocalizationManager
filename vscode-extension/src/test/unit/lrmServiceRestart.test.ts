import { expect } from 'chai';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';

// The global vscode mock is installed by test/setup.js. We grab it so we can point
// the service's resource-path resolution at a temp directory for this test.
// eslint-disable-next-line @typescript-eslint/no-var-requires
const { mockVscode } = require('../setup.js');

import { LrmService } from '../../backend/lrmService';

/**
 * Integration test for the Restart Backend flow (issue #6). Drives the REAL bundled
 * `lrm web` backend, so it is gated on the platform binary being present and given
 * generous timeouts (the self-contained binary takes several seconds to boot).
 */
describe('LrmService restart flow (integration)', function () {
  this.timeout(60000);

  const extensionPath = path.resolve(__dirname, '../../..'); // vscode-extension root (has bin/)
  const binary = (() => {
    /* eslint-disable @typescript-eslint/naming-convention */
    const map: Record<string, string> = {
      'linux-x64': 'linux-x64/lrm',
      'linux-arm64': 'linux-arm64/lrm',
      'darwin-x64': 'darwin-x64/lrm',
      'darwin-arm64': 'darwin-arm64/lrm',
      'win32-x64': 'win32-x64/lrm.exe',
      'win32-arm64': 'win32-arm64/lrm.exe'
    };
    /* eslint-enable @typescript-eslint/naming-convention */
    const key = `${process.platform}-${process.arch}`;
    return map[key] ? path.join(extensionPath, 'bin', map[key]) : null;
  })();

  let tempDir: string;
  let originalGetConfiguration: any;
  let service: LrmService | null = null;

  before(function () {
    if (!binary || !fs.existsSync(binary)) {
      // The lrm backend binary is git-ignored and only present after
      // `npm run bundle-binaries` (or a packaged build). CI runs the unit tests
      // without bundling, so skip there rather than fail. The restart decision logic
      // is still covered in CI by backendHealth.test.ts; this exercises the real flow
      // locally / in packaged builds.
      // eslint-disable-next-line no-console
      console.log(`[skip] restart integration test: backend binary not found at ${binary ?? '(unsupported platform)'}`);
      this.skip();
    }

    tempDir = fs.mkdtempSync(path.join(os.tmpdir(), 'lrm-restart-'));
    fs.writeFileSync(
      path.join(tempDir, 'Strings.resx'),
      '<?xml version="1.0" encoding="utf-8"?>\n<root>\n' +
        '  <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>\n' +
        '  <data name="Hello" xml:space="preserve">\n    <value>Hi</value>\n  </data>\n</root>\n'
    );

    // Force resolveResourcePath() to use our temp dir (it reads lrm.resourcePath first).
    originalGetConfiguration = mockVscode.workspace.getConfiguration;
    mockVscode.workspace.getConfiguration = () => ({
      get: (key: string, defaultValue?: any) => (key === 'resourcePath' ? tempDir : defaultValue),
      has: () => false,
      inspect: () => undefined,
      update: async () => {}
    });
  });

  after(async function () {
    if (service) {
      try { await service.stop(); } catch { /* ignore */ }
    }
    if (originalGetConfiguration) {
      mockVscode.workspace.getConfiguration = originalGetConfiguration;
    }
    if (tempDir && fs.existsSync(tempDir)) {
      fs.rmSync(tempDir, { recursive: true, force: true });
    }
  });

  it('starts, restarts on a fresh port, stays healthy, then stops', async function () {
    service = new LrmService({ extensionPath });

    await service.start();
    expect(service.isRunning(), 'running after start').to.equal(true);
    expect(await service.isHealthy(), 'healthy after start').to.equal(true);

    const portBefore = service.getPort();
    expect(portBefore).to.be.greaterThan(0);

    await service.restart();
    expect(service.isRunning(), 'running after restart').to.equal(true);
    expect(await service.isHealthy(), 'healthy after restart').to.equal(true);

    const portAfter = service.getPort();
    expect(portAfter).to.be.greaterThan(0);
    // A new port is chosen each start, so getBaseUrl() must reflect the live process.
    expect(service.getBaseUrl()).to.equal(`http://localhost:${portAfter}`);

    await service.stop();
    expect(service.isRunning(), 'not running after stop').to.equal(false);
    expect(await service.isHealthy(), 'not healthy after stop').to.equal(false);
    service = null;
  });

  it('survives several consecutive restarts without leaking listeners or losing health', async function () {
    service = new LrmService({ extensionPath });
    await service.start();
    expect(await service.isHealthy(), 'healthy after initial start').to.equal(true);

    const ports = new Set<number>();
    ports.add(service.getPort());

    for (let i = 0; i < 3; i++) {
      await service.restart();
      expect(service.isRunning(), `running after restart ${i + 1}`).to.equal(true);
      expect(await service.isHealthy(), `healthy after restart ${i + 1}`).to.equal(true);
      ports.add(service.getPort());

      // No stray exit listeners should accumulate on the live process across restarts.
      // start() adds one ('exit') and one ('error'); stop() uses once() so it adds none
      // that survive. Allow a small margin but catch unbounded growth.
      const proc = (service as unknown as { process: { listenerCount(e: string): number } | null }).process;
      if (proc) {
        expect(proc.listenerCount('exit'), `exit listeners after restart ${i + 1}`).to.be.lessThan(4);
      }
    }

    await service.stop();
    expect(service.isRunning(), 'not running after final stop').to.equal(false);
    service = null;
  });
});
