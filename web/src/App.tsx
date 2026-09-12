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
              <RequireRole roles={["donor"]}>
                <DonorProfilePage />
              </RequireRole>
            </RequireAuth>
          }
        />

        {/* ── Protected console pages (inside AppShell) ── */}
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

        {/* Catch-all */}
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </BrowserRouter>
  );
}