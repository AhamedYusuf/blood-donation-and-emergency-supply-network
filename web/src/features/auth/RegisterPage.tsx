import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useDispatch } from "react-redux";
import { useRegisterMutation } from "./authApi";
import { setCredentials } from "./authSlice";
import { AuthCard } from "../../components/AuthCard";
import { FormField } from "../../components/FormField";

// ── Validation helpers ────────────────────────────────────────────────────────

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const PHONE_RE = /^\+?[\d\s\-]{7,15}$/;

function validateFullName(v: string) {
  if (!v.trim()) return "Full name is required.";
  if (v.trim().length < 2) return "Must be at least 2 characters.";
  if (/\d/.test(v)) return "Name must not contain numbers.";
  return "";
}
function validateEmail(v: string) {
  if (!v.trim()) return "Email is required.";
  if (!EMAIL_RE.test(v)) return "Enter a valid email address.";
  return "";
}
function validatePhone(v: string) {
  if (!v.trim()) return "Phone number is required.";
  if (!PHONE_RE.test(v)) return "Enter a valid phone number (7–15 digits).";
  return "";
}
function validatePassword(v: string) {
  if (!v) return "Password is required.";
  if (v.length < 8) return "Password must be at least 8 characters.";
  if (!/(?=.*[a-zA-Z])(?=.*\d)/.test(v))
    return "Must contain at least one letter and one number.";
  return "";
}
function validateConfirm(password: string, confirm: string) {
  if (!confirm) return "Please confirm your password.";
  if (confirm !== password) return "Passwords do not match.";
  return "";
}

type Fields = "fullName" | "email" | "phone" | "password" | "confirm";
type Touched = Record<Fields, boolean>;

// ── Step indicator ─────────────────────────────────────────────────────────────

function StepIndicator({ step }: { step: 1 | 2 }) {
  const steps = [
    { n: 1, label: "Account" },
    { n: 2, label: "Donor Profile" },
  ] as const;

  return (
    <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
      {steps.map((s, i) => (
        <div key={s.n} style={{ display: "flex", alignItems: "center", gap: 8 }}>
          <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
            <div
              style={{
                width: 24,
                height: 24,
                borderRadius: "var(--radius-full)",
                background:
                  s.n < step
                    ? "var(--color-success)"
                    : s.n === step
                    ? "var(--color-primary)"
                    : "var(--color-surface-sunken)",
                color:
                  s.n <= step ? "white" : "var(--color-ink-faint)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: 11,
                fontWeight: 600,
              }}
            >
              {s.n < step ? "✓" : s.n}
            </div>
            <span
              className="text-caption"
              style={{
                color:
                  s.n === step
                    ? "var(--color-ink)"
                    : s.n < step
                    ? "var(--color-success)"
                    : "var(--color-ink-faint)",
                fontWeight: s.n === step ? 600 : 400,
              }}
            >
              {s.label}
            </span>
          </div>
          {i < steps.length - 1 && (
            <div
              style={{
                width: 24,
                height: 1,
                background:
                  step > 1
                    ? "var(--color-success)"
                    : "var(--color-hairline-strong)",
              }}
            />
          )}
        </div>
      ))}
    </div>
  );
}

// ── Component ─────────────────────────────────────────────────────────────────

export function RegisterPage() {
  const [fullName, setFullName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [touched, setTouched] = useState<Touched>({
    fullName: false,
    email: false,
    phone: false,
    password: false,
    confirm: false,
  });
  const [apiError, setApiError] = useState<string | null>(null);

  const [register, { isLoading }] = useRegisterMutation();
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const touch = (field: Fields) =>
    setTouched((t) => ({ ...t, [field]: true }));

  const errors = {
    fullName: touched.fullName ? validateFullName(fullName) : "",
    email: touched.email ? validateEmail(email) : "",
    phone: touched.phone ? validatePhone(phone) : "",
    password: touched.password ? validatePassword(password) : "",
    confirm: touched.confirm ? validateConfirm(password, confirm) : "",
  };

  const isFormValid =
    !validateFullName(fullName) &&
    !validateEmail(email) &&
    !validatePhone(phone) &&
    !validatePassword(password) &&
    !validateConfirm(password, confirm);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setTouched({ fullName: true, email: true, phone: true, password: true, confirm: true });
    setApiError(null);

    if (!isFormValid) return;

    try {
      const result = await register({
        fullName: fullName.trim(),
        email: email.trim(),
        phoneNumber: phone.trim(),
        password,
        role: "donor",
      }).unwrap();

      dispatch(
        setCredentials({
          token: result.accessToken,
          refreshToken: result.refreshToken,
          userId: result.userId,
          email: result.email,
          fullName: result.fullName,
          role: result.role,
        })
      );

      // Proceed to Step 2 — donor profile
      navigate("/register/donor-profile");
    } catch (err: unknown) {
      const e = err as { status?: number; data?: { message?: string } };
      if (e.status === 409 || (e.data?.message ?? "").toLowerCase().includes("exist")) {
        setApiError("An account with this email already exists.");
      } else if (e.data?.message) {
        setApiError(e.data.message);
      } else {
        setApiError("Registration failed. Please try again.");
      }
    }
  };

  return (
    <AuthCard
      title="Create your account"
      subtitle="Step 1 of 2 — Account details"
      stepIndicator={<StepIndicator step={1} />}
    >
      <form
        onSubmit={handleSubmit}
        noValidate
        style={{ display: "flex", flexDirection: "column", gap: "var(--space-md)" }}
      >
        {/* API error banner */}
        {apiError && (
          <div
            role="alert"
            style={{
              background: "var(--color-critical-subtle)",
              border: "1px solid var(--color-critical)",
              borderRadius: "var(--radius-sm)",
              padding: "10px 14px",
              display: "flex",
              alignItems: "center",
              gap: 8,
            }}
          >
            <span style={{ fontSize: 14 }}>⚠</span>
            <span
              className="text-body-sm"
              style={{ color: "var(--color-critical)" }}
            >
              {apiError}
            </span>
          </div>
        )}

        <FormField
          id="reg-fullname"
          label="Full name"
          type="text"
          autoComplete="name"
          value={fullName}
          onChange={(e) => setFullName(e.target.value)}
          onBlur={() => touch("fullName")}
          error={errors.fullName}
          required
          placeholder="Jane Smith"
        />

        <FormField
          id="reg-email"
          label="Email address"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          onBlur={() => touch("email")}
          error={errors.email}
          required
          placeholder="you@example.com"
        />

        <FormField
          id="reg-phone"
          label="Phone number"
          type="tel"
          autoComplete="tel"
          value={phone}
          onChange={(e) => setPhone(e.target.value)}
          onBlur={() => touch("phone")}
          error={errors.phone}
          required
          placeholder="+94 77 123 4567"
        />

        {/* Password row */}
        <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-sm)" }}>
          <FormField
            id="reg-password"
            label="Password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            onBlur={() => touch("password")}
            error={errors.password}
            required
            placeholder="••••••••"
          />
          <FormField
            id="reg-confirm"
            label="Confirm password"
            type="password"
            autoComplete="new-password"
            value={confirm}
            onChange={(e) => setConfirm(e.target.value)}
            onBlur={() => touch("confirm")}
            error={errors.confirm}
            required
            placeholder="••••••••"
          />
        </div>

        {/* Password strength hint */}
        <p
          className="text-caption"
          style={{ margin: 0, color: "var(--color-ink-faint)" }}
        >
          Min. 8 characters, including a letter and a number.
        </p>

        <button
          type="submit"
          disabled={isLoading}
          className="transition-fast"
          style={{
            marginTop: "var(--space-xs)",
            background: isLoading
              ? "var(--color-primary-press)"
              : "var(--color-primary)",
            color: "var(--color-on-primary)",
            border: "none",
            borderRadius: "var(--radius-sm)",
            padding: "9px 16px",
            fontSize: 13,
            fontWeight: 500,
            fontFamily: "var(--font-sans)",
            cursor: isLoading ? "not-allowed" : "pointer",
            width: "100%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            gap: 8,
          }}
          onMouseEnter={(e) => {
            if (!isLoading)
              e.currentTarget.style.background = "var(--color-primary-hover)";
          }}
          onMouseLeave={(e) => {
            if (!isLoading)
              e.currentTarget.style.background = "var(--color-primary)";
          }}
        >
          {isLoading ? (
            <>
              <span
                style={{
                  display: "inline-block",
                  width: 12,
                  height: 12,
                  border: "2px solid rgba(255,255,255,0.4)",
                  borderTopColor: "#fff",
                  borderRadius: "50%",
                  animation: "spin 0.7s linear infinite",
                }}
              />
              Creating account…
            </>
          ) : (
            "Continue →"
          )}
        </button>

        <p
          className="text-body-sm"
          style={{
            margin: 0,
            textAlign: "center",
            color: "var(--color-ink-muted)",
          }}
        >
          Already have an account?{" "}
          <Link
            to="/login"
            style={{
              color: "var(--color-primary)",
              textDecoration: "none",
              fontWeight: 500,
            }}
          >
            Log in
          </Link>
        </p>
      </form>

      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </AuthCard>
  );
}
