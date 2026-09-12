import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { useSelector } from "react-redux";
import type { RootState } from "./app/store";

import { LoginPage } from "./features/auth/LoginPage";
import { RegisterPage } from "./features/auth/RegisterPage";

import { DonorProfilePage } from "./features/donors/DonorProfilePage";
import { DonorVerificationQueuePage } from "./features/donors/DonorVerificationQueuePage";
import { DonorSearchPage } from "./features/donors/DonorSearchPage";

import { AppointmentsConsolePage } from "./features/appointments/AppointmentsConsolePage";
import { AppShell } from "./components/AppShell";

import { OrganizationsPage } from "./features/organizations/OrganizationsPage";
import { InventoryPage } from "./features/inventory/InventoryPage";
import { InventoryManagePage } from "./features/inventory/InventoryManagePage";
import { InventoryEmergencyPage } from "./features/inventory/InventoryEmergencyPage";

// ── Auth guards ───────────────────────────────────────────────────────────────

function RequireAuth({ children }: { children: React.ReactNode }) {
  const token = useSelector((state: RootState) => state.auth.token);

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}

function RedirectIfAuthed({ children }: { children: React.ReactNode }) {
  const token = useSelector((state: RootState) => state.auth.token);

  if (token) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}

function RequireRole({
  children,
  roles,
}: {
  children: React.ReactNode;
  roles: string[];
}) {
  const role = useSelector((state: RootState) => state.auth.role ?? "");

  if (!roles.map((r) => r.toLowerCase()).includes(role.toLowerCase())) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}

/**
 * Resume flow for a donor whose account exists but who never finished
 * step 2 of registration. LoginPage already sends them to
 * /register/donor-profile the moment they log in — but a donor who
 * abandoned mid-flow can also come back with a still-valid token already
 * in localStorage (no fresh login, so that redirect never runs) and land
 * straight on a protected route. This is the same check, applied on every
 * page load instead of only at login.
 */
function RequireDonorProfile({ children }: { children: React.ReactNode }) {
  const role = useSelector((state: RootState) => (state.auth.role ?? "").toLowerCase());
  const donorId = useSelector((state: RootState) => state.auth.donorId);
  if (role === "donor" && !donorId) {
    return <Navigate to="/register/donor-profile" replace />;
  }
  return <>{children}</>;
}

// ── App ───────────────────────────────────────────────────────────────────────

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* ── Public auth pages ── */}
        <Route
          path="/login"
          element={
            <RedirectIfAuthed>
              <LoginPage />
            </RedirectIfAuthed>
          }
        />

        <Route
          path="/register"
          element={
            <RedirectIfAuthed>
              <RegisterPage />
            </RedirectIfAuthed>
          }
        />

        {/* Step 2 of registration */}
        <Route
          path="/register/donor-profile"
          element={
            <RequireAuth>
              <RequireRole roles={["donor"]}>
                <DonorProfilePage />
              </RequireRole>
            </RequireAuth>
          }
        />

        {/* ── Main protected console ── */}
        <Route
          path="/"
          element={
            <RequireAuth>
              <RequireDonorProfile>
                <AppShell>
                  <AppointmentsConsolePage />
                </AppShell>
              </RequireDonorProfile>
            </RequireAuth>
          }
        />

        {/* Staff + Admin: donor search */}
        <Route
          path="/donors/search"
          element={
            <RequireAuth>
              <RequireRole roles={["staff", "admin"]}>
                <AppShell>
                  <DonorSearchPage />
                </AppShell>
              </RequireRole>
            </RequireAuth>
          }
        />

        {/* Staff + Admin: verification queue */}
        <Route
          path="/donors/verification-queue"
          element={
            <RequireAuth>
              <RequireRole roles={["staff", "admin"]}>
                <AppShell>
                  <DonorVerificationQueuePage />
                </AppShell>
              </RequireRole>
            </RequireAuth>
          }
        />

        {/* ── Inventory / Organization module — staff+admin only, same as
            donor search/verification below. The backend already enforces
            this on every mutating endpoint; this stops a donor from
            landing on a staff-facing management screen client-side at
            all. ── */}
        <Route
          path="/organizations"
          element={
            <RequireAuth>
              <RequireRole roles={["staff", "admin"]}>
                <OrganizationsPage />
              </RequireRole>
            </RequireAuth>
          }
        />

        <Route
          path="/inventory"
          element={
            <RequireAuth>
              <RequireRole roles={["staff", "admin"]}>
                <InventoryPage />
              </RequireRole>
            </RequireAuth>
          }
        />

        <Route
          path="/inventory/manage"
          element={
            <RequireAuth>
              <RequireRole roles={["staff", "admin"]}>
                <InventoryManagePage />
              </RequireRole>
            </RequireAuth>
          }
        />

        <Route
          path="/inventory/emergency"
          element={
            <RequireAuth>
              <RequireRole roles={["staff", "admin"]}>
                <InventoryEmergencyPage />
              </RequireRole>
            </RequireAuth>
          }
        />

        {/* ── Catch-all ── */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}