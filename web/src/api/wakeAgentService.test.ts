import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { resetWakeAgentServiceForTests, wakeAgentService } from "./wakeAgentService";

describe("wakeAgentService", () => {
  const fetchMock = vi.fn(() => Promise.resolve(new Response()));

  beforeEach(() => {
    resetWakeAgentServiceForTests();
    fetchMock.mockClear();
    vi.stubGlobal("fetch", fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("pings the agent's health endpoint without needing CORS", () => {
    expect(wakeAgentService("https://agent.example.com/", 0)).toBe(true);
    expect(fetchMock).toHaveBeenCalledWith("https://agent.example.com/health", {
      mode: "no-cors",
      cache: "no-store",
    });
  });

  it("does nothing when no agent address is configured", () => {
    expect(wakeAgentService("", 0)).toBe(false);
    expect(fetchMock).not.toHaveBeenCalled();
  });

  it("pings at most once every five minutes", () => {
    wakeAgentService("https://agent.example.com", 0);
    expect(wakeAgentService("https://agent.example.com", 4 * 60 * 1000)).toBe(false);
    expect(wakeAgentService("https://agent.example.com", 5 * 60 * 1000)).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(2);
  });
});
