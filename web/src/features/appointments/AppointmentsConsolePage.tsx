import { useEffect, useState } from "react";
import { useSelector } from "react-redux";
import type { RootState } from "../../app/store";
import { BloodDrops } from "../../components/blood-effects/BloodDrops";
import {
  useGetUpcomingByOrganizationQuery,
  useCompleteAppointmentMutation,
  useUpdateAppointmentStatusMutation,
  type Appointment,
} from "./appointmentsApi";
import { useGetOrganizationsQuery } from "../organizations/organizationsApi";
import { StatusBadge, STATUS_CONFIG } from "./StatusBadge";
import { AgentTag } from "./AgentTag";
import "./appointments.css";

function formatTime(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
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
  if (diffHrs < 1) return `in ${Math.round(diffMs / 60000)}m`;
  if (diffHrs < 24) return `in ${Math.round(diffHrs)}h`;
  return `in ${Math.round(diffHrs / 24)}d`;
}

const FILTERS = [
  { value: "all", label: "All" },
  { value: "scheduled", label: "Scheduled" },
  { value: "completed", label: "Completed" },
  { value: "no_show", label: "No-show" },
  { value: "cancelled", label: "Cancelled" },
];

function SkeletonTable() {
  return (
    <div>
      {Array.from({ length: 6 }).map((_, i) => (
        <div key={i} className="skeleton-row">
          <div className="skeleton-bar" style={{ width: 90 }} />
          <div className="skeleton-bar" style={{ width: 110 }} />
          <div className="skeleton-bar" style={{ width: 80 }} />
          <div className="skeleton-bar" style={{ width: 60 }} />
        </div>
      ))}
    </div>
  );
}

function EmptyState({ filter }: { filter: string }) {
  return (
    <div
      className="fade-enter"
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        padding: "var(--space-xxl) var(--space-lg)",
        textAlign: "center",
      }}
    >
      <div
        style={{
          width: 40,
          height: 40,
          borderRadius: "var(--radius-full)",
          background: "var(--color-surface-sunken)",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          marginBottom: "var(--space-sm)",
          color: "var(--color-ink-faint)",
          fontSize: 18,
        }}
      >
        ○
      </div>
      <p className="text-body" style={{ color: "var(--color-ink-secondary)", margin: 0 }}>
        No {filter === "all" ? "" : filter.replace("_", " ") + " "}appointments
      </p>
      <p className="text-body-sm" style={{ color: "var(--color-ink-faint)", marginTop: 4 }}>
        {filter === "all" ? "Nothing scheduled at this blood bank right now." : "Try a different filter above."}
      </p>
    </div>
  );
}

export function AppointmentsConsolePage() {
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [selected, setSelected] = useState<Appointment | null>(null);
  const [unitsDonated, setUnitsDonated] = useState("1");

  const organizationId = useSelector((s: RootState) => s.auth.organizationId);
  const role = useSelector((s: RootState) => s.auth.role);
  // role isn't consistently cased across the API (e.g. login returns
  // "Admin", other places return lowercase "admin") — compare
  // case-insensitively rather than assuming one, matching the pattern
  // App.tsx's RequireRole already uses for the same reason.
  const isAdmin = (role ?? "").toLowerCase() === "admin";

  // Staff belong to one org and always see that queue. Admins don't
  // belong to any single org (the backend already allows them to query
  // any org's queue — AppointmentService.GetUpcomingByOrganizationAsync
  // only enforces the same-org check for non-admins), so they pick which
  // one to view instead of hitting a dead end.
  const { data: organizations } = useGetOrganizationsQuery(undefined, { skip: !isAdmin });
  const [selectedOrgId, setSelectedOrgId] = useState<string>("");

  useEffect(() => {
    if (isAdmin && !selectedOrgId && organizations && organizations.length > 0) {
      setSelectedOrgId(organizations[0].id);
    }
  }, [isAdmin, organizations, selectedOrgId]);

  const effectiveOrgId = organizationId ?? (isAdmin ? selectedOrgId : "");

  const { data, isLoading, error, isFetching, refetch } = useGetUpcomingByOrganizationQuery(
    { orgId: effectiveOrgId, page: 1, pageSize: 50 },
    { skip: !effectiveOrgId }
  );

  const [completeAppointment, { isLoading: isCompleting }] = useCompleteAppointmentMutation();
  const [updateStatus, { isLoading: isUpdatingStatus }] = useUpdateAppointmentStatusMutation();

  const appointments = data?.items ?? [];
  const filtered = statusFilter === "all" ? appointments : appointments.filter((a) => a.status === statusFilter);

  const counts = FILTERS.reduce<Record<string, number>>((acc, f) => {
    acc[f.value] = f.value === "all" ? appointments.length : appointments.filter((a) => a.status === f.value).length;
    return acc;
  }, {});

  const agentCount = appointments.filter((a) => a.relatedWorkflowId).length;
  const completedCount = appointments.filter((a) => a.status === "completed").length;
  const completionRate = appointments.length > 0 ? Math.round((completedCount / appointments.length) * 100) : 0;
  const next24h = appointments.filter((a) => {
    const diffHrs = (new Date(a.scheduledTime).getTime() - Date.now()) / (1000 * 60 * 60);
    return diffHrs >= 0 && diffHrs <= 24 && a.status === "scheduled";
  }).length;

  const handleComplete = async () => {
    if (!selected) return;
    await completeAppointment({ id: selected.id, unitsDonated: Number(unitsDonated) }).unwrap();
    setSelected(null);
    setUnitsDonated("1");
  };

  const handleNoShow = async () => {
    if (!selected) return;
    await updateStatus({ id: selected.id, newStatus: "no_show" }).unwrap();
    setSelected(null);
  };

  if (!effectiveOrgId) {
    // Admins aren't tied to one org (organizationId is only ever set for
    // staff), but the backend already lets them query any org's queue —
    // this only shows for donors, or for an admin whose org list hasn't
    // loaded yet / has no organizations registered.
    const message = isAdmin
      ? organizations && organizations.length === 0
        ? "No organizations are registered yet — create one to see an appointment queue."
        : "Loading organizations…"
      : "This console shows a blood bank's appointment queue. Your account isn't linked to an organization, so there is nothing to display here.";

    return (
      <div style={{ padding: "var(--space-xl)", fontFamily: "var(--font-sans)", background: "var(--color-canvas)", height: "100%" }}>
        <h1 className="text-display" style={{ margin: "0 0 var(--space-sm)", color: "var(--color-ink)" }}>
          Upcoming Appointments
        </h1>
        <p className="text-body" style={{ color: "var(--color-ink-secondary)", margin: 0, maxWidth: 440 }}>
          {message}
        </p>
      </div>
    );
  }

  return (
    <div className="appointments-page" style={{ display: "flex", height: "100%", fontFamily: "var(--font-sans)" }}>
      <div
        className="appointments-dot-grid"
        style={{
          flex: 1,
          padding: "var(--space-lg)",
          opacity: selected ? 0.55 : 1,
          transform: selected ? "scale(0.995)" : "scale(1)",
          transition: `opacity var(--duration-base) var(--ease-standard), transform var(--duration-base) var(--ease-standard)`,
          overflowY: "auto",
        }}
      >
        {/* Hero — deliberately built from the same recipe as the
            Organizations page hero (see appointments.css): gradient +
            glow, an orbiting-drop visual, a floating status card, and a
            wave transition the stat cards float over. The refresh button
            and "Live queue" indicator are real (refetch()), not
            decoration — this console has no create action of its own
            (donors book, staff don't), so it stands in for Organizations'
            "+ Add Organization" CTA. */}
        <section className="appointments-hero">
          <BloodDrops count={8} intensity="subtle" />
          <div className="appointments-hero__glow" />

          <div className="appointments-hero__content">
            <div className="appointments-hero__copy">
              <span className="appointments-eyebrow">BLOOD BANK CONSOLE</span>
              <h1>
                Every appointment.
                <br />
                <span>One life closer to saved.</span>
              </h1>
              <p>
                Track, complete and manage donor appointments at your
                organization — from first booking to the moment blood
                reaches someone who needs it.
              </p>
              <div className="appointments-hero__actions">
                {isAdmin && organizations && organizations.length > 0 && (
                  <select
                    value={selectedOrgId}
                    onChange={(e) => setSelectedOrgId(e.target.value)}
                    className="appointments-org-picker"
                    aria-label="Organization"
                  >
                    {organizations.map((org) => (
                      <option key={org.id} value={org.id}>
                        {org.name}
                      </option>
                    ))}
                  </select>
                )}
                <button type="button" onClick={() => refetch()} disabled={isFetching}>
                  {isFetching ? "Refreshing…" : "↻ Refresh queue"}
                </button>
                <div className="appointments-hero__live">
                  <span className="appointments-live-dot" />
                  Live queue data
                </div>
              </div>
            </div>

            <div className="appointments-hero__visual">
              <div className="hero-blood-orbit hero-blood-orbit--one" />
              <div className="hero-blood-orbit hero-blood-orbit--two" />
              <div className="hero-blood-drop" />
              <div className="appointments-hero__queue-card">
                <span>DUE NEXT 24H</span>
                <strong>{next24h}</strong>
                <small>{next24h === 1 ? "appointment" : "appointments"}</small>
              </div>
            </div>
          </div>

          <div className="appointments-hero__wave">
            <div />
            <div />
          </div>
        </section>

        {/* Stats strip — real computed numbers, not decoration. Wrapped
            with a bounded, quiet drop layer (never behind the table
            below, where movement would fight legibility) so the page's
            texture doesn't stop dead the moment the hero ends. */}
        <div className="appointments-stat-grid-wrap">
          <BloodDrops count={5} intensity="subtle" />
          <div className="appointments-stat-grid">
            <div className="appointments-stat-card">
              <div className="appointments-stat-card__icon" style={{ color: "var(--color-urgent)" }}>◷</div>
              <div>
                <span>Due next 24h</span>
                <strong>{next24h}</strong>
              </div>
            </div>
            <div className="appointments-stat-card">
              <div className="appointments-stat-card__icon" style={{ color: "var(--color-agent)" }}>✦</div>
              <div>
                <span>Agent-matched</span>
                <strong>{agentCount}</strong>
              </div>
            </div>
            <div className="appointments-stat-card">
              <div className="appointments-stat-card__icon" style={{ color: "var(--color-success)" }}>✓</div>
              <div>
                <span>Completion rate</span>
                <strong>{completionRate}%</strong>
              </div>
            </div>
            <div className="appointments-stat-card">
              <div className="appointments-stat-card__icon" style={{ color: "var(--color-primary)" }}>Σ</div>
              <div>
                <span>Total</span>
                <strong>{data?.totalCount ?? 0}</strong>
              </div>
            </div>
          </div>
        </div>

        <div style={{ height: "var(--space-lg)" }} />

        <div className="appointments-section-header">
          <div>
            <span className="appointments-section-eyebrow">DONOR QUEUE</span>
            <h2>Today's appointments</h2>
            <p>Filter, complete and manage bookings at your organization.</p>
          </div>
          <div className="appointments-count-badge">
            {appointments.length} appointment{appointments.length === 1 ? "" : "s"}
          </div>
        </div>

        {/* Queue-mix bar — real proportions, not a fixed decorative
            fill: width comes straight from `counts`, the same numbers
            the filter pills below show. */}
        {appointments.length > 0 && (
          <>
            <div className="appointments-mix-bar">
              <span style={{ width: `${(counts.scheduled / appointments.length) * 100}%`, background: "var(--color-urgent)" }} />
              <span style={{ width: `${(counts.completed / appointments.length) * 100}%`, background: "var(--color-success)" }} />
              <span style={{ width: `${(counts.no_show / appointments.length) * 100}%`, background: "var(--color-critical)" }} />
              <span style={{ width: `${(counts.cancelled / appointments.length) * 100}%`, background: "var(--color-ink-faint)" }} />
            </div>
            <div className="appointments-mix-legend">
              <div><i style={{ background: "var(--color-urgent)" }} />Scheduled {counts.scheduled}</div>
              <div><i style={{ background: "var(--color-success)" }} />Completed {counts.completed}</div>
              <div><i style={{ background: "var(--color-critical)" }} />No-show {counts.no_show}</div>
              <div><i style={{ background: "var(--color-ink-faint)" }} />Cancelled {counts.cancelled}</div>
            </div>
          </>
        )}

        <div
          style={{
            display: "flex",
            gap: "var(--space-xs)",
            padding: "var(--space-xs) var(--space-sm)",
            background: "var(--color-surface)",
            border: "1px solid var(--color-hairline)",
            borderRadius: "var(--radius-md)",
            marginBottom: "var(--space-md)",
          }}
        >
          {FILTERS.map((f) => {
            const active = statusFilter === f.value;
            return (
              <button
                key={f.value}
                onClick={() => setStatusFilter(f.value)}
                className="transition-fast"
                style={{
                  display: "flex",
                  alignItems: "center",
                  gap: 6,
                  padding: "6px 12px",
                  fontSize: 13,
                  fontWeight: 500,
                  fontFamily: "var(--font-sans)",
                  border: "1px solid",
                  borderColor: active ? "var(--color-primary)" : "transparent",
                  background: active ? "var(--color-primary-subtle)" : "transparent",
                  color: active ? "var(--color-primary)" : "var(--color-ink-muted)",
                  borderRadius: "var(--radius-sm)",
                  cursor: "pointer",
                }}
              >
                {f.label}
                <span
                  className="tabular-nums"
                  style={{
                    fontSize: 11,
                    padding: "1px 5px",
                    borderRadius: "var(--radius-full)",
                    background: active ? "var(--color-primary)" : "var(--color-surface-sunken)",
                    color: active ? "var(--color-on-primary)" : "var(--color-ink-faint)",
                  }}
                >
                  {counts[f.value] ?? 0}
                </span>
              </button>
            );
          })}
        </div>

        <div style={{ background: "var(--color-surface)", border: "1px solid var(--color-hairline)", borderRadius: "var(--radius-md)", overflow: "hidden" }}>
          {isLoading && <SkeletonTable />}

          {error && (
            <div style={{ padding: "var(--space-lg)" }}>
              <p className="text-body" style={{ color: "var(--color-critical)", margin: 0 }}>
                Could not load appointments. Is the backend running?
              </p>
            </div>
          )}

          {!isLoading && !error && filtered.length === 0 && <EmptyState filter={statusFilter} />}

          {!isLoading && !error && filtered.length > 0 && (
            <table style={{ width: "100%", borderCollapse: "collapse" }}>
              <thead>
                <tr style={{ background: "var(--color-surface-sunken)" }}>
                  {["", "Time", "Donor", "Blood Type", "Status", "Source", ""].map((h, i) => (
                    <th
                      key={i}
                      className="text-label"
                      style={{
                        textAlign: "left",
                        padding: "10px 16px",
                        color: "var(--color-ink-muted)",
                        borderBottom: "1px solid var(--color-hairline-strong)",
                        width: i === 0 ? 3 : undefined,
                      }}
                    >
                      {h}
                    </th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {filtered.map((appt) => {
                  const isSelected = selected?.id === appt.id;
                  const accentColor = STATUS_CONFIG[appt.status]?.color ?? STATUS_CONFIG.cancelled.color;
                  return (
                    <tr
                      key={appt.id}
                      onClick={() => setSelected(appt)}
                      className="transition-fast"
                      style={{
                        cursor: "pointer",
                        borderBottom: "1px solid var(--color-hairline)",
                        background: isSelected ? "var(--color-primary-subtle)" : "var(--color-surface)",
                      }}
                      onMouseEnter={(e) => {
                        if (!isSelected) e.currentTarget.style.background = "var(--color-surface-sunken)";
                      }}
                      onMouseLeave={(e) => {
                        if (!isSelected) e.currentTarget.style.background = "var(--color-surface)";
                      }}
                    >
                      <td style={{ padding: 0 }}>
                        <div style={{ width: 3, height: 44, background: accentColor }} />
                      </td>
                      <td style={{ padding: "12px 16px" }}>
                        <div className="tabular-nums text-body-sm">{formatTime(appt.scheduledTime)}</div>
                        <div className="text-caption" style={{ color: "var(--color-ink-faint)" }}>
                          {formatRelative(appt.scheduledTime)}
                        </div>
                      </td>
                      <td className="tabular-nums text-body-sm" style={{ padding: "12px 16px", color: "var(--color-ink-secondary)" }}>
                        #{appt.donorId.slice(0, 8)}
                      </td>
                      <td style={{ padding: "12px 16px" }}>
                        {appt.donorBloodType ? (
                          <span className="text-body-sm appointments-blood-chip">
                            🩸 {appt.donorBloodType}
                          </span>
                        ) : (
                          <span className="text-body-sm" style={{ color: "var(--color-ink-faint)" }}>
                            —
                          </span>
                        )}
                      </td>
                      <td style={{ padding: "12px 16px" }}>
                        <StatusBadge status={appt.status} />
                      </td>
                      <td style={{ padding: "12px 16px" }}>
                        {appt.relatedWorkflowId ? (
                          <AgentTag />
                        ) : (
                          <span className="text-caption" style={{ color: "var(--color-ink-faint)" }}>
                            Direct
                          </span>
                        )}
                      </td>
                      <td style={{ padding: "12px 16px", color: "var(--color-ink-faint)", fontSize: 14 }}>›</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          )}
        </div>

        {/* Matching & Dispatch Agent — this screen's own equivalent of
            Inventory's "AI Inventory Intelligence" section, scoped
            honestly to what this agent actually has: one agent, two
            modes, and Mode 2 genuinely isn't live yet. */}
        <section className="appointments-ai-section">
          <div className="appointments-ai-heading">
            <div>
              <span className="appointments-section-eyebrow" style={{ color: "var(--color-agent)" }}>
                MATCHING &amp; DISPATCH AGENT
              </span>
              <h2>What's finding your donors</h2>
              <p>
                Every appointment tagged "Agent" above was booked through this
                agent's donor-ranking search rather than a walk-in booking.
              </p>
            </div>
            <div className="appointments-ai-badge">
              <i />
              {agentCount} matched
            </div>
          </div>

          <div className="appointments-ai-grid">
            <div className="appointments-ai-card">
              <div className="appointments-ai-card__top">
                <div className="appointments-ai-icon">⌕</div>
                <span className="appointments-ai-status appointments-ai-status--ready">READY</span>
              </div>
              <div>
                <span className="appointments-ai-kicker">MODE 01</span>
                <h3>Donor Search &amp; Ranking</h3>
                <p>
                  Ranks nearby, verified donors by proximity, urgency and
                  reliability score whenever a workflow requests candidates.
                </p>
              </div>
            </div>

            <div className="appointments-ai-card appointments-ai-card--pending">
              <div className="appointments-ai-card__top">
                <div className="appointments-ai-icon appointments-ai-icon--pending">⇢</div>
                <span className="appointments-ai-status appointments-ai-status--pending">PENDING</span>
              </div>
              <div>
                <span className="appointments-ai-kicker" style={{ color: "var(--color-ink-muted)" }}>MODE 02</span>
                <h3>Notify &amp; Dispatch</h3>
                <p>
                  Sends push alerts and creates appointment invites once a
                  workflow is approved — waiting on the shared workflow/
                  approval tables before this can go live.
                </p>
              </div>
            </div>
          </div>

          <div className="appointments-ai-note">
            Ranking weights: 50% proximity, 20% urgency, 30% donor reliability.
          </div>
        </section>
      </div>

      {selected && (
        <div
          key={selected.id}
          className="panel-enter"
          style={{
            width: 420,
            background: "var(--color-surface)",
            borderLeft: "1px solid var(--color-hairline)",
            boxShadow: "0 4px 16px rgba(23, 32, 51, 0.10), 0 1px 3px rgba(23, 32, 51, 0.06)",
            padding: "var(--space-lg)",
            overflowY: "auto",
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "start" }}>
            <div>
              <p className="text-caption" style={{ color: "var(--color-ink-faint)", margin: 0, marginBottom: 2 }}>
                DONOR
              </p>
              <h2 className="text-heading tabular-nums" style={{ margin: 0, color: "var(--color-ink)" }}>
                #{selected.donorId.slice(0, 8)}
              </h2>
            </div>
            <button
              onClick={() => setSelected(null)}
              className="transition-fast"
              style={{
                border: "none",
                background: "var(--color-surface-sunken)",
                width: 28,
                height: 28,
                borderRadius: "var(--radius-full)",
                cursor: "pointer",
                fontSize: 16,
                color: "var(--color-ink-muted)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
              }}
            >
              ×
            </button>
          </div>

          <div style={{ marginTop: "var(--space-sm)", display: "flex", alignItems: "center", gap: 8 }}>
            <StatusBadge status={selected.status} />
            <span className="text-body-sm tabular-nums" style={{ color: "var(--color-ink-muted)" }}>
              {formatTime(selected.scheduledTime)} · {formatRelative(selected.scheduledTime)}
            </span>
          </div>

          <div style={{ marginTop: "var(--space-sm)", display: "flex", gap: "var(--space-lg)" }}>
            <div>
              <p className="text-label" style={{ color: "var(--color-ink-muted)", margin: 0 }}>
                BLOOD TYPE
              </p>
              <p className="text-body-sm" style={{ margin: "2px 0 0", color: "var(--color-ink)", fontWeight: 500 }}>
                {selected.donorBloodType ?? "—"}
              </p>
            </div>
            {selected.unitsDonated != null && (
              <div>
                <p className="text-label" style={{ color: "var(--color-ink-muted)", margin: 0 }}>
                  UNITS DONATED
                </p>
                <p className="text-body-sm tabular-nums" style={{ margin: "2px 0 0", color: "var(--color-ink)", fontWeight: 500 }}>
                  {selected.unitsDonated}
                </p>
              </div>
            )}
          </div>

          {selected.relatedWorkflowId && (
            <div
              style={{
                marginTop: "var(--space-md)",
                padding: "var(--space-sm)",
                background: "var(--color-agent-subtle)",
                borderRadius: "var(--radius-sm)",
                borderLeft: "2px solid var(--color-agent)",
              }}
            >
              <p className="text-subheading" style={{ color: "var(--color-agent)", margin: 0 }}>
                Agent reasoning
              </p>
              <p className="text-body-sm" style={{ color: "var(--color-ink-muted)", marginTop: 4, marginBottom: 0 }}>
                Created by the Matching &amp; Dispatch Agent. Rank/distance/score
                display pending — requires reading the agent_steps log for this
                workflow, not yet built.
              </p>
            </div>
          )}

          {selected.status === "scheduled" && (
            <div style={{ marginTop: "var(--space-lg)" }}>
              <p className="text-subheading" style={{ marginBottom: "var(--space-xs)", color: "var(--color-ink)" }}>
                Complete appointment
              </p>
              <label className="text-label" style={{ color: "var(--color-ink-muted)", display: "block", marginBottom: 4 }}>
                Units donated
              </label>
              <input
                type="number"
                min={1}
                value={unitsDonated}
                onChange={(e) => setUnitsDonated(e.target.value)}
                className="tabular-nums transition-fast"
                style={{
                  width: "100%",
                  padding: "8px 12px",
                  border: "1px solid var(--color-hairline-strong)",
                  borderRadius: "var(--radius-sm)",
                  fontSize: 14,
                  fontFamily: "var(--font-sans)",
                  marginBottom: "var(--space-sm)",
                  outline: "none",
                }}
                onFocus={(e) => {
                  e.currentTarget.style.borderColor = "var(--color-primary)";
                  e.currentTarget.style.boxShadow = "0 0 0 3px var(--color-primary-subtle)";
                }}
                onBlur={(e) => {
                  e.currentTarget.style.borderColor = "var(--color-hairline-strong)";
                  e.currentTarget.style.boxShadow = "none";
                }}
              />
              <button
                onClick={handleComplete}
                disabled={isCompleting}
                className="transition-fast"
                style={{
                  width: "100%",
                  padding: "8px 14px",
                  background: isCompleting ? "var(--color-primary-hover)" : "var(--color-primary)",
                  color: "var(--color-on-primary)",
                  border: "none",
                  borderRadius: "var(--radius-sm)",
                  fontSize: 13,
                  fontWeight: 500,
                  fontFamily: "var(--font-sans)",
                  cursor: isCompleting ? "default" : "pointer",
                  marginBottom: "var(--space-xs)",
                }}
              >
                {isCompleting ? "Completing…" : "Complete"}
              </button>
              <button
                onClick={handleNoShow}
                disabled={isUpdatingStatus}
                className="transition-fast"
                style={{
                  width: "100%",
                  padding: "8px 14px",
                  background: "var(--color-surface)",
                  color: "var(--color-critical)",
                  border: "1px solid var(--color-critical)",
                  borderRadius: "var(--radius-sm)",
                  fontSize: 13,
                  fontWeight: 500,
                  fontFamily: "var(--font-sans)",
                  cursor: isUpdatingStatus ? "default" : "pointer",
                }}
              >
                {isUpdatingStatus ? "Updating…" : "Mark no-show"}
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}