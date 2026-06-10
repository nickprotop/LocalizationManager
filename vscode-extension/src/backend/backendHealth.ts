/**
 * Pure helpers for deciding whether the LRM backend is healthy. Extracted so the
 * decision logic can be unit-tested without spawning a process or hitting the
 * network. Both LrmService (readiness polling / health checks) and the restart
 * command handler use these so "healthy" means the same thing everywhere.
 */

/**
 * True when an HTTP status code from the backend's health endpoint indicates the
 * service is up and serving. A successful (2xx) response is required; a connection
 * refusal surfaces as an undefined/0 status and counts as unhealthy.
 */
export function isHealthyResponse(statusCode: number | undefined): boolean {
  if (!statusCode) {
    return false;
  }
  return statusCode >= 200 && statusCode < 300;
}

/**
 * Whether the restart command should report success. Success is claimed only when a
 * post-restart health check actually passed — previously the success notification
 * fired unconditionally while the status bar showed "Failed" (issue #6).
 */
export function restartSucceeded(postRestartHealthy: boolean): boolean {
  return postRestartHealthy === true;
}
