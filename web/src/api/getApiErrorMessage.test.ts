import { describe, it, expect } from "vitest";
import { getApiErrorMessage } from "./getApiErrorMessage";

describe("getApiErrorMessage", () => {
  it("prefers the backend's own message field", () => {
    const error = { data: { message: "A donor profile already exists for this user." } };
    expect(getApiErrorMessage(error, "fallback")).toBe(
      "A donor profile already exists for this user."
    );
  });

  it("falls back to ProblemDetails' title when there's no message", () => {
    const error = { data: { title: "Bad Request" } };
    expect(getApiErrorMessage(error, "fallback")).toBe("Bad Request");
  });

  it("prefers message over title when both are present", () => {
    const error = { data: { message: "Specific reason", title: "Bad Request" } };
    expect(getApiErrorMessage(error, "fallback")).toBe("Specific reason");
  });

  it("uses the fallback when data has neither message nor title", () => {
    const error = { data: { errors: {} } };
    expect(getApiErrorMessage(error, "fallback")).toBe("fallback");
  });

  it("uses the fallback for a blank message", () => {
    const error = { data: { message: "   " } };
    expect(getApiErrorMessage(error, "fallback")).toBe("fallback");
  });

  it("uses the fallback for a network error with no data field at all", () => {
    expect(getApiErrorMessage(new Error("Network error"), "fallback")).toBe("fallback");
  });

  it("uses the fallback for null/undefined", () => {
    expect(getApiErrorMessage(null, "fallback")).toBe("fallback");
    expect(getApiErrorMessage(undefined, "fallback")).toBe("fallback");
  });
});
