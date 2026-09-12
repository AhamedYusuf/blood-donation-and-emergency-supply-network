import { useState, type FormEvent } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useDispatch } from "react-redux";
import { useLoginMutation } from "./authApi";
import { setCredentials } from "./authSlice";
import { AuthCard } from "../../components/AuthCard";
import { FormField } from "../../components/FormField";

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function validateEmail(v: string) {
  if (!v.trim()) return "Email is required.";
  if (!EMAIL_RE.test(v)) return "Enter a valid email address.";
  return "";
}

function validatePassword(v: string) {
  if (!v) return "Password is required.";
  if (v.length < 8) return "Password must be at least 8 characters.";
  return "";
}

export function LoginPage() {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const [touched, setTouched] = useState({
    email: false,
    password: false,
  });

  const [apiError, setApiError] = useState<string | null>(null);

  const [login, { isLoading }] = useLoginMutation();

  const dispatch = useDispatch();
  const navigate = useNavigate();

  const errors = {
    email: touched.email ? validateEmail(email) : "",
    password: touched.password ? validatePassword(password) : "",
  };

  const isFormValid =
    !validateEmail(email) &&
    !validatePassword(password);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();

    setTouched({
      email: true,
      password: true,
    });

    setApiError(null);

    if (!isFormValid) return;

    try {
      const result = await login({
        email: email.trim(),
        password,
      }).unwrap();

      dispatch(
        setCredentials({
          token: result.accessToken,
          refreshToken: result.refreshToken,
          userId: result.userId,
          donorProfileId: result.donorProfileId,
          email: result.email,
          fullName: result.fullName,
          role: result.role,
          organizationId: result.organizationId,
        })
      );

      const role = result.role?.toLowerCase();

      // Donor profile must be completed first
      if (role === "donor" && !result.donorProfileId) {
        navigate("/register/donor-profile");
        return;
      }

      // For now, all authenticated users can test the request module
      navigate("/requests");
    } catch (err: unknown) {
      const apiErr = err as {
        status?: number;
        data?: {
          message?: string;
        };
      };

      if (apiErr.status === 401 || apiErr.status === 400) {
        setApiError(
          "Invalid email or password. Please try again."
        );
      } else {
        setApiError(
          "Something went wrong. Please try again later."
        );
      }
    }
  };

  return (
    <AuthCard
      title="Log in to your account"
      subtitle="Blood Donation &amp; Emergency Supply Network"
    >
      <form
        onSubmit={handleSubmit}
        noValidate
        style={{
          display: "flex",
          flexDirection: "column",
          gap: "var(--space-md)",
        }}
      >
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
              style={{
                color: "var(--color-critical)",
              }}
            >
              {apiError}
            </span>
          </div>
        )}

        <FormField
          id="login-email"
          label="Email address"
          type="email"
          autoComplete="email"
          value={email}
          onChange={(e) => setEmail(e.target.value)}
          onBlur={() =>
            setTouched((t) => ({
              ...t,
              email: true,
            }))
          }
          error={errors.email}
          required
          placeholder="you@example.com"
        />

        <FormField
          id="login-password"
          label="Password"
          type="password"
          autoComplete="current-password"
          value={password}
          onChange={(e) => setPassword(e.target.value)}
          onBlur={() =>
            setTouched((t) => ({
              ...t,
              password: true,
            }))
          }
          error={errors.password}
          required
          placeholder="••••••••"
        />

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
            cursor: isLoading
              ? "not-allowed"
              : "pointer",
            width: "100%",
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            gap: 8,
          }}
          onMouseEnter={(e) => {
            if (!isLoading) {
              e.currentTarget.style.background =
                "var(--color-primary-hover)";
            }
          }}
          onMouseLeave={(e) => {
            if (!isLoading) {
              e.currentTarget.style.background =
                "var(--color-primary)";
            }
          }}
          onMouseDown={(e) => {
            e.currentTarget.style.background =
              "var(--color-primary-press)";
          }}
          onMouseUp={(e) => {
            e.currentTarget.style.background =
              "var(--color-primary)";
          }}
        >
          {isLoading ? (
            <>
              <span
                style={{
                  display: "inline-block",
                  width: 12,
                  height: 12,
                  border:
                    "2px solid rgba(255,255,255,0.4)",
                  borderTopColor: "#fff",
                  borderRadius: "50%",
                  animation:
                    "spin 0.7s linear infinite",
                }}
              />
              Logging in…
            </>
          ) : (
            "Log in"
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
          Don't have an account?{" "}
          <Link
            to="/register"
            style={{
              color: "var(--color-primary)",
              textDecoration: "none",
              fontWeight: 500,
            }}
          >
            Register
          </Link>
        </p>
      </form>

      <style>
        {`
          @keyframes spin {
            to {
              transform: rotate(360deg);
            }
          }
        `}
      </style>
    </AuthCard>
  );
}