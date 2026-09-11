import { useMemo } from "react";
import {
  useGetInventoryQuery,
  useGetLowStockQuery,
  useGetStockRiskQuery,
  useGetTransactionsQuery,
} from "./inventoryApi";
import type {
  BloodType,
  InventoryResponse,
  InventoryTransactionResponse,
} from "./inventoryTypes";
import "./inventory.css";

const ORGANIZATION_ID =
  import.meta.env.VITE_INVENTORY_ORGANIZATION_ID as string;

const BLOOD_TYPES: BloodType[] = [
  "APositive",
  "ANegative",
  "BPositive",
  "BNegative",
  "ABPositive",
  "ABNegative",
  "OPositive",
  "ONegative",
];

const BLOOD_LABELS: Record<BloodType, string> = {
  APositive: "A+",
  ANegative: "A−",
  BPositive: "B+",
  BNegative: "B−",
  ABPositive: "AB+",
  ABNegative: "AB−",
  OPositive: "O+",
  ONegative: "O−",
};

/*
 * Backend BloodType enum:
 *
 * 0 = APositive
 * 1 = ANegative
 * 2 = BPositive
 * 3 = BNegative
 * 4 = ABPositive
 * 5 = ABNegative
 * 6 = OPositive
 * 7 = ONegative
 */
const BLOOD_TYPE_FROM_NUMBER: Record<number, BloodType> = {
  0: "APositive",
  1: "ANegative",
  2: "BPositive",
  3: "BNegative",
  4: "ABPositive",
  5: "ABNegative",
  6: "OPositive",
  7: "ONegative",
};

/*
 * Backend InventoryTransactionType enum:
 *
 * 0 = DonationIn
 * 1 = UsageOut
 * 2 = TransferIn
 * 3 = TransferOut
 */
const TRANSACTION_TYPE_FROM_NUMBER: Record<
  number,
  "DonationIn" | "UsageOut" | "TransferIn" | "TransferOut"
> = {
  0: "DonationIn",
  1: "UsageOut",
  2: "TransferIn",
  3: "TransferOut",
};

function getBloodTypeKey(value: BloodType | number): BloodType | null {
  if (typeof value === "number") {
    return BLOOD_TYPE_FROM_NUMBER[value] ?? null;
  }

  if (BLOOD_TYPES.includes(value)) {
    return value;
  }

  return null;
}

function getBloodLabel(value: BloodType | number): string {
  const key = getBloodTypeKey(value);

  return key ? BLOOD_LABELS[key] : "Unknown";
}

function getTransactionType(
  value: string | number
): "DonationIn" | "UsageOut" | "TransferIn" | "TransferOut" | null {
  if (typeof value === "number") {
    return TRANSACTION_TYPE_FROM_NUMBER[value] ?? null;
  }

  if (
    value === "DonationIn" ||
    value === "UsageOut" ||
    value === "TransferIn" ||
    value === "TransferOut"
  ) {
    return value;
  }

  return null;
}

function getTransactionLabel(value: string | number): string {
  const transactionType = getTransactionType(value);

  switch (transactionType) {
    case "DonationIn":
      return "Donation received";

    case "UsageOut":
      return "Blood used";

    case "TransferIn":
      return "Transfer received";

    case "TransferOut":
      return "Transfer sent";

    default:
      return "Inventory transaction";
  }
}

function isInboundTransaction(value: string | number): boolean {
  const transactionType = getTransactionType(value);

  return (
    transactionType === "DonationIn" ||
    transactionType === "TransferIn"
  );
}

function getStockStatus(item?: InventoryResponse) {
  if (!item) {
    return {
      label: "Not stocked",
      className: "status-neutral",
    };
  }

  if (item.unitsAvailable <= 0) {
    return {
      label: "Critical",
      className: "status-critical",
    };
  }

  if (item.isLowStock) {
    return {
      label: "Low stock",
      className: "status-low",
    };
  }

  return {
    label: "Healthy",
    className: "status-healthy",
  };
}

function getProgressPercentage(item?: InventoryResponse): number {
  if (!item || item.unitsAvailable <= 0) {
    return 0;
  }

  const threshold = Math.max(item.lowStockThreshold, 1);

  /*
   * We display the bar up to 100%.
   * A stock level of at least 2x the threshold is considered full.
   */
  const percentage = (item.unitsAvailable / (threshold * 2)) * 100;

  return Math.min(Math.max(percentage, 4), 100);
}

function formatDate(date: string): string {
  const parsedDate = new Date(date);

  if (Number.isNaN(parsedDate.getTime())) {
    return "Unknown time";
  }

  return parsedDate.toLocaleString([], {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function BloodDrop() {
  return (
    <span className="blood-drop" aria-hidden="true">
      +
    </span>
  );
}

function LoadingState() {
  return (
    <div className="inventory-loading">
      <div className="loading-drop">
        <BloodDrop />
      </div>

      <h2>Loading inventory</h2>

      <p>
        Retrieving the latest blood-stock information...
      </p>
    </div>
  );
}

function ErrorState({ onRetry }: { onRetry: () => void }) {
  return (
    <div className="inventory-error">
      <div className="error-icon">!</div>

      <h2>Unable to load inventory</h2>

      <p>
        We couldn't retrieve the current blood-stock information.
      </p>

      <button
        type="button"
        className="primary-button"
        onClick={onRetry}
      >
        Try again
      </button>
    </div>
  );
}

function TransactionRow({
  transaction,
}: {
  transaction: InventoryTransactionResponse;
}) {
  const inbound = isInboundTransaction(transaction.transactionType);

  return (
    <div className="transaction-row">
      <div className="transaction-blood">
        {getBloodLabel(transaction.bloodType)}
      </div>

      <div className="transaction-info">
        <strong>
          {getTransactionLabel(transaction.transactionType)}
        </strong>

        <span>
          {formatDate(transaction.createdAt)}
        </span>
      </div>

      <div
        className={
          inbound
            ? "transaction-in"
            : "transaction-out"
        }
      >
        {inbound ? "+" : "-"}
        {transaction.units}
      </div>
    </div>
  );
}

export function InventoryPage() {
  const {
    data: inventory = [],
    isLoading,
    isError,
    refetch,
  } = useGetInventoryQuery(ORGANIZATION_ID);

  const {
    data: lowStock = [],
  } = useGetLowStockQuery(ORGANIZATION_ID);

  const {
    data: stockRisk,
    isFetching: isStockRiskFetching,
  } = useGetStockRiskQuery(ORGANIZATION_ID);

  const {
    data: transactionData,
  } = useGetTransactionsQuery({
    organizationId: ORGANIZATION_ID,
    page: 1,
    pageSize: 5,
  });

  const inventoryMap = useMemo(() => {
    const map = new Map<BloodType, InventoryResponse>();

    inventory.forEach((item) => {
      const key = getBloodTypeKey(item.bloodType);

      if (key) {
        map.set(key, item);
      }
    });

    return map;
  }, [inventory]);

  const totalUnits = inventory.reduce(
    (total, item) => total + item.unitsAvailable,
    0
  );

  const healthyTypes = inventory.filter(
    (item) =>
      !item.isLowStock &&
      item.unitsAvailable > 0
  ).length;

  const criticalTypes = inventory.filter(
    (item) => item.unitsAvailable <= 0
  ).length;

  const lowStockCount = lowStock.length;

  const transactions =
    transactionData?.items ?? [];

  if (isLoading) {
    return <LoadingState />;
  }

  if (isError) {
    return <ErrorState onRetry={() => refetch()} />;
  }

  return (
    <div className="inventory-shell">
      {/* =====================================================
          HEADER
          ===================================================== */}

      <header className="inventory-header">
        <div className="inventory-hero-visual" aria-hidden="true">
          <div className="inventory-hero-visual__header">
            <span>LIVE STOCK</span>
            <i />
          </div>
          <div className="inventory-hero-visual__chart">
            <span style={{ height: "42%" }} />
            <span style={{ height: "68%" }} />
            <span style={{ height: "54%" }} />
            <span style={{ height: "86%" }} />
            <span style={{ height: "61%" }} />
            <span style={{ height: "74%" }} />
          </div>
          <div className="inventory-hero-visual__legend">
            <b>8 types</b>
            <small>Stock monitored</small>
          </div>
          <div className="inventory-hero-visual__scan" />
        </div>

        <div className="brand-area">
          <div className="brand-mark">
            <BloodDrop />
          </div>

          <div>
            <span className="brand-eyebrow">
              BLOOD DONATION NETWORK
            </span>

            <h1>Inventory Management</h1>
          </div>
        </div>

        <button
          type="button"
          className="refresh-button"
          onClick={() => refetch()}
        >
          ↻ Refresh
        </button>
      </header>

      {/* =====================================================
          MAIN
          ===================================================== */}

      <main className="inventory-content">
        {/* ---------- Welcome ---------- */}

        <section className="welcome-section">
          <div>
            <span className="section-label">
              INVENTORY OVERVIEW
            </span>

            <h2>Blood Stock Dashboard</h2>

            <p>
              Monitor blood availability and identify
              low-stock supplies across your organization.
            </p>
          </div>

          <div className="welcome-actions">
            <div className="live-indicator">
              <span />
              Live inventory
            </div>

            <a
              href="/inventory/manage"
              className="manage-inventory-button"
            >
              Manage Inventory →
            </a>
          </div>
        </section>

        {/* =================================================
            SUMMARY CARDS
            ================================================= */}

        <section className="summary-grid">
          {/* Total units */}

          <div className="summary-card summary-primary">
            <div className="summary-icon">
              <BloodDrop />
            </div>

            <div>
              <span>Total blood units</span>

              <strong>{totalUnits}</strong>

              <small>
                Currently available
              </small>
            </div>
          </div>

          {/* Low stock */}

          <div className="summary-card">
            <div className="summary-icon warning-icon">
              !
            </div>

            <div>
              <span>Low-stock types</span>

              <strong>{lowStockCount}</strong>

              <small>
                Need attention
              </small>
            </div>
          </div>

          {/* Healthy */}

          <div className="summary-card">
            <div className="summary-icon success-icon">
              ✓
            </div>

            <div>
              <span>Healthy types</span>

              <strong>{healthyTypes}</strong>

              <small>
                Good availability
              </small>
            </div>
          </div>

          {/* Critical */}

          <div className="summary-card">
            <div className="summary-icon critical-icon">
              !
            </div>

            <div>
              <span>Critical types</span>

              <strong>{criticalTypes}</strong>

              <small>
                Currently empty
              </small>
            </div>
          </div>
        </section>

        {/* =================================================
            LOW STOCK ALERT
            ================================================= */}

        {lowStockCount > 0 && (
          <section className="alert-banner">
            <div className="alert-symbol">
              !
            </div>

            <div className="alert-content">
              <strong>
                Low-stock alert
              </strong>

              <span>
                {lowStockCount} blood type
                {lowStockCount !== 1 ? "s are" : " is"}{" "}
                below the configured threshold.
              </span>
            </div>

            <a
              className="alert-action"
              href="/inventory/manage"
            >
              Review stock →
            </a>
          </section>
        )}

        {/* =================================================
            AI INVENTORY INTELLIGENCE
            ================================================= */}

        <section className="ai-intelligence-section">
          <div className="ai-intelligence-heading">
            <div>
              <span className="section-label">
                AI INVENTORY INTELLIGENCE
              </span>

              <h2>Three agents for faster stock decisions</h2>

              <p>
                Use the inventory agents to check availability,
                detect stock risk, and respond to emergency demand.
              </p>
            </div>

            <div className="ai-live-badge">
              <i />
              {isStockRiskFetching
                ? "Analyzing"
                : "AI services ready"}
            </div>
          </div>

          <div className="ai-agent-grid">
            <a
              className="ai-agent-card"
              href="/inventory/emergency"
            >
              <div className="ai-agent-card__top">
                <div className="ai-agent-icon ai-agent-icon--check">
                  <span>✓</span>
                </div>

                <span className="ai-agent-status ai-agent-status--ready">
                  READY
                </span>
              </div>

              <div className="ai-agent-card__body">
                <span className="ai-agent-kicker">
                  AGENT 01
                </span>

                <h3>Stock Check Agent</h3>

                <p>
                  Checks whether the requested blood type and
                  quantity can be fulfilled from available stock.
                </p>
              </div>

              <div className="ai-agent-card__footer">
                <span>Open Emergency Check</span>
                <strong>→</strong>
              </div>
            </a>

            <div className="ai-agent-card ai-agent-card--live">
              <div className="ai-agent-card__top">
                <div className="ai-agent-icon ai-agent-icon--risk">
                  <span>◉</span>
                </div>

                <span className={`ai-agent-status ${
                  stockRisk?.riskLevel?.toUpperCase() === "HIGH"
                    ? "ai-agent-status--high"
                    : stockRisk?.riskLevel?.toUpperCase() === "MEDIUM"
                      ? "ai-agent-status--medium"
                      : "ai-agent-status--healthy"
                }`}>
                  {stockRisk?.riskLevel?.toUpperCase() ?? "LIVE"}
                </span>
              </div>

              <div className="ai-agent-card__body">
                <span className="ai-agent-kicker">
                  AGENT 02
                </span>

                <h3>Stock Risk Agent</h3>

                <p>
                  Continuously evaluates low-stock blood types
                  and highlights where replenishment is needed.
                </p>
              </div>

              <div className="ai-agent-metrics">
                <div>
                  <strong>
                    {stockRisk?.lowStockTypes ?? lowStockCount}
                  </strong>
                  <span>At-risk types</span>
                </div>

                <div>
                  <strong>
                    {stockRisk?.totalUnits ?? totalUnits}
                  </strong>
                  <span>Total units</span>
                </div>
              </div>

              {stockRisk?.recommendation && (
                <div className="ai-agent-note">
                  {stockRisk.recommendation}
                </div>
              )}
            </div>

            <a
              className="ai-agent-card"
              href="/inventory/emergency"
            >
              <div className="ai-agent-card__top">
                <div className="ai-agent-icon ai-agent-icon--emergency">
                  <span>!</span>
                </div>

                <span className="ai-agent-status ai-agent-status--ready">
                  READY
                </span>
              </div>

              <div className="ai-agent-card__body">
                <span className="ai-agent-kicker">
                  AGENT 03
                </span>

                <h3>Emergency Recommendation Agent</h3>

                <p>
                  Recommends the safest next action when an
                  emergency request creates a stock shortage.
                </p>
              </div>

              <div className="ai-agent-card__footer">
                <span>Open Emergency Recommendations</span>
                <strong>→</strong>
              </div>
            </a>
          </div>
        </section>

        {/* =================================================
            BLOOD STOCK
            ================================================= */}

        <section className="stock-section">
          <div className="section-heading">
            <div>
              <span className="section-label">
                CURRENT STOCK
              </span>

              <h2>Blood Type Availability</h2>
            </div>

            <span className="stock-count">
              {inventory.length} of 8 types
            </span>
          </div>

          <div className="blood-grid">
            {BLOOD_TYPES.map((bloodType) => {
              const item = inventoryMap.get(bloodType);

              const status = getStockStatus(item);

              const progress =
                getProgressPercentage(item);

              return (
                <div
                  className="blood-card"
                  key={bloodType}
                >
                  <div className="blood-card-top">
                    <div className="blood-type-mark">
                      <BloodDrop />
                    </div>

                    <span
                      className={`stock-status ${status.className}`}
                    >
                      {status.label}
                    </span>
                  </div>

                  <div className="blood-type-name">
                    {getBloodLabel(bloodType)}
                  </div>

                  <div className="units-value">
                    {item?.unitsAvailable ?? 0}{" "}
                    <span>units</span>
                  </div>

                  <div className="threshold-row">
                    <span>
                      Low-stock threshold
                    </span>

                    <strong>
                      {item?.lowStockThreshold ?? 0}
                    </strong>
                  </div>

                  <div className="stock-progress">
                    <span
                      style={{
                        width: `${progress}%`,
                      }}
                    />
                  </div>
                </div>
              );
            })}
          </div>
        </section>

        {/* =================================================
            BOTTOM SECTION
            ================================================= */}

        <section className="bottom-grid">
          {/* ---------- Transactions ---------- */}

          <div className="transactions-panel">
            <div className="panel-heading">
              <div>
                <span className="section-label">
                  ACTIVITY
                </span>

                <h2>Recent Transactions</h2>
              </div>

              <a href="/inventory/manage">
                View all →
              </a>
            </div>

            {transactions.length > 0 ? (
              <div className="transaction-list">
                {transactions.map(
                  (transaction) => (
                    <TransactionRow
                      key={transaction.id}
                      transaction={transaction}
                    />
                  )
                )}
              </div>
            ) : (
              <div className="empty-state">
                <div>
                  <span>🩸</span>

                  <p>
                    No recent transactions.
                  </p>
                </div>
              </div>
            )}
          </div>

          {/* ---------- Quick Actions ---------- */}

          <aside className="quick-actions">
            <div className="panel-heading">
              <div>
                <span className="section-label">
                  ACTIONS
                </span>

                <h2>Quick Actions</h2>
              </div>
            </div>

            <a
              href="/inventory/manage"
              className="quick-action"
            >
              <span className="action-icon">
                +
              </span>

              <span>
                <strong>
                  Manage stock
                </strong>

                <small>
                  Adjust units and record transactions
                </small>
              </span>

              <span>→</span>
            </a>

            <a
              href="/inventory/emergency"
              className="quick-action"
            >
              <span className="action-icon emergency-action">
                !
              </span>

              <span>
                <strong>
                  Emergency stock
                </strong>

                <small>
                  Check availability for urgent needs
                </small>
              </span>

              <span>→</span>
            </a>

            <a
              href="/inventory/emergency"
              className="quick-action quick-action--ai"
            >
              <span className="action-icon ai-action-icon">
                AI
              </span>

              <span>
                <strong>
                  AI emergency analysis
                </strong>

                <small>
                  Run Stock Check and Recommendation agents
                </small>
              </span>

              <span>→</span>
            </a>
          </aside>
        </section>
      </main>
    </div>
  );
}