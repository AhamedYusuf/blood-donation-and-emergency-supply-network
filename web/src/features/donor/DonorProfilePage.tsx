import { useDispatch, useSelector } from "react-redux";
import type { RootState } from "../../app/store";
import { logout } from "../auth/authSlice";
import "./donor.css";

export function DonorAccountPage() {
  const dispatch = useDispatch();
  const auth = useSelector((state: RootState) => state.auth);
  return (
    <main className="donor-page donor-profile-page">
      <header className="donor-page-heading"><div><span className="donor-eyebrow">ACCOUNT</span><h1>Profile</h1><p>Keep your account details close at hand.</p></div><div className="donor-avatar">{(auth.fullName || "D").slice(0, 1).toUpperCase()}</div></header>
      <section className="donor-profile-grid">
        <article className="donor-profile-card"><span className="donor-card-label">Personal details</span><h2>{auth.fullName || "Donor"}</h2><dl><div><dt>Email</dt><dd>{auth.email || "Not provided"}</dd></div><div><dt>Role</dt><dd>Donor</dd></div></dl></article>
        <article className="donor-profile-card"><span className="donor-card-label">About your account</span><h2>Every visit counts.</h2><p>Your appointments are coordinated through the network so your donation can reach the right need.</p></article>
      </section>
      <button className="donor-danger-button" type="button" onClick={() => dispatch(logout())}>Sign out</button>
    </main>
  );
}
