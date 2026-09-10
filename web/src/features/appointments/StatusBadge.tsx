export const STATUS_CONFIG: Record<string, { label: string; color: string; bg: string }> = {
  scheduled: { label: "Scheduled", color: "var(--color-urgent)", bg: "var(--color-urgent-subtle)" },
  completed: { label: "Completed", color: "var(--color-success)", bg: "var(--color-success-subtle)" },
  no_show: { label: "No-show", color: "var(--color-critical)", bg: "var(--color-critical-subtle)" },
  cancelled: { label: "Cancelled", color: "var(--color-neutral-status)", bg: "var(--color-neutral-status-subtle)" },
};

export function StatusBadge({ status }: { status: string }) {
  const config = STATUS_CONFIG[status] ?? STATUS_CONFIG.cancelled;
  return (
    <span
      style={{
        display: "inline-flex",
        alignItems: "center",
        gap: 4,
        padding: "3px 9px",
        borderRadius: "var(--radius-full)",
        fontSize: 11,
        fontWeight: 500,
        letterSpacing: "0.3px",
        color: config.color,
        background: config.bg,
      }}
    >
      <span style={{ width: 6, height: 6, borderRadius: "50%", background: config.color }} />
      {config.label}
    </span>
  );
}