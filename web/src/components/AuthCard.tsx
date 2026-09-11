import type { ReactNode } from "react";

interface AuthCardProps {
  children: ReactNode;
  /** Shown above the card — e.g. "Log in to your account" */
  title: string;
  /** Optional subtitle below the title */
  subtitle?: string;
  /** Optional step indicator markup */
  stepIndicator?: ReactNode;
}

/**
 * Full-page centered layout for auth / onboarding pages.
 * No nav rail — intentionally outside AppShell.
 *
 * Structure:
 *   canvas bg
 *   └─ centred column
 *      ├─ "B" logo mark
 *      ├─ title + subtitle
 *      ├─ optional step indicator
 *      └─ white surface card (the form)
 */
export function AuthCard({
  children,
  title,
  subtitle,
  stepIndicator,
}: AuthCardProps) {
  return (
    <div
      className="fade-enter"
      style={{
        minHeight: "100vh",
        background: "var(--color-canvas)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "var(--space-lg)",
        fontFamily: "var(--font-sans)",
      }}
    >
      <div
        style={{
          width: "100%",
          maxWidth: 420,
          display: "flex",
          flexDirection: "column",
          gap: "var(--space-lg)",
        }}
      >
        {/* Logo mark */}
        <div
          style={{
            display: "flex",
            flexDirection: "column",
            alignItems: "center",
            gap: "var(--space-md)",
          }}
        >
          <div
            style={{
              width: 40,
              height: 40,
              borderRadius: "var(--radius-md)",
              background: "var(--color-primary)",
              color: "var(--color-on-primary)",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              fontWeight: 700,
              fontSize: 20,
              letterSpacing: "-0.5px",
            }}
          >
            B
          </div>

          {/* Title + subtitle */}
          <div style={{ textAlign: "center" }}>
            <h1
              className="text-heading"
              style={{
                margin: 0,
                color: "var(--color-ink)",
              }}
            >
              {title}
            </h1>
            {subtitle && (
              <p
                className="text-body-sm"
                style={{
                  margin: "4px 0 0",
                  color: "var(--color-ink-muted)",
                }}
              >
                {subtitle}
              </p>
            )}
          </div>

          {/* Step indicator (optional) */}
          {stepIndicator}
        </div>

        {/* Surface card */}
        <div
          style={{
            background: "var(--color-surface)",
            border: "1px solid var(--color-hairline)",
            borderRadius: "var(--radius-lg)",
            padding: "var(--space-lg)",
          }}
        >
          {children}
        </div>
      </div>
    </div>
  );
}
