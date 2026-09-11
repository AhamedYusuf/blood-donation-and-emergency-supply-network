import { useMemo, useState } from "react";
import {
  useCheckStockMutation,
  useGetEmergencyRecommendationMutation,
  useGetInventoryQuery,
  useGetLowStockQuery,
  useGetStockRiskQuery,
} from "./inventoryApi";
import type {
  BloodType,
  EmergencyInventoryRecommendation,
  InventoryResponse,
} from "./inventoryTypes";
import "./inventoryEmergency.css";

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

function getBloodType(
  value: BloodType | number
): BloodType | null {
  if (typeof value === "number") {
    return BLOOD_TYPES[value] ?? null;
  }

  return BLOOD_TYPES.includes(value)
    ? value
    : null;
}

function getRiskLevel(
  inventory: InventoryResponse[]
) {
  const critical = inventory.filter(
    (item) => item.unitsAvailable <= 0
  ).length;

  const low = inventory.filter(
    (item) =>
      item.unitsAvailable > 0 &&
      item.isLowStock
  ).length;

  if (critical >= 2) {
    return {
      label: "Critical",
      className: "risk-critical",
      description:
        "Multiple blood types currently have no available stock.",
    };
  }

  if (critical === 1 || low >= 2) {
    return {
      label: "High",
      className: "risk-high",
      description:
        "Several blood-stock levels require immediate attention.",
    };
  }

  if (low === 1) {
    return {
      label: "Moderate",
      className: "risk-moderate",
      description:
        "At least one blood type is below its stock threshold.",
    };
  }

  return {
    label: "Low",
    className: "risk-low",
    description:
      "Current inventory is generally within healthy levels.",
  };
}

export function InventoryEmergencyPage() {
  const {
    data: inventory = [],
    isLoading,
    isError,
    refetch,
  } = useGetInventoryQuery(ORGANIZATION_ID);

  const { data: lowStock = [] } =
    useGetLowStockQuery(ORGANIZATION_ID);

  const {
    data: stockRisk,
    isFetching: isRiskFetching,
    isError: isRiskError,
    refetch: refetchRisk,
  } = useGetStockRiskQuery(ORGANIZATION_ID);

  const [checkStock, checkStockState] =
    useCheckStockMutation();

  const [getEmergencyRecommendation, recommendationState] =
    useGetEmergencyRecommendationMutation();

  const [bloodType, setBloodType] =
    useState<BloodType>("OPositive");

  const [requiredUnits, setRequiredUnits] =
    useState("");

  const [urgency, setUrgency] =
    useState<"Normal" | "Urgent" | "Critical">(
      "Urgent"
    );

  const [checked, setChecked] =
    useState(false);

  const [error, setError] =
    useState("");

  const [stockCheckResult, setStockCheckResult] =
    useState<{
      requiredUnits: number;
      availableUnits: number;
      remainingUnits: number;
      sufficient: boolean;
      lowStock: boolean;
    } | null>(null);

  const [recommendation, setRecommendation] =
    useState<EmergencyInventoryRecommendation | null>(null);

  const inventoryMap = useMemo(() => {
    const map = new Map<
      BloodType,
      InventoryResponse
    >();

    inventory.forEach((item) => {
      const type = getBloodType(
        item.bloodType
      );

      if (type) {
        map.set(type, item);
      }
    });

    return map;
  }, [inventory]);

  const selectedInventory =
    inventoryMap.get(bloodType);

  const availableUnits =
    selectedInventory?.unitsAvailable ?? 0;

  const requestedUnits =
    Number(requiredUnits) || 0;

  const resultAvailableUnits =
    stockCheckResult?.availableUnits ?? availableUnits;

  const resultRequiredUnits =
    stockCheckResult?.requiredUnits ?? requestedUnits;

  const resultRemainingUnits =
    stockCheckResult?.remainingUnits ?? 0;

  const resultShortfall = Math.max(
    resultRequiredUnits - resultAvailableUnits,
    0
  );

  const sufficient =
    stockCheckResult?.sufficient ?? false;

  const totalUnits = inventory.reduce(
    (total, item) =>
      total + item.unitsAvailable,
    0
  );

  const localRisk = getRiskLevel(inventory);

  const risk = stockRisk
    ? {
        label: stockRisk.riskLevel,
        className:
          stockRisk.riskLevel.toLowerCase() === "high"
            ? "risk-high"
            : stockRisk.riskLevel.toLowerCase() === "medium"
              ? "risk-moderate"
              : "risk-low",
        description: stockRisk.recommendation,
      }
    : localRisk;

  async function handleCheck() {
    setError("");
    setChecked(false);
    setStockCheckResult(null);
    setRecommendation(null);

    if (!requiredUnits.trim()) {
      setError("Required units are required.");
      return;
    }

    const units = Number(requiredUnits);

    if (!Number.isInteger(units)) {
      setError("Required units must be a whole number.");
      return;
    }

    if (units <= 0) {
      setError("Required units must be greater than 0.");
      return;
    }

    try {
      const stockResult = await checkStock({
        organizationId: ORGANIZATION_ID,
        bloodType,
        requiredUnits: units,
      }).unwrap();

      setStockCheckResult({
        requiredUnits: stockResult.requiredUnits,
        availableUnits: stockResult.availableUnits,
        remainingUnits: stockResult.remainingUnits,
        sufficient: stockResult.sufficient,
        lowStock: stockResult.lowStock,
      });

      const recommendationResult =
        await getEmergencyRecommendation({
          bloodType,
          requiredUnits: units,
          urgency,
        }).unwrap();

      setRecommendation(recommendationResult);
      setChecked(true);
    } catch (requestError) {
      console.error(requestError);
      setError(
        "We could not complete the emergency stock analysis. Please check your connection and try again."
      );
    }
  }

  if (isLoading) {
    return (
      <div className="emergency-loading">
        <div className="emergency-loading-icon">
          !
        </div>

        <h2>
          Loading emergency dashboard
        </h2>

        <p>
          Checking current blood-stock information...
        </p>
      </div>
    );
  }

  if (isError) {
    return (
      <div className="emergency-error">
        <div className="emergency-error-icon">
          !
        </div>

        <h2>
          Unable to load inventory
        </h2>

        <p>
          We couldn't retrieve the current stock
          information.
        </p>

        <button
          type="button"
          className="emergency-primary-button"
          onClick={() => refetch()}
        >
          Try again
        </button>
      </div>
    );
  }

  return (
    <div className="emergency-shell">
      <header className="emergency-header">
        <div className="emergency-hero-visual" aria-hidden="true">
          <div className="emergency-hero-visual__ring emergency-hero-visual__ring--one" />
          <div className="emergency-hero-visual__ring emergency-hero-visual__ring--two" />
          <div className="emergency-hero-visual__ring emergency-hero-visual__ring--three" />
          <div className="emergency-hero-visual__core">AI</div>
          <div className="emergency-hero-visual__sweep" />
          <div className="emergency-hero-visual__status">
            <i />
            <b>RESPONSE READY</b>
            <small>Emergency analysis online</small>
          </div>
        </div>

        <div className="emergency-brand">
          <a
            href="/inventory"
            className="emergency-back"
          >
            ←
          </a>

          <div>
            <span>
              BLOOD DONATION NETWORK
            </span>

            <h1>
              Emergency & Risk
            </h1>
          </div>
        </div>

        <a
          href="/inventory"
          className="emergency-dashboard-link"
        >
          Dashboard
        </a>
      </header>

      <main className="emergency-content">
        <section className="emergency-intro">
          <div>
            <span className="emergency-eyebrow">
              EMERGENCY RESPONSE
            </span>

            <h2>
              Emergency Blood Availability
            </h2>

            <p>
              Quickly check whether your organization
              has enough blood stock for an urgent request.
            </p>
          </div>

          <div className="emergency-total">
            <span>Total available</span>

            <strong>
              {totalUnits}
            </strong>

            <small>units</small>
          </div>
        </section>

        <section className="emergency-card">
          <div className="emergency-card-heading">
            <div>
              <span className="emergency-eyebrow">
                STOCK CHECK
              </span>

              <h2>
                Check Emergency Requirement
              </h2>
            </div>

            <div className="emergency-warning-icon">
              !
            </div>
          </div>

          <div className="emergency-form">
            <div className="emergency-form-grid">
              <div className="emergency-field">
                <label htmlFor="emergencyBloodType">
                  Blood Type <span>*</span>
                </label>

                <select
                  id="emergencyBloodType"
                  value={bloodType}
                  onChange={(event) => {
                    setBloodType(
                      event.target.value as BloodType
                    );
                    setChecked(false);
                  }}
                >
                  {BLOOD_TYPES.map((type) => (
                    <option
                      key={type}
                      value={type}
                    >
                      {BLOOD_LABELS[type]}
                    </option>
                  ))}
                </select>
              </div>

              <div className="emergency-field">
                <label htmlFor="requiredUnits">
                  Required Units <span>*</span>
                </label>

                <input
                  id="requiredUnits"
                  type="number"
                  min="1"
                  step="1"
                  value={requiredUnits}
                  onChange={(event) => {
                    setRequiredUnits(
                      event.target.value
                    );
                    setChecked(false);
                  }}
                  placeholder="Enter required units"
                />
              </div>

              <div className="emergency-field">
                <label htmlFor="urgency">
                  Request Urgency <span>*</span>
                </label>

                <select
                  id="urgency"
                  value={urgency}
                  onChange={(event) =>
                    setUrgency(
                      event.target.value as
                        | "Normal"
                        | "Urgent"
                        | "Critical"
                    )
                  }
                >
                  <option value="Normal">
                    Normal
                  </option>

                  <option value="Urgent">
                    Urgent
                  </option>

                  <option value="Critical">
                    Critical
                  </option>
                </select>
              </div>
            </div>

            {error && (
              <div className="emergency-form-error">
                <span>!</span>
                {error}
              </div>
            )}

            <div className="emergency-form-footer">
              <div className="emergency-current">
                Current{" "}
                <strong>
                  {BLOOD_LABELS[bloodType]}
                </strong>{" "}
                stock:{" "}
                <strong>
                  {resultAvailableUnits} units
                </strong>
              </div>

              <button
                type="button"
                className="emergency-primary-button"
                onClick={handleCheck}
                disabled={
                  checkStockState.isLoading ||
                  recommendationState.isLoading
                }
              >
                {checkStockState.isLoading || recommendationState.isLoading
                  ? "Analyzing..."
                  : "Check Availability"}
              </button>
            </div>
          </div>
        </section>

        {checked && (
          <>
            <section
              className={`emergency-result ${
                sufficient
                  ? "result-sufficient"
                  : "result-insufficient"
              }`}
            >
              <div className="result-icon">
                {sufficient ? "✓" : "!"}
              </div>

              <div className="result-main">
                <span className="result-label">
                  {sufficient
                    ? "STOCK AVAILABLE"
                    : "INSUFFICIENT STOCK"}
                </span>

                <h2>
                  {BLOOD_LABELS[bloodType]}{" "}
                  {sufficient
                    ? "requirement can be fulfilled"
                    : "shortfall detected"}
                </h2>

                <p>
                  {sufficient
                    ? `There are enough ${BLOOD_LABELS[bloodType]} units available for this request.`
                    : `The request requires ${resultRequiredUnits} units, but only ${resultAvailableUnits} are currently available.`}
                </p>
              </div>

              <div className="result-stats">
                <div>
                  <span>Required</span>
                  <strong>
                    {resultRequiredUnits}
                  </strong>
                </div>

                <div>
                  <span>Available</span>
                  <strong>
                    {resultAvailableUnits}
                  </strong>
                </div>

                <div>
                  <span>
                    {sufficient
                      ? "Remaining"
                      : "Shortfall"}
                  </span>

                  <strong>
                    {sufficient
                      ? resultRemainingUnits
                      : resultShortfall}
                  </strong>
                </div>
              </div>
            </section>

            <section className="recommendation-card">
              <div className="recommendation-icon">
                AI
              </div>

              <div>
                <span className="emergency-eyebrow">
                  AI RECOMMENDED ACTION
                </span>

                <h2>
                  {recommendation?.recommendation ??
                    "Recommendation available"}
                </h2>

                <p>
                  {recommendation?.suggestedAction ??
                    "Review the emergency inventory recommendation."}
                </p>
              </div>

              <div
                className={`urgency-badge urgency-${urgency.toLowerCase()}`}
              >
                {recommendation?.priority ?? urgency}
              </div>
            </section>
          </>
        )}

        <section className="risk-grid">
          <div className="risk-card">
            <div className="risk-card-heading">
              <div>
                <span className="emergency-eyebrow">
                  INVENTORY RISK
                </span>

                <h2>
                  Overall Stock Risk
                </h2>
              </div>

              <span
                className={`risk-badge ${risk.className}`}
              >
                {risk.label}
              </span>
            </div>

            <p className="risk-description">
              {risk.description}
            </p>

            {isRiskError && (
              <button
                type="button"
                className="emergency-secondary-button"
                onClick={() => refetchRisk()}
              >
                Refresh AI risk analysis
              </button>
            )}

            {isRiskFetching && (
              <div className="risk-analysis-status">
                AI risk analysis updating...
              </div>
            )}

            <div className="risk-metrics">
              <div>
                <span>Total units</span>

                <strong>
                  {totalUnits}
                </strong>
              </div>

              <div>
                <span>Low-stock types</span>

                <strong>
                  {stockRisk?.lowStockTypes ?? lowStock.length}
                </strong>
              </div>

              <div>
                <span>Inventory records</span>

                <strong>
                  {inventory.length}
                </strong>
              </div>
            </div>
          </div>

          <div className="risk-card">
            <div className="risk-card-heading">
              <div>
                <span className="emergency-eyebrow">
                  ATTENTION
                </span>

                <h2>
                  At-Risk Blood Types
                </h2>
              </div>
            </div>

            <div className="at-risk-list">
              {(stockRisk?.atRiskInventory ??
                inventory.filter(
                  (item) =>
                    item.isLowStock ||
                    item.unitsAvailable <= 0
                )).length > 0 ? (
                (stockRisk?.atRiskInventory ??
                  inventory.filter(
                    (item) =>
                      item.isLowStock ||
                      item.unitsAvailable <= 0
                  ))
                  .map((item) => {
                    const type =
                      getBloodType(
                        item.bloodType
                      );

                    return (
                      <div
                        className="at-risk-row"
                        key={item.id}
                      >
                        <div className="risk-blood">
                          {type
                            ? BLOOD_LABELS[type]
                            : "?"}
                        </div>

                        <div>
                          <strong>
                            {type
                              ? BLOOD_LABELS[type]
                              : "Unknown"}
                          </strong>

                          <span>
                            {item.unitsAvailable}{" "}
                            units available
                          </span>
                        </div>

                        <span className="risk-row-status">
                          {item.unitsAvailable <= 0
                            ? "Critical"
                            : "Low"}
                        </span>
                      </div>
                    );
                  })
              ) : (
                <div className="risk-empty">
                  <span>✓</span>

                  <p>
                    No blood types are currently
                    below their configured threshold.
                  </p>
                </div>
              )}
            </div>
          </div>
        </section>

        <section className="emergency-actions">
          <a
            href="/inventory/manage"
            className="emergency-action-link"
          >
            <span className="action-link-icon">
              +
            </span>

            <span>
              <strong>
                Manage Inventory
              </strong>

              <small>
                Adjust stock or record a transaction
              </small>
            </span>

            <span>→</span>
          </a>

          <a
            href="/inventory"
            className="emergency-action-link"
          >
            <span className="action-link-icon">
              ♥
            </span>

            <span>
              <strong>
                Back to Dashboard
              </strong>

              <small>
                View complete inventory overview
              </small>
            </span>

            <span>→</span>
          </a>
        </section>
      </main>
    </div>
  );
}