import type { ReactNode } from "react";
import { useDispatch, useSelector } from "react-redux";
import { useNavigate, useLocation } from "react-router-dom";
import type { RootState } from "../app/store";
import { logout } from "../features/auth/authSlice";
import "./app-shell.css";

// ── Nav item definitions ──────────────────────────────────────────────────────
// `roles`: which roles can navigate to this item. Empty = all authenticated users.
// `adminPath` / `staffPath`: role-specific destination overrides.

interface NavItem {
  label: string;
  icon: string;
  path: string;
  /** If defined, admin users go here instead of `path`. */
  adminPath?: string;
  /** If defined, staff users go here instead of `path`. */
  staffPath?: string;
  /** Roles that can see/use this item. Undefined = all roles. */
  allowedRoles?: string[];
  /** False = no route exists for this yet (App.tsx has nothing registered
   * at `path`) — shown dimmed with an explanatory tooltip instead of a
   * dead link, the same "coming soon" treatment the mobile app uses.
   * Defaults to true. */
  built?: boolean;
}

const OPERATIONS_NAV_ITEMS: NavItem[] = [
  { label: "Appointments", icon: "◧", path: "/" },
  { label: "Requests", icon: "◇", path: "/requests" },
  { label: "Inventory", icon: "▤", path: "/inventory", allowedRoles: ["staff", "admin"] },
  { label: "Organizations", icon: "▦", path: "/organizations", allowedRoles: ["admin"] },
  { label: "Invite staff", icon: "✉", path: "/staff-invitations", allowedRoles: ["admin"] },
  {
    label: "Donors",
    icon: "◎",
    path: "/donors/search",
    adminPath: "/donors/verification-queue",
    staffPath: "/donors/verification-queue",
    allowedRoles: ["staff", "admin"],
  },
];

const DONOR_NAV_ITEMS: NavItem[] = [
  { label: "Home", icon: "⌂", path: "/" },
  { label: "Donations", icon: "◷", path: "/appointments" },
  { label: "Requests", icon: "◇", path: "/requests" },
  { label: "Profile", icon: "◎", path: "/profile" },
];

// ── Component ─────────────────────────────────────────────────────────────────

export function AppShell({ children }: { children: ReactNode }) {
  const email = useSelector((state: RootState) => state.auth.email);
  const fullName = useSelector((state: RootState) => state.auth.fullName);
  const role = useSelector((state: RootState) => (state.auth.role ?? "").toLowerCase());
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const location = useLocation();
  const navItems = role === "donor" ? DONOR_NAV_ITEMS : OPERATIONS_NAV_ITEMS;

  const resolveNavPath = (item: NavItem): string | null => {
    // Not built yet — no route exists to send them to.
    if (item.built === false) return null;
    // If item is role-restricted and user doesn't qualify, return null (inactive)
    if (item.allowedRoles && !item.allowedRoles.includes(role)) return null;

    if (role === "admin" && item.adminPath) return item.adminPath;
    if (role === "staff" && item.staffPath) return item.staffPath;
    return item.path;
  };

  const isActive = (item: NavItem): boolean => {
    const path = resolveNavPath(item);
    if (!path) return false;
    // Appointments is active on exact "/" only
    if (item.path === "/") return location.pathname === "/";
    return location.pathname.startsWith(item.path.split("/")[1] ? `/${item.path.split("/")[1]}` : item.path);
  };

  return (
    <div className="app-shell">
      <nav className="app-sidebar" aria-label="Main navigation">
        <div className="app-brand">
          <div className="app-brand__mark" aria-hidden="true">
            <span className="app-brand__plus" />
          </div>
          <div>
            <strong>PulsePoint</strong>
            <span>Blood Donation Network</span>
          </div>
        </div>

        <div className="app-sidebar__section-label">Workspace</div>
        <div className="app-sidebar__nav">
          {navItems.map((item) => {
            const targetPath = resolveNavPath(item);
            const active = isActive(item);
            const clickable = targetPath !== null;

            return (
              <button
                key={item.label}
                type="button"
                className={`app-nav-item${active ? " app-nav-item--active" : ""}${!clickable ? " app-nav-item--disabled" : ""}`}
                title={
                  clickable
                    ? item.label
                    : item.built === false
                    ? `${item.label} — coming soon`
                    : `${item.label} — not available for your role`
                }
                onClick={() => clickable && navigate(targetPath!)}
                disabled={!clickable}
              >
                <span className="app-nav-item__icon" aria-hidden="true">{item.icon}</span>
                <span>{item.label}</span>
              </button>
            );
          })}
        </div>

        <div className="app-sidebar__footer">
          <span className="app-sidebar__role">{role || "USER"}</span>
          <span className="app-sidebar__footer-copy">Secure donor network</span>
        </div>
      </nav>

      {/* ── Content area ── */}
      <div className="app-shell__content">
        {/* Light header */}
        <header className="app-header">
          <div className="app-header__title">
            {/* TODO: organization name needs a lookup — the JWT/auth response
                only carries organizationId, not the name. */}
            <span className="text-subheading" style={{ color: "var(--color-ink)" }}>
              PulsePoint
            </span>
            <span className="app-header__role">{role || "USER"}</span>
          </div>

          <div className="app-header__account">
            {fullName && <span className="app-header__name">{fullName}</span>}
            <span className="app-header__email">{email}</span>
            <button onClick={() => dispatch(logout())} className="app-header__logout">Log out</button>
          </div>
        </header>

        <div style={{ flex: 1, minHeight: 0 }}>{children}</div>
      </div>
    </div>
  );
}