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

// ── Auth guards ───────────────────────────────────────────────────────────────

/** Redirects to /login when no JWT is present. */
function RequireAuth({ children }: { children: React.ReactNode }) {
  const token = useSelector((state: RootState) => state.auth.token);
  if (!token) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

/** Redirects already-authenticated users away from auth pages to the console. */
function RedirectIfAuthed({ children }: { children: React.ReactNode }) {
  const token = useSelector((state: RootState) => state.auth.token);
  if (token) return <Navigate to="/" replace />;
  return <>{children}</>;
}

/**
 * Role-based guard. Redirects to "/" if the authenticated user's role
 * doesn't match the allowed set (case-insensitive).
 */
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
        {/* ── Public auth pages (no AppShell) ── */}
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

        {/* Step 2 of registration — requires JWT from Step 1, but no AppShell */}
        <Route
          path="/register/donor-profile"
          element={
            <RequireAuth>
              <DonorProfilePage />
            </RequireAuth>
          }
        />

        {/* ── Protected console pages (inside AppShell) ── */}
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

        {/* Admin only: verification queue */}
        <Route
          path="/donors/verification-queue"
          element={
            <RequireAuth>
              <RequireRole roles={["admin"]}>
                <AppShell>
                  <DonorVerificationQueuePage />
                </AppShell>
              </RequireRole>
            </RequireAuth>
          }
        />

        {/* Catch-all */}
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}