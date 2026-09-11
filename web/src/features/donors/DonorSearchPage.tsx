import { useState } from "react";
import {
  useSearchDonorsQuery,
  BLOOD_TYPE_OPTIONS,
  type DonorProfileResponse,
} from "./donorApi";

// ── Status config ─────────────────────────────────────────────────────────────

const STATUS_CONFIG: Record<
  string,
  { label: string; color: string; bg: string; accent: string }
> = {
  eligible: {
    label: "Eligible",
    color: "var(--color-success)",
    bg: "var(--color-success-subtle)",
    accent: "var(--color-success)",
  },
  not_eligible: {
    label: "Not eligible",
    color: "var(--color-critical)",
    bg: "var(--color-critical-subtle)",
    accent: "var(--color-critical)",
  },
  pending_review: {
    label: "Pending review",
    color: "var(--color-urgent)",
    bg: "var(--color-urgent-subtle)",
    accent: "var(--color-urgent)",
  },
};

function StatusBadge({ status }: { status: string }) {
  const cfg = STATUS_CONFIG[status] ?? {
    label: status,
    color: "var(--color-neutral-status)",
    bg: "var(--color-neutral-status-subtle)",
    accent: "var(--color-neutral-status)",
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

function VerifiedChip({ verified }: { verified: boolean }) {
  return (
    <span
      className="text-caption"
      style={{
        background: verified
          ? "var(--color-success-subtle)"
          : "var(--color-surface-sunken)",
        color: verified
          ? "var(--color-success)"
          : "var(--color-ink-faint)",
        borderRadius: "var(--radius-xs)",
        padding: "2px 6px",
        whiteSpace: "nowrap",
      }}
    >
      {verified ? "Admin verified" : "Unverified"}
    </span>
  );
}

// ── Skeleton ──────────────────────────────────────────────────────────────────

function SkeletonRow() {
  return (
    <div className="skeleton-row">
      <div className="skeleton-bar" style={{ width: 3, flexShrink: 0 }} />
      <div className="skeleton-bar" style={{ width: 48 }} />
      <div className="skeleton-bar" style={{ flex: 1 }} />
      <div className="skeleton-bar" style={{ width: 90 }} />
      <div className="skeleton-bar" style={{ width: 100 }} />
      <div className="skeleton-bar" style={{ width: 90 }} />
    </div>
  );
}

// ── Donor row ─────────────────────────────────────────────────────────────────

function DonorRow({ donor }: { donor: DonorProfileResponse }) {
  const [hovered, setHovered] = useState(false);
  const cfg =
    STATUS_CONFIG[donor.eligibilityStatus] ?? STATUS_CONFIG["pending_review"];

  return (
    <div
      onMouseEnter={() => setHovered(true)}
      onMouseLeave={() => setHovered(false)}
      style={{
        display: "grid",
        gridTemplateColumns: "3px 56px 1fr 110px 130px 110px",
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
      {/* Accent bar — matches status color */}
      <div
        style={{
          alignSelf: "stretch",
          background: cfg.accent,
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

      {/* ID + address */}
      <div style={{ display: "flex", flexDirection: "column", gap: 2, minWidth: 0 }}>
        <span
          className="text-body-sm tabular-nums"
          style={{ color: "var(--color-ink-secondary)" }}
        >
          {donor.id.slice(0, 8).toUpperCase()}
        </span>
        {donor.address ? (
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
        ) : (
          <span className="text-caption" style={{ color: "var(--color-ink-faint)" }}>
            No address
          </span>
        )}
      </div>

      {/* DOB */}
      <span
        className="text-body-sm tabular-nums"
        style={{ color: "var(--color-ink-muted)" }}
      >
        {donor.dateOfBirth}
      </span>

      {/* Status */}
      <StatusBadge status={donor.eligibilityStatus} />

      {/* Verified chip */}
      <VerifiedChip verified={donor.verifiedByAdmin} />
    </div>
  );
}

// ── Pagination controls ───────────────────────────────────────────────────────

function PaginationBar({
  page,
  pageSize,
  total,
  onPage,
}: {
  page: number;
  pageSize: number;
  total: number;
  onPage: (p: number) => void;
}) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  if (totalPages <= 1) return null;

  const btnBase: React.CSSProperties = {
    background: "var(--color-surface)",
    border: "1px solid var(--color-hairline-strong)",
    borderRadius: "var(--radius-sm)",
    padding: "5px 10px",
    fontSize: 12,
    fontWeight: 500,
    fontFamily: "var(--font-sans)",
    color: "var(--color-ink-secondary)",
    cursor: "pointer",
  };

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: "var(--space-md) var(--space-lg)",
      }}
    >
      <span
        className="text-caption"
        style={{ color: "var(--color-ink-faint)" }}
      >
        {total} donors · page {page} of {totalPages}
      </span>
      <div style={{ display: "flex", gap: 6 }}>
        <button
          style={btnBase}
          disabled={page <= 1}
          onClick={() => onPage(page - 1)}
        >
          ← Prev
        </button>
        <button
          style={btnBase}
          disabled={page >= totalPages}
          onClick={() => onPage(page + 1)}
        >
          Next →
        </button>
      </div>
    </div>
  );
}

// ── Page ──────────────────────────────────────────────────────────────────────

export function DonorSearchPage() {
  const [bloodTypeFilter, setBloodTypeFilter] = useState("");
  const [page, setPage] = useState(1);

  const { data, isLoading, isError, isFetching } = useSearchDonorsQuery({
    bloodType: bloodTypeFilter || undefined,
    page,
    pageSize: 20,
  });

  const handleBloodTypeChange = (v: string) => {
    setBloodTypeFilter(v);
    setPage(1); // reset to page 1 on filter change
  };

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
      <div style={{ padding: "var(--space-lg) var(--space-lg) var(--space-md)" }}>
        <h1
          className="text-display"
          style={{ margin: "0 0 4px", color: "var(--color-ink)" }}
        >
          Donor Search
        </h1>
        <p
          className="text-body-sm"
          style={{ margin: 0, color: "var(--color-ink-muted)" }}
        >
          Search registered donors by blood type. Location filtering available
          once geocoding is fully wired.
        </p>
      </div>

      {/* Filter bar */}
      <div
        style={{
          margin: "0 var(--space-lg) var(--space-md)",
          padding: "var(--space-xs) var(--space-sm)",
          background: "var(--color-surface)",
          border: "1px solid var(--color-hairline)",
          borderRadius: "var(--radius-md)",
          display: "flex",
          alignItems: "center",
          gap: "var(--space-md)",
          flexWrap: "wrap",
        }}
      >
        {/* Blood type tabs */}
        <div style={{ display: "flex", alignItems: "center", gap: 4, flexWrap: "wrap" }}>
          <span
            className="text-label"
            style={{ color: "var(--color-ink-muted)", marginRight: 4 }}
          >
            Blood type
          </span>
          {[{ value: "", label: "All" }, ...BLOOD_TYPE_OPTIONS].map((opt) => {
            const active = bloodTypeFilter === opt.value;
            return (
              <button
                key={opt.value}
                onClick={() => handleBloodTypeChange(opt.value)}
                className="transition-fast"
                style={{
                  background: active
                    ? "var(--color-primary-subtle)"
                    : "transparent",
                  color: active
                    ? "var(--color-primary)"
                    : "var(--color-ink-secondary)",
                  border: active
                    ? "1px solid var(--color-primary)"
                    : "1px solid transparent",
                  borderRadius: "var(--radius-sm)",
                  padding: "4px 10px",
                  fontSize: 12,
                  fontWeight: active ? 600 : 400,
                  fontFamily: "var(--font-sans)",
                  cursor: "pointer",
                }}
              >
                {opt.label}
              </button>
            );
          })}
        </div>

        {/* Fetching indicator */}
        {isFetching && !isLoading && (
          <span className="text-caption" style={{ color: "var(--color-ink-faint)" }}>
            Updating…
          </span>
        )}

        {/* Result count */}
        {!isLoading && !isError && data && (
          <span
            className="text-caption tabular-nums"
            style={{
              marginLeft: "auto",
              color: "var(--color-ink-muted)",
            }}
          >
            {data.totalCount} result{data.totalCount !== 1 ? "s" : ""}
          </span>
        )}
      </div>

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
            gridTemplateColumns: "3px 56px 1fr 110px 130px 110px",
            gap: 16,
            padding: "10px 16px",
            background: "var(--color-surface-sunken)",
            borderBottom: "1px solid var(--color-hairline-strong)",
          }}
        >
          <div />
          {["Blood", "Donor ID / Address", "Date of Birth", "Status", "Verified"].map(
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

        {/* Loading */}
        {isLoading && Array.from({ length: 6 }).map((_, i) => <SkeletonRow key={i} />)}

        {/* Error */}
        {isError && (
          <div
            className="fade-enter"
            style={{ padding: "var(--space-xxl)", textAlign: "center" }}
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
            <p
              className="text-body-sm"
              style={{ color: "var(--color-critical)", margin: 0 }}
            >
              Failed to load donors. Check your connection and try again.
            </p>
          </div>
        )}

        {/* Empty */}
        {!isLoading && !isError && data && data.items.length === 0 && (
          <div
            className="fade-enter"
            style={{ padding: "var(--space-xxl)", textAlign: "center" }}
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
                margin: "0 auto var(--space-sm)",
                fontSize: 20,
                color: "var(--color-ink-faint)",
              }}
            >
              ◎
            </div>
            <p
              className="text-subheading"
              style={{ color: "var(--color-ink)", margin: "0 0 4px" }}
            >
              No donors found
            </p>
            <p
              className="text-body-sm"
              style={{ color: "var(--color-ink-muted)", margin: 0 }}
            >
              {bloodTypeFilter
                ? `No registered donors with blood type ${bloodTypeFilter}.`
                : "No donors have registered yet."}
            </p>
          </div>
        )}

        {/* Rows */}
        {!isLoading &&
          !isError &&
          data?.items.map((donor) => <DonorRow key={donor.id} donor={donor} />)}
      </div>

      {/* Pagination */}
      {data && (
        <PaginationBar
          page={page}
          pageSize={20}
          total={data.totalCount}
          onPage={setPage}
        />
      )}
    </div>
  );
}
