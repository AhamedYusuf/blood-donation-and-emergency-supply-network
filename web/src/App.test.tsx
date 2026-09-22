import { describe, it, expect } from "vitest";
import { screen } from "@testing-library/react";
import { renderWithAuth } from "./test/renderWithAuth";
import { RequireAuth, RequireRole, RequireDonorProfile } from "./App";

// These guards are the frontend's whole authorization boundary — every
// protected route in App.tsx is wrapped in some combination of them. They
// were never covered by an automated test before, only exercised manually
// (see the CDP-driven live verifications in the project's session history).
// This mirrors the backend's own controller-authorization test coverage.

const Protected = () => <div>protected content</div>;

describe("RequireAuth", () => {
  it("renders children when a token is present", () => {
    renderWithAuth(
      <RequireAuth>
        <Protected />
      </RequireAuth>,
      { token: "a-real-token" }
    );

    expect(screen.getByText("protected content")).toBeInTheDocument();
  });

  it("redirects away (does not render children) with no token", () => {
    renderWithAuth(
      <RequireAuth>
        <Protected />
      </RequireAuth>,
      { token: null }
    );

    expect(screen.queryByText("protected content")).not.toBeInTheDocument();
  });
});

describe("RequireRole", () => {
  it("allows a role that's in the allowed list", () => {
    renderWithAuth(
      <RequireRole roles={["staff", "admin"]}>
        <Protected />
      </RequireRole>,
      { role: "staff" }
    );

    expect(screen.getByText("protected content")).toBeInTheDocument();
  });

  it("is case-insensitive — a capitalized role from the login API still matches", () => {
    // Regression: the real login endpoint returns "Admin"/"Staff" capitalized,
    // not lowercase. A naive exact-match check here would incorrectly lock
    // out every legitimately-authorized user (this exact bug was caught and
    // fixed once already in AppointmentsConsolePage's own isAdmin check).
    renderWithAuth(
      <RequireRole roles={["admin"]}>
        <Protected />
      </RequireRole>,
      { role: "Admin" }
    );

    expect(screen.getByText("protected content")).toBeInTheDocument();
  });

  it("blocks a role that's not in the allowed list", () => {
    renderWithAuth(
      <RequireRole roles={["admin"]}>
        <Protected />
      </RequireRole>,
      { role: "donor" }
    );

    expect(screen.queryByText("protected content")).not.toBeInTheDocument();
  });

  it("blocks when there's no role at all", () => {
    renderWithAuth(
      <RequireRole roles={["admin"]}>
        <Protected />
      </RequireRole>,
      { role: null }
    );

    expect(screen.queryByText("protected content")).not.toBeInTheDocument();
  });
});

describe("RequireDonorProfile", () => {
  it("lets a donor with a donor profile through", () => {
    renderWithAuth(
      <RequireDonorProfile>
        <Protected />
      </RequireDonorProfile>,
      { role: "donor", donorId: "donor-profile-id" }
    );

    expect(screen.getByText("protected content")).toBeInTheDocument();
  });

  it("redirects a donor with no donor profile yet", () => {
    renderWithAuth(
      <RequireDonorProfile>
        <Protected />
      </RequireDonorProfile>,
      { role: "donor", donorId: null }
    );

    expect(screen.queryByText("protected content")).not.toBeInTheDocument();
  });

  it("lets a non-donor role through regardless of donorId", () => {
    renderWithAuth(
      <RequireDonorProfile>
        <Protected />
      </RequireDonorProfile>,
      { role: "staff", donorId: null }
    );

    expect(screen.getByText("protected content")).toBeInTheDocument();
  });
});
