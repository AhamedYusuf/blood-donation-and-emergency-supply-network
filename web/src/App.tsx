import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { useSelector } from "react-redux";
import type { RootState } from "./app/store";

import { LoginPage } from "./features/auth/LoginPage";
import { OrganizationsPage } from "./features/organizations/OrganizationsPage";
import { InventoryPage } from "./features/inventory/InventoryPage";
import { InventoryManagePage } from "./features/inventory/InventoryManagePage";
import { InventoryEmergencyPage } from "./features/inventory/InventoryEmergencyPage";

function HomePage() {
  const email = useSelector((state: RootState) => state.auth.email);

  return (
    <div>
      <h1>Logged in as {email}</h1>
    </div>
  );
}

function RequireAuth({
  children,
}: {
  children: React.ReactNode;
}) {
  const token = useSelector(
    (state: RootState) => state.auth.token,
  );

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  return <>{children}</>;
}

export function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route
          path="/login"
          element={<LoginPage />}
        />

        <Route
          path="/"
          element={
            <RequireAuth>
              <HomePage />
            </RequireAuth>
          }
        />

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

        <Route
          path="*"
          element={<Navigate to="/" replace />}
        />
      </Routes>
    </BrowserRouter>
  );
}