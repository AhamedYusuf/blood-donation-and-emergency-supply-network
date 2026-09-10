export function AgentTag() {
  return (
    <span
      style={{
        display: "inline-block",
        padding: "2px 6px",
        borderRadius: "var(--radius-xs)",
        fontSize: 11,
        fontWeight: 500,
        letterSpacing: "0.3px",
        color: "var(--color-agent)",
        background: "var(--color-agent-subtle)",
      }}
    >
      Agent
    </span>
  );
}