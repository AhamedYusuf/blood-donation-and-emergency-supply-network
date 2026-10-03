import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useGetOrganizationsQuery } from "../organizations/organizationsApi";
import { useBookAppointmentMutation } from "./appointmentsApi";
import { FormField } from "../../components/FormField";
import { SelectField } from "../../components/SelectField";

// Donor-facing booking form — mirrors the mobile app's
// book_appointment_screen.dart (pick a blood bank, a date, a time),
// web's own equivalent of that screen. Previously there was no way for
// a donor to book an appointment on web at all; `/` only ever showed
// the staff-facing console, which 403s for a donor's own
// GET /appointments/bloodbank/{orgId}/upcoming call (not that a donor
// was ever shown a way to call it).

export function BookAppointmentPage() {
  const navigate = useNavigate();

  const { data: organizations = [], isLoading: isLoadingOrgs } = useGetOrganizationsQuery();
  // The backend serializes OrganizationType as its raw C# enum name
  // ("BloodBank", "Hospital" — see OrganizationService.ToResponse),
  // not a snake_case string. A "blood_bank" comparison here (the
  // mobile app's own appointments_repository.dart had the identical
  // bug) would never match anything, leaving this picker permanently
  // empty against live data — confirmed live against the real API,
  // not assumed.
  const bloodBanks = organizations.filter(
    (o) => o.type.toLowerCase() === "bloodbank"
  );

  const [organizationId, setOrganizationId] = useState("");
  const [date, setDate] = useState("");
  const [time, setTime] = useState("");
  const [touched, setTouched] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const [bookAppointment, { isLoading: isBooking }] = useBookAppointmentMutation();

  const today = new Date().toISOString().split("T")[0];

  const scheduledTime =
    date && time ? new Date(`${date}T${time}:00`) : null;

  const errors = {
    organizationId:
      touched && !organizationId ? "Choose a blood bank." : "",
    date: touched && !date ? "Pick a date." : "",
    time: touched && !time ? "Pick a time." : "",
    slot:
      touched && scheduledTime && scheduledTime.getTime() <= Date.now()
        ? "Pick a date and time in the future."
        : "",
  };

  const isFormValid =
    !!organizationId &&
    !!scheduledTime &&
    scheduledTime.getTime() > Date.now();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setTouched(true);
    setApiError(null);

    if (!isFormValid || !scheduledTime) return;

    try {
      await bookAppointment({
        organizationId,
        scheduledTime: scheduledTime.toISOString(),
      }).unwrap();

      navigate("/");
    } catch (err: unknown) {
      const e = err as { data?: { message?: string } };
      setApiError(e.data?.message ?? "Could not book that slot. Please try again.");
    }
  };

  return (
    <div
      style={{
        padding: "var(--space-xl)",
        fontFamily: "var(--font-sans)",
        background: "var(--color-canvas)",
        height: "100%",
        overflowY: "auto",
      }}
    >
      <div style={{ maxWidth: 440 }}>
        <h1 className="text-display" style={{ margin: "0 0 var(--space-xxs)", color: "var(--color-ink)" }}>
          Book a donation
        </h1>
        <p className="text-body" style={{ color: "var(--color-ink-secondary)", margin: "0 0 var(--space-xl)" }}>
          Pick a blood bank and a time that works for you.
        </p>

        <form
          onSubmit={handleSubmit}
          noValidate
          style={{ display: "flex", flexDirection: "column", gap: "var(--space-md)" }}
        >
          {apiError && (
            <div
              role="alert"
              style={{
                background: "var(--color-critical-subtle)",
                border: "1px solid var(--color-critical)",
                borderRadius: "var(--radius-sm)",
                padding: "10px 14px",
                color: "var(--color-critical)",
              }}
              className="text-body-sm"
            >
              {apiError}
            </div>
          )}

          <SelectField
            id="book-org"
            label="Blood bank"
            value={organizationId}
            onChange={(e) => setOrganizationId(e.target.value)}
            error={errors.organizationId}
            required
            placeholder={isLoadingOrgs ? "Loading…" : "Choose a blood bank"}
            disabled={isLoadingOrgs}
            options={bloodBanks.map((b) => ({ value: b.id, label: b.name }))}
          />

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-sm)" }}>
            <FormField
              id="book-date"
              label="Date"
              type="date"
              min={today}
              value={date}
              onChange={(e) => setDate(e.target.value)}
              error={errors.date}
              required
            />
            <FormField
              id="book-time"
              label="Time"
              type="time"
              value={time}
              onChange={(e) => setTime(e.target.value)}
              error={errors.time}
              required
            />
          </div>
          {errors.slot && (
            <p className="text-body-sm" style={{ margin: 0, color: "var(--color-critical)" }}>
              {errors.slot}
            </p>
          )}

          <div style={{ display: "flex", gap: "var(--space-sm)", marginTop: "var(--space-xs)" }}>
            <button
              type="button"
              onClick={() => navigate("/")}
              style={{
                flex: 1,
                background: "var(--color-surface)",
                color: "var(--color-ink-secondary)",
                border: "1px solid var(--color-hairline-strong)",
                borderRadius: "var(--radius-sm)",
                padding: "9px 16px",
                fontSize: 13,
                fontWeight: 500,
                fontFamily: "var(--font-sans)",
                cursor: "pointer",
              }}
            >
              Cancel
            </button>
            <button
              type="submit"
              disabled={isBooking}
              style={{
                flex: 2,
                background: isBooking ? "var(--color-primary-press)" : "var(--color-primary)",
                color: "var(--color-on-primary)",
                border: "none",
                borderRadius: "var(--radius-sm)",
                padding: "9px 16px",
                fontSize: 13,
                fontWeight: 500,
                fontFamily: "var(--font-sans)",
                cursor: isBooking ? "not-allowed" : "pointer",
              }}
            >
              {isBooking ? "Booking…" : "Confirm booking"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
