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
              <AppShell>
                <AppointmentsConsolePage />
              </AppShell>
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

        {/* ── Inventory / Organization module ── */}
        <Route
          path="/organizations"
          element={
            <RequireAuth>
              <OrganizationsPage />
            </RequireAuth>
          }
        />

        <Route
          path="/inventory"
          element={
            <RequireAuth>
              <InventoryPage />
            </RequireAuth>
          }
        />

        <Route
          path="/inventory/manage"
          element={
            <RequireAuth>
              <InventoryManagePage />
            </RequireAuth>
          }
        />

        <Route
          path="/inventory/emergency"
          element={
            <RequireAuth>
              <InventoryEmergencyPage />
            </RequireAuth>
          }
        />

        {/* ── Catch-all ── */}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  );
}