import { FormEvent, useMemo, useState } from "react";
import {
  useAdjustInventoryMutation,
  useCreateTransactionMutation,
  useGetInventoryQuery,
  useGetTransactionsQuery,
} from "./inventoryApi";
import { useGetOrganizationsQuery } from "../organizations/organizationsApi";
import type {
  BloodType,
  InventoryResponse,
  InventoryTransactionType,
} from "./inventoryTypes";
import "./inventoryManage.css";

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

const TRANSACTION_TYPES: {
  value: InventoryTransactionType;
  label: string;
}[] = [
  {
    value: "DonationIn",
    label: "Donation In",
  },
  {
    value: "UsageOut",
    label: "Usage Out",
  },
  {
    value: "TransferIn",
    label: "Transfer In",
  },
  {
    value: "TransferOut",
    label: "Transfer Out",
  },
];

const BLOOD_TYPE_FROM_NUMBER: Record<
  number,
  BloodType
> = {
  0: "APositive",
  1: "ANegative",
  2: "BPositive",
  3: "BNegative",
  4: "ABPositive",
  5: "ABNegative",
  6: "OPositive",
  7: "ONegative",
};

const TRANSACTION_TYPE_FROM_NUMBER: Record<
  number,
  InventoryTransactionType
> = {
  0: "DonationIn",
  1: "UsageOut",
  2: "TransferIn",
  3: "TransferOut",
};

function normalizeBloodType(
  value: BloodType | number
): BloodType | null {
  if (typeof value === "number") {
    return BLOOD_TYPE_FROM_NUMBER[value] ?? null;
  }

  return BLOOD_TYPES.includes(value)
    ? value
    : null;
}

function normalizeTransactionType(
  value: InventoryTransactionType | number
): InventoryTransactionType | null {
  if (typeof value === "number") {
    return TRANSACTION_TYPE_FROM_NUMBER[value] ?? null;
  }

  return TRANSACTION_TYPES.some(
    (item) => item.value === value
  )
    ? value
    : null;
}

function formatDate(value: string) {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return "Unknown";
  }

  return date.toLocaleString([], {
    dateStyle: "medium",
    timeStyle: "short",
  });
}

function getStatus(item: InventoryResponse) {
  if (item.unitsAvailable <= 0) {
    return {
      label: "Critical",
      className: "manage-status-critical",
    };
  }

  if (item.isLowStock) {
    return {
      label: "Low stock",
      className: "manage-status-low",
    };
  }

  return {
    label: "Healthy",
    className: "manage-status-healthy",
  };
}

export function InventoryManagePage() {
  const {
    data: inventory = [],
    isLoading: inventoryLoading,
    isError: inventoryError,
    refetch: refetchInventory,
  } = useGetInventoryQuery(ORGANIZATION_ID);

  const {
    data: transactionData,
    isLoading: transactionsLoading,
  } = useGetTransactionsQuery({
    organizationId: ORGANIZATION_ID,
    page: 1,
    pageSize: 20,
  });

  const {
    data: organizations = [],
    isLoading: organizationsLoading,
  } = useGetOrganizationsQuery();

  const [createTransaction, createState] =
    useCreateTransactionMutation();

  const [adjustInventory, adjustState] =
    useAdjustInventoryMutation();

  const [bloodType, setBloodType] =
    useState<BloodType>("OPositive");

  const [units, setUnits] =
    useState("");

  const [transactionType, setTransactionType] =
    useState<InventoryTransactionType>(
      "DonationIn"
    );

  const [relatedAppointmentId, setRelatedAppointmentId] =
    useState("");

  const [relatedTransferOrgId, setRelatedTransferOrgId] =
    useState("");

  const [selectedInventory, setSelectedInventory] =
    useState<InventoryResponse | null>(null);

  const [adjustmentUnits, setAdjustmentUnits] =
    useState("");

  const [adjustmentType, setAdjustmentType] =
    useState<InventoryTransactionType>("DonationIn");

  const [formError, setFormError] =
    useState("");

  const [successMessage, setSuccessMessage] =
    useState("");

  const [adjustError, setAdjustError] =
    useState("");

  const inventoryMap = useMemo(() => {
    const map = new Map<
      BloodType,
      InventoryResponse
    >();

    inventory.forEach((item) => {
      const type = normalizeBloodType(
        item.bloodType
      );

      if (type) {
        map.set(type, item);
      }
    });

    return map;
  }, [inventory]);

  const transactions =
    transactionData?.items ?? [];

  const transferOrganizations = useMemo(
    () =>
      organizations.filter(
        (organization) => organization.id !== ORGANIZATION_ID
      ),
    [organizations],
  );

  const isTransfer =
    transactionType === "TransferIn" ||
    transactionType === "TransferOut";

  const isSaving =
    createState.isLoading ||
    adjustState.isLoading;

  function validateUnits(value: string) {
    if (!value.trim()) {
      return "Units are required.";
    }

    const parsed = Number(value);

    if (!Number.isInteger(parsed)) {
      return "Units must be a whole number.";
    }

    if (parsed <= 0) {
      return "Units must be greater than 0.";
    }

    return "";
  }

  function isValidGuid(value: string) {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      value.trim(),
    );
  }

  function getApiErrorMessage(error: unknown, fallback: string) {
    if (
      typeof error === "object" &&
      error !== null &&
      "data" in error
    ) {
      const data = (error as { data?: unknown }).data;

      if (typeof data === "object" && data !== null) {
        const message = (data as { message?: unknown }).message;

        if (typeof message === "string" && message.trim()) {
          return message;
        }

        const title = (data as { title?: unknown }).title;

        if (typeof title === "string" && title.trim()) {
          return title;
        }
      }
    }

    return fallback;
  }

  async function handleCreateTransaction(
    event: FormEvent
  ) {
    event.preventDefault();

    setFormError("");
    setSuccessMessage("");

    const unitsError =
      validateUnits(units);

    if (unitsError) {
      setFormError(unitsError);
      return;
    }

    const numericUnits = Number(units);

    const currentStock =
      inventoryMap.get(bloodType)
        ?.unitsAvailable ?? 0;

    const isOutbound =
      transactionType === "UsageOut" ||
      transactionType === "TransferOut";

    if (
      isOutbound &&
      numericUnits > currentStock
    ) {
      setFormError(
        `Insufficient ${BLOOD_LABELS[bloodType]} stock. Available: ${currentStock} units.`
      );
      return;
    }

    if (relatedAppointmentId.trim() && !isValidGuid(relatedAppointmentId)) {
      setFormError(
        "Appointment ID must be a valid UUID. Leave it empty when there is no linked appointment."
      );
      return;
    }

    if (isTransfer && !relatedTransferOrgId.trim()) {
      setFormError(
        "Please select a transfer organization."
      );
      return;
    }

    if (
      isTransfer &&
      relatedTransferOrgId === ORGANIZATION_ID
    ) {
      setFormError(
        "The transfer organization must be different from the current organization."
      );
      return;
    }

    if (
      isTransfer &&
      !isValidGuid(relatedTransferOrgId)
    ) {
      setFormError(
        "The selected transfer organization has an invalid ID."
      );
      return;
    }

    try {
      await createTransaction({
        organizationId: ORGANIZATION_ID,
        bloodType,
        units: numericUnits,
        transactionType,
        relatedAppointmentId:
          relatedAppointmentId.trim() || null,
        relatedTransferOrgId:
          relatedTransferOrgId.trim() || null,
      }).unwrap();

      setUnits("");
      setRelatedAppointmentId("");
      setRelatedTransferOrgId("");

      setSuccessMessage(
        `${BLOOD_LABELS[bloodType]} transaction recorded successfully.`
      );
    } catch (error) {
      console.error(error);

      setFormError(
        getApiErrorMessage(
          error,
          "The transaction could not be recorded. Please check the transfer organization and try again.",
        )
      );
    }
  }

  async function handleAdjustment(
    event: FormEvent
  ) {
    event.preventDefault();

    if (!selectedInventory) {
      return;
    }

    setAdjustError("");
    setSuccessMessage("");

    const unitsError =
      validateUnits(adjustmentUnits);

    if (unitsError) {
      setAdjustError(unitsError);
      return;
    }

    const numericUnits =
      Number(adjustmentUnits);

    const isOutbound =
      adjustmentType === "UsageOut" ||
      adjustmentType === "TransferOut";

    if (
      isOutbound &&
      numericUnits >
        selectedInventory.unitsAvailable
    ) {
      setAdjustError(
        `Cannot remove ${numericUnits} units. Only ${selectedInventory.unitsAvailable} units are available.`
      );
      return;
    }

    try {
      await adjustInventory({
        inventoryId: selectedInventory.id,
        request: {
          units: numericUnits,
          transactionType: adjustmentType,
          relatedAppointmentId: null,
          relatedTransferOrgId: null,
        },
      }).unwrap();

      const type = normalizeBloodType(
        selectedInventory.bloodType
      );

      setSuccessMessage(
        `${type ? BLOOD_LABELS[type] : "Inventory"} stock updated successfully.`
      );

      setSelectedInventory(null);
      setAdjustmentUnits("");
    } catch (error) {
      console.error(error);

      setAdjustError(
        "The inventory adjustment failed. Please try again."
      );
    }
  }

  if (inventoryLoading) {
    return (
      <div className="manage-loading">
        <div className="manage-loading-icon">
          ♥
        </div>

        <h2>Loading inventory</h2>

        <p>
          Retrieving current stock information...
        </p>
      </div>
    );
  }

  if (inventoryError) {
    return (
      <div className="manage-error">
        <div className="manage-error-icon">
          !
        </div>

        <h2>Unable to load inventory</h2>

        <p>
          We couldn't retrieve the inventory data.
        </p>

        <button
          type="button"
          className="manage-primary-button"
          onClick={() => refetchInventory()}
        >
          Try again
        </button>
      </div>
    );
  }

  return (
    <div className="manage-shell">
      {/* HEADER */}

      <header className="manage-header">
        <div className="manage-hero-visual" aria-hidden="true">
          <div className="manage-hero-visual__clipboard">
            <div className="manage-hero-visual__clip" />
            <span />
            <span />
            <span />
            <span />
          </div>
          <div className="manage-hero-visual__status">
            <i />
            <b>CONTROL CENTER</b>
            <small>Inventory operations active</small>
          </div>
        </div>

        <div className="manage-brand">
          <a
            href="/inventory"
            className="manage-back"
          >
            ←
          </a>

          <div>
            <span>
              BLOOD DONATION NETWORK
            </span>

            <h1>Manage Inventory</h1>
          </div>
        </div>

        <a
          href="/inventory"
          className="manage-dashboard-link"
        >
          Dashboard
        </a>
      </header>

      <main className="manage-content">
        {/* PAGE INTRO */}

        <section className="manage-intro">
          <div>
            <span className="manage-eyebrow">
              INVENTORY OPERATIONS
            </span>

            <h2>
              Manage Blood Stock
            </h2>

            <p>
              Record inventory movements and adjust
              current blood-stock quantities.
            </p>
          </div>

          <div className="manage-info-badge">
            {inventory.length} inventory records
          </div>
        </section>

        {/* SUCCESS MESSAGE */}

        {successMessage && (
          <div className="manage-success">
            <span>✓</span>

            <div>
              <strong>Success</strong>

              <p>{successMessage}</p>
            </div>

            <button
              type="button"
              onClick={() =>
                setSuccessMessage("")
              }
            >
              ×
            </button>
          </div>
        )}

        {/* CREATE TRANSACTION */}

        <section className="manage-card">
          <div className="manage-card-heading">
            <div>
              <span className="manage-eyebrow">
                CREATE
              </span>

              <h2>
                Record Inventory Transaction
              </h2>
            </div>

            <div className="manage-create-icon">
              +
            </div>
          </div>

          <form
            className="inventory-form"
            onSubmit={handleCreateTransaction}
          >
            <div className="form-grid">
              {/* Blood type */}

              <div className="form-field">
                <label htmlFor="bloodType">
                  Blood Type
                  <span>*</span>
                </label>

                <select
                  id="bloodType"
                  value={bloodType}
                  onChange={(event) =>
                    setBloodType(
                      event.target.value as BloodType
                    )
                  }
                >
                  {BLOOD_TYPES.map(
                    (type) => (
                      <option
                        key={type}
                        value={type}
                      >
                        {BLOOD_LABELS[type]}
                      </option>
                    )
                  )}
                </select>
              </div>

              {/* Units */}

              <div className="form-field">
                <label htmlFor="units">
                  Units
                  <span>*</span>
                </label>

                <input
                  id="units"
                  type="number"
                  min="1"
                  step="1"
                  value={units}
                  onChange={(event) =>
                    setUnits(event.target.value)
                  }
                  placeholder="Enter units"
                />

                <small>
                  Must be a whole number greater
                  than zero.
                </small>
              </div>

              {/* Transaction */}

              <div className="form-field">
                <label htmlFor="transactionType">
                  Transaction Type
                  <span>*</span>
                </label>

                <select
                  id="transactionType"
                  value={transactionType}
                  onChange={(event) => {
                    const nextType =
                      event.target.value as InventoryTransactionType;

                    setTransactionType(nextType);

                    if (
                      nextType !== "TransferIn" &&
                      nextType !== "TransferOut"
                    ) {
                      setRelatedTransferOrgId("");
                    }
                  }}
                >
                  {TRANSACTION_TYPES.map(
                    (type) => (
                      <option
                        key={type.value}
                        value={type.value}
                      >
                        {type.label}
                      </option>
                    )
                  )}
                </select>
              </div>

              {/* Appointment */}

              <div className="form-field">
                <label htmlFor="appointmentId">
                  Appointment ID
                  <span className="optional">
                    Optional
                  </span>
                </label>

                <input
                  id="appointmentId"
                  type="text"
                  value={relatedAppointmentId}
                  onChange={(event) =>
                    setRelatedAppointmentId(
                      event.target.value
                    )
                  }
                  placeholder="Optional UUID"
                />

                <small>
                  Use the full appointment UUID, or leave empty.
                </small>
              </div>

              {/* Transfer organization */}

              <div className="form-field form-field-wide">
                <label htmlFor="transferOrgId">
                  Transfer Organization
                  {isTransfer && <span>*</span>}
                </label>

                <select
                  id="transferOrgId"
                  value={relatedTransferOrgId}
                  onChange={(event) =>
                    setRelatedTransferOrgId(
                      event.target.value
                    )
                  }
                  disabled={!isTransfer || organizationsLoading}
                >
                  <option value="">
                    {organizationsLoading
                      ? "Loading organizations..."
                      : isTransfer
                        ? "Select transfer organization"
                        : "Not required for this transaction"}
                  </option>

                  {transferOrganizations.map(
                    (organization) => (
                      <option
                        key={organization.id}
                        value={organization.id}
                      >
                        {organization.name}
                      </option>
                    )
                  )}
                </select>

                {isTransfer &&
                  !organizationsLoading &&
                  transferOrganizations.length === 0 && (
                    <small>
                      No other organizations are available for transfer.
                    </small>
                  )}
              </div>
            </div>

            {formError && (
              <div className="form-error">
                <span>!</span>
                {formError}
              </div>
            )}

            <div className="form-footer">
              <div className="current-stock-hint">
                Current{" "}
                {BLOOD_LABELS[bloodType]} stock:
                <strong>
                  {
                    inventoryMap.get(
                      bloodType
                    )?.unitsAvailable ?? 0
                  }{" "}
                  units
                </strong>
              </div>

              <button
                type="submit"
                className="manage-primary-button"
                disabled={isSaving}
              >
                {createState.isLoading
                  ? "Recording..."
                  : "Record Transaction"}
              </button>
            </div>
          </form>
        </section>

        {/* CURRENT INVENTORY */}

        <section className="manage-card">
          <div className="manage-card-heading">
            <div>
              <span className="manage-eyebrow">
                UPDATE
              </span>

              <h2>
                Current Inventory
              </h2>
            </div>

            <span className="manage-card-count">
              {inventory.length} records
            </span>
          </div>

          <div className="inventory-table-wrapper">
            <table className="inventory-table">
              <thead>
                <tr>
                  <th>Blood Type</th>
                  <th>Available</th>
                  <th>Threshold</th>
                  <th>Status</th>
                  <th>Last Updated</th>
                  <th>Action</th>
                </tr>
              </thead>

              <tbody>
                {inventory.length > 0 ? (
                  inventory.map((item) => {
                    const type =
                      normalizeBloodType(
                        item.bloodType
                      );

                    const status =
                      getStatus(item);

                    return (
                      <tr key={item.id}>
                        <td>
                          <div className="table-blood">
                            <span>♥</span>

                            <strong>
                              {type
                                ? BLOOD_LABELS[type]
                                : "Unknown"}
                            </strong>
                          </div>
                        </td>

                        <td>
                          <strong className="table-units">
                            {item.unitsAvailable}
                          </strong>{" "}
                          units
                        </td>

                        <td>
                          {item.lowStockThreshold}
                        </td>

                        <td>
                          <span
                            className={`manage-status ${status.className}`}
                          >
                            {status.label}
                          </span>
                        </td>

                        <td className="table-date">
                          {formatDate(
                            item.lastUpdated
                          )}
                        </td>

                        <td>
                          <button
                            type="button"
                            className="table-action"
                            onClick={() => {
                              setSelectedInventory(
                                item
                              );
                              setAdjustmentUnits("");
                              setAdjustmentType(
                                "DonationIn"
                              );
                              setAdjustError("");
                            }}
                          >
                            Adjust
                          </button>
                        </td>
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td
                      colSpan={6}
                      className="table-empty"
                    >
                      No inventory records found.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>

        {/* TRANSACTIONS */}

        <section className="manage-card">
          <div className="manage-card-heading">
            <div>
              <span className="manage-eyebrow">
                READ
              </span>

              <h2>
                Transaction History
              </h2>
            </div>

            <span className="manage-card-count">
              {transactionData?.totalCount ?? 0} total
            </span>
          </div>

          {transactionsLoading ? (
            <div className="transactions-loading">
              Loading transaction history...
            </div>
          ) : transactions.length > 0 ? (
            <div className="history-list">
              {transactions.map(
                (transaction) => {
                  const type =
                    normalizeBloodType(
                      transaction.bloodType
                    );

                  const transactionType =
                    normalizeTransactionType(
                      transaction.transactionType
                    );

                  const inbound =
                    transactionType ===
                      "DonationIn" ||
                    transactionType ===
                      "TransferIn";

                  const label =
                    transactionType ===
                      "DonationIn"
                      ? "Donation received"
                      : transactionType ===
                          "UsageOut"
                        ? "Blood used"
                        : transactionType ===
                            "TransferIn"
                          ? "Transfer received"
                          : transactionType ===
                              "TransferOut"
                            ? "Transfer sent"
                            : "Inventory transaction";

                  return (
                    <div
                      className="history-row"
                      key={transaction.id}
                    >
                      <div className="history-blood">
                        {type
                          ? BLOOD_LABELS[type]
                          : "?"}
                      </div>

                      <div className="history-details">
                        <strong>
                          {label}
                        </strong>

                        <span>
                          {formatDate(
                            transaction.createdAt
                          )}
                        </span>
                      </div>

                      <div
                        className={
                          inbound
                            ? "history-in"
                            : "history-out"
                        }
                      >
                        {inbound ? "+" : "-"}
                        {transaction.units}
                      </div>
                    </div>
                  );
                }
              )}
            </div>
          ) : (
            <div className="history-empty">
              No transactions have been recorded yet.
            </div>
          )}
        </section>
      </main>

      {/* ADJUSTMENT MODAL */}

      {selectedInventory && (
        <div
          className="modal-backdrop"
          onMouseDown={() =>
            setSelectedInventory(null)
          }
        >
          <div
            className="adjust-modal"
            onMouseDown={(event) =>
              event.stopPropagation()
            }
          >
            <div className="adjust-modal-header">
              <div>
                <span className="manage-eyebrow">
                  UPDATE STOCK
                </span>

                <h2>
                  Adjust{" "}
                  {(() => {
                    const type =
                      normalizeBloodType(
                        selectedInventory.bloodType
                      );

                    return type
                      ? BLOOD_LABELS[type]
                      : "Inventory";
                  })()}
                </h2>
              </div>

              <button
                type="button"
                className="modal-close"
                onClick={() =>
                  setSelectedInventory(null)
                }
              >
                ×
              </button>
            </div>

            <div className="adjust-current">
              <span>Current stock</span>

              <strong>
                {selectedInventory.unitsAvailable} units
              </strong>
            </div>

            <form
              onSubmit={handleAdjustment}
              className="adjust-form"
            >
              <div className="form-field">
                <label htmlFor="adjustmentType">
                  Adjustment Type
                  <span>*</span>
                </label>

                <select
                  id="adjustmentType"
                  value={adjustmentType}
                  onChange={(event) =>
                    setAdjustmentType(
                      event.target.value as InventoryTransactionType
                    )
                  }
                >
                  {TRANSACTION_TYPES.map(
                    (type) => (
                      <option
                        key={type.value}
                        value={type.value}
                      >
                        {type.label}
                      </option>
                    )
                  )}
                </select>
              </div>

              <div className="form-field">
                <label htmlFor="adjustmentUnits">
                  Units
                  <span>*</span>
                </label>

                <input
                  id="adjustmentUnits"
                  type="number"
                  min="1"
                  step="1"
                  value={adjustmentUnits}
                  onChange={(event) =>
                    setAdjustmentUnits(
                      event.target.value
                    )
                  }
                  placeholder="Enter units"
                />
              </div>

              {adjustError && (
                <div className="form-error">
                  <span>!</span>
                  {adjustError}
                </div>
              )}

              <div className="adjust-modal-actions">
                <button
                  type="button"
                  className="secondary-button"
                  onClick={() =>
                    setSelectedInventory(null)
                  }
                >
                  Cancel
                </button>

                <button
                  type="submit"
                  className="manage-primary-button"
                  disabled={adjustState.isLoading}
                >
                  {adjustState.isLoading
                    ? "Updating..."
                    : "Update Stock"}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}