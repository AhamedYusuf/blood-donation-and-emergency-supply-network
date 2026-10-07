// On Render's free plan the agent service sleeps after about 15 idle minutes.
// A request from the API (another Render service) does not wake it, but a
// request from outside Render does, and waking takes about 20 to 50 seconds.
// So while staff use the app, the browser pings the agent's /health. By the
// time someone submits a blood request the agent is awake, and the API's own
// wait-and-retry (AgentServiceWakeUpHandler) can start the workflow.

const MIN_INTERVAL_MS = 5 * 60 * 1000;

let lastPingAt: number | null = null;

export function wakeAgentService(
  baseUrl: string | undefined = import.meta.env.VITE_AGENT_BASE_URL,
  now: number = Date.now(),
): boolean {
  if (!baseUrl) {
    return false;
  }

  if (lastPingAt !== null && now - lastPingAt < MIN_INTERVAL_MS) {
    return false;
  }

  lastPingAt = now;

  // no-cors: the agent service sends no CORS headers, and the response is
  // not needed; the request reaching Render is what wakes the service.
  fetch(`${baseUrl.replace(/\/+$/, "")}/health`, {
    mode: "no-cors",
    cache: "no-store",
  }).catch(() => {
    // Waking is best effort; the API still retries on its own.
  });

  return true;
}

export function resetWakeAgentServiceForTests(): void {
  lastPingAt = null;
}
