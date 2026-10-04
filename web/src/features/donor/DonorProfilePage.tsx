import { useEffect, useState } from "react";
import { useDispatch, useSelector } from "react-redux";
import type { RootState } from "../../app/store";
import { logout } from "../auth/authSlice";
import { getApiErrorMessage } from "../../api/getApiErrorMessage";
import {
  useGetDonorEligibilityQuery,
  useGetDonorProfileQuery,
  useUpdateDonorProfileMutation,
} from "../donors/donorApi";
import "./donor.css";

const MEDICAL_FLAG_LABELS: Record<string, string> = {
  recent_illness: "Recent illness or infection",
  recent_surgery: "Recent surgery",
  chronic_condition: "Chronic condition",
  hiv_positive: "HIV positive",
  hepatitis: "Hepatitis",
};

function formatDate(value?: string | null) {
  if (!value) return "Not recorded";
  return new Date(`${value}T00:00:00`).toLocaleDateString(undefined, {
    year: "numeric",
    month: "short",
    day: "numeric",
  });
}

export function DonorAccountPage() {
  const dispatch = useDispatch();
  const auth = useSelector((state: RootState) => state.auth);
  const donorId = auth.donorId;
  const { data: profile, isLoading, isError, refetch } = useGetDonorProfileQuery(
    donorId ?? "",
    { skip: !donorId },
  );
  const { data: eligibility, refetch: refetchEligibility } = useGetDonorEligibilityQuery(
    donorId ?? "",
    { skip: !donorId },
  );
  const [updateProfile, { isLoading: isSaving }] = useUpdateDonorProfileMutation();
  const [address, setAddress] = useState("");
  const [medicalFlags, setMedicalFlags] = useState<Record<string, boolean>>({});
  const [editing, setEditing] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!profile) return;
    setAddress(profile.address ?? "");
    setMedicalFlags(profile.medicalFlags ?? {});
  }, [profile]);

  const saveChanges = async () => {
    if (!donorId) return;
    setMessage(null);
    setError(null);
    try {
      await updateProfile({
        id: donorId,
        body: { address: address.trim(), medicalFlags },
      }).unwrap();
      await Promise.all([refetch(), refetchEligibility()]);
      setEditing(false);
      setMessage("Profile updated successfully.");
    } catch (saveError: unknown) {
      setError(getApiErrorMessage(saveError, "Unable to update your profile."));
    }
  };

  if (isLoading) {
    return <main className="donor-page"><div className="donor-empty-card"><h3>Loading your donor profile...</h3></div></main>;
  }

  if (isError || !profile) {
    return <main className="donor-page"><div className="donor-empty-card"><div><h3>We could not load your donor profile.</h3><p>Check your connection and try again.</p></div><button className="donor-button" type="button" onClick={() => refetch()}>Try again</button></div></main>;
  }

  const activeFlags = Object.entries(profile.medicalFlags ?? {}).filter(([, value]) => value).map(([key]) => MEDICAL_FLAG_LABELS[key] ?? key);
  const availability = eligibility?.isEligible
    ? "Eligible to donate now"
    : eligibility?.daysUntilEligible && eligibility.daysUntilEligible > 0
      ? `${eligibility.daysUntilEligible} days until you can donate again`
      : "Not currently eligible";

  return (
    <main className="donor-page donor-profile-page">
      <header className="donor-page-heading"><div><span className="donor-eyebrow">DONOR ACCOUNT</span><h1>Profile</h1><p>Review the information used to match you with donation needs.</p></div><div className="donor-avatar">{(profile.fullName || auth.fullName || "D").slice(0, 1).toUpperCase()}</div></header>
      {message && <div className="donor-profile-message donor-profile-message--success">{message}</div>}
      {error && <div className="donor-profile-message donor-profile-message--error">{error}</div>}
      <section className="donor-profile-grid">
        <article className="donor-profile-card"><span className="donor-card-label">Personal details</span><h2>{profile.fullName || auth.fullName || "Donor"}</h2><dl><div><dt>Email</dt><dd>{auth.email || "Not provided"}</dd></div><div><dt>Blood type</dt><dd>{profile.bloodType}</dd></div><div><dt>Date of birth</dt><dd>{formatDate(profile.dateOfBirth)}</dd></div><div><dt>Verification</dt><dd>{profile.verifiedByAdmin ? "Verified" : "Pending review"}</dd></div></dl></article>
        <article className="donor-profile-card"><span className="donor-card-label">Donation availability</span><h2 className={eligibility?.isEligible ? "donor-eligibility donor-eligibility--ready" : "donor-eligibility"}>{availability}</h2><dl><div><dt>Last donation</dt><dd>{formatDate(profile.lastDonationDate)}</dd></div><div><dt>Eligibility status</dt><dd>{profile.eligibilityStatus.replaceAll("_", " ")}</dd></div></dl>{eligibility?.reason && !eligibility.isEligible && <p className="donor-profile-note">{eligibility.reason.replaceAll("_", " ")}</p>}</article>
      </section>
      <section className="donor-profile-card donor-profile-card--wide"><div className="donor-profile-card__header"><div><span className="donor-card-label">Contact and health details</span><h2>Information for safe matching</h2></div>{!editing && <button className="donor-button" type="button" onClick={() => setEditing(true)}>Edit details</button>}</div>{editing ? <><label className="donor-field-label" htmlFor="donor-address">Address</label><textarea id="donor-address" className="donor-field" rows={3} value={address} onChange={(event) => setAddress(event.target.value)} /><p className="donor-profile-note">Saving a new address automatically refreshes your coordinates for nearby matching.</p><fieldset className="donor-medical-flags"><legend>Illness and medical cases</legend>{Object.entries(MEDICAL_FLAG_LABELS).map(([key, label]) => <label key={key}><input type="checkbox" checked={Boolean(medicalFlags[key])} onChange={(event) => setMedicalFlags((current) => ({ ...current, [key]: event.target.checked }))} />{label}</label>)}</fieldset><div className="donor-form-actions"><button className="donor-button" type="button" disabled={isSaving || !address.trim()} onClick={saveChanges}>{isSaving ? "Saving..." : "Save changes"}</button><button className="donor-secondary-button" type="button" disabled={isSaving} onClick={() => { setEditing(false); setAddress(profile.address ?? ""); setMedicalFlags(profile.medicalFlags ?? {}); }}>Cancel</button></div></> : <><dl><div><dt>Address</dt><dd>{profile.address || "Not provided"}</dd></div><div><dt>Location</dt><dd>{profile.locationVerified && profile.latitude != null && profile.longitude != null ? `${profile.latitude.toFixed(4)}, ${profile.longitude.toFixed(4)}` : "Location not verified"}</dd></div><div><dt>Illness or medical cases</dt><dd>{activeFlags.length > 0 ? activeFlags.join(", ") : "None reported"}</dd></div></dl></>}</section>
      <button className="donor-danger-button" type="button" onClick={() => dispatch(logout())}>Sign out</button>
    </main>
  );
}
