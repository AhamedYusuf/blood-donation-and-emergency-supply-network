import { useState } from "react";
import {
  useSearchDonorsQuery,
  useUpdateDonorProfileMutation,
  useVerifyDonorMutation,
  type DonorProfileResponse,
} from "./donorApi";

// ── Status config — single source of truth (mirrors frontend.md) ─────────────

const STATUS_CONFIG: Record<
  string,
  { label: string; color: string; bg: string }
> = {
  eligible: {
    label: "Eligible",
    color: "var(--color-success)",
    bg: "var(--color-success-subtle)",
  },
  not_eligible: {
    label: "Not eligible",
    color: "var(--color-critical)",
    bg: "var(--color-critical-subtle)",
  },
  pending_review: {
    label: "Pending review",
    color: "var(--color-urgent)",
    bg: "var(--color-urgent-subtle)",
  },
};

const MEDICAL_FLAG_LABELS: Record<string, string> = {
  recent_illness: "Recent illness",
  recent_surgery: "Recent surgery",
  chronic_condition: "Chronic condition",
  hiv_positive: "HIV positive",
  hepatitis: "Hepatitis",
};

function StatusBadge({ status }: { status: string }) {
  const cfg = STATUS_CONFIG[status] ?? {
    label: status,
    color: "var(--color-neutral-status)",
    bg: "var(--color-neutral-status-subtle)",
  };
  return (
    <span
      className="text-caption"
      style={{
        background: cfg.bg,
        color: cfg.color,
        borderRadius: "var(--radius-full)",
        padding: "3px 9px",
        whiteSpace: "nowrap",
      }}
    >
      {cfg.label}
    </span>
  );
}

// ── Skeleton row ──────────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <div className="skeleton-row">
      <div className="skeleton-bar" style={{ width: 3, flexShrink: 0 }} />
      <div className="skeleton-bar" style={{ width: 60 }} />
      <div className="skeleton-bar" style={{ flex: 1 }} />
      <div className="skeleton-bar" style={{ width: 80 }} />
      <div className="skeleton-bar" style={{ width: 100 }} />
      <div className="skeleton-bar" style={{ width: 72 }} />
    </div>
  );
}

// ── Row ───────────────────────────────────────────────────────────────────────

function DonorRow({ donor }: { donor: DonorProfileResponse }) {
  const [verify, { isLoading }] = useVerifyDonorMutation();
  const [updateProfile, { isLoading: isSavingMedicalFlags }] = useUpdateDonorProfileMutation();
  const [hovered, setHovered] = useState(false);
  const [editingMedicalFlags, setEditingMedicalFlags] = useState(false);
  const [medicalFlags, setMedicalFlags] = useState<Record<string, boolean>>(donor.medicalFlags ?? {});
  const [medicalError, setMedicalError] = useState<string | null>(null);

  const handleVerify = async () => {
    try {
      await verify(donor.id).unwrap();
    } catch {
      // Error feedback handled globally; the row disappears on success via cache invalidation
    }
  };

  const handleSaveMedicalFlags = async () => {
    setMedicalError(null);
    try {
      await updateProfile({ id: donor.id, body: { medicalFlags } }).unwrap();
      setEditingMedicalFlags(false);
    } catch {
      setMedicalError("Could not save medical flags.");
    }
  };

  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        display: "grid",
        gridTemplateColumns: "3px 56px 1fr 1fr 130px 190px",
        alignItems: "center",
        gap: 16,
        padding: "12px 16px",
        borderBottom: "1px solid var(--color-hairline)",
        background: hovered
          ? "var(--color-surface-sunken)"
          : "var(--color-surface)",
        transition: `background var(--duration-fast) var(--ease-standard)`,
      }}
    >
      {/* Accent bar — always urgent/pending for this queue */}
      <div
        style={{
          alignSelf: "stretch",
          background: "var(--color-urgent)",
          borderRadius: 2,
          minHeight: 20,
        }}
      />

      {/* Blood type */}
      <span
        className="text-body-sm tabular-nums"
        style={{ fontWeight: 600, color: "var(--color-ink)" }}
      >
        {donor.bloodType}
      </span>

      {/* Donor name + secondary identifiers and address */}
      <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 0 }}>
        <span
          className="text-body-sm tabular-nums"
          style={{
            color: "var(--color-ink-secondary)",
            overflow: "hidden",
            textOverflow: "ellipsis",
            whiteSpace: "nowrap",
          }}
        >
          {donor.fullName || "Unnamed donor"}
        </span>
        <span
          className="text-caption tabular-nums"
          style={{ color: "var(--color-ink-faint)", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}
        >
          Profile ID: {donor.id} · Account ID: {donor.userId}
        </span>
        {donor.address && (
          <span
            className="text-caption"
            style={{
              color: "var(--color-ink-faint)",
              overflow: "hidden",
              textOverflow: "ellipsis",
              whiteSpace: "nowrap",
            }}
          >
            {donor.address}
          </span>
        )}
        <span className="text-caption" style={{ color: "var(--color-ink-faint)" }}>
          {Object.entries(donor.medicalFlags ?? {})
            .filter(([, active]) => active)
            .map(([key]) => MEDICAL_FLAG_LABELS[key] ?? key)
            .join(", ") || "No medical conditions reported"}
        </span>
        <span className="text-caption" style={{ color: "var(--color-ink-faint)" }}>
          {donor.locationVerified && donor.latitude != null && donor.longitude != null
            ? `Location verified (${donor.latitude.toFixed(4)}, ${donor.longitude.toFixed(4)})`
            : "Location not verified"}
        </span>
      </div>

      {/* DOB */}
      <span className="text-body-sm tabular-nums" style={{ color: "var(--color-ink-muted)" }}>
        {donor.dateOfBirth}
      </span>

      {/* Eligibility status */}
      <StatusBadge status={donor.eligibilityStatus} />

      <div style={{ display: "flex", flexDirection: "column", alignItems: "stretch", gap: 6 }}>
        {!donor.verifiedByAdmin && (
          <button
            onClick={handleVerify}
            disabled={isLoading}
            style={{ background: isLoading ? "var(--color-primary-press)" : "var(--color-primary)", color: "var(--color-on-primary)", border: 0, borderRadius: "var(--radius-sm)", padding: "6px 12px", fontSize: 12, fontWeight: 500, fontFamily: "var(--font-sans)", cursor: isLoading ? "not-allowed" : "pointer" }}
          >
            {isLoading ? "Verifying…" : "✓ Approve donor"}
          </button>
        )}
        <button
          type="button"
          onClick={() => setEditingMedicalFlags((current) => !current)}
          style={{ background: "var(--color-surface)", color: "var(--color-ink-secondary)", border: "1px solid var(--color-hairline-strong)", borderRadius: "var(--radius-sm)", padding: "6px 12px", fontSize: 12, fontWeight: 500, fontFamily: "var(--font-sans)", cursor: "pointer" }}
        >
          {editingMedicalFlags ? "Close medical flags" : "Flag medical illness"}
        </button>
        {editingMedicalFlags && (
          <div style={{ display: "flex", flexDirection: "column", gap: 5, padding: 8, background: "var(--color-surface-sunken)", borderRadius: "var(--radius-sm)" }}>
            {Object.entries(MEDICAL_FLAG_LABELS).map(([key, label]) => (
              <label key={key} className="text-caption" style={{ display: "flex", gap: 6, alignItems: "center", color: "var(--color-ink-secondary)" }}>
                <input
                  type="checkbox"
                  checked={Boolean(medicalFlags[key])}
                  onChange={(event) => setMedicalFlags((current) => ({ ...current, [key]: event.target.checked }))}
                  style={{ accentColor: "var(--color-primary)" }}
                />
                {label}
              </label>
            ))}
            {medicalError && <span className="text-caption" style={{ color: "var(--color-critical)" }}>{medicalError}</span>}
            <button type="button" onClick={handleSaveMedicalFlags} disabled={isSavingMedicalFlags} style={{ background: "var(--color-primary)", color: "var(--color-on-primary)", border: 0, borderRadius: "var(--radius-sm)", padding: "5px 8px", fontSize: 12, cursor: "pointer" }}>
              {isSavingMedicalFlags ? "Saving…" : "Save flags"}
            </button>
          </div>
        )}
      </div>
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function DonorVerificationQueuePage() {
  const [view, setView] = useState<"pending" | "all">("pending");
  const { data, isLoading, isError, error, refetch } = useSearchDonorsQuery({
    pageSize: 1000,
  });
  const unverified = (data?.items ?? []).filter((d) => !d.verifiedByAdmin);
  const visibleDonors = view === "pending" ? unverified : (data?.items ?? []);
  const errorStatus = typeof error === "object" && error !== null && "status" in error
    ? String(error.status)
    : "";
  const errorMessage = errorStatus === "401" || errorStatus === "403"
    ? "Your admin session is not authorized to view donors. Log out and sign in again with an admin account."
    : errorStatus === "FETCH_ERROR"
      ? "The donor service could not be reached. Check that the backend is running."
      : "Failed to load donors. Try refreshing.";

  return (
    <div
      style={{
        height: "100%",
        overflow: "auto",
        background: "var(--color-canvas)",
        fontFamily: "var(--font-sans)",
      }}
    >
      {/* Page header */}
      <div
        style={{
          padding: "var(--space-lg) var(--space-lg) var(--space-md)",
          display: "flex",
          alignItems: "baseline",
          justifyContent: "space-between",
        }}
      >
        <div>
          <h1 className="text-display" style={{ margin: 0, color: "var(--color-ink)" }}>
            Donor verification
          </h1>
          <p
            className="text-body-sm"
            style={{ margin: "4px 0 0", color: "var(--color-ink-muted)" }}
          >
            Review registration status and maintain donor medical information.
          </p>
        </div>
        <button
          onClick={() => refetch()}
          className="transition-fast"
          style={{
            background: "var(--color-surface)",
            border: "1px solid var(--color-hairline-strong)",
            borderRadius: "var(--radius-sm)",
            padding: "7px 12px",
            fontSize: 12,
            fontWeight: 500,
            fontFamily: "var(--font-sans)",
            color: "var(--color-ink-secondary)",
            cursor: "pointer",
          }}
          onMouseEnter={(e) =>
            (e.currentTarget.style.background = "var(--color-surface-sunken)")
          }
          onMouseLeave={(e) =>
            (e.currentTarget.style.background = "var(--color-surface)")
          }
        >
          ↺ Refresh
        </button>
      </div>

      <div style={{ display: "flex", gap: 8, padding: "0 var(--space-lg) var(--space-md)" }}>
        {(["pending", "all"] as const).map((option) => (
          <button key={option} type="button" onClick={() => setView(option)} style={{ border: "1px solid var(--color-hairline-strong)", borderRadius: "var(--radius-sm)", padding: "6px 10px", background: view === option ? "var(--color-ink)" : "var(--color-surface)", color: view === option ? "#fff" : "var(--color-ink-secondary)", font: "inherit", fontSize: 12, cursor: "pointer" }}>
            {option === "pending" ? `Pending verification (${unverified.length})` : `All donors (${data?.items.length ?? 0})`}
          </button>
        ))}
      </div>

      {/* Counter badge */}
      {!isLoading && !isError && (
        <div style={{ padding: "0 var(--space-lg) var(--space-md)" }}>
          <span
            className="text-caption"
            style={{
              background: unverified.length > 0
                ? "var(--color-urgent-subtle)"
                : "var(--color-success-subtle)",
              color: unverified.length > 0
                ? "var(--color-urgent)"
                : "var(--color-success)",
              borderRadius: "var(--radius-full)",
              padding: "3px 10px",
            }}
          >
            {unverified.length > 0
              ? `${unverified.length} pending verification`
              : "All donors verified"}
          </span>
        </div>
      )}

      {/* Table */}
      <div
        style={{
          margin: "0 var(--space-lg)",
          background: "var(--color-surface)",
          border: "1px solid var(--color-hairline)",
          borderRadius: "var(--radius-md)",
          overflow: "hidden",
        }}
      >
        {/* Table header */}
        <div
          style={{
            display: "grid",
            gridTemplateColumns: "3px 56px minmax(220px, 1fr) 1fr 130px 210px",
            gap: 16,
            padding: "10px 16px",
            background: "var(--color-surface-sunken)",
            borderBottom: "1px solid var(--color-hairline-strong)",
          }}
        >
          <div />
          {["Blood", "Donor name / Address", "Date of birth", "Status", "Admin actions"].map(
            (h) => (
              <span
                key={h}
                className="text-label"
                style={{ color: "var(--color-ink-muted)" }}
              >
                {h}
              </span>
            )
          )}
        </div>

        {/* Loading state */}
        {isLoading && Array.from({ length: 6 }).map((_, i) => <SkeletonRow key={i} />)}

        {/* Error state */}
        {isError && (
          <div
            className="fade-enter"
            style={{
              padding: "var(--space-xxl)",
              textAlign: "center",
            }}
          >
            <div
              style={{
                width: 40,
                height: 40,
                borderRadius: "var(--radius-full)",
                background: "var(--color-critical-subtle)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                margin: "0 auto var(--space-sm)",
                fontSize: 18,
                color: "var(--color-critical)",
              }}
            >
              ✕
            </div>
            <p className="text-body-sm" style={{ color: "var(--color-critical)", margin: 0 }}>
              {errorMessage}
            </p>
          </div>
        )}

        {/* Empty state */}
        {!isLoading && !isError && visibleDonors.length === 0 && (
          <div
            className="fade-enter"
            style={{
              padding: "var(--space-xxl)",
              textAlign: "center",
            }}
          >
            <div
              style={{
                width: 40,
                height: 40,
                borderRadius: "var(--radius-full)",
                background: "var(--color-success-subtle)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                margin: "0 auto var(--space-sm)",
                fontSize: 20,
                color: "var(--color-success)",
              }}
            >
              ✓
            </div>
            <p
              className="text-subheading"
              style={{ color: "var(--color-ink)", margin: "0 0 4px" }}
            >
              {view === "pending" ? "Queue is clear" : "No donors registered"}
            </p>
            <p
              className="text-body-sm"
              style={{ color: "var(--color-ink-muted)", margin: 0 }}
            >
              {view === "pending" ? "All registered donors have been verified." : "Registered donors will appear here."}
            </p>
          </div>
        )}

        {/* Rows */}
        {!isLoading &&
          !isError &&
          visibleDonors.map((donor) => <DonorRow key={donor.id} donor={donor} />)}
      </div>

      <style>{`@keyframes spin { to { transform: rotate(360deg); } }`}</style>
    </div>
  );
}
