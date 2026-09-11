import type { InputHTMLAttributes } from "react";

interface FormFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  id: string;
  label: string;
  error?: string;
}

/**
 * Labeled text input matching the BloodLine-Console design tokens.
 * Supports all native <input> props via spread.
 */
export function FormField({ id, label, error, ...inputProps }: FormFieldProps) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <label
        htmlFor={id}
        className="text-label"
        style={{ color: "var(--color-ink-secondary)" }}
      >
        {label}
        {inputProps.required && (
          <span style={{ color: "var(--color-critical)", marginLeft: 2 }}>
            *
          </span>
        )}
      </label>
      <input
        id={id}
        {...inputProps}
        style={{
          fontFamily: "var(--font-sans)",
          fontSize: 14,
          fontWeight: 400,
          lineHeight: 1.5,
          color: "var(--color-ink)",
          background: "var(--color-surface)",
          border: error
            ? "1px solid var(--color-critical)"
            : "1px solid var(--color-hairline-strong)",
          borderRadius: "var(--radius-sm)",
          padding: "8px 12px",
          outline: "none",
          width: "100%",
          boxSizing: "border-box",
          transition: `border-color var(--duration-fast) var(--ease-standard),
                       box-shadow var(--duration-fast) var(--ease-standard)`,
        }}
        onFocus={(e) => {
          e.currentTarget.style.border = error
            ? "1px solid var(--color-critical)"
            : "1px solid var(--color-primary)";
          e.currentTarget.style.boxShadow = error
            ? "0 0 0 3px var(--color-critical-subtle)"
            : "0 0 0 3px var(--color-primary-subtle)";
          inputProps.onFocus?.(e);
        }}
        onBlur={(e) => {
          e.currentTarget.style.boxShadow = "none";
          e.currentTarget.style.border = error
            ? "1px solid var(--color-critical)"
            : "1px solid var(--color-hairline-strong)";
          inputProps.onBlur?.(e);
        }}
      />
      {error && (
        <span
          className="text-caption"
          style={{ color: "var(--color-critical)" }}
        >
          {error}
        </span>
      )}
    </div>
  );
}
