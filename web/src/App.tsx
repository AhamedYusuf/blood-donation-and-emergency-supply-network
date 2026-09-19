import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
} from "react-router-dom";

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

import RequestsPage from "./features/requests/RequestsPage";
import CreateRequestPage from "./features/requests/CreateRequestPage";
import RequestDetailsPage from "./features/requests/RequestDetailsPage";

// =====================================================
// AUTH GUARDS
// =====================================================

function RequireAuth({
  children,
}: {
  children: React.ReactNode;
}) {
  const token = useSelector(
    (state: RootState) => state.auth.token
  );

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}

function RedirectIfAuthed({
  children,
}: {
  children: React.ReactNode;
}) {
  const token = useSelector(
    (state: RootState) => state.auth.token
  );

  if (token) {
    return <Navigate to="/requests" replace />;
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
  const role = useSelector(
    (state: RootState) => state.auth.role ?? ""
  );

  const allowedRoles = roles.map((r) =>
    r.toLowerCase()
  );

  if (!allowedRoles.includes(role.toLowerCase())) {
    return <Navigate to="/" replace />;
  }

  return <>{children}</>;
}

/**
 * Donors must complete their donor profile before
 * accessing the normal protected application pages.
 *
 * This also handles donors who close the browser during
 * registration and later return with a valid token still
 * stored in localStorage.
 */
function RequireDonorProfile({
  children,
}: {
  children: React.ReactNode;
}) {
  const role = useSelector(
    (state: RootState) =>
      (state.auth.role ?? "").toLowerCase()
  );

  const donorId = useSelector(
    (state: RootState) => state.auth.donorId
  );

  if (role === "donor" && !donorId) {
    return (
      <Navigate
        to="/register/donor-profile"
        replace
      />
    );
  }

  return <>{children}</>;
}

// =====================================================
// APP
// =====================================================

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* =================================================
            PUBLIC AUTH
           ================================================= */}

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

        {/* =================================================
            DONOR PROFILE REGISTRATION
           ================================================= */}

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

        {/* =================================================
            MAIN CONSOLE
           ================================================= */}

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

        {/* =================================================
            DONOR MODULE
           ================================================= */}

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

        {/* =================================================
            ORGANIZATION MODULE
            Staff + Admin only
           ================================================= */}

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

        {/* =================================================
            INVENTORY MODULE
            Staff + Admin only
           ================================================= */}

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

        {/* =================================================
            BLOOD REQUEST MODULE

            For now, authenticated users can access these
            routes while we finish and test the request
            workflow. Exact role restrictions can be added
            later when the workflow roles are finalized.
           ================================================= */}

        <Route
          path="/requests"
          element={
            <RequireAuth>
              <RequireDonorProfile>
                <RequestsPage />
              </RequireDonorProfile>
            </RequireAuth>
          }
        />

        <Route
          path="/requests/create"
          element={
            <RequireAuth>
              <RequireDonorProfile>
                <CreateRequestPage />
              </RequireDonorProfile>
            </RequireAuth>
          }
        />

        <Route
          path="/requests/:id"
          element={
            <RequireAuth>
              <RequireDonorProfile>
                <RequestDetailsPage />
              </RequireDonorProfile>
            </RequireAuth>
          }
        />

        {/* =================================================
            CATCH ALL
           ================================================= */}

        <Route
          path="*"
          element={
            <Navigate to="/requests" replace />
          }
        />
      </Routes>
    </BrowserRouter>
  );
}