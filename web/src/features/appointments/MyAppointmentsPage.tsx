import { useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useSelector } from "react-redux";
import type { RootState } from "../../app/store";
import {
  useGetByDonorQuery,
  useConfirmAppointmentMutation,
  useDeclineAppointmentMutation,
  useRescheduleAppointmentMutation,
  useUpdateAppointmentStatusMutation,
  type Appointment,
} from "./appointmentsApi";
import { StatusBadge } from "./StatusBadge";
import { AgentTag } from "./AgentTag";

// Donor-facing "my appointments" — web's equivalent of the mobile app's
// my_appointments_screen.dart. Previously `/` only ever rendered the
// staff-facing AppointmentsConsolePage for every role, so a donor
// landing there got a dead-end "your account isn't linked to an
// organization" message with no way to book or see their own
// appointments at all — the exact gap this page closes.

function formatTime(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function formatRelative(iso: string) {
  const diffMs = new Date(iso).getTime() - Date.now();
  const diffHrs = diffMs / (1000 * 60 * 60);
  if (diffMs < 0) return "past";
  if (diffHrs < 1) return `in ${Math.max(1, Math.round(diffMs / 60000))}m`;
  if (diffHrs < 24) return `in ${Math.round(diffHrs)}h`;
  return `in ${Math.round(diffHrs / 24)}d`;
}

type PendingAction =
  | { kind: "cancel"; appointment: Appointment }
  | { kind: "decline"; appointment: Appointment }
  | { kind: "reschedule"; appointment: Appointment };

export function MyAppointmentsPage() {
  const navigate = useNavigate();
  const donorId = useSelector((s: RootState) => s.auth.donorId);

  const { data: appointments = [], isLoading, isError, refetch } = useGetByDonorQuery(
    donorId ?? "",
    { skip: !donorId }
  );

  const [confirmAppointment] = useConfirmAppointmentMutation();
  const [declineAppointment] = useDeclineAppointmentMutation();
  const [rescheduleAppointment] = useRescheduleAppointmentMutation();
  const [updateStatus] = useUpdateAppointmentStatusMutation();

  const [action, setAction] = useState<PendingAction | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);
  const [rescheduleDate, setRescheduleDate] = useState("");
  const [rescheduleTime, setRescheduleTime] = useState("");

  const { history, next, laterUpcoming } = useMemo(() => {
    const isUpcoming = (a: Appointment) =>
      (a.status === "scheduled" || a.status === "pending_confirmation") &&
      new Date(a.scheduledTime).getTime() > Date.now();

    const upcoming = appointments
      .filter(isUpcoming)
      .slice()
      .sort((a, b) => new Date(a.scheduledTime).getTime() - new Date(b.scheduledTime).getTime());

    const history = appointments
      .filter((a) => !isUpcoming(a))
      .slice()
      .sort((a, b) => new Date(b.scheduledTime).getTime() - new Date(a.scheduledTime).getTime());

    return { upcoming, history, next: upcoming[0] ?? null, laterUpcoming: upcoming.slice(1) };
  }, [appointments]);

  const openReschedule = (appointment: Appointment) => {
    const d = new Date(appointment.scheduledTime);
    setRescheduleDate(d.toISOString().split("T")[0]);
    setRescheduleTime(d.toTimeString().slice(0, 5));
    setActionError(null);
    setAction({ kind: "reschedule", appointment });
  };

  const handleConfirm = async (appointment: Appointment) => {
    setActionError(null);
    try {
      await confirmAppointment(appointment.id).unwrap();
    } catch (err: unknown) {
      const e = err as { data?: { message?: string; error?: string } };
      setActionError(e.data?.message ?? e.data?.error ?? "Could not confirm that appointment.");
    }
  };

  const handleDeclineConfirmed = async () => {
    if (action?.kind !== "decline") return;
    setActionError(null);
    try {
      await declineAppointment(action.appointment.id).unwrap();
      setAction(null);
    } catch (err: unknown) {
      const e = err as { data?: { message?: string; error?: string } };
      setActionError(e.data?.message ?? e.data?.error ?? "Could not decline that appointment.");
    }
  };

  const handleCancelConfirmed = async () => {
    if (action?.kind !== "cancel") return;
    setActionError(null);
    try {
      await updateStatus({ id: action.appointment.id, newStatus: "cancelled" }).unwrap();
      setAction(null);
    } catch (err: unknown) {
      const e = err as { data?: { message?: string; error?: string } };
      setActionError(e.data?.message ?? e.data?.error ?? "Could not cancel that appointment.");
    }
  };

  const handleRescheduleConfirmed = async () => {
    if (action?.kind !== "reschedule") return;
    setActionError(null);
    if (!rescheduleDate || !rescheduleTime) {
      setActionError("Pick a date and a time.");
      return;
    }
    const newScheduledTime = new Date(`${rescheduleDate}T${rescheduleTime}:00`);
    if (newScheduledTime.getTime() <= Date.now()) {
      setActionError("Pick a date and time in the future.");
      return;
    }
    try {
      await rescheduleAppointment({
        id: action.appointment.id,
        newScheduledTime: newScheduledTime.toISOString(),
      }).unwrap();
      setAction(null);
    } catch (err: unknown) {
      const e = err as { data?: { message?: string; error?: string } };
      setActionError(e.data?.message ?? e.data?.error ?? "Could not reschedule that appointment.");
    }
  };

  if (!donorId) {
    return (
      <PageShell title="My appointments">
        <p className="text-body" style={{ color: "var(--color-ink-secondary)" }}>
          Complete your donor profile to book and track appointments.
        </p>
      </PageShell>
    );
  }

  if (isLoading) {
    return (
      <PageShell title="My appointments">
        <p className="text-body" style={{ color: "var(--color-ink-secondary)" }}>Loading your appointments…</p>
      </PageShell>
    );
  }

  if (isError) {
    return (
      <PageShell title="My appointments">
        <p className="text-body" style={{ color: "var(--color-critical)", marginBottom: "var(--space-md)" }}>
          We couldn't load your appointments.
        </p>
        <RetryButton onClick={() => refetch()} />
      </PageShell>
    );
  }

  return (
    <PageShell
      title="My appointments"
      action={
        <button
          type="button"
          onClick={() => navigate("/appointments/book")}
          style={primaryButtonStyle}
        >
          + Book a donation
        </button>
      }
    >
      {appointments.length === 0 ? (
        <EmptyState onBook={() => navigate("/appointments/book")} />
      ) : (
        <>
          <SectionLabel>Next donation</SectionLabel>
          {next ? (
            <HeroCard
              appointment={next}
              onConfirm={() => handleConfirm(next)}
              onDecline={() => setAction({ kind: "decline", appointment: next })}
              onCancel={() => setAction({ kind: "cancel", appointment: next })}
              onReschedule={() => openReschedule(next)}
            />
          ) : (
            <EmptyState onBook={() => navigate("/appointments/book")} compact />
          )}

          {laterUpcoming.length > 0 && (
            <>
              <SectionLabel>Also upcoming</SectionLabel>
              <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-xs)" }}>
                {laterUpcoming.map((a) => (
                  <Row
                    key={a.id}
                    appointment={a}
                    onConfirm={() => handleConfirm(a)}
                    onDecline={() => setAction({ kind: "decline", appointment: a })}
                    onCancel={() => setAction({ kind: "cancel", appointment: a })}
                    onReschedule={() => openReschedule(a)}
                  />
                ))}
              </div>
            </>
          )}

          {history.length > 0 && (
            <>
              <SectionLabel>History ({history.length})</SectionLabel>
              <div style={{ display: "flex", flexDirection: "column", gap: "var(--space-xs)" }}>
                {history.map((a) => (
                  <Row key={a.id} appointment={a} />
                ))}
              </div>
            </>
          )}
        </>
      )}

      {action && (
        <ActionModal
          action={action}
          error={actionError}
          rescheduleDate={rescheduleDate}
          rescheduleTime={rescheduleTime}
          onRescheduleDateChange={setRescheduleDate}
          onRescheduleTimeChange={setRescheduleTime}
          onCancel={() => {
            setAction(null);
            setActionError(null);
          }}
          onConfirm={
            action.kind === "cancel"
              ? handleCancelConfirmed
              : action.kind === "decline"
                ? handleDeclineConfirmed
                : handleRescheduleConfirmed
          }
        />
      )}
    </PageShell>
  );
}

// ── Layout helpers ──────────────────────────────────────────────────────────

function PageShell({
  title,
  action,
  children,
}: {
  title: string;
  action?: React.ReactNode;
  children: React.ReactNode;
}) {
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
      <div style={{ maxWidth: 720 }}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            marginBottom: "var(--space-xl)",
          }}
        >
          <h1 className="text-display" style={{ margin: 0, color: "var(--color-ink)" }}>
            {title}
          </h1>
          {action}
        </div>
        {children}
      </div>
    </div>
  );
}

function SectionLabel({ children }: { children: React.ReactNode }) {
  return (
    <div
      className="text-label"
      style={{
        color: "var(--color-ink-faint)",
        textTransform: "uppercase",
        letterSpacing: "0.5px",
        margin: "var(--space-lg) 0 var(--space-xs)",
      }}
    >
      {children}
    </div>
  );
}

function RetryButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      style={{
        border: "1px solid var(--color-hairline-strong)",
        background: "var(--color-surface)",
        borderRadius: "var(--radius-sm)",
        padding: "8px 16px",
        fontSize: 13,
        fontFamily: "var(--font-sans)",
        cursor: "pointer",
      }}
    >
      Try again
    </button>
  );
}

function EmptyState({ onBook, compact = false }: { onBook: () => void; compact?: boolean }) {
  return (
    <div
      style={{
        background: "var(--color-surface)",
        border: "1px solid var(--color-hairline)",
        borderRadius: "var(--radius-md)",
        padding: compact ? "var(--space-lg)" : "var(--space-xxl) var(--space-lg)",
        textAlign: "center",
      }}
    >
      <p className="text-body" style={{ color: "var(--color-ink)", fontWeight: 600, margin: "0 0 4px" }}>
        {compact ? "Nothing booked" : "No donations booked"}
      </p>
      <p className="text-body-sm" style={{ color: "var(--color-ink-secondary)", margin: "0 0 var(--space-md)" }}>
        Schedule your first donation — it only takes a minute.
      </p>
      <button type="button" onClick={onBook} style={primaryButtonStyle}>
        Book a donation
      </button>
    </div>
  );
}

const primaryButtonStyle: React.CSSProperties = {
  background: "var(--color-primary)",
  color: "var(--color-on-primary)",
  border: "none",
  borderRadius: "var(--radius-sm)",
  padding: "8px 16px",
  fontSize: 13,
  fontWeight: 500,
  fontFamily: "var(--font-sans)",
  cursor: "pointer",
};

// ── Hero / row ───────────────────────────────────────────────────────────────

function HeroCard({
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
  const a = appointment;
  const isPending = a.status === "pending_confirmation";
  const isScheduled = a.status === "scheduled";

  return (
    <div
      style={{
        background: "linear-gradient(135deg, var(--color-primary), var(--color-primary-press))",
        borderRadius: "var(--radius-lg)",
        padding: "var(--space-lg)",
        color: "#fff",
        boxShadow: "0 8px 24px rgba(0,0,0,0.12)",
      }}
    >
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start" }}>
        <div>
          <div className="text-body" style={{ fontWeight: 600, fontSize: 18 }}>
            {formatTime(a.scheduledTime)}
          </div>
          <div className="text-body-sm" style={{ opacity: 0.75, margin: "2px 0 var(--space-sm)" }}>
            {formatRelative(a.scheduledTime)}
          </div>
          <div style={{ display: "flex", gap: 6, flexWrap: "wrap" }}>
            <StatusBadge status={a.status} />
            {a.donorBloodType && (
              <span
                className="text-caption"
                style={{
                  background: "rgba(255,255,255,0.18)",
                  padding: "3px 9px",
                  borderRadius: "var(--radius-full)",
                  color: "#fff",
                }}
              >
                🩸 {a.donorBloodType}
              </span>
            )}
            {a.relatedWorkflowId && <AgentTag />}
          </div>
        </div>
      </div>

      <div style={{ display: "flex", gap: "var(--space-xs)", marginTop: "var(--space-lg)" }}>
        {isPending && (
          <>
            <button type="button" onClick={onConfirm} style={heroPrimaryButtonStyle}>
              Accept
            </button>
            <button type="button" onClick={onReschedule} style={heroOutlineButtonStyle}>
              Reschedule
            </button>
            <button type="button" onClick={onDecline} style={heroGhostButtonStyle}>
              Decline
            </button>
          </>
        )}
        {isScheduled && (
          <>
            <button type="button" onClick={onCancel} style={heroGhostButtonStyle}>
              Cancel
            </button>
            <button type="button" onClick={onReschedule} style={heroOutlineButtonStyle}>
              Reschedule
            </button>
          </>
        )}
      </div>
    </div>
  );
}

const heroPrimaryButtonStyle: React.CSSProperties = {
  background: "#fff",
  color: "var(--color-primary)",
  border: "none",
  borderRadius: "var(--radius-sm)",
  padding: "8px 14px",
  fontSize: 13,
  fontWeight: 600,
  fontFamily: "var(--font-sans)",
  cursor: "pointer",
};

const heroOutlineButtonStyle: React.CSSProperties = {
  background: "transparent",
  color: "#fff",
  border: "1px solid rgba(255,255,255,0.6)",
  borderRadius: "var(--radius-sm)",
  padding: "8px 14px",
  fontSize: 13,
  fontWeight: 500,
  fontFamily: "var(--font-sans)",
  cursor: "pointer",
};

const heroGhostButtonStyle: React.CSSProperties = {
  background: "transparent",
  color: "rgba(255,255,255,0.85)",
  border: "none",
  borderRadius: "var(--radius-sm)",
  padding: "8px 14px",
  fontSize: 13,
  fontWeight: 500,
  fontFamily: "var(--font-sans)",
  cursor: "pointer",
};

function Row({
  appointment,
  onConfirm,
  onDecline,
  onCancel,
  onReschedule,
}: {
  appointment: Appointment;
  onConfirm?: () => void;
  onDecline?: () => void;
  onCancel?: () => void;
  onReschedule?: () => void;
}) {
  const a = appointment;
  const canConfirmOrDecline = a.status === "pending_confirmation";
  const canCancel = a.status === "scheduled";
  const canReschedule = canConfirmOrDecline || canCancel;
  const hasActions = Boolean((onConfirm && canConfirmOrDecline) || (onCancel && canCancel) || (onReschedule && canReschedule));

  return (
    <div
      style={{
        background: "var(--color-surface)",
        border: "1px solid var(--color-hairline)",
        borderRadius: "var(--radius-md)",
        padding: "var(--space-sm) var(--space-md)",
        display: "flex",
        alignItems: "center",
        gap: "var(--space-sm)",
      }}
    >
      <div style={{ flex: 1, minWidth: 0 }}>
        <div className="text-body-sm" style={{ fontWeight: 600, color: "var(--color-ink)" }}>
          {formatTime(a.scheduledTime)}
        </div>
        <div style={{ display: "flex", gap: 6, marginTop: 4, flexWrap: "wrap", alignItems: "center" }}>
          <StatusBadge status={a.status} />
          {a.status === "completed" && a.unitsDonated != null && (
            <span className="text-caption" style={{ color: "var(--color-ink-muted)" }}>
              {a.unitsDonated} unit{a.unitsDonated === 1 ? "" : "s"}
            </span>
          )}
          {a.relatedWorkflowId && <AgentTag />}
        </div>
      </div>

      {hasActions && (
        <div style={{ display: "flex", gap: 6, flexShrink: 0 }}>
          {onConfirm && canConfirmOrDecline && (
            <RowButton onClick={onConfirm} label="Accept" />
          )}
          {onReschedule && canReschedule && (
            <RowButton onClick={onReschedule} label="Reschedule" />
          )}
          {onDecline && canConfirmOrDecline && (
            <RowButton onClick={onDecline} label="Decline" critical />
          )}
          {onCancel && canCancel && (
            <RowButton onClick={onCancel} label="Cancel" critical />
          )}
        </div>
      )}
    </div>
  );
}

function RowButton({ onClick, label, critical = false }: { onClick: () => void; label: string; critical?: boolean }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className="text-caption"
      style={{
        border: "1px solid var(--color-hairline-strong)",
        background: "var(--color-surface)",
        color: critical ? "var(--color-critical)" : "var(--color-ink-secondary)",
        borderRadius: "var(--radius-xs)",
        padding: "4px 8px",
        cursor: "pointer",
        fontFamily: "var(--font-sans)",
      }}
    >
      {label}
    </button>
  );
}

// ── Action modal (cancel / decline / reschedule) ────────────────────────────

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
    cancel: {
      title: "Cancel this appointment?",
      body: "The slot is released and you can book another any time.",
      confirmLabel: "Cancel appointment",
    },
    decline: {
      title: "Decline this donation?",
      body:
        "You were matched for this slot. Declining releases it so it can be offered to someone else — you can always book or get matched again later.",
      confirmLabel: "Decline",
    },
    reschedule: {
      title: "Pick a new time",
      body: "",
      confirmLabel: "Confirm new time",
    },
  }[action.kind];

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        background: "rgba(20, 27, 44, 0.4)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        zIndex: 50,
      }}
      onClick={onCancel}
    >
      <div
        style={{
          background: "var(--color-surface)",
          borderRadius: "var(--radius-md)",
          padding: "var(--space-lg)",
          width: 360,
          maxWidth: "90vw",
        }}
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-subheading" style={{ margin: "0 0 var(--space-xs)", color: "var(--color-ink)" }}>
          {copy.title}
        </h2>
        {copy.body && (
          <p className="text-body-sm" style={{ color: "var(--color-ink-secondary)", margin: "0 0 var(--space-md)" }}>
            {copy.body}
          </p>
        )}

        {action.kind === "reschedule" && (
          <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-sm)", marginBottom: "var(--space-md)" }}>
            <FieldInput
              label="Date"
              type="date"
              min={today}
              value={rescheduleDate}
              onChange={onRescheduleDateChange}
            />
            <FieldInput
              label="Time"
              type="time"
              value={rescheduleTime}
              onChange={onRescheduleTimeChange}
            />
          </div>
        )}

        {error && (
          <p className="text-body-sm" style={{ color: "var(--color-critical)", margin: "0 0 var(--space-md)" }}>
            {error}
          </p>
        )}

        <div style={{ display: "flex", gap: "var(--space-sm)" }}>
          <button
            type="button"
            onClick={onCancel}
            style={{
              flex: 1,
              background: "var(--color-surface)",
              color: "var(--color-ink-secondary)",
              border: "1px solid var(--color-hairline-strong)",
              borderRadius: "var(--radius-sm)",
              padding: "9px 16px",
              fontSize: 13,
              fontFamily: "var(--font-sans)",
              cursor: "pointer",
            }}
          >
            Keep it
          </button>
          <button
            type="button"
            onClick={onConfirm}
            style={{
              flex: 1,
              background: action.kind === "reschedule" ? "var(--color-primary)" : "var(--color-critical)",
              color: "#fff",
              border: "none",
              borderRadius: "var(--radius-sm)",
              padding: "9px 16px",
              fontSize: 13,
              fontWeight: 500,
              fontFamily: "var(--font-sans)",
              cursor: "pointer",
            }}
          >
            {copy.confirmLabel}
          </button>
        </div>
      </div>
    </div>
  );
}

function FieldInput({
  label,
  type,
  min,
  value,
  onChange,
}: {
  label: string;
  type: string;
  min?: string;
  value: string;
  onChange: (v: string) => void;
}) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 4 }}>
      <label className="text-label" style={{ color: "var(--color-ink-secondary)" }}>
        {label}
      </label>
      <input
        type={type}
        min={min}
        value={value}
        onChange={(e) => onChange(e.target.value)}
        style={{
          fontFamily: "var(--font-sans)",
          fontSize: 14,
          color: "var(--color-ink)",
          background: "var(--color-surface)",
          border: "1px solid var(--color-hairline-strong)",
          borderRadius: "var(--radius-sm)",
          padding: "8px 10px",
        }}
      />
    </div>
  );
}
