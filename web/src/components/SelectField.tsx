import type { SelectHTMLAttributes } from "react";

interface SelectFieldProps extends SelectHTMLAttributes<HTMLSelectElement> {
  id: string;
  label: string;
  error?: string;
  options: { value: string; label: string }[];
  placeholder?: string;
}

/**
 * Labeled <select> matching FormField's design-token styling.
 */
export function SelectField({
  id,
  label,
  error,
  options,
  placeholder,
  ...selectProps
}: SelectFieldProps) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 6 }}>
      <label
        htmlFor={id}
        className="text-label"
        style={{ color: "var(--color-ink-secondary)" }}
      >
        {label}
        {selectProps.required && (
          <span style={{ color: "var(--color-critical)", marginLeft: 2 }}>
            *
          </span>
        )}
      </label>
      <select
        id={id}
        {...selectProps}
        style={{
          fontFamily: "var(--font-sans)",
          fontSize: 14,
          fontWeight: 400,
          lineHeight: 1.5,
          color: selectProps.value ? "var(--color-ink)" : "var(--color-ink-faint)",
          background: "var(--color-surface)",
          border: error
            ? "1px solid var(--color-critical)"
            : "1px solid var(--color-hairline-strong)",
          borderRadius: "var(--radius-sm)",
          padding: "8px 12px",
          outline: "none",
          width: "100%",
          boxSizing: "border-box",
          cursor: "pointer",
          appearance: "none",
          backgroundImage: `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='12' height='12' viewBox='0 0 12 12'%3E%3Cpath fill='%236B7684' d='M6 8L1 3h10z'/%3E%3C/svg%3E")`,
          backgroundRepeat: "no-repeat",
          backgroundPosition: "right 12px center",
          paddingRight: 32,
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
          selectProps.onFocus?.(e);
        }}
        onBlur={(e) => {
          e.currentTarget.style.boxShadow = "none";
          e.currentTarget.style.border = error
            ? "1px solid var(--color-critical)"
            : "1px solid var(--color-hairline-strong)";
          selectProps.onBlur?.(e);
        }}
      >
        {placeholder && (
          <option value="" disabled>
            {placeholder}
          </option>
        )}
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
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
