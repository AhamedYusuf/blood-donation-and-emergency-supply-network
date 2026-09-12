import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";

import {
  useCloseRequestMutation,
  useDeleteRequestMutation,
  useGetRequestByIdQuery,
  useUpdateRequestStatusMutation,
} from "./requestsApi";

import {
  BloodRequestStatus,
  RequestUrgency,
  BloodType,
} from "./requestTypes";

import "./requests.css";

const getBloodTypeLabel = (bloodType: BloodType) => {
  const labels: Record<BloodType, string> = {
    [BloodType.APositive]: "A+",
    [BloodType.ANegative]: "A-",
    [BloodType.BPositive]: "B+",
    [BloodType.BNegative]: "B-",
    [BloodType.ABPositive]: "AB+",
    [BloodType.ABNegative]: "AB-",
    [BloodType.OPositive]: "O+",
    [BloodType.ONegative]: "O-",
  };

  return labels[bloodType] ?? "Unknown";
};

const getUrgencyLabel = (urgency: RequestUrgency) => {
  switch (urgency) {
    case RequestUrgency.Normal:
      return "Normal";
    case RequestUrgency.Urgent:
      return "Urgent";
    case RequestUrgency.Critical:
      return "Critical";
    default:
      return "Unknown";
  }
};

const getStatusLabel = (status: BloodRequestStatus) => {
  switch (status) {
    case BloodRequestStatus.Pending:
      return "Pending";
    case BloodRequestStatus.AwaitingApproval:
      return "Awaiting Approval";
    case BloodRequestStatus.Approved:
      return "Approved";
    case BloodRequestStatus.Dispatched:
      return "Dispatched";
    case BloodRequestStatus.Fulfilled:
      return "Fulfilled";
    case BloodRequestStatus.Closed:
      return "Closed";
    case BloodRequestStatus.Cancelled:
      return "Cancelled";
    default:
      return "Unknown";
  }
};

export default function RequestDetailsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const {
    data: request,
    isLoading,
    isError,
    refetch,
  } = useGetRequestByIdQuery(id ?? "", {
    skip: !id,
  });

  const [updateStatus, { isLoading: isUpdating }] =
    useUpdateRequestStatusMutation();

  const [closeRequest, { isLoading: isClosing }] =
    useCloseRequestMutation();

  const [deleteRequest, { isLoading: isDeleting }] =
    useDeleteRequestMutation();

  const [selectedStatus, setSelectedStatus] =
    useState<BloodRequestStatus | null>(null);

  const [actionMessage, setActionMessage] =
    useState<string | null>(null);

  const [actionError, setActionError] =
    useState<string | null>(null);

  const handleUpdateStatus = async () => {
    if (!request || selectedStatus === null) return;

    setActionError(null);
    setActionMessage(null);

    try {
      await updateStatus({
        id: request.id,
        body: {
          status: selectedStatus,
        },
      }).unwrap();

      setActionMessage("Request status updated successfully.");
      setSelectedStatus(null);
      refetch();
    } catch {
      setActionError("Unable to update request status.");
    }
  };

  const handleCloseRequest = async () => {
    if (!request) return;

    const confirmed = window.confirm(
      "Are you sure you want to close this blood request?"
    );

    if (!confirmed) return;

    setActionError(null);
    setActionMessage(null);

    try {
      await closeRequest(request.id).unwrap();

      setActionMessage("Blood request closed successfully.");
      refetch();
    } catch {
      setActionError("Unable to close this request.");
    }
  };

  const handleDeleteRequest = async () => {
    if (!request) return;

    const confirmed = window.confirm(
      "Delete this blood request permanently?"
    );

    if (!confirmed) return;

    setActionError(null);

    try {
      await deleteRequest(request.id).unwrap();

      navigate("/requests");
    } catch {
      setActionError("Unable to delete this request.");
    }
  };

  if (!id) {
    return (
      <main className="requests-page">
        <section className="requests-error-state">
          <h3>Invalid request</h3>
          <p>No request ID was provided.</p>

          <Link
            to="/requests"
            className="requests-primary-button"
          >
            Back to Requests
          </Link>
        </section>
      </main>
    );
  }

  if (isLoading) {
    return (
      <main className="requests-page">
        <section className="requests-content-card">
          <div className="requests-state">
            <div className="requests-loading-card" />
            <div className="requests-loading-card" />
            <div className="requests-loading-card" />
          </div>
        </section>
      </main>
    );
  }

  if (isError || !request) {
    return (
      <main className="requests-page">
        <section className="requests-error-state">
          <h3>Unable to load blood request</h3>

          <p>
            The request may not exist or could not be loaded.
          </p>

          <div className="request-details-actions">
            <button
              type="button"
              className="requests-secondary-button"
              onClick={() => refetch()}
            >
              Retry
            </button>

            <Link
              to="/requests"
              className="requests-primary-button"
            >
              Back to Requests
            </Link>
          </div>
        </section>
      </main>
    );
  }

  return (
    <main className="requests-page">
      <section className="request-details-hero">
        <div>
          <span className="requests-eyebrow">
            BLOOD REQUEST DETAILS
          </span>

          <h1>{request.hospitalName}</h1>

          <div className="request-details-hero-meta">
            <span
              className={`request-pill request-pill--urgency-${request.urgency}`}
            >
              {getUrgencyLabel(request.urgency)}
            </span>

            <span
              className={`request-pill request-pill--status-${request.status}`}
            >
              {getStatusLabel(request.status)}
            </span>
          </div>
        </div>

        <Link
          to="/requests"
          className="requests-secondary-button"
        >
          ← Back to Requests
        </Link>
      </section>

      {actionMessage && (
        <div className="request-form-alert request-form-alert--success">
          {actionMessage}
        </div>
      )}

      {actionError && (
        <div
          className="request-form-alert request-form-alert--error"
          role="alert"
        >
          {actionError}
        </div>
      )}

      <section className="request-details-layout">
        <div className="request-details-card">
          <div className="request-details-heading">
            <span className="requests-eyebrow">
              REQUEST INFORMATION
            </span>

            <h2>Blood Requirement</h2>
          </div>

          <div className="request-details-grid">
            <div className="request-detail-item">
              <span>Blood Type</span>
              <strong>
                {getBloodTypeLabel(request.bloodType)}
              </strong>
            </div>

            <div className="request-detail-item">
              <span>Units Requested</span>
              <strong>{request.unitsRequested}</strong>
            </div>

            <div className="request-detail-item">
              <span>Urgency</span>
              <strong>
                {getUrgencyLabel(request.urgency)}
              </strong>
            </div>

            <div className="request-detail-item">
              <span>Status</span>
              <strong>
                {getStatusLabel(request.status)}
              </strong>
            </div>

            <div className="request-detail-item">
              <span>Created At</span>
              <strong>
                {new Date(request.createdAt).toLocaleString()}
              </strong>
            </div>

            <div className="request-detail-item">
              <span>Request ID</span>
              <strong className="request-detail-id">
                {request.id}
              </strong>
            </div>

            <div className="request-detail-item">
              <span>Organization ID</span>
              <strong className="request-detail-id">
                {request.organizationId}
              </strong>
            </div>

            <div className="request-detail-item">
              <span>Requester ID</span>
              <strong className="request-detail-id">
                {request.requesterId}
              </strong>
            </div>

            <div className="request-detail-item">
              <span>Latitude</span>
              <strong>{request.latitude}</strong>
            </div>

            <div className="request-detail-item">
              <span>Longitude</span>
              <strong>{request.longitude}</strong>
            </div>

            {request.fulfilledAt && (
              <div className="request-detail-item">
                <span>Fulfilled At</span>
                <strong>
                  {new Date(
                    request.fulfilledAt
                  ).toLocaleString()}
                </strong>
              </div>
            )}

            {request.closedAt && (
              <div className="request-detail-item">
                <span>Closed At</span>
                <strong>
                  {new Date(
                    request.closedAt
                  ).toLocaleString()}
                </strong>
              </div>
            )}
          </div>

          <div className="request-details-notes">
            <span>Notes</span>

            <p>
              {request.notes.trim()
                ? request.notes
                : "No additional notes were provided."}
            </p>
          </div>
        </div>

        <aside className="request-actions-card">
          <span className="requests-eyebrow">
            REQUEST ACTIONS
          </span>

          <h3>Manage Request</h3>

          <p>
            Update the operational status or close the
            request when coordination is complete.
          </p>

          <div className="request-action-group">
            <label htmlFor="requestStatus">
              Update Status
            </label>

            <select
              id="requestStatus"
              value={
                selectedStatus === null
                  ? ""
                  : selectedStatus
              }
              onChange={(event) =>
                setSelectedStatus(
                  event.target.value === ""
                    ? null
                    : (Number(
                        event.target.value
                      ) as BloodRequestStatus)
                )
              }
            >
              <option value="">
                Select new status
              </option>

              <option value={BloodRequestStatus.Pending}>
                Pending
              </option>

              <option
                value={
                  BloodRequestStatus.AwaitingApproval
                }
              >
                Awaiting Approval
              </option>

              <option value={BloodRequestStatus.Approved}>
                Approved
              </option>

              <option
                value={BloodRequestStatus.Dispatched}
              >
                Dispatched
              </option>

              <option value={BloodRequestStatus.Fulfilled}>
                Fulfilled
              </option>

              <option value={BloodRequestStatus.Cancelled}>
                Cancelled
              </option>
            </select>

            <button
              type="button"
              className="requests-primary-button"
              disabled={
                selectedStatus === null || isUpdating
              }
              onClick={handleUpdateStatus}
            >
              {isUpdating
                ? "Updating..."
                : "Update Status"}
            </button>
          </div>

          <div className="request-actions-divider" />

          <button
            type="button"
            className="request-close-button"
            disabled={
              isClosing ||
              request.status ===
                BloodRequestStatus.Closed
            }
            onClick={handleCloseRequest}
          >
            {request.status ===
            BloodRequestStatus.Closed
              ? "Request Closed"
              : isClosing
                ? "Closing..."
                : "Close Request"}
          </button>

          <button
            type="button"
            className="request-delete-button"
            disabled={isDeleting}
            onClick={handleDeleteRequest}
          >
            {isDeleting
              ? "Deleting..."
              : "Delete Request"}
          </button>
        </aside>
      </section>
    </main>
  );
}