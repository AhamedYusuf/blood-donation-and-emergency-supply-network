import type { ReactNode } from "react";
import { useDispatch, useSelector } from "react-redux";
import type { RootState } from "../app/store";
import { logout } from "../features/auth/authSlice";

const NAV_ITEMS = [
  { label: "Appointments", icon: "◧", active: true },
  { label: "Requests", icon: "◇", active: false },
  { label: "Inventory", icon: "▤", active: false },
  { label: "Donors", icon: "◎", active: false },
];

export function AppShell({ children }: { children: ReactNode }) {
  const email = useSelector((state: RootState) => state.auth.email);
  const dispatch = useDispatch();

  return (
    <div style={{ display: "flex", height: "100vh", fontFamily: "var(--font-sans)" }}>
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
            fontWeight: 600,
            fontSize: 14,
            marginBottom: "var(--space-md)",
          }}
        >
          B
        </div>
        {NAV_ITEMS.map((item) => (
          <div
            key={item.label}
            title={item.active ? item.label : `${item.label} — coming soon`}
            style={{
              width: 36,
              height: 36,
              borderRadius: "var(--radius-sm)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontSize: 16,
              cursor: item.active ? "pointer" : "default",
              background: item.active ? "rgba(255,255,255,0.12)" : "transparent",
              color: item.active ? "#FFFFFF" : "rgba(255,255,255,0.32)",
            }}
          >
            {item.icon}
          </div>
        ))}
      </nav>

      <div style={{ flex: 1, display: "flex", flexDirection: "column", minWidth: 0 }}>
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
            <span className="text-subheading" style={{ color: "var(--color-ink)" }}>
              Test Blood Bank
            </span>
            <span
              className="text-caption"
              style={{
                padding: "2px 6px",
                borderRadius: "var(--radius-xs)",
                background: "var(--color-surface-sunken)",
                color: "var(--color-ink-muted)",
              }}
            >
              STAFF
            </span>
          </div>
          <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
            <span className="text-body-sm" style={{ color: "var(--color-ink-muted)" }}>
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