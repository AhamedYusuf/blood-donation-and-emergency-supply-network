import { Link } from "react-router-dom";
import { useSelector } from "react-redux";
import type { RootState } from "../../app/store";
import { useGetRequestsQuery } from "../requests/requestsApi";
import { BloodRequestStatus } from "../requests/requestTypes";
import { useGetDonorProfileQuery } from "../donors/donorApi";
import { useGetOrganizationsQuery } from "../organizations/organizationsApi";
import "./donor.css";

// Previously this fetched every blood request network-wide, from every
// organization, with no location filtering at all — a donor in one city
// would see requests from a hospital on the other side of the country.
// Now it passes the donor's own DonorProfile coordinates as nearLat/
// nearLng, so the backend restricts results to within radiusKm (default
// 50km, matching the radius the Matching & Dispatch Agent itself searches
// within) of the donor.

export function DonorRequestsPage() {
  const donorId = useSelector((state: RootState) => state.auth.donorId);

  const { data: profile } = useGetDonorProfileQuery(donorId ?? "", { skip: !donorId });
  const { data: organizations = [], isLoading: organizationsLoading } = useGetOrganizationsQuery();
  const organizationNames = new Map(
    organizations.map((organization) => [
      organization.id,
      organization.name,
    ]),
  );
  const hasLocation = profile?.latitude != null && profile?.longitude != null;

  const { data = [], isLoading, isError, refetch } = useGetRequestsQuery(
    {
      page: 1,
      pageSize: 100,
      sortBy: "distance",
      descending: false,
      nearLat: profile?.latitude ?? undefined,
      nearLng: profile?.longitude ?? undefined,
    },
    { skip: !hasLocation },
  );

  const active = data.filter(
    (request) => ![BloodRequestStatus.Fulfilled, BloodRequestStatus.Expired, BloodRequestStatus.Cancelled].includes(request.status),
  );

  return (
    <main className="donor-page">
      <header className="donor-page-heading">
        <div>
          <span className="donor-eyebrow">NETWORK NEEDS</span>
          <h1>Blood requests</h1>
          <p>Requests from blood banks within 50km of you.</p>
        </div>
        <button className="donor-button" type="button" onClick={() => refetch()}>Refresh</button>
      </header>

      {donorId && !profile && <div className="donor-empty-card"><h3>Loading your location...</h3></div>}

      {profile && !hasLocation && (
        <div className="donor-empty-card">
          <div><h3>We don't have your location yet</h3><p>Add an address to your profile to see requests near you.</p></div>
          <Link className="donor-button" to="/profile">Go to profile</Link>
        </div>
      )}

      {hasLocation && isLoading && <div className="donor-empty-card"><h3>Loading nearby requests...</h3></div>}
      {hasLocation && isError && (
        <div className="donor-empty-card">
          <h3>We could not load requests.</h3>
          <button className="donor-button" type="button" onClick={() => refetch()}>Try again</button>
        </div>
      )}
      {hasLocation && !isLoading && !isError && active.length === 0 && (
        <div className="donor-empty-card"><h3>No active requests nearby right now.</h3><p>Check back soon for new needs.</p></div>
      )}

      {active.length > 0 && (
        <section className="donor-list">
          {active.map((request) => (
            <article className="donor-request-row" key={request.id}>
              <div className={`donor-blood-type donor-blood-type--${request.urgency}`}>{request.bloodType}</div>
              <div>
                <span className="donor-card-label">{request.urgency} need</span>
                <h2>
                  {organizationNames.get(request.organizationId) ??
                    (organizationsLoading
                      ? "Loading organization..."
                      : "Organization unavailable")}
                </h2>
                <p>
                  {request.unitsRequested} unit{request.unitsRequested === 1 ? "" : "s"} requested · {request.status.replaceAll("_", " ")}
                  {request.distanceKm != null ? ` · ${request.distanceKm.toFixed(1)} km away` : ""}
                </p>
              </div>
              <Link to={`/requests/${request.id}`}>View</Link>
            </article>
          ))}
        </section>
      )}
    </main>
  );
}
