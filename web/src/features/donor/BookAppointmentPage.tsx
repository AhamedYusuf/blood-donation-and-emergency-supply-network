import { useEffect, useState, type FormEvent } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { useGetOrganizationsQuery } from "../organizations/organizationsApi";
import { useBookAppointmentMutation } from "../appointments/appointmentsApi";
import { FormField } from "../../components/FormField";
import { SelectField } from "../../components/SelectField";
import "./donor.css";

// Donor-facing booking form — mirrors the mobile app's
// book_appointment_screen.dart (pick a blood bank, a date, a time).
// Styled to match the rest of the donor shell (donor.css) rather than
// standing apart from DonorHomePage/DonorAppointmentsPage.

interface BookingNavState {
  organizationId?: string;
  hospitalName?: string;
}

export function BookAppointmentPage() {
  const navigate = useNavigate();
  const location = useLocation();
  // Set when arriving from a blood request's "I can donate" button
  // (RequestDetailsPage) — pre-fills the blood bank when it is one.
  const navState = (location.state ?? {}) as BookingNavState;

  const { data: organizations = [], isLoading: isLoadingOrgs } = useGetOrganizationsQuery();
  // The backend serializes OrganizationType as its raw C# enum name
  // ("BloodBank", "Hospital" — OrganizationService.ToResponse does
  // `organization.Type.ToString()`), not a snake_case string — confirmed
  // live against the real API. Mobile's own equivalent filter had the
  // identical bug (checking for 'blood_bank'), fixed alongside this.
  const bloodBanks = organizations.filter((o) => o.type.toLowerCase() === "bloodbank");

  const [organizationId, setOrganizationId] = useState(navState.organizationId ?? "");

  // A blood request's organizationId isn't necessarily a blood bank (a
  // hospital can also create one) — only pre-select once we can confirm
  // it's actually in the bookable list, otherwise leave it for the donor
  // to pick themselves.
  useEffect(() => {
    if (navState.organizationId && bloodBanks.some((b) => b.id === navState.organizationId)) {
      setOrganizationId(navState.organizationId);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [bloodBanks.length]);
  const [date, setDate] = useState("");
  const [time, setTime] = useState("");
  const [touched, setTouched] = useState(false);
  const [apiError, setApiError] = useState<string | null>(null);

  const [bookAppointment, { isLoading: isBooking }] = useBookAppointmentMutation();

  const today = new Date().toISOString().split("T")[0];
  const scheduledTime = date && time ? new Date(`${date}T${time}:00`) : null;

  const errors = {
    organizationId: touched && !organizationId ? "Choose a blood bank." : "",
    date: touched && !date ? "Pick a date." : "",
    time: touched && !time ? "Pick a time." : "",
    slot:
      touched && scheduledTime && scheduledTime.getTime() <= Date.now()
        ? "Pick a date and time in the future."
        : "",
  };

  const isFormValid = !!organizationId && !!scheduledTime && scheduledTime.getTime() > Date.now();

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setTouched(true);
    setApiError(null);
    if (!isFormValid || !scheduledTime) return;

    try {
      await bookAppointment({ organizationId, scheduledTime: scheduledTime.toISOString() }).unwrap();
      navigate("/appointments");
    } catch (err: unknown) {
      const e = err as { data?: { message?: string } };
      setApiError(e.data?.message ?? "Could not book that slot. Please try again.");
    }
  };

  return (
    <main className="donor-page">
      <header className="donor-page-heading">
        <div>
          <span className="donor-eyebrow">DONATIONS</span>
          <h1>Book a donation</h1>
          <p>Pick a blood bank and a time that works for you.</p>
        </div>
      </header>

      <section className="donor-profile-card" style={{ marginTop: 28, maxWidth: 480, padding: 28 }}>
        <form onSubmit={handleSubmit} noValidate style={{ display: "flex", flexDirection: "column", gap: 16 }}>
          {navState.hospitalName && (
            <div
              style={{
                background: "var(--color-primary-subtle)",
                borderRadius: "var(--radius-sm)",
                padding: "10px 14px",
                color: "var(--color-primary)",
                fontSize: 13,
              }}
            >
              Responding to a request from {navState.hospitalName}.
            </div>
          )}

          {apiError && (
            <div
              role="alert"
              style={{
                background: "var(--color-critical-subtle)",
                border: "1px solid var(--color-critical)",
                borderRadius: "var(--radius-sm)",
                padding: "10px 14px",
                color: "var(--color-critical)",
                fontSize: 13,
              }}
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

          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 12 }}>
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
            <p style={{ margin: 0, color: "var(--color-critical)", fontSize: 13 }}>{errors.slot}</p>
          )}

          <div style={{ display: "flex", gap: 10, marginTop: 4 }}>
            <button
              type="button"
              onClick={() => navigate("/appointments")}
              style={{
                flex: 1,
                border: "1px solid var(--color-hairline-strong)",
                borderRadius: 9,
                padding: "11px 16px",
                background: "var(--color-surface)",
                color: "var(--color-ink-secondary)",
                font: "inherit",
                fontSize: 13,
                fontWeight: 600,
                cursor: "pointer",
              }}
            >
              Cancel
            </button>
            <button type="submit" disabled={isBooking} className="donor-button" style={{ flex: 2 }}>
              {isBooking ? "Booking…" : "Confirm booking"}
            </button>
          </div>
        </form>
      </section>
    </main>
  );
}
