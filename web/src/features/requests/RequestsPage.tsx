import { Link } from "react-router-dom";
import { useSelector } from "react-redux";
import type { RootState } from "../../app/store";

import {
  BloodRequestStatus,
  RequestUrgency,
} from "./requestTypes";

import { useGetRequestsQuery } from "./requestsApi";

import "./requests.css";


const getStatusLabel = (
  status: BloodRequestStatus
) => {
  switch (status) {
    case BloodRequestStatus.Open:
      return "Open";

    case BloodRequestStatus.Matching:
      return "Matching";

    case BloodRequestStatus.AwaitingApproval:
      return "Awaiting Approval";

    case BloodRequestStatus.DonorsNotified:
      return "Donors Notified";

    case BloodRequestStatus.PartiallyFulfilled:
      return "Partially Fulfilled";

    case BloodRequestStatus.Fulfilled:
      return "Fulfilled";

    case BloodRequestStatus.Expired:
      return "Expired";

    case BloodRequestStatus.Cancelled:
      return "Cancelled";

    default:
      return "Unknown";
  }
};


const getUrgencyLabel = (
  urgency: RequestUrgency
) => {
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


export default function RequestsPage() {
  const role = useSelector(
    (state: RootState) => (state.auth.role ?? "").toLowerCase()
  );

  const canManageRequests = role === "staff" || role === "admin";

  const {
    data: requests = [],
    isLoading,
    isError,
    refetch,
  } = useGetRequestsQuery({
    page: 1,
    pageSize: 100,
    sortBy: "createdAt",
    descending: true,
  });


  /*
   * Requests that still need coordination.
   */
  const pendingCount = requests.filter(
    (request) =>
      request.status ===
        BloodRequestStatus.Open ||
      request.status ===
        BloodRequestStatus.Matching ||
      request.status ===
        BloodRequestStatus.AwaitingApproval
  ).length;


  /*
   * Priority requests.
   */
  const urgentCount = requests.filter(
    (request) =>
      request.urgency ===
        RequestUrgency.Urgent ||
      request.urgency ===
        RequestUrgency.Critical
  ).length;


  /*
   * Requests where coordination has progressed
   * beyond initial matching/approval.
   */
  const coordinatingCount = requests.filter(
    (request) =>
      request.status ===
        BloodRequestStatus.DonorsNotified ||
      request.status ===
        BloodRequestStatus.PartiallyFulfilled
  ).length;


  /*
   * Successfully completed requests.
   */
  const completedCount = requests.filter(
    (request) =>
      request.status ===
        BloodRequestStatus.Fulfilled
  ).length;


  return (
    <main className="requests-page">

      <section className="requests-hero">

        <div className="requests-hero__content">

          <span className="requests-eyebrow">
            BLOOD REQUEST MANAGEMENT
          </span>

          <h1>
            Blood Requests
          </h1>

          <p>
            Create, monitor and coordinate blood
            requests through one streamlined
            healthcare workflow.
          </p>

          {canManageRequests && (
            <Link
              to="/requests/create"
              className="requests-primary-button"
            >
              + Create Blood Request
            </Link>
          )}

        </div>


        <div
          className="requests-hero__visual"
          aria-hidden="true"
        >
          <div className="medical-orb medical-orb--large" />

          <div className="medical-orb medical-orb--small" />

          <div className="blood-request-icon">
            <span className="blood-request-icon__drop">
              ♥
            </span>
          </div>
        </div>

      </section>


      <section className="requests-summary-grid">

        <article className="request-summary-card">

          <span className="request-summary-card__label">
            Pending
          </span>

          <strong>
            {pendingCount}
          </strong>

          <p>
            Requests awaiting action
          </p>

        </article>


        <article className="request-summary-card">

          <span className="request-summary-card__label">
            Urgent / Critical
          </span>

          <strong>
            {urgentCount}
          </strong>

          <p>
            Requests requiring priority attention
          </p>

        </article>


        <article className="request-summary-card">

          <span className="request-summary-card__label">
            In Coordination
          </span>

          <strong>
            {coordinatingCount}
          </strong>

          <p>
            Donor coordination currently in progress
          </p>

        </article>


        <article className="request-summary-card">

          <span className="request-summary-card__label">
            Completed
          </span>

          <strong>
            {completedCount}
          </strong>

          <p>
            Successfully fulfilled requests
          </p>

        </article>

      </section>


      <section className="requests-content-card">

        <div className="requests-section-heading">

          <div>

            <span className="requests-eyebrow">
              REQUEST ACTIVITY
            </span>

            <h2>
              Recent Blood Requests
            </h2>

          </div>


          <button
            type="button"
            className="requests-secondary-button"
            onClick={() => refetch()}
          >
            Refresh
          </button>

        </div>


        {isLoading && (
          <div className="requests-state">

            <div className="requests-loading-card" />

            <div className="requests-loading-card" />

            <div className="requests-loading-card" />

          </div>
        )}


        {isError && (
          <div className="requests-error-state">

            <h3>
              Unable to load blood requests
            </h3>

            <p>
              Please try again.
            </p>

            <button
              type="button"
              className="requests-primary-button"
              onClick={() => refetch()}
            >
              Retry
            </button>

          </div>
        )}


        {!isLoading &&
          !isError &&
          requests.length === 0 && (

            <div className="requests-empty-state">

              <div className="requests-empty-state__icon">
                ♥
              </div>

              <h3>
                No blood requests yet
              </h3>

              <p>
                Create the first blood request to
                start coordinating availability and
                fulfillment.
              </p>

              <Link
                to="/requests/create"
                className="requests-primary-button"
              >
                Create Blood Request
              </Link>

            </div>
          )}


        {!isLoading &&
          !isError &&
          requests.length > 0 && (

            <div className="requests-table-wrapper">

              <table className="requests-table">

                <thead>
                  <tr>
                    <th>Hospital</th>
                    <th>Blood Type</th>
                    <th>Units</th>
                    <th>Urgency</th>
                    <th>Status</th>
                    <th>Created</th>
                    <th />
                  </tr>
                </thead>


                <tbody>

                  {requests.map((request) => (

                    <tr key={request.id}>

                      <td>
                        <strong>
                          {request.hospitalName}
                        </strong>
                      </td>


                      <td>
                        {request.bloodType}
                      </td>


                      <td>
                        {request.unitsRequested}
                      </td>


                      <td>

                        <span
                          className={
                            `request-pill ` +
                            `request-pill--urgency-${request.urgency}`
                          }
                        >
                          {getUrgencyLabel(
                            request.urgency
                          )}
                        </span>

                      </td>


                      <td>

                        <span
                          className={
                            `request-pill ` +
                            `request-pill--status-${request.status}`
                          }
                        >
                          {getStatusLabel(
                            request.status
                          )}
                        </span>

                      </td>


                      <td>
                        {new Date(
                          request.createdAt
                        ).toLocaleDateString()}
                      </td>


                      <td>

                        <Link
                          to={`/requests/${request.id}`}
                          className="request-view-link"
                        >
                          View →
                        </Link>

                      </td>

                    </tr>
                  ))}

                </tbody>

              </table>

            </div>
          )}

      </section>

    </main>
  );
}