import { Link } from "react-router-dom";
import { useSelector } from "react-redux";
import type { RootState } from "../../app/store";
import { useGetMyAppointmentsQuery } from "../appointments/appointmentsApi";
import "./donor.css";

function formatDate(value: string) {
  return new Date(value).toLocaleDateString(undefined, {
    weekday: "short",
    month: "short",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
  });
}

export function DonorHomePage() {
  const name = useSelector((state: RootState) => state.auth.fullName);
  const donorId = useSelector((state: RootState) => state.auth.userId);
  const { data: appointments = [], isLoading } = useGetMyAppointmentsQuery(
    { donorId: donorId ?? "" },
    { skip: !donorId },
  );
  const upcoming = appointments
    .filter((appointment) => new Date(appointment.scheduledTime) >= new Date() && appointment.status !== "cancelled")
    .sort((a, b) => a.scheduledTime.localeCompare(b.scheduledTime));
  const completed = appointments.filter((appointment) => appointment.status === "completed").length;
  const next = upcoming[0];

  return (
    <main className="donor-page">
      <section className="donor-welcome">
        <div>
          <span className="donor-eyebrow">DONOR HOME</span>
          <h1>Good to see you, {name?.split(" ")[0] || "Donor"}.</h1>
          <p>Your donations help move the right blood to the people who need it most.</p>
        </div>
        <div className="donor-welcome__drop" aria-hidden="true">+</div>
      </section>

      <section className="donor-stat-grid" aria-label="Donation summary">
        <div className="donor-stat"><span>Upcoming</span><strong>{isLoading ? "-" : upcoming.length}</strong><small>planned donations</small></div>
        <div className="donor-stat"><span>Completed</span><strong>{isLoading ? "-" : completed}</strong><small>donations made</small></div>
        <div className="donor-stat donor-stat--accent"><span>Next step</span><strong>{next ? "Ready" : "Book"}</strong><small>{next ? "your next visit" : "a donation appointment"}</small></div>
      </section>

      <section className="donor-section">
        <div className="donor-section__heading"><div><span className="donor-eyebrow">DONATIONS</span><h2>Next donation</h2></div><Link to="/appointments">View all</Link></div>
        {next ? (
          <article className="donor-feature-card"><div className="donor-feature-card__icon">+</div><div><span className="donor-card-label">{next.status.replace("_", " ")}</span><h3>{formatDate(next.scheduledTime)}</h3><p>Keep this time free for your donation visit.</p></div><Link to="/appointments">Manage</Link></article>
        ) : (
          <article className="donor-empty-card"><div><h3>No donation booked yet</h3><p>Choose a blood bank and a time when you are ready to give.</p></div><Link className="donor-button" to="/appointments">View donations</Link></article>
        )}
      </section>

      <section className="donor-section">
        <div className="donor-section__heading"><div><span className="donor-eyebrow">REQUESTS</span><h2>Help where it matters</h2></div><Link to="/requests">View requests</Link></div>
        <article className="donor-info-card"><div className="donor-info-card__icon">!</div><div><h3>See active blood requests</h3><p>Stay informed about urgent needs across the network.</p></div><Link to="/requests">Explore</Link></article>
      </section>
    </main>
  );
}
