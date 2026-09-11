import type { ReactNode } from "react";
import { useDispatch, useSelector } from "react-redux";
import { useNavigate, useLocation } from "react-router-dom";
import type { RootState } from "../app/store";
import { logout } from "../features/auth/authSlice";

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
}

const NAV_ITEMS: NavItem[] = [
  { label: "Appointments", icon: "◧", path: "/" },
  { label: "Requests", icon: "◇", path: "/requests" },
  { label: "Inventory", icon: "▤", path: "/inventory" },
  {
    label: "Donors",
    icon: "◎",
    path: "/donors/search",
    adminPath: "/donors/verification-queue",
    staffPath: "/donors/verification-queue",
    allowedRoles: ["staff", "admin"],
  },
];

// ── Component ─────────────────────────────────────────────────────────────────

export function AppShell({ children }: { children: ReactNode }) {
  const email = useSelector((state: RootState) => state.auth.email);
  const fullName = useSelector((state: RootState) => state.auth.fullName);
  const role = useSelector((state: RootState) => (state.auth.role ?? "").toLowerCase());
  const dispatch = useDispatch();
  const navigate = useNavigate();
  const location = useLocation();

  const resolveNavPath = (item: NavItem): string | null => {
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
    <div style={{ display: "flex", height: "100vh", fontFamily: "var(--font-sans)" }}>
      {/* ── Dark nav rail ── */}
      <nav
        style={{
          width: 56,
          background: "var(--color-ink)",
          display: "flex",
          flexDirection: "column",
          alignItems: "center",
          paddingTop: "var(--space-md)",
          gap: 4,
          flexShrink: 0,
        }}
      >
        {/* Logo mark */}
        <div
          style={{
            width: 32,
            height: 32,
            borderRadius: "var(--radius-sm)",
            background: "var(--color-primary)",
            color: "var(--color-on-primary)",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            fontWeight: 700,
            fontSize: 14,
            marginBottom: "var(--space-md)",
          }}
        >
          B
        </div>

        {NAV_ITEMS.map((item) => {
          const targetPath = resolveNavPath(item);
          const active = isActive(item);
          const clickable = targetPath !== null;

          return (
            <div
              key={item.label}
              title={
                clickable
                  ? item.label
                  : `${item.label} — not available for your role`
              }
              onClick={() => clickable && navigate(targetPath!)}
              style={{
                width: 36,
                height: 36,
                borderRadius: "var(--radius-sm)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: 16,
                cursor: clickable ? "pointer" : "default",
                background: active
                  ? "rgba(255,255,255,0.12)"
                  : "transparent",
                color: active
                  ? "#FFFFFF"
                  : clickable
                  ? "rgba(255,255,255,0.55)"
                  : "rgba(255,255,255,0.20)",
                transition: `background var(--duration-fast) var(--ease-standard)`,
              }}
              onMouseEnter={(e) => {
                if (clickable && !active)
                  e.currentTarget.style.background = "rgba(255,255,255,0.08)";
              }}
              onMouseLeave={(e) => {
                if (!active) e.currentTarget.style.background = "transparent";
              }}
            >
              {item.icon}
            </div>
          );
        })}
      </nav>

      {/* ── Content area ── */}
      <div style={{ flex: 1, display: "flex", flexDirection: "column", minWidth: 0 }}>
        {/* Light header */}
        <header
          style={{
            height: 52,
            borderBottom: "1px solid var(--color-hairline)",
            background: "var(--color-surface)",
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "0 var(--space-lg)",
            flexShrink: 0,
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            {/* TODO: organization name needs a lookup — the JWT/auth response
                only carries organizationId, not the name. */}
            <span className="text-subheading" style={{ color: "var(--color-ink)" }}>
              Blood Donation Network
            </span>
            <span
              className="text-caption"
              style={{
                padding: "2px 6px",
                borderRadius: "var(--radius-xs)",
                background: "var(--color-surface-sunken)",
                color: "var(--color-ink-muted)",
                textTransform: "uppercase",
                letterSpacing: "0.3px",
              }}
            >
              {role || "USER"}
            </span>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            {fullName && (
              <span
                className="text-body-sm"
                style={{ color: "var(--color-ink-secondary)", fontWeight: 500 }}
              >
                {fullName}
              </span>
            )}
            <span
              className="text-body-sm"
              style={{ color: "var(--color-ink-faint)" }}
            >
              {email}
            </span>
            <button
              onClick={() => dispatch(logout())}
              className="transition-fast"
              style={{
                border: "1px solid var(--color-hairline-strong)",
                background: "var(--color-surface)",
                borderRadius: "var(--radius-sm)",
                padding: "5px 10px",
                fontSize: 12,
                fontFamily: "var(--font-sans)",
                color: "var(--color-ink-secondary)",
                cursor: "pointer",
              }}
              onMouseEnter={(e) =>
                (e.currentTarget.style.background = "var(--color-surface-sunken)")
              }
              onMouseLeave={(e) =>
                (e.currentTarget.style.background = "var(--color-surface)")
              }
            >
              Log out
            </button>
          </div>
        </header>

        <div style={{ flex: 1, minHeight: 0 }}>{children}</div>
      </div>
    </div>
  );
}