import { useState, type FormEvent } from "react";
import { useDispatch } from "react-redux";
import { useNavigate, useSearchParams } from "react-router-dom";
import { AuthCard } from "../../components/AuthCard";
import { FormField } from "../../components/FormField";
import { useRegisterStaffMutation } from "../auth/authApi";
import { setCredentials } from "../auth/authSlice";

function required(value: string, label: string) {
  return value.trim() ? "" : `${label} is required.`;
}

function passwordError(value: string) {
  if (!value) return "Password is required.";
  return value.length >= 8 ? "" : "Password must be at least 8 characters.";
}

function phoneError(value: string) {
  if (!value.trim()) return "Phone number is required.";
  return value.trim().length >= 7 ? "" : "Enter a valid phone number.";
}

function getApiMessage(error: unknown) {
  const value = error as { data?: { message?: string } } | undefined;
  return value?.data?.message ?? "This invitation could not be accepted. It may be expired or already used.";
}

export function AcceptInvitationPage() {
  const [params] = useSearchParams();
  const [token, setToken] = useState(params.get("token") ?? "");
  const [fullName, setFullName] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [password, setPassword] = useState("");
  const [touched, setTouched] = useState({ token: false, fullName: false, phoneNumber: false, password: false });
  const [apiError, setApiError] = useState<string | null>(null);
  const [registerStaff, { isLoading }] = useRegisterStaffMutation();
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const errors = {
    token: touched.token ? required(token, "Invitation token") : "",
    fullName: touched.fullName ? required(fullName, "Full name") : "",
    phoneNumber: touched.phoneNumber ? phoneError(phoneNumber) : "",
    password: touched.password ? passwordError(password) : "",
  };
  const valid = Boolean(token.trim()) && Boolean(fullName.trim()) && !phoneError(phoneNumber) && !passwordError(password);

  const submit = async (event: FormEvent) => {
    event.preventDefault();
    setTouched({ token: true, fullName: true, phoneNumber: true, password: true });
    setApiError(null);
    if (!valid) return;

    try {
      const result = await registerStaff({
        token: token.trim(),
        fullName: fullName.trim(),
        phoneNumber: phoneNumber.trim(),
        password,
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
      setApiError(getApiMessage(error));
    }
  };

  return (
    <AuthCard title="Accept your staff invitation" subtitle="Create your Blood Donation Network account">
      <form onSubmit={submit} noValidate style={{ display: "flex", flexDirection: "column", gap: "var(--space-md)" }}>
        <div className="text-body-sm" style={{ color: "var(--color-ink-muted)", background: "var(--color-surface-sunken)", borderRadius: "var(--radius-sm)", padding: "10px 12px" }}>
          Use the token shared by your administrator. Each invitation can only be redeemed once.
        </div>
        <FormField id="invitation-token" label="Invitation token" required value={token} placeholder="Paste your invitation token" error={errors.token} onChange={(event) => setToken(event.target.value)} onBlur={() => setTouched((current) => ({ ...current, token: true }))} />
        <FormField id="staff-full-name" label="Full name" required value={fullName} placeholder="Your full name" autoComplete="name" error={errors.fullName} onChange={(event) => setFullName(event.target.value)} onBlur={() => setTouched((current) => ({ ...current, fullName: true }))} />
        <FormField id="staff-phone-number" label="Phone number" required type="tel" value={phoneNumber} placeholder="+94 77 123 4567" autoComplete="tel" error={errors.phoneNumber} onChange={(event) => setPhoneNumber(event.target.value)} onBlur={() => setTouched((current) => ({ ...current, phoneNumber: true }))} />
        <FormField id="staff-password" label="Password" required type="password" value={password} placeholder="At least 8 characters" autoComplete="new-password" error={errors.password} onChange={(event) => setPassword(event.target.value)} onBlur={() => setTouched((current) => ({ ...current, password: true }))} />
        {apiError && <div role="alert" className="text-body-sm" style={{ color: "var(--color-critical)", background: "var(--color-critical-subtle)", borderRadius: "var(--radius-sm)", padding: "10px 12px" }}>{apiError}</div>}
        <button type="submit" disabled={isLoading} style={{ border: 0, borderRadius: "var(--radius-sm)", padding: "10px 14px", background: "var(--color-primary)", color: "var(--color-on-primary)", font: "inherit", fontWeight: 700, cursor: isLoading ? "wait" : "pointer" }}>{isLoading ? "Creating account…" : "Accept invitation"}</button>
      </form>
    </AuthCard>
  );
}
