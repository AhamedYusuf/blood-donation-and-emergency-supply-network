import { useState, type FormEvent, type ChangeEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useSelector, useDispatch } from "react-redux";
import type { RootState } from "../../app/store";
import { useRegisterDonorProfileMutation } from "./donorApi";
import { setDonorId } from "../../features/auth/authSlice";
import { AuthCard } from "../../components/AuthCard";
import { SelectField } from "../../components/SelectField";
import { FormField } from "../../components/FormField";

// ── Constants ─────────────────────────────────────────────────────────────────

const BLOOD_TYPES = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"];

const BLOOD_TYPE_OPTIONS = BLOOD_TYPES.map((bt) => ({ value: bt, label: bt }));

/**
 * Keys MUST match what EligibilityRuleEngine.cs looks up via TryGetValue.
 * Backend uses snake_case — do not rename these.
 */
const MEDICAL_FLAG_LABELS: Record<string, string> = {
  recent_illness: "Recent illness or infection",
  recent_surgery: "Recent surgery (within 6 months)",
  chronic_condition: "Chronic condition (e.g. diabetes, heart disease)",
  hiv_positive: "HIV positive",
  hepatitis: "Hepatitis B or C",
};

// ── Validation ────────────────────────────────────────────────────────────────

function validateBloodType(v: string) {
  if (!v) return "Please select your blood type.";
  if (!BLOOD_TYPES.includes(v)) return "Invalid blood type.";
  return "";
}

function validateDob(v: string) {
  if (!v) return "Date of birth is required.";
  const dob = new Date(v);
  if (isNaN(dob.getTime())) return "Enter a valid date.";
  const now = new Date();
  const age18 = new Date(now.getFullYear() - 18, now.getMonth(), now.getDate());
  if (dob > age18) return "You must be at least 18 years old to register.";
  const age100 = new Date(now.getFullYear() - 100, now.getMonth(), now.getDate());
  if (dob < age100) return "Please enter a valid date of birth.";
  return "";
}

// ── Step indicator (re-used from RegisterPage pattern) ────────────────────────

function StepIndicator() {
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
                  s.n === 1
                    ? "var(--color-success)"
                    : "var(--color-primary)",
                color: "white",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                fontSize: 11,
                fontWeight: 600,
              }}
            >
              {s.n === 1 ? "✓" : "2"}
            </div>
            <span
              className="text-caption"
              style={{
                color: s.n === 1 ? "var(--color-success)" : "var(--color-ink)",
                fontWeight: s.n === 2 ? 600 : 400,
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
                background: "var(--color-success)",
              }}
            />
          )}
        </div>
      ))}
    </div>
  );
}

// ── Component ─────────────────────────────────────────────────────────────────

export function DonorProfilePage() {
  const navigate = useNavigate();
  const dispatch = useDispatch();
  const fullName = useSelector((s: RootState) => s.auth.fullName);

  const [bloodType, setBloodType] = useState("");
  const [dob, setDob] = useState("");
  const [address, setAddress] = useState("");
  const [medicalFlags, setMedicalFlags] = useState<Record<string, boolean>>(
    Object.fromEntries(Object.keys(MEDICAL_FLAG_LABELS).map((k) => [k, false]))
  );

  const [touched, setTouched] = useState({ bloodType: false, dob: false, address: false });
  const [apiError, setApiError] = useState<string | null>(null);

  const [registerDonorProfile, { isLoading }] = useRegisterDonorProfileMutation();

  const errors = {
    bloodType: touched.bloodType ? validateBloodType(bloodType) : "",
    dob: touched.dob ? validateDob(dob) : "",
    address: touched.address && !address.trim() ? "Address is required." : "",
  };

  const isFormValid =
    !validateBloodType(bloodType) && !validateDob(dob) && !!address.trim();

  const handleFlagChange = (key: string, e: ChangeEvent<HTMLInputElement>) => {
    setMedicalFlags((prev) => ({ ...prev, [key]: e.target.checked }));
  };

  // Max DOB: 18 years ago today
  const maxDob = (() => {
    const d = new Date();
    d.setFullYear(d.getFullYear() - 18);
    return d.toISOString().split("T")[0];
  })();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setTouched({ bloodType: true, dob: true, address: true });
    setApiError(null);

    if (!isFormValid) return;

    // Only include flags that are true
    const activeFlags = Object.fromEntries(
      Object.entries(medicalFlags).filter(([, v]) => v)
    );

    try {
      const result = await registerDonorProfile({
        bloodType,
        dateOfBirth: dob,
        address: address.trim(),
        // All flags are stored (true and false) so the engine can evaluate each rule.
        // Only send the map if at least one flag was touched/checked.
        medicalFlags: Object.keys(activeFlags).length > 0 ? activeFlags : undefined,
      }).unwrap();

      // D2 fix: persist the donor profile UUID so future calls to
      // GET /api/donors/{id}, PUT /api/donors/{id}, and eligibility
      // can resolve the correct donor ID from Redux.
      dispatch(setDonorId(result.id));

      navigate("/");
    } catch (err: unknown) {
      const e = err as { status?: number; data?: { message?: string } };
      if (e.status === 400) {
        setApiError(
          e.data?.message ?? "A donor profile already exists for this account."
        );
      } else {
        setApiError("Failed to save donor profile. Please try again.");
      }
    }
  };

  return (
    <AuthCard
      title={`Welcome${fullName ? `, ${fullName.split(" ")[0]}` : ""}!`}
      subtitle="Step 2 of 2 — Donor profile"
      stepIndicator={<StepIndicator />}
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

        {/* Blood type + DOB side by side */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "1fr 1fr",
            gap: "var(--space-sm)",
          }}
        >
          <SelectField
            id="donor-blood-type"
            label="Blood type"
            value={bloodType}
            onChange={(e) => setBloodType(e.target.value)}
            onBlur={() => setTouched((t) => ({ ...t, bloodType: true }))}
            error={errors.bloodType}
            required
            placeholder="Select…"
            options={BLOOD_TYPE_OPTIONS}
          />

          <FormField
            id="donor-dob"
            label="Date of birth"
            type="date"
            value={dob}
            max={maxDob}
            onChange={(e) => setDob(e.target.value)}
            onBlur={() => setTouched((t) => ({ ...t, dob: true }))}
            error={errors.dob}
            required
          />
        </div>

        <FormField
          id="donor-address"
          label="Address (optional)"
          type="text"
          autoComplete="street-address"
          value={address}
          onChange={(e) => setAddress(e.target.value)}
          onBlur={() => setTouched((t) => ({ ...t, address: true }))}
          error={errors.address}
          required
          placeholder="123 Main St, Colombo"
        />

        {/* Medical flags section */}
        <div style={{ display: "flex", flexDirection: "column", gap: 10 }}>
          <span
            className="text-label"
            style={{ color: "var(--color-ink-secondary)" }}
          >
            Medical conditions{" "}
            <span
              className="text-caption"
              style={{ color: "var(--color-ink-faint)", fontWeight: 400 }}
            >
              (select all that apply)
            </span>
          </span>

          <div
            style={{
              background: "var(--color-surface-sunken)",
              borderRadius: "var(--radius-md)",
              padding: "var(--space-sm)",
              display: "flex",
              flexDirection: "column",
              gap: 10,
            }}
          >
            {Object.entries(MEDICAL_FLAG_LABELS).map(([key, flagLabel]) => (
              <label
                key={key}
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 10,
                  cursor: "pointer",
                }}
              >
                <input
                  id={`flag-${key}`}
                  type="checkbox"
                  checked={medicalFlags[key] ?? false}
                  onChange={(e) => handleFlagChange(key, e)}
                  style={{
                    width: 16,
                    height: 16,
                    accentColor: "var(--color-primary)",
                    cursor: "pointer",
                    flexShrink: 0,
                  }}
                />
                <span
                  className="text-body-sm"
                  style={{ color: "var(--color-ink-secondary)" }}
                >
                  {flagLabel}
                </span>
              </label>
            ))}
          </div>

          <p
            className="text-caption"
            style={{ margin: 0, color: "var(--color-ink-faint)" }}
          >
            This information is used to assess your donation eligibility and is kept confidential.
          </p>
        </div>

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
              Saving profile…
            </>
          ) : (
            "Complete registration →"
          )}
        </button>
      </form>

      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </AuthCard>
  );
}
