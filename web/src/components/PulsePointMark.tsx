interface PulsePointMarkProps {
  size?: number;
  /** Badge background. Defaults to the brand primary. */
  background?: string;
  /** Pulse line + point color. Defaults to on-primary (white). */
  foreground?: string;
  className?: string;
}

/**
 * The PulsePoint mark: a pulse line (the "Pulse") that settles into a dot
 * (the "Point") on a rounded badge. Used wherever the product needs a
 * logo — the login screen, the sidebar brand, anywhere else a standalone
 * mark is needed — so there's exactly one drawing of it to keep in sync.
 */
export function PulsePointMark({
  size = 36,
  background = "var(--color-primary)",
  foreground = "var(--color-on-primary)",
  className,
}: PulsePointMarkProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 36 36"
      fill="none"
      className={className}
      role="img"
      aria-label="PulsePoint"
    >
      <rect width="36" height="36" rx="10" fill={background} />
      <path
        d="M5 19H10.5L13.5 13.5L17 25.5L20.5 7.5L24 22H27.5"
        stroke={foreground}
        strokeWidth="2.25"
        strokeLinecap="round"
        strokeLinejoin="round"
        fill="none"
      />
      <circle cx="29.5" cy="22" r="2.25" fill={foreground} />
    </svg>
  );
}
