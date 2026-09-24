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

const coordinationStages = [
  {
    icon: "◈",
    title: "Stock Analysis",
    description: "Checks local inventory and nearby transfer options.",
  },
  {
    icon: "◎",
    title: "Donor Matching",
    description: "Finds compatible donor candidates when stock is limited.",
  },
  {
    icon: "✓",
    title: "Eligibility Validation",
    description: "Validates donor eligibility before a decision is requested.",
  },
  {
    icon: "▣",
    title: "Human Approval",
    description: "Keeps authorized staff in control of the final decision.",
  },
];


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
  const openCount = requests.filter(
    (request) => request.status === BloodRequestStatus.Open
  ).length;

  const awaitingApprovalCount = requests.filter(
    (request) => request.status === BloodRequestStatus.AwaitingApproval
  ).length;

  const fulfilledCount = requests.filter(
    (request) => request.status === BloodRequestStatus.Fulfilled
  ).length;

  const activeCount = requests.filter(
    (request) =>
      request.status === BloodRequestStatus.Matching ||
      request.status === BloodRequestStatus.DonorsNotified ||
      request.status === BloodRequestStatus.PartiallyFulfilled
  ).length;


  return (
    <main className="requests-page">

      <section className="requests-hero">

        <div className="requests-hero__content">

          <span className="requests-eyebrow">
            COORDINATOR AI · REQUEST OPERATIONS
          </span>

          <h1>
            Blood Request Coordination
          </h1>

          <p>
            Create, prioritize and coordinate emergency blood requests with AI-assisted workflow management.
          </p>

          {canManageRequests && (
            <Link
              to="/requests/create"
              className="requests-primary-button"
            >
              + Create Request
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
            Open Requests
          </span>

          <strong>
            {openCount}
          </strong>

          <p>
            Newly submitted requests
          </p>

        </article>


        <article className="request-summary-card">

          <span className="request-summary-card__label">
            Awaiting Approval
          </span>

          <strong>
            {awaitingApprovalCount}
          </strong>

          <p>
            Requests paused for staff review
          </p>

        </article>


        <article className="request-summary-card">

          <span className="request-summary-card__label">
            Fulfilled Requests
          </span>

          <strong>
            {fulfilledCount}
          </strong>

          <p>
            Requests completed successfully
          </p>

        </article>


        <article className="request-summary-card">

          <span className="request-summary-card__label">
            Active / In Progress
          </span>

          <strong>
            {activeCount}
          </strong>

          <p>
            Matching, notifying or partially fulfilled
          </p>

        </article>

      </section>

      <section className="requests-coordinator-section">
        <div className="requests-section-heading requests-section-heading--coordinator">
          <div>
            <span className="requests-eyebrow">COORDINATOR AI</span>
            <h2>One workflow, clear human oversight</h2>
          </div>
          <span className="requests-ai-badge">AI-assisted · Human-led</span>
        </div>

        <div className="requests-coordinator-grid">
          {coordinationStages.map((stage) => (
            <article className="requests-coordinator-card" key={stage.title}>
              <span className="requests-coordinator-card__icon" aria-hidden="true">
                {stage.icon}
              </span>
              <div>
                <h3>{stage.title}</h3>
                <p>{stage.description}</p>
              </div>
            </article>
          ))}
        </div>
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