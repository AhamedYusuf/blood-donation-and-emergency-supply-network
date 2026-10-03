import { Link } from "react-router-dom";
import { useSelector } from "react-redux";
import type { RootState } from "../../app/store";
import { useGetMyAppointmentsQuery } from "../appointments/appointmentsApi";
import "./donor.css";

function formatDate(value: string) {
  return new Date(value).toLocaleDateString(undefined, { weekday: "short", month: "short", day: "numeric" });
}
function formatTime(value: string) {
  return new Date(value).toLocaleTimeString(undefined, { hour: "numeric", minute: "2-digit" });
}

export function DonorAppointmentsPage() {
  const donorId = useSelector((state: RootState) => state.auth.userId);
  const { data = [], isLoading, isError, refetch } = useGetMyAppointmentsQuery({ donorId: donorId ?? "" }, { skip: !donorId });
  const upcoming = data.filter((item) => new Date(item.scheduledTime) >= new Date() && item.status !== "cancelled").sort((a, b) => a.scheduledTime.localeCompare(b.scheduledTime));
  const history = data.filter((item) => !upcoming.includes(item));

  return (
    <main className="donor-page">
      <header className="donor-page-heading"><div><span className="donor-eyebrow">DONOR SPACE</span><h1>My donations</h1><p>Book and manage the visits you make to the network.</p></div><button className="donor-button" type="button" onClick={() => refetch()}>Refresh</button></header>
      {isLoading && <div className="donor-empty-card"><h3>Loading your donations...</h3></div>}
      {isError && <div className="donor-empty-card"><h3>We could not load your donations.</h3><button className="donor-button" type="button" onClick={() => refetch()}>Try again</button></div>}
      {!isLoading && !isError && data.length === 0 && <div className="donor-empty-card"><div><h3>No donations booked</h3><p>Your appointment history will appear here.</p></div></div>}
      {upcoming.length > 0 && <section className="donor-section"><div className="donor-section__heading"><h2>Upcoming</h2><span>{upcoming.length}</span></div><div className="donor-list">{upcoming.map((item) => <article className="donor-list-row" key={item.id}><div className="donor-list-row__icon">+</div><div><span className="donor-card-label">{item.status.replace("_", " ")}</span><h3>{formatDate(item.scheduledTime)} at {formatTime(item.scheduledTime)}</h3><p>Appointment at your selected blood bank</p></div><Link to={`/requests`}>Details</Link></article>)}</div></section>}
      {history.length > 0 && <section className="donor-section"><div className="donor-section__heading"><h2>History</h2><span>{history.length}</span></div><div className="donor-list">{history.map((item) => <article className="donor-list-row donor-list-row--muted" key={item.id}><div className="donor-list-row__icon">+</div><div><span className="donor-card-label">{item.status.replace("_", " ")}</span><h3>{formatDate(item.scheduledTime)}</h3><p>{item.unitsDonated ? `${item.unitsDonated} unit${item.unitsDonated === 1 ? "" : "s"} donated` : "Donation appointment"}</p></div></article>)}</div></section>}
    </main>
  );
}
