import { useState, type FormEvent } from "react";
import { useDispatch } from "react-redux";
import { useNavigate, useSearchParams } from "react-router-dom";
import { AuthCard } from "../../components/AuthCard";
import { FormField } from "../../components/FormField";
import { setCredentials } from "../auth/authSlice";
import { useRegisterStaffMutation } from "../auth/authApi";

function validateRequired(value: string, label: string) {
  return value.trim() ? "" : `${label} is required.`;
}

function validatePassword(value: string) {
  if (!value) return "Password is required.";
  if (value.length < 8) return "Password must be at least 8 characters.";
  return "";
}

function validatePhone(value: string) {
  if (!value.trim()) return "Phone number is required.";
  if (value.trim().length < 7) return "Enter a valid phone number.";
  return "";
}

function apiMessage(error: unknown) {
  const value = error as { data?: { message?: string } } | undefined;
  return value?.data?.message ?? "This invitation could not be accepted. It may be expired or already used.";
}

export function AcceptInvitationPage() {
  const [searchParams] = useSearchParams();
  const [token, setToken] = useState(searchParams.get("token") ?? "");
  const [password, setPassword] = useState("");
  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [touched, setTouched] = useState({ token: false, password: false, fullName: false, phoneNumber: false });
  const [apiError, setApiError] = useState<string | null>(null);
  const [registerStaff, { isLoading }] = useRegisterStaffMutation();
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const errors = {
    token: touched.token ? validateRequired(token, "Invitation token") : "",
    password: touched.password ? validatePassword(password) : "",
    fullName: touched.fullName ? validateRequired(fullName, "Full name") : "",
    phoneNumber: touched.phoneNumber ? validatePhone(phoneNumber) : "",
  };
  const isValid = Boolean(token.trim()) && !validatePassword(password) && Boolean(fullName.trim()) && !validatePhone(phoneNumber);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setTouched({ token: true, password: true, fullName: true, phoneNumber: true });
    setApiError(null);
    if (!isValid) return;

    try {
      const result = await registerStaff({
        token: token.trim(),
        password,
        fullName: fullName.trim(),
        phoneNumber: phoneNumber.trim(),
      }).unwrap();

      dispatch(setCredentials({
        token: result.accessToken,
        refreshToken: result.refreshToken,
        userId: result.userId,
        donorProfileId: result.donorProfileId,
        email: result.email,
        fullName: result.fullName,
        role: result.role,
        organizationId: result.organizationId,
      }));
      navigate("/requests", { replace: true });
    } catch (error) {
      setApiError(apiMessage(error));
    }
  };

  return (
    <AuthCard title="Accept your staff invitation" subtitle="Create your Blood Donation Network account">
      <form onSubmit={submit} noValidate style={{ display: "flex", flexDirection: "column", gap: "var(--space-md)" }}>
        <div className="text-body-sm" style={{ color: "var(--color-ink-muted)", background: "var(--color-surface-sunken)", borderRadius: "var(--radius-sm)", padding: "10px 12px" }}>
          Use the invitation token shared by your administrator. It can only be redeemed once.
        </div>
        <FormField
          id="invitation-token"
          label="Invitation token"
          value={token}
          onChange={(event) => setToken(event.target.value)}
          onBlur={() => setTouched((current) => ({ ...current, token: true }))}
          error={errors.token}
          required
          placeholder="Paste your invitation token"
        />
        <FormField
          id="staff-full-name"
          label="Full name"
          value={fullName}
          onChange={(event) => setFullName(event.target.value)}
          onBlur={() => setTouched((current) => ({ ...current, fullName: true }))}
          error={errors.fullName}
          required
          autoComplete="name"
          placeholder="Your full name"
        />
        <FormField
          id="staff-phone-number"
          label="Phone number"
          type="tel"
          value={phoneNumber}
          onChange={(event) => setPhoneNumber(event.target.value)}
          onBlur={() => setTouched((current) => ({ ...current, phoneNumber: true }))}
          error={errors.phoneNumber}
          required
          autoComplete="tel"
          placeholder="+94 77 123 4567"
        />
        <FormField
          id="staff-password"
          label="Password"
          type="password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          onBlur={() => setTouched((current) => ({ ...current, password: true }))}
          error={errors.password}
          required
          autoComplete="new-password"
          placeholder="At least 8 characters"
        />
        {apiError && <div role="alert" className="text-body-sm" style={{ color: "var(--color-critical)", background: "var(--color-critical-subtle)", borderRadius: "var(--radius-sm)", padding: "10px 12px" }}>{apiError}</div>}
        <button type="submit" disabled={isLoading} style={{ border: 0, borderRadius: "var(--radius-sm)", padding: "10px 14px", background: "var(--color-primary)", color: "var(--color-on-primary)", font: "inherit", fontWeight: 700, cursor: isLoading ? "wait" : "pointer" }}>
          {isLoading ? "Creating account…" : "Accept invitation"}
        </button>
      </form>
    </AuthCard>
  );
}