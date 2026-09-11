import "./blood-effects.css";

type BloodDropsProps = {
  count?: number;
  intensity?: "subtle" | "normal" | "strong";
  className?: string;
};

const DROP_POSITIONS = [
  { left: "3%", delay: "0s", duration: "8s", size: 18, opacity: 0.42 },
  { left: "10%", delay: "2.4s", duration: "10s", size: 12, opacity: 0.3 },
  { left: "18%", delay: "1.2s", duration: "7.5s", size: 24, opacity: 0.5 },
  { left: "27%", delay: "4.1s", duration: "11s", size: 10, opacity: 0.26 },
  { left: "35%", delay: "0.8s", duration: "9s", size: 16, opacity: 0.36 },
  { left: "44%", delay: "3.3s", duration: "8.5s", size: 11, opacity: 0.28 },
  { left: "53%", delay: "1.8s", duration: "10.5s", size: 22, opacity: 0.46 },
  { left: "62%", delay: "5s", duration: "7.8s", size: 13, opacity: 0.32 },
  { left: "71%", delay: "2.1s", duration: "9.8s", size: 20, opacity: 0.42 },
  { left: "79%", delay: "4.7s", duration: "8.2s", size: 11, opacity: 0.3 },
  { left: "87%", delay: "0.4s", duration: "10.8s", size: 25, opacity: 0.5 },
  { left: "94%", delay: "3.7s", duration: "7.2s", size: 14, opacity: 0.34 },
];

export function BloodDrops({
  count = 8,
  intensity = "normal",
  className = "",
}: BloodDropsProps) {
  const drops = DROP_POSITIONS.slice(0, Math.min(count, DROP_POSITIONS.length));

  return (
    <div
      className={`blood-drops blood-drops--${intensity} ${className}`}
      aria-hidden="true"
    >
      {drops.map((drop, index) => (
        <span
          key={`${drop.left}-${index}`}
          className="blood-drop"
          style={
            {
              "--drop-left": drop.left,
              "--drop-delay": drop.delay,
              "--drop-duration": drop.duration,
              "--drop-size": `${drop.size}px`,
              "--drop-opacity": drop.opacity,
            } as React.CSSProperties
          }
        >
          <span className="blood-drop__shape" />
        </span>
      ))}
    </div>
  );
}