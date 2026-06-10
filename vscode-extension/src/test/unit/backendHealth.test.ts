import { expect } from 'chai';
import { isHealthyResponse, restartSucceeded } from '../../backend/backendHealth';

describe('isHealthyResponse', () => {
  it('is healthy on HTTP 200', () => {
    expect(isHealthyResponse(200)).to.be.true;
  });

  it('is healthy on any 2xx', () => {
    expect(isHealthyResponse(204)).to.be.true;
  });

  it('is not healthy on 4xx', () => {
    expect(isHealthyResponse(400)).to.be.false;
    expect(isHealthyResponse(404)).to.be.false;
  });

  it('is not healthy on 5xx', () => {
    expect(isHealthyResponse(500)).to.be.false;
    expect(isHealthyResponse(503)).to.be.false;
  });

  it('is not healthy on undefined/0 (connection refused)', () => {
    expect(isHealthyResponse(undefined)).to.be.false;
    expect(isHealthyResponse(0)).to.be.false;
  });
});

describe('restartSucceeded', () => {
  it('reports success only when the post-restart health check passed', () => {
    expect(restartSucceeded(true)).to.be.true;
  });

  it('reports failure when the backend is not reachable after restart', () => {
    // This is the issue #6 regression: previously success was reported
    // unconditionally even though the status bar immediately showed Failed.
    expect(restartSucceeded(false)).to.be.false;
  });
});
