import { useState } from "react";
import { Link } from "react-router-dom";
import { useSelector } from "react-redux";
import type { RootState } from "../../app/store";
import {
  useGetMyAppointmentsQuery,
  useConfirmAppointmentMutation,
  useDeclineAppointmentMutation,
  useRescheduleAppointmentMutation,
  useUpdateAppointmentStatusMutation,
  type Appointment,
} from "../appointments/appointmentsApi";
import "./donor.css";

// Previously this page only ever displayed appointments — no way to
// book one (no link anywhere led to a booking flow), and an upcoming
// appointment's only action was a "Details" link that actually pointed
// at /requests (the blood-requests list, unrelated). Agent-dispatched
// appointments also had no way to be accepted/declined at all. This
// adds all three, matching the mobile app's my_appointments_screen.dart.

function formatDate(value: string) {
  return new Date(value).toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" });
}
function formatTime(value: string) {
  return new Date(value).toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
}

type PendingAction =
  | { kind: "cancel"; appointment: Appointment }
  | { kind: "decline"; appointment: Appointment }
  | { kind: "reschedule"; appointment: Appointment };

export function DonorAppointmentsPage() {
  const donorId = useSelector((state: RootState) => state.auth.userId);
  const { data = [], isLoading, isError, refetch } = useGetMyAppointmentsQuery({ donorId: donorId ?? "" }, { skip: !donorId });

  const [confirmAppointment] = useConfirmAppointmentMutation();
  const [declineAppointment] = useDeclineAppointmentMutation();
  const [rescheduleAppointment] = useRescheduleAppointmentMutation();
  const [updateStatus] = useUpdateAppointmentStatusMutation();

  const [action, setAction] = useState<PendingAction | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [rescheduleDate, setRescheduleDate] = useState("");
  const [rescheduleTime, setRescheduleTime] = useState("");

  const upcoming = data
    .filter((item) => new Date(item.scheduledTime) >= new Date() && item.status !== "cancelled" && item.status !== "declined")
    .sort((a, b) => a.scheduledTime.localeCompare(b.scheduledTime));
  const history = data.filter((item) => !upcoming.includes(item));

  const openReschedule = (appointment: Appointment) => {
    const d = new Date(appointment.scheduledTime);
    setRescheduleDate(d.toISOString().split("T")[0]);
    setRescheduleTime(d.toTimeString().slice(0, 5));
    setActionError(null);
    setAction({ kind: "reschedule", appointment });
  };

  const extractMessage = (err: unknown, fallback: string) => {
    const e = err as { data?: { message?: string; error?: string } };
    return e.data?.message ?? e.data?.error ?? fallback;
  };

  const handleConfirm = async (appointment: Appointment) => {
    setActionError(null);
    try {
      await confirmAppointment(appointment.id).unwrap();
    } catch (err) {
      setActionError(extractMessage(err, "Could not confirm that appointment."));
    }
  };

  const handleActionConfirmed = async () => {
    if (!action) return;
    setActionError(null);
    try {
      if (action.kind === "cancel") {
        await updateStatus({ id: action.appointment.id, newStatus: "cancelled" }).unwrap();
      } else if (action.kind === "decline") {
        await declineAppointment(action.appointment.id).unwrap();
      } else {
        if (!rescheduleDate || !rescheduleTime) {
          setActionError("Pick a date and a time.");
          return;
        }
        const newScheduledTime = new Date(`${rescheduleDate}T${rescheduleTime}:00`);
        if (newScheduledTime.getTime() <= Date.now()) {
          setActionError("Pick a date and time in the future.");
          return;
        }
        await rescheduleAppointment({ id: action.appointment.id, newScheduledTime: newScheduledTime.toISOString() }).unwrap();
      }
      setAction(null);
    } catch (err) {
      setActionError(extractMessage(err, "That didn't go through. Please try again."));
    }
  };

  return (
    <main className="donor-page">
      <header className="donor-page-heading">
        <div>
          <span className="donor-eyebrow">DONOR SPACE</span>
          <h1>My donations</h1>
          <p>Book and manage the visits you make to the network.</p>
        </div>
        <div style={{ display: "flex", gap: 10 }}>
          <button className="donor-button" type="button" onClick={() => refetch()}>Refresh</button>
          <Link className="donor-button" to="/appointments/book">+ Book a donation</Link>
        </div>
      </header>

      {isLoading && <div className="donor-empty-card"><h3>Loading your donations...</h3></div>}
      {isError && <div className="donor-empty-card"><h3>We could not load your donations.</h3><button className="donor-button" type="button" onClick={() => refetch()}>Try again</button></div>}
      {!isLoading && !isError && data.length === 0 && (
        <div className="donor-empty-card">
          <div><h3>No donations booked</h3><p>Your appointment history will appear here.</p></div>
          <Link className="donor-button" to="/appointments/book">Book a donation</Link>
        </div>
      )}

      {upcoming.length > 0 && (
        <section className="donor-section">
          <div className="donor-section__heading"><h2>Upcoming</h2><span>{upcoming.length}</span></div>
          <div className="donor-list">
            {upcoming.map((item) => (
              <AppointmentRow
                key={item.id}
                appointment={item}
                onConfirm={() => handleConfirm(item)}
                onDecline={() => setAction({ kind: "decline", appointment: item })}
                onCancel={() => setAction({ kind: "cancel", appointment: item })}
                onReschedule={() => openReschedule(item)}
              />
            ))}
          </div>
        </section>
      )}

      {history.length > 0 && (
        <section className="donor-section">
          <div className="donor-section__heading"><h2>History</h2><span>{history.length}</span></div>
          <div className="donor-list">
            {history.map((item) => (
              <article className="donor-list-row donor-list-row--muted" key={item.id}>
                <div className="donor-list-row__icon">+</div>
                <div>
                  <span className="donor-card-label">{item.status.replace("_", " ")}</span>
                  <h3>{formatDate(item.scheduledTime)}</h3>
                  <p>{item.unitsDonated ? `${item.unitsDonated} unit${item.unitsDonated === 1 ? "" : "s"} donated` : "Donation appointment"}</p>
                </div>
              </article>
            ))}
          </div>
        </section>
      )}

      {action && (
        <ActionModal
          action={action}
          error={actionError}
          rescheduleDate={rescheduleDate}
          rescheduleTime={rescheduleTime}
          onRescheduleDateChange={setRescheduleDate}
          onRescheduleTimeChange={setRescheduleTime}
          onCancel={() => { setAction(null); setActionError(null); }}
          onConfirm={handleActionConfirmed}
        />
      )}
    </main>
  );
}

function AppointmentRow({
  appointment,
  onConfirm,
  onDecline,
  onCancel,
  onReschedule,
}: {
  appointment: Appointment;
  onConfirm: () => void;
  onDecline: () => void;
  onCancel: () => void;
  onReschedule: () => void;
}) {
  const item = appointment;
  const canConfirmOrDecline = item.status === "pending_confirmation";
  const canCancel = item.status === "scheduled";

  return (
    <article className="donor-list-row">
      <div className="donor-list-row__icon">+</div>
      <div>
        <span className="donor-card-label">{item.status.replace("_", " ")}</span>
        <h3>{formatDate(item.scheduledTime)} at {formatTime(item.scheduledTime)}</h3>
        <p>Appointment at your selected blood bank</p>
      </div>
      <div style={{ display: "flex", gap: 8, flexWrap: "wrap", justifyContent: "flex-end" }}>
        {canConfirmOrDecline && (
          <button type="button" className="donor-button" style={{ padding: "7px 12px", fontSize: 12 }} onClick={onConfirm}>
            Accept
          </button>
        )}
        {(canConfirmOrDecline || canCancel) && (
          <RowLinkButton onClick={onReschedule} label="Reschedule" />
        )}
        {canConfirmOrDecline && <RowLinkButton onClick={onDecline} label="Decline" critical />}
        {canCancel && <RowLinkButton onClick={onCancel} label="Cancel" critical />}
      </div>
    </article>
  );
}

function RowLinkButton({ onClick, label, critical = false }: { onClick: () => void; label: string; critical?: boolean }) {
  return (
    <button
      type="button"
      onClick={onClick}
      style={{
        border: "1px solid var(--color-hairline-strong)",
        background: "var(--color-surface)",
        color: critical ? "var(--color-critical)" : "var(--color-ink-secondary)",
        borderRadius: 7,
        padding: "7px 12px",
        fontSize: 12,
        fontWeight: 600,
        cursor: "pointer",
        font: "inherit",
      }}
    >
      {label}
    </button>
  );
}

function ActionModal({
  action,
  error,
  rescheduleDate,
  rescheduleTime,
  onRescheduleDateChange,
  onRescheduleTimeChange,
  onCancel,
  onConfirm,
}: {
  action: PendingAction;
  error: string | null;
  rescheduleDate: string;
  rescheduleTime: string;
  onRescheduleDateChange: (v: string) => void;
  onRescheduleTimeChange: (v: string) => void;
  onCancel: () => void;
  onConfirm: () => void;
}) {
  const today = new Date().toISOString().split("T")[0];
  const copy = {
    cancel: { title: "Cancel this appointment?", body: "The slot is released and you can book another any time.", confirmLabel: "Cancel appointment" },
    decline: { title: "Decline this donation?", body: "You were matched for this slot. Declining releases it so it can be offered to someone else.", confirmLabel: "Decline" },
    reschedule: { title: "Pick a new time", body: "", confirmLabel: "Confirm new time" },
  }[action.kind];

  return (
    <div
      style={{ position: "fixed", inset: 0, background: "rgba(20, 27, 44, 0.4)", display: "flex", alignItems: "center", justifyContent: "center", zIndex: 50 }}
      onClick={onCancel}
    >
      <div className="donor-profile-card" style={{ width: 360, maxWidth: "90vw", padding: 24 }} onClick={(e) => e.stopPropagation()}>
        <h2 style={{ margin: "0 0 6px", fontSize: 18 }}>{copy.title}</h2>
        {copy.body && <p style={{ margin: "0 0 16px", color: "var(--color-ink-secondary)", fontSize: 13 }}>{copy.body}</p>}

        {action.kind === "reschedule" && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 10, marginBottom: 16 }}>
            <FormDateInput label="Date" type="date" min={today} value={rescheduleDate} onChange={onRescheduleDateChange} />
            <FormDateInput label="Time" type="time" value={rescheduleTime} onChange={onRescheduleTimeChange} />
          </div>
        )}

        {error && <p style={{ margin: "0 0 16px", color: "var(--color-critical)", fontSize: 13 }}>{error}</p>}

        <div style={{ display: "flex", gap: 10 }}>
          <button
            type="button"
            onClick={onCancel}
            style={{ flex: 1, border: "1px solid var(--color-hairline-strong)", borderRadius: 9, padding: "10px 16px", background: "var(--color-surface)", color: "var(--color-ink-secondary)", font: "inherit", fontSize: 13, fontWeight: 600, cursor: "pointer" }}
          >
            Keep it
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className={action.kind === "reschedule" ? "donor-button" : "donor-danger-button"}
            style={{ flex: 1, margin: 0 }}
          >
            {copy.confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

function FormDateInput({ label, type, min, value, onChange }: { label: string; type: string; min?: string; value: string; onChange: (v: string) => void }) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      <label style={{ fontSize: 12, fontWeight: 600, color: "var(--color-ink-secondary)" }}>{label}</label>
      <input
        type={type}
        min={min}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        style={{ font: "inherit", fontSize: 13, color: "var(--color-ink)", background: "var(--color-surface)", border: "1px solid var(--color-hairline-strong)", borderRadius: 7, padding: "8px 10px" }}
      />
    </div>
  );
}
