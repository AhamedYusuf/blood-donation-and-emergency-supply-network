import { Link } from "react-router-dom";
import { useGetRequestsQuery } from "../requests/requestsApi";
import { BloodRequestStatus } from "../requests/requestTypes";
import "./donor.css";

export function DonorRequestsPage() {
  const { data = [], isLoading, isError, refetch } = useGetRequestsQuery({ page: 1, pageSize: 100, sortBy: "createdAt", descending: true });
  const active = data.filter((request) => ![BloodRequestStatus.Fulfilled, BloodRequestStatus.Expired, BloodRequestStatus.Cancelled].includes(request.status));
  return (
    <main className="donor-page"><header className="donor-page-heading"><div><span className="donor-eyebrow">NETWORK NEEDS</span><h1>Blood requests</h1><p>See where donations are needed across the network.</p></div><button className="donor-button" type="button" onClick={() => refetch()}>Refresh</button></header>
      {isLoading && <div className="donor-empty-card"><h3>Loading active requests...</h3></div>}
      {isError && <div className="donor-empty-card"><h3>We could not load requests.</h3><button className="donor-button" type="button" onClick={() => refetch()}>Try again</button></div>}
      {!isLoading && !isError && active.length === 0 && <div className="donor-empty-card"><h3>No active requests right now.</h3><p>Check back soon for new needs.</p></div>}
      {active.length > 0 && <section className="donor-list">{active.map((request) => <article className="donor-request-row" key={request.id}><div className={`donor-blood-type donor-blood-type--${request.urgency}`}>{request.bloodType}</div><div><span className="donor-card-label">{request.urgency} need</span><h2>{request.hospitalName}</h2><p>{request.unitsRequested} unit{request.unitsRequested === 1 ? "" : "s"} requested · {request.status.replaceAll("_", " ")}</p></div><Link to={`/requests/${request.id}`}>View</Link></article>)}</section>}
    </main>
  );
}
