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

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        {/* PUBLIC AUTH */}

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

        {/* DONOR PROFILE */}

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

        {/* MAIN CONSOLE */}

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

        {/* DONOR MODULE */}

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

        {/* ORGANIZATIONS */}

        <Route
          path="/organizations"
          element={
            <RequireAuth>
              <OrganizationsPage />
            </RequireAuth>
          }
        />

        {/* INVENTORY */}

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

        {/* BLOOD REQUEST MODULE
            For now: any authenticated user can access.
            We will add exact role restrictions later.
        */}

        <Route
          path="/requests"
          element={
            <RequireAuth>
              <RequestsPage />
            </RequireAuth>
          }
        />

        <Route
          path="/requests/create"
          element={
            <RequireAuth>
              <CreateRequestPage />
            </RequireAuth>
          }
        />

        <Route
          path="/requests/:id"
          element={
            <RequireAuth>
              <RequestDetailsPage />
            </RequireAuth>
          }
        />

        {/* CATCH ALL */}

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